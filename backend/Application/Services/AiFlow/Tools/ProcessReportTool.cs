using System.Globalization;
using System.Text.Json;
using Application.Dto;

namespace Application.Services.AiFlow.Tools;

/// <summary>
/// Self-contained report processor.
/// 
/// DI note:
/// This tool intentionally DOES NOT depend on IDataProcessorAgent,
/// so deleting/renaming "old agents" won't break the app startup.
/// </summary>
public sealed class ProcessReportTool : IAiTool
{
    public string Name => "ProcessReport";

    public string Description =>
        "Converts raw SQL rows into a structured ReportResponse (buckets/overall). Args: { rowsHandle: string, reportRequest?: { metric, from, to, intervalMinutes, stats?:[], topN?:number } }";

    public Task<string> InvokeAsync(string argsJson, AiToolContext ctx, CancellationToken ct)
    {
        Guid rowsHandle = default;
        var req = new ReportRequest();

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
            if (doc.RootElement.TryGetProperty("rowsHandle", out var rh) && rh.ValueKind == JsonValueKind.String)
            {
                var s = rh.GetString();
                if (!string.IsNullOrWhiteSpace(s)) Guid.TryParse(s, out rowsHandle);
            }

            if (doc.RootElement.TryGetProperty("reportRequest", out var rr) && rr.ValueKind == JsonValueKind.Object)
            {
                if (rr.TryGetProperty("metric", out var mp) && mp.ValueKind == JsonValueKind.String)
                    req.Metric = mp.GetString() ?? string.Empty;

                if (rr.TryGetProperty("from", out var fp) && fp.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(fp.GetString(), null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var from))
                    req.From = from;

                if (rr.TryGetProperty("to", out var tp) && tp.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(tp.GetString(), null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var to))
                    req.To = to;

                if (rr.TryGetProperty("intervalMinutes", out var ip) && ip.ValueKind == JsonValueKind.Number)
                    req.IntervalMinutes = ip.GetInt32();

                if (rr.TryGetProperty("topN", out var tn) && tn.ValueKind == JsonValueKind.Number)
                    req.TopN = tn.GetInt32();

                if (rr.TryGetProperty("stats", out var st) && st.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var s in st.EnumerateArray())
                    {
                        if (s.ValueKind == JsonValueKind.String)
                        {
                            var v = s.GetString();
                            if (!string.IsNullOrWhiteSpace(v)) list.Add(v);
                        }
                    }
                    if (list.Count > 0) req.Stats = list.ToArray();
                }
            }
        }
        catch
        {
            // ignore
        }

        if (rowsHandle == default)
            return Task.FromResult(JsonSerializer.Serialize(new { ok = false, error = "Missing rowsHandle" }));

        if (!ctx.Memory.TryGet<List<Dictionary<string, object?>>>(ctx.ChatId, rowsHandle, out var rows) || rows == null)
            return Task.FromResult(JsonSerializer.Serialize(new { ok = false, error = "Unknown rowsHandle" }));

        // Convert rows -> points (Dt + Value)
        var points = new List<(DateTime Dt, double Value)>();
        foreach (var r in rows)
        {
            if (!TryGetDt(r, out var dt)) continue;
            if (!TryGetDouble(r, out var value)) continue;
            points.Add((dt.ToUniversalTime(), value));
        }

        if (points.Count == 0)
        {
            var empty = new ReportResponse();
            var emptyHandle = ctx.Memory.Put(ctx.ChatId, empty);
            return Task.FromResult(JsonSerializer.Serialize(new { ok = true, handle = emptyHandle.ToString(), buckets = 0 },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }

        var processed = BuildReport(points, req);
        var handle = ctx.Memory.Put(ctx.ChatId, processed);

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            ok = true,
            handle = handle.ToString(),
            buckets = processed.Buckets?.Count ?? 0,
            overall = processed.Overall
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    private static ReportResponse BuildReport(List<(DateTime Dt, double Value)> points, ReportRequest req)
    {
        var resp = new ReportResponse();

        var intervalMinutes = req.IntervalMinutes ?? 60;
        if (intervalMinutes <= 0) intervalMinutes = 60;

        var from = (req.From ?? points.Min(p => p.Dt)).ToUniversalTime();
        var to = (req.To ?? points.Max(p => p.Dt)).ToUniversalTime();

        // normalize start to bucket boundary
        var startMinute = (from.Minute / intervalMinutes) * intervalMinutes;
        var cursor = new DateTime(from.Year, from.Month, from.Day, from.Hour, startMinute, 0, DateTimeKind.Utc);
        if (cursor > from) cursor = cursor.AddMinutes(-intervalMinutes);

        var buckets = new List<ReportBucket>();
        while (cursor <= to)
        {
            var end = cursor.AddMinutes(intervalMinutes);
            var inBucket = points
                .Where(p => p.Dt >= cursor && p.Dt < end)
                .Select(p => p.Value)
                .ToList();

            var values = new Dictionary<string, double>();
            if (inBucket.Count > 0)
            {
                values["avg"] = Math.Round(inBucket.Average(), 4);
                values["min"] = Math.Round(inBucket.Min(), 4);
                values["max"] = Math.Round(inBucket.Max(), 4);
                values["count"] = inBucket.Count;
            }

            buckets.Add(new ReportBucket { Label = cursor.ToString("u"), Values = values });
            cursor = end;
        }

        resp.Buckets = buckets;

        // overall
        var all = points.Select(p => p.Value).ToList();
        resp.Overall = new Dictionary<string, double>
        {
            ["avg"] = Math.Round(all.Average(), 4),
            ["min"] = Math.Round(all.Min(), 4),
            ["max"] = Math.Round(all.Max(), 4),
            ["median"] = Math.Round(GetPercentile(all, 50), 4),
            ["p90"] = Math.Round(GetPercentile(all, 90), 4),
            ["count"] = all.Count
        };

        // requested stats
        if (req.Stats != null && req.Stats.Length > 0)
        {
            resp.Stats = new Dictionary<string, double>();
            foreach (var s in req.Stats)
            {
                var key = (s ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(key)) continue;

                if (key == "median") resp.Stats[key] = Math.Round(GetPercentile(all, 50), 4);
                else if (key == "p90" || key == "p-90" || key == "90th") resp.Stats["p90"] = Math.Round(GetPercentile(all, 90), 4);
                else if (key == "avg") resp.Stats[key] = Math.Round(all.Average(), 4);
                else if (key == "min") resp.Stats[key] = Math.Round(all.Min(), 4);
                else if (key == "max") resp.Stats[key] = Math.Round(all.Max(), 4);
                else if (key == "count") resp.Stats[key] = all.Count;
            }
        }

        // top N
        if (req.TopN.HasValue && req.TopN.Value > 0)
        {
            resp.TopReadings = points
                .OrderByDescending(p => p.Value)
                .Take(req.TopN.Value)
                .Select(p => new ReadingDto { Dt = p.Dt.ToString("o"), Value = p.Value })
                .ToList();
        }

        return resp;
    }

    private static double GetPercentile(List<double> seq, double percentile)
    {
        if (seq.Count == 0) return 0;
        var sorted = seq.OrderBy(x => x).ToList();
        var n = (sorted.Count - 1) * percentile / 100.0 + 1;
        if (n <= 1) return sorted[0];
        if (n >= sorted.Count) return sorted[^1];
        var k = (int)n;
        var d = n - k;
        return sorted[k - 1] + d * (sorted[k] - sorted[k - 1]);
    }

    private static bool TryGetDt(Dictionary<string, object?> row, out DateTime dt)
    {
        dt = default;
        if (!TryGet(row, "dt", out var v) && !TryGet(row, "Dt", out v)) return false;
        if (v == null) return false;
        if (v is DateTime d) { dt = d; return true; }
        if (DateTime.TryParse(v.ToString(), null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            dt = parsed;
            return true;
        }
        return false;
    }

    private static bool TryGetDouble(Dictionary<string, object?> row, out double val)
    {
        val = default;
        if (!TryGet(row, "value", out var v) && !TryGet(row, "Value", out v)) return false;
        if (v == null) return false;
        if (v is double d) { val = d; return true; }
        if (v is float f) { val = f; return true; }
        if (v is int i) { val = i; return true; }
        if (v is long l) { val = l; return true; }
        return double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out val);
    }

    private static bool TryGet(Dictionary<string, object?> row, string key, out object? value)
    {
        foreach (var kv in row)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }
        value = null;
        return false;
    }
}
