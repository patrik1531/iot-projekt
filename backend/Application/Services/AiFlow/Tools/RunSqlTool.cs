using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Services.AiFlow;
using Npgsql;

namespace Application.Services.AiFlow.Tools;

public sealed class RunSqlTool : IAiTool
{
    private readonly SqlQueryRunner _runner;
    private readonly AiSqlSafety _safety;

    public RunSqlTool(SqlQueryRunner runner, AiSqlSafety safety)
    {
        _runner = runner;
        _safety = safety;
    }

    public string Name => "RunSql";

    public string Description =>
        "Executes a SAFE parameterized SELECT SQL and stores result in memory. Args: { sql: string, parameters?: [{name,value}] }";

    public async Task<string> InvokeAsync(string argsJson, AiToolContext ctx, CancellationToken ct)
    {
        string sql = "";
        var parms = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

            if (doc.RootElement.TryGetProperty("sql", out var sp) && sp.ValueKind == JsonValueKind.String)
                sql = sp.GetString() ?? "";

            if (doc.RootElement.TryGetProperty("parameters", out var pp) && pp.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in pp.EnumerateArray())
                {
                    var name = p.TryGetProperty("name", out var np) ? np.GetString() : null;
                    var value = p.TryGetProperty("value", out var vp) ? vp.GetString() : null;

                    if (string.IsNullOrWhiteSpace(name) || value == null)
                        continue;

                    parms[NormalizeParamName(name)] = value;
                }
            }
        }
        catch
        {
            // ignore parse errors
        }

        if (string.IsNullOrWhiteSpace(sql))
            return JsonSerializer.Serialize(new { ok = false, error = "Missing sql" });

        if (!_safety.IsSafeSelect(sql, out var reason))
            return JsonSerializer.Serialize(new { ok = false, error = "Unsafe SQL", reason });

        List<Dictionary<string, object?>> rows;

        try
        {
            rows = await _runner.ExecuteSelectAsync(sql, parms, ct);
        }
        catch (PostgresException pg)
        {
            return JsonSerializer.Serialize(new
            {
                ok = false,
                error = "Postgres error",
                sqlState = pg.SqlState,
                message = pg.MessageText,
                hint = "Ak filtruješ čas, používaj v SQL (@from::timestamptz) a (@to::timestamptz). Skontroluj aj názvy tabuliek/stĺpcov v úvodzovkách."
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        catch (NpgsqlException ex)
        {
            return JsonSerializer.Serialize(new
            {
                ok = false,
                error = "Database connection/query failed",
                message = ex.Message
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                ok = false,
                error = "RunSql failed",
                message = ex.Message
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        var handle = ctx.Memory.Put(ctx.ChatId, rows);

        return JsonSerializer.Serialize(new
        {
            ok = true,
            handle = handle.ToString(),
            rowCount = rows.Count,
            sampleRows = rows.Take(3).ToList()
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private static string NormalizeParamName(string name)
    {
        name = name.Trim();
        if (name.StartsWith("@", StringComparison.Ordinal)) return name.Substring(1);
        if (name.StartsWith(":", StringComparison.Ordinal)) return name.Substring(1);
        return name;
    }

    private static object ParseBestEffort(string raw)
    {
        var s = raw.Trim();

        if (bool.TryParse(s, out var b)) return b;

        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;

        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;

        if (DateTimeOffset.TryParse(
                s,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
            return dto;

        return s;
    }
}
