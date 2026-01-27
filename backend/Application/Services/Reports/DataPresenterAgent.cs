using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.AI;
using Application.Dto;

namespace Application.Services.Reports;

public class DataPresenterAgent : IDataPresenterAgent
{
    public DataPresenterAgent() { }

    public async Task<string> PresentAsync(ReportRequest req, ReportResponse processed, CancellationToken ct)
    {
        // Always produce deterministic summary: overall avg/min/max, then per-bucket avg/min/max
        // If format=json requested, return JSON with the same structure.

        double? overallAvg = null, overallMin = null, overallMax = null;
        if (processed.Overall != null)
        {
            if (processed.Overall.TryGetValue("avg", out var oavg)) overallAvg = oavg;
            if (processed.Overall.TryGetValue("min", out var omin)) overallMin = omin;
            if (processed.Overall.TryGetValue("max", out var omax)) overallMax = omax;
        }

        // Fallback compute from buckets if overall missing
        if (!overallAvg.HasValue)
        {
            var allVals = processed.Buckets.SelectMany(b => b.Values.Values).ToList();
            if (allVals.Any())
            {
                overallAvg = Math.Round(allVals.Average(), 4);
                overallMin = Math.Round(allVals.Min(), 4);
                overallMax = Math.Round(allVals.Max(), 4);
            }
        }

        // Prepare buckets: format label according to requested interval
        int interval = req.IntervalMinutes ?? 60;

        string FormatLabel(string label)
        {
            if (!DateTime.TryParse(label, out var dt))
                return label;
            dt = dt.ToLocalTime();

            if (interval >= 30 * 24 * 60)
            {
                // months
                return dt.ToString("MM.yyyy");
            }
            else if (interval >= 24 * 60)
            {
                // days
                return dt.ToString("dd.MM.yyyy");
            }
            else if (interval >= 60)
            {
                // hours
                return dt.ToString("dd.MM.yyyy HH:00");
            }
            else
            {
                // minutes
                return dt.ToString("dd.MM.yyyy HH:mm");
            }
        }

        var bucketItems = new List<object>();
        foreach (var b in processed.Buckets)
        {
            double? avg = null, min = null, max = null;
            if (b.Values != null)
            {
                if (b.Values.TryGetValue("avg", out var a)) avg = a;
                if (b.Values.TryGetValue("min", out var mi)) min = mi;
                if (b.Values.TryGetValue("max", out var ma)) max = ma;
            }

            bucketItems.Add(new
            {
                label = FormatLabel(b.Label),
                avg,
                min,
                max
            });
        }

        if ((req.Format ?? string.Empty).ToLowerInvariant() == "json")
        {
            var outObj = new
            {
                overall = new { avg = overallAvg, min = overallMin, max = overallMax },
                buckets = bucketItems
            };
            return System.Text.Json.JsonSerializer.Serialize(outObj, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        }

        // Build Slovak textual output
        var sb = new System.Text.StringBuilder();
        if (overallAvg.HasValue || overallMin.HasValue || overallMax.HasValue)
        {
            sb.AppendLine($"Priemer: {overallAvg?.ToString() ?? "-"}, Min: {overallMin?.ToString() ?? "-"}, Max: {overallMax?.ToString() ?? "-"}");
        }
        else
        {
            sb.AppendLine("No data");
        }

        foreach (var obj in bucketItems)
        {
            var label = obj.GetType().GetProperty("label")!.GetValue(obj)?.ToString();
            var a = obj.GetType().GetProperty("avg")!.GetValue(obj);
            var mi = obj.GetType().GetProperty("min")!.GetValue(obj);
            var ma = obj.GetType().GetProperty("max")!.GetValue(obj);
            sb.AppendLine($"{label}: avg={a ?? "-"}, min={mi ?? "-"}, max={ma ?? "-"}");
        }

        return sb.ToString();
    }
}
