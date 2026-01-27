using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.AI;
using Application.Services.AiFlow;

namespace Application.Services.AiFlow.Tools;

public sealed class BuildSqlTool : IAiTool
{
    private readonly IChatClient _chat;
    private readonly AiSqlSafety _safety;

    public BuildSqlTool(IChatClient chat, AiSqlSafety safety)
    {
        _chat = chat;
        _safety = safety;
    }

    public string Name => "BuildSql";

    public string Description =>
        "Uses AI to convert user's intent into a SAFE parameterized SELECT SQL. Args: { userText: string, schemaHandle?: string }";

    public async Task<string> InvokeAsync(string argsJson, AiToolContext ctx, CancellationToken ct)
    {
        string userText = "";
        Guid? schemaHandle = null;

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
            if (doc.RootElement.TryGetProperty("userText", out var ut) && ut.ValueKind == JsonValueKind.String)
                userText = ut.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("schemaHandle", out var sh) && sh.ValueKind == JsonValueKind.String)
            {
                var s = sh.GetString();
                if (!string.IsNullOrWhiteSpace(s) && Guid.TryParse(s, out var g)) schemaHandle = g;
            }
        }
        catch
        {
            // ignore and let AI ask for clarification
        }

        DbSchema? schema = null;
        if (schemaHandle.HasValue)
            ctx.Memory.TryGet(ctx.ChatId, schemaHandle.Value, out schema);

        if (schema == null)
        {
            return JsonSerializer.Serialize(new
            {
                ask = true,
                question = "Chýba schéma databázy. Skús to prosím znovu (interná chyba: schemaHandle)."
            });
        }

        var schemaText = JsonSerializer.Serialize(new
        {
            tables = schema.Tables.Select(t => new { name = t.Name, columns = t.Columns })
        });

        var system = new StringBuilder();
        system.AppendLine("You are a strict SQL builder for a PostgreSQL database.");
        system.AppendLine("Convert the user's request into ONE SAFE parameterized SELECT query.");
        system.AppendLine("Rules:");
        system.AppendLine("- Output MUST be ONLY JSON (no markdown).");
        system.AppendLine("- SQL MUST be SELECT-only. No INSERT/UPDATE/DELETE/DDL. No semicolons.");
        system.AppendLine("- Use ONLY tables/columns from the provided schema.");
        system.AppendLine("- IMPORTANT: Use identifiers EXACTLY as in schema (case-sensitive). If table/column contains uppercase letters, wrap it in double quotes (e.g. FROM \"Temperature\").");
        system.AppendLine("- Always alias output columns exactly as: Dt (timestamp) and Value (numeric).");
        system.AppendLine("- ALWAYS use parameters for time range: @from and @to.");
        system.AppendLine("- CRITICAL: Since parameters may arrive as strings, ALWAYS CAST timestamp parameters in SQL:");
        system.AppendLine("  use (@from::timestamptz) and (@to::timestamptz) when comparing to timestamp columns.");
        system.AppendLine("- Example time filter:");
        system.AppendLine("  WHERE t.\"Dt\" >= (@from::timestamptz) AND t.\"Dt\" <= (@to::timestamptz)");
        system.AppendLine("- If the request is missing metric or time range, set ask=true and provide a single question.");
        system.AppendLine();
        system.AppendLine("Schema JSON:");
        system.AppendLine(schemaText);
        system.AppendLine();
        system.AppendLine("Return JSON with fields:");
        system.AppendLine("{ ask: boolean, question?: string, sql?: string, parameters?: [{name:string, value:string}], reportRequest?: { metric:string, from?:string, to?:string, intervalMinutes?:number } }");

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, system.ToString()),
            new(ChatRole.User, userText)
        };

        var resp = await _chat.GetResponseAsync(messages, cancellationToken: ct);
        var text = resp.Text ?? "";

        var parsed = ExtractJson(text);
        if (parsed == null)
        {
            return JsonSerializer.Serialize(new
            {
                ask = true,
                question = "Neviem z toho vytvoriť SQL. Napíš, prosím, akú metriku (napr. teplota/vlhkosť) a za aké obdobie."
            });
        }

        var root = parsed.RootElement;

        if (root.TryGetProperty("ask", out var askProp) && askProp.ValueKind == JsonValueKind.True)
        {
            var q = root.TryGetProperty("question", out var qp) ? qp.GetString() : null;
            return JsonSerializer.Serialize(new { ask = true, question = q ?? "Upresníš, prosím, aké údaje chceš?" });
        }

        var sql = root.TryGetProperty("sql", out var sp) ? (sp.GetString() ?? "") : "";
        if (string.IsNullOrWhiteSpace(sql))
        {
            return JsonSerializer.Serialize(new
            {
                ask = true,
                question = "AI nevygenerovalo SQL. Skús prosím opísať požiadavku inak.",
                reason = "Missing sql"
            });
        }

        if (!_safety.IsSafeSelect(sql, out var reason))
        {
            return JsonSerializer.Serialize(new
            {
                ask = true,
                question = "AI vygenerovalo nebezpečné alebo neplatné SQL. Skús prosím opísať požiadavku inak.",
                reason
            });
        }

        // store original response JSON in memory (optional)
        var handle = ctx.Memory.Put(ctx.ChatId, JsonSerializer.Deserialize<JsonElement>(root.GetRawText()));

        // --- clone parameters/reportRequest safely (JsonElement is a struct) ---
        JsonElement? parametersEl = null;
        if (root.TryGetProperty("parameters", out var pp) &&
            pp.ValueKind != JsonValueKind.Null &&
            pp.ValueKind != JsonValueKind.Undefined)
        {
            parametersEl = pp.Clone(); // <-- clone to detach from parsed document lifetime
        }

        JsonElement? reportRequestEl = null;
        if (root.TryGetProperty("reportRequest", out var rr) &&
            rr.ValueKind != JsonValueKind.Null &&
            rr.ValueKind != JsonValueKind.Undefined)
        {
            reportRequestEl = rr.Clone();
        }

        return JsonSerializer.Serialize(new
        {
            handle = handle.ToString(),
            sql,
            parameters = parametersEl,
            reportRequest = reportRequestEl
        });
    }

    private static JsonDocument? ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        var json = text.Substring(start, end - start + 1);
        try { return JsonDocument.Parse(json); } catch { return null; }
    }
}