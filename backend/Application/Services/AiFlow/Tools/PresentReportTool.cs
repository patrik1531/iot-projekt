using System.Globalization;
using System.Text;
using System.Text.Json;
using Application.Dto;

namespace Application.Services.AiFlow.Tools;

/// <summary>
/// Self-contained report presenter.
/// 
/// DI note:
/// This tool intentionally DOES NOT depend on IDataPresenterAgent.
/// </summary>
public sealed class PresentReportTool : IAiTool
{
    public string Name => "PresentReport";

    public string Description =>
        "Formats a ReportResponse into structured JSON + human-readable text. Args: { reportHandle: string, reportRequest?: { metric, intervalMinutes, format, maxBuckets } }";

    // Internal view model for output buckets (strict typing, usable for FE/BE)
    private sealed record PresentBucket(
        string RawLabel,
        string Label,
        string? DtLocal,
        double? Avg,
        double? Min,
        double? Max
    );

    public Task<string> InvokeAsync(string argsJson, AiToolContext ctx, CancellationToken ct)
    {
        Guid handle = default;
        var req = new ReportRequest();
        int? maxBuckets = null;

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);

            if (doc.RootElement.TryGetProperty("reportHandle", out var rh) && rh.ValueKind == JsonValueKind.String)
            {
                var s = rh.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    Guid.TryParse(s, out handle);
            }

            if (doc.RootElement.TryGetProperty("reportRequest", out var rr) && rr.ValueKind == JsonValueKind.Object)
            {
                if (rr.TryGetProperty("metric", out var mp) && mp.ValueKind == JsonValueKind.String)
                    req.Metric = mp.GetString() ?? string.Empty;

                if (rr.TryGetProperty("intervalMinutes", out var ip) && ip.ValueKind == JsonValueKind.Number)
                    req.IntervalMinutes = ip.GetInt32();

                if (rr.TryGetProperty("format", out var fp) && fp.ValueKind == JsonValueKind.String)
                    req.Format = fp.GetString() ?? "text";

                if (rr.TryGetProperty("maxBuckets", out var mb) && mb.ValueKind == JsonValueKind.Number)
                    maxBuckets = mb.GetInt32();
            }
        }
        catch
        {
            // ignore
        }

        if (handle == default)
            return Task.FromResult(JsonSerializer.Serialize(new { ok = false, error = "Missing reportHandle" }));

        if (!ctx.Memory.TryGet<ReportResponse>(ctx.ChatId, handle, out var rep) || rep == null)
            return Task.FromResult(JsonSerializer.Serialize(new { ok = false, error = "Unknown reportHandle" }));

        var outObj = Present(req, rep, maxBuckets);

        return Task.FromResult(JsonSerializer.Serialize(outObj, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }

    private static object Present(ReportRequest req, ReportResponse processed, int? maxBucketsOverride)
    {
        var metric = string.IsNullOrWhiteSpace(req.Metric) ? "Metric" : req.Metric.Trim();
        var interval = req.IntervalMinutes ?? 60;
        var unit = GetUnit(metric);

        // ----------------------------
        // Overall stats
        // ----------------------------
        double? overallAvg = null, overallMin = null, overallMax = null;
        if (processed.Overall != null)
        {
            if (processed.Overall.TryGetValue("avg", out var a)) overallAvg = a;
            if (processed.Overall.TryGetValue("min", out var mi)) overallMin = mi;
            if (processed.Overall.TryGetValue("max", out var ma)) overallMax = ma;
        }

        // fallback if overall missing
        if (!overallAvg.HasValue || !overallMin.HasValue || !overallMax.HasValue)
        {
            var allVals = processed.Buckets.SelectMany(b => b.Values.Values).ToList();
            if (allVals.Count > 0)
            {
                overallAvg ??= allVals.Average();
                overallMin ??= allVals.Min();
                overallMax ??= allVals.Max();
            }
        }

        // ----------------------------
        // Helpers for label parsing
        // ----------------------------
        static bool TryParseLabelDate(string label, out DateTime dtLocal)
        {
            dtLocal = default;

            if (!DateTime.TryParse(label, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt)
                && !DateTime.TryParse(label, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out dt))
                return false;

            dtLocal = dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
            return true;
        }

        string FormatLabel(string label)
        {
            if (!TryParseLabelDate(label, out var dt))
                return label;

            if (interval >= 30 * 24 * 60) return dt.ToString("MM.yyyy");
            if (interval >= 24 * 60) return dt.ToString("dd.MM.yyyy");
            if (interval >= 60) return dt.ToString("dd.MM.yyyy HH:00");
            return dt.ToString("dd.MM.yyyy HH:mm");
        }

        // ----------------------------
        // Determine display range (local)
        // ----------------------------
        DateTime? minDt = null, maxDt = null;
        foreach (var b in processed.Buckets)
        {
            if (TryParseLabelDate(b.Label, out var dt))
            {
                minDt = !minDt.HasValue || dt < minDt.Value ? dt : minDt;
                maxDt = !maxDt.HasValue || dt > maxDt.Value ? dt : maxDt;
            }
        }

        // ----------------------------
        // Structured buckets (for FE/BE)
        // ----------------------------
        var maxBuckets = Math.Clamp(maxBucketsOverride ?? 5000, 5, 50000);

        var ordered = processed.Buckets
            .Select(b =>
            {
                string? dtLocalStr = null;
                if (TryParseLabelDate(b.Label, out var dt)) dtLocalStr = dt.ToString("yyyy-MM-ddTHH:mm:ss");

                b.Values.TryGetValue("avg", out var a);
                b.Values.TryGetValue("min", out var mi);
                b.Values.TryGetValue("max", out var ma);

                return new PresentBucket(
                    RawLabel: b.Label,
                    Label: FormatLabel(b.Label),
                    DtLocal: dtLocalStr,
                    Avg: b.Values.ContainsKey("avg") ? a : null,
                    Min: b.Values.ContainsKey("min") ? mi : null,
                    Max: b.Values.ContainsKey("max") ? ma : null
                );
            })
            .OrderBy(x => x.DtLocal ?? "9999-12-31T23:59:59")
            .TakeLast(maxBuckets)
            .ToList();

        // ----------------------------
        // Text output (Telegram-friendly)
        // ----------------------------
        var text = BuildPrettyText(metric, unit, interval, minDt, maxDt, overallAvg, overallMin, overallMax, ordered);

        // Always return structured + text (best for FE + BE).
        return new
        {
            ok = true,
            text,
            metric,
            unit,
            intervalMinutes = interval,
            range = new
            {
                fromLocal = minDt?.ToString("yyyy-MM-ddTHH:mm:ss"),
                toLocal = maxDt?.ToString("yyyy-MM-ddTHH:mm:ss")
            },
            overall = new { avg = overallAvg, min = overallMin, max = overallMax },
            buckets = ordered.Select(b => new
            {
                rawLabel = b.RawLabel,
                label = b.Label,
                dtLocal = b.DtLocal,
                avg = b.Avg,
                min = b.Min,
                max = b.Max
            }).ToList()
        };
    }

    private static string BuildPrettyText(
        string metric,
        string unit,
        int intervalMinutes,
        DateTime? fromLocal,
        DateTime? toLocal,
        double? avg,
        double? min,
        double? max,
        List<PresentBucket> buckets)
    {
        string IntervalLabel(int minutes)
        {
            if (minutes >= 60 * 24) return $"{minutes / (60 * 24)} d";
            if (minutes % 60 == 0) return $"{minutes / 60} h";
            return $"{minutes} min";
        }

        static string Fmt(double? v, string unit)
        {
            if (!v.HasValue) return "-";
            var s = v.Value.ToString("0.##", CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(unit) ? s : $"{s} {unit}";
        }

        var sb = new StringBuilder();

        sb.AppendLine($"{GetMetricEmoji(metric)} {PrettyMetric(metric)}" +
                      (string.IsNullOrWhiteSpace(unit) ? "" : $" ({unit})"));
        sb.AppendLine($"Interval: {IntervalLabel(intervalMinutes)}");

        if (fromLocal.HasValue && toLocal.HasValue)
        {
            sb.AppendLine($"Od: {fromLocal:dd.MM.yyyy HH:mm}");
            sb.AppendLine($"Do: {toLocal:dd.MM.yyyy HH:mm}");
        }

        sb.AppendLine($"Počet bodov: {buckets.Count}");
        sb.AppendLine();

        if (avg.HasValue || min.HasValue || max.HasValue)
        {
            sb.AppendLine("📌 Súhrn");
            sb.AppendLine($"• Priemer: {Fmt(avg, unit)}");
            sb.AppendLine($"• Min: {Fmt(min, unit)}");
            sb.AppendLine($"• Max: {Fmt(max, unit)}");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("❗ Nenašli sa žiadne dáta v zadanom rozsahu.");
            return sb.ToString();
        }

        // show last 20 in text
        var show = buckets.Count > 20 ? buckets.TakeLast(20).ToList() : buckets;

        sb.AppendLine($"🕒 Hodnoty (posledných {show.Count} bodov)");
        foreach (var b in show)
        {
            var parts = new List<string>();
            if (b.Avg.HasValue) parts.Add($"avg {Fmt(b.Avg, unit)}");
            if (b.Min.HasValue) parts.Add($"min {Fmt(b.Min, unit)}");
            if (b.Max.HasValue) parts.Add($"max {Fmt(b.Max, unit)}");

            sb.AppendLine(parts.Count == 0
                ? $"• {b.Label}: -"
                : $"• {b.Label}: {string.Join(", ", parts)}");
        }

        if (buckets.Count > show.Count)
        {
            sb.AppendLine();
            sb.AppendLine($"…skrátené (v texte {show.Count} z {buckets.Count}).");
        }

        return sb.ToString();
    }

    private static string GetUnit(string metric)
    {
        metric = metric.Trim().ToLowerInvariant();

        if (metric.Contains("temp")) return "°C";
        if (metric.Contains("humid")) return "%";
        if (metric.Contains("percent")) return "%";
        if (metric.Contains("airquality")) return ""; // unknown
        return "";
    }

    private static string GetMetricEmoji(string metric)
    {
        metric = metric.Trim().ToLowerInvariant();

        if (metric.Contains("temp")) return "🌡️";
        if (metric.Contains("humid")) return "💧";
        if (metric.Contains("airquality")) return "🫁";
        if (metric.Contains("motion")) return "🚶";
        if (metric.Contains("window")) return "🪟";
        return "📊";
    }

    private static string PrettyMetric(string metric)
    {
        var m = metric.Trim();

        if (m.Equals("Temperature", StringComparison.OrdinalIgnoreCase)) return "Teplota";
        if (m.Equals("Humidity", StringComparison.OrdinalIgnoreCase)) return "Vlhkosť";
        if (m.Equals("AirQuality", StringComparison.OrdinalIgnoreCase)) return "Kvalita vzduchu";
        if (m.Equals("AirQualityPercent", StringComparison.OrdinalIgnoreCase)) return "Kvalita vzduchu (%)";
        if (m.Equals("Motion", StringComparison.OrdinalIgnoreCase)) return "Pohyb";
        if (m.Equals("Window", StringComparison.OrdinalIgnoreCase)) return "Okno";
        return m;
    }
}