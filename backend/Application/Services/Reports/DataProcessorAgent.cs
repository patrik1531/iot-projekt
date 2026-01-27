using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.AI;
using Application.Dto;
using System.Reflection;

namespace Application.Services.Reports;

public class DataProcessorAgent : IDataProcessorAgent
{
    public Task<ReportResponse> ProcessAsync(object rawData, ReportRequest req, CancellationToken ct)
    {
        var resp = new ReportResponse();

        if (rawData == null) return Task.FromResult(resp);

        if (rawData is ReportResponse rr)
            return Task.FromResult(rr);

        if (rawData is IEnumerable<object> items)
        {
            var points = new List<(DateTime Dt, double Value)>();

            foreach (var it in items)
            {
                if (it == null) continue;

                var tProp = it.GetType().GetProperty("Dt") ?? it.GetType().GetProperty("dt") ?? it.GetType().GetProperty("Time");
                var vProp = it.GetType().GetProperty("Value") ?? it.GetType().GetProperty("value") ?? it.GetType().GetProperty("Val");

                if (tProp == null || vProp == null) continue;
                var tObj = tProp.GetValue(it);
                var vObj = vProp.GetValue(it);

                if (tObj == null || vObj == null) continue;

                DateTime dt;
                if (tObj is DateTime ddt) dt = ddt;
                else if (DateTime.TryParse(tObj.ToString(), out var parsed)) dt = parsed;
                else continue;

                if (!double.TryParse(vObj.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val))
                    continue;

                points.Add((dt.ToUniversalTime(), val));
            }

            if (!points.Any()) return Task.FromResult(resp);

            // determine interval
            var intervalMinutes = req.IntervalMinutes ?? 60;
            var from = req.From ?? points.Min(p => p.Dt);
            var to = req.To ?? points.Max(p => p.Dt);

            // normalize from to bucket boundary
            var buckets = new List<ReportBucket>();
            var cursor = new DateTime(from.Year, from.Month, from.Day, from.Hour, (from.Minute / intervalMinutes) * intervalMinutes, 0, DateTimeKind.Utc);
            while (cursor <= to)
            {
                var end = cursor.AddMinutes(intervalMinutes);
                var inBucket = points.Where(p => p.Dt >= cursor && p.Dt < end).Select(p => p.Value).ToList();
                var valuesDict = new Dictionary<string,double>();
                if (inBucket.Any())
                {
                    valuesDict["avg"] = Math.Round(inBucket.Average(), 4);
                    valuesDict["min"] = Math.Round(inBucket.Min(), 4);
                    valuesDict["max"] = Math.Round(inBucket.Max(), 4);
                    valuesDict["count"] = inBucket.Count;
                }

                buckets.Add(new ReportBucket { Label = cursor.ToString("u"), Values = valuesDict });
                cursor = end;
            }

            resp.Buckets = buckets;

            // overall stats
            var all = points.Select(p => p.Value).ToList();
            if (all.Any())
            {
                resp.Overall = new Dictionary<string,double>
                {
                    ["avg"] = Math.Round(all.Average(),4),
                    ["min"] = Math.Round(all.Min(),4),
                    ["max"] = Math.Round(all.Max(),4),
                    ["median"] = Math.Round(GetPercentile(all,50),4),
                    ["p90"] = Math.Round(GetPercentile(all,90),4),
                    ["count"] = all.Count
                };

                // compute requested stats
                if (req.Stats != null && req.Stats.Any())
                {
                    resp.Stats = new Dictionary<string,double>();
                    foreach (var s in req.Stats)
                    {
                        var key = s.ToLowerInvariant();
                        if (key == "median") resp.Stats[key] = Math.Round(GetPercentile(all,50),4);
                        else if (key == "p90") resp.Stats[key] = Math.Round(GetPercentile(all,90),4);
                        else if (key == "avg") resp.Stats[key] = Math.Round(all.Average(),4);
                        else if (key == "min") resp.Stats[key] = Math.Round(all.Min(),4);
                        else if (key == "max") resp.Stats[key] = Math.Round(all.Max(),4);
                        else if (key == "count") resp.Stats[key] = all.Count;
                    }
                }

                // top N readings
                if (req.TopN.HasValue && req.TopN.Value > 0)
                {
                    var top = points.OrderByDescending(p => p.Value).Take(req.TopN.Value);
                    resp.TopReadings = top.Select(p => new ReadingDto { Dt = p.Dt.ToString("o"), Value = p.Value }).ToList();
                }
            }

            return Task.FromResult(resp);
        }

        return Task.FromResult(resp);
    }

    private static double GetPercentile(List<double> seq, double percentile)
    {
        if (seq == null || !seq.Any()) return 0;
        var sorted = seq.OrderBy(x => x).ToList();
        var N = sorted.Count;
        var n = (N - 1) * percentile / 100.0 + 1;
        if (n == 1d) return sorted[0];
        if (n == N) return sorted[N-1];
        var k = (int)n;
        var d = n - k;
        return sorted[k-1] + d * (sorted[k] - sorted[k-1]);
    }
}
