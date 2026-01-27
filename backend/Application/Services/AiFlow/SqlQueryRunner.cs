using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Application.Data;

namespace Application.Services.AiFlow;

public sealed class SqlQueryRunner
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SqlQueryRunner(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<List<Dictionary<string, object?>>> ExecuteSelectAsync(string sql, IReadOnlyDictionary<string, string> parameters, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandType = CommandType.Text;

        foreach (var kv in parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = kv.Key.StartsWith("@") ? kv.Key : "@" + kv.Key;
            // Best-effort typing: ISO date -> DateTime, number -> double, otherwise string
            if (DateTime.TryParse(kv.Value, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var dt))
            {
                p.Value = dt;
            }
            else if (double.TryParse(kv.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
            {
                p.Value = d;
            }
            else
            {
                p.Value = kv.Value;
            }
            cmd.Parameters.Add(p);
        }

        var rows = new List<Dictionary<string, object?>>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                object? val = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
                dict[name] = val;
            }
            rows.Add(dict);
        }

        return rows;
    }
}
