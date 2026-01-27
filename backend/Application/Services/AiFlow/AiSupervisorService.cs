using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Application.Abstractions.AI;

namespace Application.Services.AiFlow;

/// <summary>
/// AI-first supervisor:
/// - clarifies missing info by asking follow-up questions
/// - generates SQL for the DB
/// - executes SQL
/// - processes + presents results
/// The supervisor drives the flow using tool-calls encoded as strict JSON.
/// </summary>
public sealed class AiSupervisorService : IAiSupervisorService
{
    private readonly IChatClient _chat;
    private readonly IEnumerable<IAiTool> _tools;
    private readonly AiMemoryStore _mem;
    private readonly ILogger<AiSupervisorService> _log;

    private readonly ConcurrentDictionary<long, List<ChatMessage>> _history = new();

    // schemaHandle per chatId so BuildSql never guesses table names
    private readonly ConcurrentDictionary<long, Guid> _schemaHandleByChat = new();

    public AiSupervisorService(IChatClient chat, IEnumerable<IAiTool> tools, AiMemoryStore mem)
        : this(chat, tools, mem, NullLogger<AiSupervisorService>.Instance)
    {
    }

    public AiSupervisorService(IChatClient chat, IEnumerable<IAiTool> tools, AiMemoryStore mem, ILogger<AiSupervisorService> log)
    {
        _chat = chat;
        _tools = tools;
        _mem = mem;
        _log = log ?? NullLogger<AiSupervisorService>.Instance;
    }

    public async Task<string> ReplyAsync(long chatId, string userText, CancellationToken ct = default)
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        var sw = Stopwatch.StartNew();
        _log.LogInformation("[AI:{Trace}] ReplyAsync start chatId={ChatId} userText={UserText}", traceId, chatId, Trunc(userText, 240));

        var messages = _history.GetOrAdd(chatId, _ => new List<ChatMessage>
        {
            new(ChatRole.System, BuildSystemPrompt(_tools))
        });

        IReadOnlyList<ChatMessage> snapshot;
        lock (messages)
        {
            messages.Add(new ChatMessage(ChatRole.User, userText));
            Trim(messages, 60);
            snapshot = messages.ToList();
        }

        var toolMap = _tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        _log.LogDebug("[AI:{Trace}] historyCount={Count} tools={ToolCount}", traceId, snapshot.Count, toolMap.Count);

        // Prevent infinite loops: block repeated tool calls with same args in a single ReplyAsync run
        var executedToolCalls = new HashSet<string>(StringComparer.Ordinal);

        // ------------------------------------------------------------
        // Ensure schema is available for this chat so BuildSql never guesses.
        // ------------------------------------------------------------
        if (!_schemaHandleByChat.ContainsKey(chatId) && toolMap.TryGetValue("GetDbSchema", out var schemaTool))
        {
            try
            {
                var ctx = new AiToolContext(chatId, _mem);
                var schemaJson = await schemaTool.InvokeAsync("{}", ctx, ct);
                var handle = TryExtractHandle(schemaJson);
                if (handle.HasValue)
                {
                    _schemaHandleByChat[chatId] = handle.Value;
                    _log.LogInformation("[AI:{Trace}] schemaHandle loaded {SchemaHandle}", traceId, handle.Value);

                    // Add explicit system note into conversation history
                    lock (messages)
                    {
                        messages.Add(new ChatMessage(ChatRole.System,
                            "DB schema loaded for this chat. Always use schemaHandle when calling BuildSql."));
                        Trim(messages, 60);
                        snapshot = messages.ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[AI:{Trace}] GetDbSchema failed", traceId);
                // ignore: model can still ask clarification or continue without schema
            }
        }

        // Single user call may require multiple tool steps.
        for (var step = 0; step < 10; step++)
        {
            _log.LogInformation("[AI:{Trace}] step={Step} snapshotMessages={MsgCount}", traceId, step, snapshot.Count);
            var resp = await _chat.GetResponseAsync(snapshot, cancellationToken: ct);
            var text = resp.Text ?? string.Empty;
            _log.LogDebug("[AI:{Trace}] modelText(len={Len})={Text}", traceId, text.Length, Trunc(text, 800));

            var action = ExtractFirstJsonObject(text);
            if (action == null)
            {
                _log.LogWarning("[AI:{Trace}] No JSON action found in model output.", traceId);
                lock (messages)
                {
                    messages.Add(new ChatMessage(ChatRole.Assistant, text));
                    Trim(messages, 60);
                }
                return text;
            }

            if (!action.RootElement.TryGetProperty("action", out var actionProp))
            {
                _log.LogWarning("[AI:{Trace}] JSON without 'action': {Json}", traceId, Trunc(action.RootElement.GetRawText(), 800));
                return FinalizeAndStore(messages, "Prosím, skús to ešte raz (AI nevrátilo pole 'action').");
            }

            var kind = actionProp.GetString() ?? string.Empty;

            if (string.Equals(kind, "answer", StringComparison.OrdinalIgnoreCase))
            {
                var outText = action.RootElement.TryGetProperty("text", out var tProp)
                    ? (tProp.GetString() ?? string.Empty)
                    : string.Empty;

                return FinalizeAndStore(messages, outText);
            }

            if (string.Equals(kind, "ask", StringComparison.OrdinalIgnoreCase))
            {
                var q = action.RootElement.TryGetProperty("question", out var qProp)
                    ? (qProp.GetString() ?? string.Empty)
                    : "Upresníš, prosím?";

                return FinalizeAndStore(messages, q);
            }

            if (string.Equals(kind, "tool", StringComparison.OrdinalIgnoreCase))
            {
                var toolName = action.RootElement.TryGetProperty("tool", out var toolProp)
                    ? (toolProp.GetString() ?? string.Empty)
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(toolName) || !toolMap.TryGetValue(toolName, out var tool))
                    return FinalizeAndStore(messages, $"Neznámy nástroj '{toolName}'.");

                var argsJson = action.RootElement.TryGetProperty("args", out var argsProp)
                    ? argsProp.GetRawText()
                    : "{}";

                // Hard guard: BuildSql must always get schemaHandle so it never guesses table names.
                // Also overwrite INVALID schemaHandle (model can hallucinate/truncate it).
                if (string.Equals(toolName, "BuildSql", StringComparison.OrdinalIgnoreCase)
                    && _schemaHandleByChat.TryGetValue(chatId, out var sh)
                    && !ArgsContainsValidSchemaHandle(argsJson))
                {
                    argsJson = InjectSchemaHandle(argsJson, sh);
                }

                // NEW: block repeated tool calls with same args (prevents loops like GetLastWeekOverview -> GetLastWeekOverview -> ...)
                var toolKey = $"{toolName}|{NormalizeArgs(argsJson)}";
                if (!executedToolCalls.Add(toolKey))
                {
                    _log.LogWarning("[AI:{Trace}] Repeated tool call blocked: {ToolKey}", traceId, toolKey);

                    lock (messages)
                    {
                        messages.Add(new ChatMessage(
                            ChatRole.System,
                            "Tento nástroj už bol vykonaný s rovnakými args. Ďalšie tool volania sú zakázané. " +
                            "Teraz MUSÍŠ vrátiť finálnu odpoveď ako JSON: {\"action\":\"answer\",\"text\":\"...\"} na základe posledného TOOL výsledku."
                        ));
                        Trim(messages, 60);
                        snapshot = messages.ToList();
                    }

                    continue;
                }

                _log.LogInformation("[AI:{Trace}] TOOL call {Tool} args={Args}", traceId, tool.Name, Trunc(argsJson, 800));
                var toolResult = await tool.InvokeAsync(argsJson, new AiToolContext(chatId, _mem), ct);
                _log.LogInformation("[AI:{Trace}] TOOL result {Tool} len={Len} preview={Preview}", traceId, tool.Name, toolResult?.Length ?? 0, Trunc(toolResult ?? string.Empty, 1000));

                // FIX #1: tool result can be { ask:true, question:"..." } or { ok:false, ... } or { action:"ask"/"answer"... }
                if (TryShortCircuitToolResult(tool.Name, toolResult, out var immediateOut))
                {
                    _log.LogInformation("[AI:{Trace}] Short-circuit from tool {Tool}", traceId, tool.Name);
                    return FinalizeAndStore(messages, immediateOut);
                }

                lock (messages)
                {
                    messages.Add(new ChatMessage(ChatRole.System, $"[TOOL:{tool.Name}] {toolResult}"));
                    Trim(messages, 60);
                    snapshot = messages.ToList();
                }

                // NEW: after overview tool, force the model to answer immediately (prevents extra tools / looping)
                if (string.Equals(toolName, "GetLastWeekOverview", StringComparison.OrdinalIgnoreCase))
                {
                    lock (messages)
                    {
                        messages.Add(new ChatMessage(
                            ChatRole.System,
                            "Máš všetky dáta z GetLastWeekOverview. Teraz okamžite vráť finálnu odpoveď ako JSON: {\"action\":\"answer\",\"text\":\"...\"}. " +
                            "Nevolaj už žiadne ďalšie nástroje."
                        ));
                        Trim(messages, 60);
                        snapshot = messages.ToList();
                    }
                }

                // FIX #2: After successful BuildSql, run the deterministic chain (RunSql -> ProcessReport -> PresentReport)
                // to prevent the model from looping on BuildSql.
                if (string.Equals(toolName, "BuildSql", StringComparison.OrdinalIgnoreCase))
                {
                    var auto = await TryAutoRunAfterBuildSqlAsync(traceId, chatId, toolMap, toolResult, messages, ct);
                    if (auto != null)
                        return auto;

                    lock (messages)
                        snapshot = messages.ToList();
                }

                continue;
            }

            return FinalizeAndStore(messages, "Prosím, skús to ešte raz (neznámy action).");
        }

        sw.Stop();
        _log.LogError("[AI:{Trace}] Stuck after max steps. elapsedMs={Ms}", traceId, sw.ElapsedMilliseconds);
        return FinalizeAndStore(messages, $"AI sa zaseklo v interných krokoch (traceId={traceId}). Pošli mi prosím logy s týmto traceId.");
    }

    private async Task<string?> TryAutoRunAfterBuildSqlAsync(
        string traceId,
        long chatId,
        Dictionary<string, IAiTool> toolMap,
        string? buildSqlResultJson,
        List<ChatMessage> messages,
        CancellationToken ct)
    {
        if (!TryParseBuildSqlResult(buildSqlResultJson, out var sql, out var parametersRaw, out var reportRequestRaw))
            return null;

        if (!toolMap.TryGetValue("RunSql", out var runSql) ||
            !toolMap.TryGetValue("ProcessReport", out var process) ||
            !toolMap.TryGetValue("PresentReport", out var present))
        {
            _log.LogWarning("[AI:{Trace}] Missing required tools for auto pipeline.", traceId);
            return null;
        }

        // --- RunSql ---
        var runArgs = BuildRunSqlArgs(sql, parametersRaw);
        _log.LogInformation("[AI:{Trace}] AUTO -> RunSql args={Args}", traceId, Trunc(runArgs, 800));
        var runRes = await runSql.InvokeAsync(runArgs, new AiToolContext(chatId, _mem), ct);
        _log.LogInformation("[AI:{Trace}] AUTO <- RunSql len={Len} preview={Preview}", traceId, runRes?.Length ?? 0, Trunc(runRes ?? "", 1000));

        if (TryShortCircuitToolResult("RunSql", runRes, out var immediateOut))
            return FinalizeAndStore(messages, immediateOut);

        lock (messages)
        {
            messages.Add(new ChatMessage(ChatRole.System, $"[TOOL:RunSql] {runRes}"));
            Trim(messages, 60);
        }

        if (!TryParseOkHandle(runRes, out var rowsHandle, out var runErr))
        {
            if (!string.IsNullOrWhiteSpace(runErr))
                return FinalizeAndStore(messages, runErr);
            return FinalizeAndStore(messages, "Nepodarilo sa vykonať SQL dotaz. Skús to prosím ešte raz.");
        }

        // --- ProcessReport ---
        var procArgs = BuildProcessReportArgs(rowsHandle, reportRequestRaw);
        _log.LogInformation("[AI:{Trace}] AUTO -> ProcessReport args={Args}", traceId, Trunc(procArgs, 800));
        var procRes = await process.InvokeAsync(procArgs, new AiToolContext(chatId, _mem), ct);
        _log.LogInformation("[AI:{Trace}] AUTO <- ProcessReport len={Len} preview={Preview}", traceId, procRes?.Length ?? 0, Trunc(procRes ?? "", 1000));

        if (TryShortCircuitToolResult("ProcessReport", procRes, out immediateOut))
            return FinalizeAndStore(messages, immediateOut);

        lock (messages)
        {
            messages.Add(new ChatMessage(ChatRole.System, $"[TOOL:ProcessReport] {procRes}"));
            Trim(messages, 60);
        }

        if (!TryParseOkHandle(procRes, out var reportHandle, out var procErr))
        {
            if (!string.IsNullOrWhiteSpace(procErr))
                return FinalizeAndStore(messages, procErr);
            return FinalizeAndStore(messages, "Nepodarilo sa spracovať výsledky reportu. Skús to prosím ešte raz.");
        }

        // --- PresentReport ---
        var presArgs = BuildPresentReportArgs(reportHandle, reportRequestRaw);
        _log.LogInformation("[AI:{Trace}] AUTO -> PresentReport args={Args}", traceId, Trunc(presArgs, 800));
        var presRes = await present.InvokeAsync(presArgs, new AiToolContext(chatId, _mem), ct);
        _log.LogInformation("[AI:{Trace}] AUTO <- PresentReport len={Len} preview={Preview}", traceId, presRes?.Length ?? 0, Trunc(presRes ?? "", 1000));

        if (TryShortCircuitToolResult("PresentReport", presRes, out immediateOut))
            return FinalizeAndStore(messages, immediateOut);

        lock (messages)
        {
            messages.Add(new ChatMessage(ChatRole.System, $"[TOOL:PresentReport] {presRes}"));
            Trim(messages, 60);
        }

        var finalText = TryParsePresentText(presRes);
        if (string.IsNullOrWhiteSpace(finalText))
            return FinalizeAndStore(messages, "Nepodarilo sa vygenerovať výstup reportu. Skús to prosím ešte raz.");

        return FinalizeAndStore(messages, finalText);
    }

    private static string BuildSystemPrompt(IEnumerable<IAiTool> tools)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Si AI supervízor pre IoT backend. Tvoja úloha: získať dáta z databázy, spracovať ich a odpovedať používateľovi.");
        sb.AppendLine();
        sb.AppendLine("Pravidlá:");
        sb.AppendLine("- Vždy najprv zistí, či máš všetky parametre (metrika, časový rozsah, interval). Ak nie, vráť action=ask a polož JEDNU jasnú otázku.");
        sb.AppendLine("- Keď treba ísť do DB, použi nástroje: GetDbSchema -> BuildSql -> RunSql -> ProcessReport -> PresentReport.");
        sb.AppendLine("- Nikdy si nevymýšľaj výsledky. Bez DB výsledkov nehovor čísla.");
        sb.AppendLine("- Odpovedaj po slovensky, stručne a vecne.");
        sb.AppendLine();

        
        // ============================================================
        // Last-week overview (NO SQL) + mandatory formatting + insights
        // ============================================================
        sb.AppendLine("- Ak používateľ žiada prehľad/týždeň/zhrnutie/odporúčanie (napr. 'teplota za posledný týždeň', 'odporúčanie', 'čo zlepšiť', 'ako vetrať'), NEPÝTAJ sa na parametre a NECHOĎ do SQL.");
        sb.AppendLine("- Namiesto toho najprv zavolaj tool GetLastWeekOverview (bez SQL): {\"action\":\"tool\",\"tool\":\"GetLastWeekOverview\",\"args\":{}}.");
        sb.AppendLine("- GetLastWeekOverview volaj najviac RAZ. Ak už bol raz zavolaný, NESMIEŠ ho volať znova.");
        sb.AppendLine("- Po tom, čo dostaneš výsledok z GetLastWeekOverview, MUSÍŠ hneď vrátiť action=answer (žiadne ďalšie tool volania).");
        sb.AppendLine("- Keď používateľ v texte spomenie metriky (teplota/vlhkosť/CO2), pošli ich do args.metrics, napr. {\"metrics\":[\"temperature\",\"humidity\",\"airQuality\"]}.");
        sb.AppendLine("- Odporúčanie a čísla musia vychádzať iba z dát z GetLastWeekOverview (metrics + flags).");
        sb.AppendLine("- Môžeš byť „živší“ v texte (interpretácie, súvislosti), ale čísla MUSIA byť len z tool výstupu.");
        sb.AppendLine("- Pri týždennom prehľade/odporúčaní je výnimka: odpoveď môže byť dlhšia, musí dodržať report formát nižšie.");
        sb.AppendLine();

        sb.AppendLine("FORMÁTOVANIE TÝŽDENNÉHO PREHĽADU (povinné pri GetLastWeekOverview):");
        sb.AppendLine("- Text v action=answer MUSÍ byť pekný report s emoji + odrážkami (bez markdown kódu).");
        sb.AppendLine("- Použi presne sekcie a poradie:");
        sb.AppendLine();
        sb.AppendLine("📊 Prehľad meraní – posledný týždeň");
        sb.AppendLine();
        sb.AppendLine("🌡️ Teplota");
        sb.AppendLine("• Priemer: XX.XX °C");
        sb.AppendLine("• Minimum: XX.XX °C");
        sb.AppendLine("• Maximum: XX.XX °C");
        sb.AppendLine("• Stabilita: (slovne podľa Max-Min, napr. „stabilné“ / „mierne kolísanie“ / „výrazné kolísanie“)");
        sb.AppendLine("• Stav: ...");
        sb.AppendLine("➡️ Tip: ...");
        sb.AppendLine();
        sb.AppendLine("💧 Vlhkosť");
        sb.AppendLine("• Priemer: XX.XX %");
        sb.AppendLine("• Minimum: XX.XX %");
        sb.AppendLine("• Maximum: XX.XX %");
        sb.AppendLine("• Stabilita: (slovne podľa Max-Min)");
        sb.AppendLine("• Stav: ...");
        sb.AppendLine("➡️ Tip: ...");
        sb.AppendLine();
        sb.AppendLine("🌬️ Kvalita vzduchu (CO₂ / air quality)");
        sb.AppendLine("• Priemer: XX.XX");
        sb.AppendLine("• Minimum: XX.XX");
        sb.AppendLine("• Maximum: XX.XX");
        sb.AppendLine("• Stabilita: (slovne podľa Max-Min)");
        sb.AppendLine("• Stav: ...");
        sb.AppendLine("➡️ Tip: ...");
        sb.AppendLine();
        sb.AppendLine("🔎 Vzťahy a odchýlky");
        sb.AppendLine("• 2–4 odrážky: hľadaj súvislosti medzi metrikami a vyzdvihni extrémy/špičky na základe Avg/Min/Max a prípadne Data[].");
        sb.AppendLine("• Príklady viet (použi len ak sú podporené dátami):");
        sb.AppendLine("  - „Teplota bola dlhodobo vyššia (tooWarm) a zároveň vlhkosť skôr suchšia (tooDry) – teplý vzduch často znižuje relatívnu vlhkosť.“");
        sb.AppendLine("  - „Kvalita vzduchu bola väčšinou v poriadku, ale maximum výrazne vyčnieva – pravdepodobne krátka špička (viac ľudí / zatvorené okno).“");
        sb.AppendLine("  - „Ak je Max výrazne nad Avg, spomeň „občasné špičky“ (bez vymýšľania konkrétneho dňa).“");
        sb.AppendLine();
        sb.AppendLine("✅ Zhrnutie");
        sb.AppendLine("1–2 vety: čo je OK + čo je hlavný problém + čo spraviť ako prvé.");
        sb.AppendLine();

        sb.AppendLine("- ČÍSLA ber iba z tool výsledku: metrics.temperature.(Avg/Min/Max), metrics.humidity.(Avg/Min/Max), metrics.airQuality.(Avg/Min/Max).");
        sb.AppendLine("- Ak niektoré z týchto polí v tool výstupe chýba (null), tú odrážku vynechaj (nepíš 0 ani odhad).");
        sb.AppendLine("- FORMÁT ČÍSEL: zaokrúhli na 2 desatinné miesta (napr. 27.35).");
        sb.AppendLine("- JEDNOTKY: teplota v °C, vlhkosť v %, airQuality bez jednotky (ak tool neposkytuje).");
        sb.AppendLine();

        sb.AppendLine("STAVY (povinná logika z flags):");
        sb.AppendLine("  * Teplota: ak flags.temperature.tooWarm=true -> \"⚠️ skôr teplé (tooWarm)\"; inak ak tooCold=true -> \"⚠️ skôr chladné (tooCold)\"; inak \"✅ v norme\".");
        sb.AppendLine("  * Vlhkosť: ak flags.humidity.tooDry=true -> \"⚠️ skôr suché (tooDry)\"; inak ak tooHumid=true -> \"⚠️ skôr vlhké (tooHumid)\"; inak \"✅ v norme\".");
        sb.AppendLine("  * Kvalita vzduchu: ak flags.airQuality.veryHigh=true -> \"⛔ veľmi vysoké\"; inak ak high=true -> \"⚠️ vysoké\"; inak ak elevated=true -> \"⚠️ zvýšené\"; inak \"✅ bez zvýšenia\".");
        sb.AppendLine();

        sb.AppendLine("STABILITA / KOLÍSANIE (slovné hodnotenie podľa Max-Min):");
        sb.AppendLine("- veľmi stabilné: rozdiel ~0 až 0.5");
        sb.AppendLine("- mierne kolísanie: ~0.5 až 2");
        sb.AppendLine("- stredné kolísanie: ~2 až 5");
        sb.AppendLine("- výrazné kolísanie: >5");
        sb.AppendLine("- Toto je len slovný popis, nevypisuj samotný rozdiel, ak ho nechceš. Čísla nevymýšľaj.");
        sb.AppendLine();

        sb.AppendLine("TIPY (musí byť personalizované podľa kombinácií, nie generické):");
        sb.AppendLine("- Ak teplota tooWarm=true a zároveň vlhkosť tooDry=true:");
        sb.AppendLine("  použi tip: „➡️ Tip: Skús vetrať krátko a intenzívne (2–5 min), ale počítaj s tým, že dlhé vetranie môže ešte viac vysušiť vzduch. Zváž aj zvlhčenie.“");
        sb.AppendLine("- Ak teplota tooWarm=true a airQuality elevated/high/veryHigh=true:");
        sb.AppendLine("  použi tip: „➡️ Tip: Priorita je vyvetrať (2–5 min intenzívne) – pomôže to teplote aj kvalite vzduchu.“");
        sb.AppendLine("- Ak teplota tooWarm=true a airQuality je OK:");
        sb.AppendLine("  použi tip: „➡️ Tip: Skús vetrať krátko a intenzívne (2–5 min), hlavne keď teplota stúpa nad ~27 °C.“");
        sb.AppendLine("- Ak teplota tooCold=true:");
        sb.AppendLine("  použi tip: „➡️ Tip: Ak je miestnosť dlhšie chladná, skús kratšie vetranie a skontroluj kúrenie.“");
        sb.AppendLine("- Ak teplota OK:");
        sb.AppendLine("  použi tip: „➡️ Tip: Teplota je stabilná, stačí bežné vetranie podľa potreby.“");
        sb.AppendLine("- Ak vlhkosť tooDry=true:");
        sb.AppendLine("  použi tip: „➡️ Tip: Ak klesne pod ~35 %, zváž zvlhčenie (napr. zvlhčovač alebo miska s vodou).“");
        sb.AppendLine("- Ak vlhkosť tooHumid=true:");
        sb.AppendLine("  použi tip: „➡️ Tip: Skús častejšie krátke vetranie, aby vlhkosť klesla.“");
        sb.AppendLine("- Ak vlhkosť OK:");
        sb.AppendLine("  použi tip: „➡️ Tip: Vlhkosť je v poriadku.“");
        sb.AppendLine("- Ak airQuality elevated/high/veryHigh=true:");
        sb.AppendLine("  použi tip: „➡️ Tip: Odporúčam vyvetrať (2–5 min) a sledovať hodnoty, najmä pri viac ľuďoch v miestnosti.“");
        sb.AppendLine("- Ak airQuality OK:");
        sb.AppendLine("  použi tip: „➡️ Tip: Stačí bežné vetranie; rieš hlavne teplotu (to je tu hlavný problém).\"");
        sb.AppendLine();

        sb.AppendLine("DÔLEŽITÉ:");
        sb.AppendLine("- NEVYMÝŠĽAJ konkrétne dni ani konkrétne denné hodnoty, ak nie sú v metrics.*.Data[].");
        sb.AppendLine("- Ak je v Data[] len 1 bod (napr. len „Utorok“), explicitne napíš: „K dispozícii je zatiaľ len 1 denný priemer, trend sa nedá spoľahlivo určiť.“");
        sb.AppendLine("- Ak je Max výrazne nad Avg, môžeš slovne spomenúť „občasné špičky“, ale bez vymýšľania kedy presne.");
        sb.AppendLine();

        // ============================================================
        // Output contract
        // ============================================================
        sb.AppendLine("Formát odpovede MUSÍ byť vždy JEDEN JSON objekt (bez markdownu):");
        sb.AppendLine("{ \"action\": \"ask\", \"question\": \"...\" }");
        sb.AppendLine("alebo");
        sb.AppendLine("{ \"action\": \"tool\", \"tool\": \"ToolName\", \"args\": { ... } }");
        sb.AppendLine("alebo");
        sb.AppendLine("{ \"action\": \"answer\", \"text\": \"...\" }");
        sb.AppendLine();

        sb.AppendLine("Dostupné nástroje:");
        foreach (var t in tools)
            sb.AppendLine($"- {t.Name}: {t.Description}");
        sb.AppendLine();

        sb.AppendLine("Poznámka k SQL: vždy generuj bezpečné SELECT dotazy s parametrami. Nikdy nepoužívaj INSERT/UPDATE/DELETE/DDL.");
        sb.AppendLine("Preferuj výstup stĺpcov ako: Dt (timestamp) a Value (numeric) pre časové rady.");

        return sb.ToString();
    }

    private static void Trim(List<ChatMessage> messages, int max)
    {
        if (messages.Count <= max) return;
        var remove = messages.Count - max;
        messages.RemoveRange(1, remove); // keep system prompt at index 0
    }

    private static JsonDocument? ExtractFirstJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        var json = text.Substring(start, end - start + 1);
        try { return JsonDocument.Parse(json); } catch { return null; }
    }

    private static bool ArgsContainsValidSchemaHandle(string argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson)) return false;
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            if (!doc.RootElement.TryGetProperty("schemaHandle", out var sh)) return false;
            if (sh.ValueKind != JsonValueKind.String) return false;
            return Guid.TryParse(sh.GetString(), out _);
        }
        catch { return false; }
    }

    private static string InjectSchemaHandle(string argsJson, Guid schemaHandle)
    {
        // Merge/override schemaHandle into tool args (safe, keeps existing keys).
        try
        {
            JsonObject obj;

            if (string.IsNullOrWhiteSpace(argsJson))
            {
                obj = new JsonObject();
            }
            else
            {
                var node = JsonNode.Parse(argsJson);
                obj = node as JsonObject ?? new JsonObject();
            }

            obj["schemaHandle"] = schemaHandle.ToString();

            // preserve property names as-is
            return obj.ToJsonString(new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                WriteIndented = false
            });
        }
        catch
        {
            return JsonSerializer.Serialize(new { schemaHandle = schemaHandle.ToString() });
        }
    }

    private static Guid? TryExtractHandle(string toolResultJson)
    {
        if (string.IsNullOrWhiteSpace(toolResultJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(toolResultJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            if (doc.RootElement.TryGetProperty("handle", out var h)
                && h.ValueKind == JsonValueKind.String
                && Guid.TryParse(h.GetString(), out var g))
                return g;
        }
        catch { }
        return null;
    }

    private static string Trunc(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (max <= 0) return string.Empty;
        return s.Length <= max ? s : s.Substring(0, max) + "...";
    }

    // NEW: stable key for (tool + args)
    private static string NormalizeArgs(string? argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson))
            return "{}";

        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                WriteIndented = false
            });
        }
        catch
        {
            return argsJson.Trim();
        }
    }

    /// <summary>
    /// Tool results may come in multiple shapes:
    /// - supervisor action: { action:"ask"/"answer", ... }
    /// - builder-style: { ask:true, question:"..." }
    /// - run-style: { ok:false, error:"...", message:"...", hint:"..." }
    /// </summary>
    private static bool TryShortCircuitToolResult(string toolName, string? toolResult, out string immediateOut)
    {
        immediateOut = string.Empty;

        if (string.IsNullOrWhiteSpace(toolResult))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(toolResult);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var root = doc.RootElement;

            // 1) Supervisor-style { action: ... }
            if (root.TryGetProperty("action", out var actionProp) && actionProp.ValueKind == JsonValueKind.String)
            {
                var action = actionProp.GetString() ?? "";
                if (string.Equals(action, "answer", StringComparison.OrdinalIgnoreCase))
                {
                    if (root.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                    {
                        immediateOut = t.GetString() ?? "";
                        return true;
                    }
                }
                if (string.Equals(action, "ask", StringComparison.OrdinalIgnoreCase))
                {
                    if (root.TryGetProperty("question", out var q) && q.ValueKind == JsonValueKind.String)
                    {
                        immediateOut = q.GetString() ?? "Upresníš, prosím?";
                        return true;
                    }
                }
            }

            // 2) Builder-style { ask:true, question:"..." }
            if (root.TryGetProperty("ask", out var askProp) && askProp.ValueKind == JsonValueKind.True)
            {
                if (root.TryGetProperty("question", out var q) && q.ValueKind == JsonValueKind.String)
                {
                    immediateOut = q.GetString() ?? "Upresníš, prosím?";
                    return true;
                }
            }

            // 3) ok:false => return a readable error (prevents loops)
            if (root.TryGetProperty("ok", out var okProp) && okProp.ValueKind == JsonValueKind.False)
            {
                var error = root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
                var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
                var hint = root.TryGetProperty("hint", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null;

                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(error)) sb.Append(error);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    if (sb.Length > 0) sb.Append(": ");
                    sb.Append(message);
                }
                if (!string.IsNullOrWhiteSpace(hint))
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append(hint);
                }

                immediateOut = sb.Length > 0
                    ? sb.ToString()
                    : "Nastala chyba pri spracovaní požiadavky. Skús to prosím ešte raz.";
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseBuildSqlResult(string? buildSqlResultJson, out string sql, out string? parametersRaw, out string? reportRequestRaw)
    {
        sql = string.Empty;
        parametersRaw = null;
        reportRequestRaw = null;

        if (string.IsNullOrWhiteSpace(buildSqlResultJson)) return false;

        try
        {
            using var doc = JsonDocument.Parse(buildSqlResultJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            if (root.TryGetProperty("ask", out var askProp) && askProp.ValueKind == JsonValueKind.True)
                return false;

            if (!root.TryGetProperty("sql", out var sp) || sp.ValueKind != JsonValueKind.String)
                return false;

            sql = sp.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(sql))
                return false;

            if (root.TryGetProperty("parameters", out var pp) && pp.ValueKind != JsonValueKind.Undefined && pp.ValueKind != JsonValueKind.Null)
                parametersRaw = pp.GetRawText();

            if (root.TryGetProperty("reportRequest", out var rr) && rr.ValueKind != JsonValueKind.Undefined && rr.ValueKind != JsonValueKind.Null)
                reportRequestRaw = rr.GetRawText();

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildRunSqlArgs(string sql, string? parametersRaw)
    {
        var obj = new JsonObject
        {
            ["sql"] = sql
        };

        if (!string.IsNullOrWhiteSpace(parametersRaw))
        {
            try { obj["parameters"] = JsonNode.Parse(parametersRaw); } catch { /* ignore */ }
        }

        return obj.ToJsonString(new JsonSerializerOptions { PropertyNamingPolicy = null, WriteIndented = false });
    }

    private static string BuildProcessReportArgs(Guid rowsHandle, string? reportRequestRaw)
    {
        var obj = new JsonObject
        {
            ["rowsHandle"] = rowsHandle.ToString()
        };

        if (!string.IsNullOrWhiteSpace(reportRequestRaw))
        {
            try { obj["reportRequest"] = JsonNode.Parse(reportRequestRaw); } catch { /* ignore */ }
        }

        return obj.ToJsonString(new JsonSerializerOptions { PropertyNamingPolicy = null, WriteIndented = false });
    }

    private static string BuildPresentReportArgs(Guid reportHandle, string? reportRequestRaw)
    {
        var obj = new JsonObject
        {
            ["reportHandle"] = reportHandle.ToString()
        };

        if (!string.IsNullOrWhiteSpace(reportRequestRaw))
        {
            try { obj["reportRequest"] = JsonNode.Parse(reportRequestRaw); } catch { /* ignore */ }
        }

        return obj.ToJsonString(new JsonSerializerOptions { PropertyNamingPolicy = null, WriteIndented = false });
    }

    private static bool TryParseOkHandle(string? json, out Guid handle, out string? errorOut)
    {
        handle = default;
        errorOut = null;

        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            // if ok=false, prepare an error string
            if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
            {
                var error = root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
                var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
                var hint = root.TryGetProperty("hint", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null;

                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(error)) sb.Append(error);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    if (sb.Length > 0) sb.Append(": ");
                    sb.Append(message);
                }
                if (!string.IsNullOrWhiteSpace(hint))
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append(hint);
                }

                errorOut = sb.Length > 0 ? sb.ToString() : "Nastala chyba.";
                return false;
            }

            if (!root.TryGetProperty("handle", out var hp) || hp.ValueKind != JsonValueKind.String)
                return false;

            return Guid.TryParse(hp.GetString(), out handle);
        }
        catch
        {
            return false;
        }
    }

    private static string? TryParsePresentText(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
                return null;

            if (root.TryGetProperty("text", out var tp) && tp.ValueKind == JsonValueKind.String)
                return tp.GetString();

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string FinalizeAndStore(List<ChatMessage> messages, string text)
    {
        lock (messages)
        {
            messages.Add(new ChatMessage(ChatRole.Assistant, text));
            Trim(messages, 60);
        }
        return text;
    }
}