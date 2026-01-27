using System.Text.Json;
using Application.Abstractions;
using Application.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Services.AiFlow.Tools;

public sealed class GetLastWeekOverviewTool : IAiTool
{
    public string Name => "GetLastWeekOverview";

    public string Description =>
        "Returns last-week sensor stats from SensorService (no SQL). " +
        "Args: { metrics?: [\"temperature\",\"humidity\",\"airQuality\"] }. " +
        "Output: { ok, range, generatedAtUtc, metrics:{...}, flags:{...} }";

    private readonly ISensorService _sensor;
    private readonly ILogger<GetLastWeekOverviewTool> _log;

    public GetLastWeekOverviewTool(ISensorService sensor)
        : this(sensor, NullLogger<GetLastWeekOverviewTool>.Instance)
    {
    }

    public GetLastWeekOverviewTool(ISensorService sensor, ILogger<GetLastWeekOverviewTool> log)
    {
        _sensor = sensor;
        _log = log ?? NullLogger<GetLastWeekOverviewTool>.Instance;
    }

    public async Task<string> InvokeAsync(string argsJson, AiToolContext ctx, CancellationToken ct)
    {
        try
        {
            var metricsArg = ParseMetricsArg(argsJson);
            var metrics = NormalizeMetrics(metricsArg);

            SensorStatsResponse? t = null;
            SensorStatsResponse? h = null;
            SensorStatsResponse? aq = null;

            if (metrics.Contains("temperature"))
                t = await _sensor.GetTemperatureLastWeekAsync();

            if (metrics.Contains("humidity"))
                h = await _sensor.GetHumidityLastWeekAsync();

            if (metrics.Contains("airQuality"))
                aq = await _sensor.GetAirQualityLastWeekAsync();

            var flags = BuildFlags(t, h, aq);

            var result = new
            {
                ok = true,
                range = "lastWeek",
                generatedAtUtc = DateTime.UtcNow,
                metrics = new
                {
                    temperature = t,
                    humidity = h,
                    airQuality = aq
                },
                flags
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                WriteIndented = false
            });
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "GetLastWeekOverview failed (chatId={ChatId})", ctx.ChatId);

            return JsonSerializer.Serialize(new
            {
                ok = false,
                error = "GetLastWeekOverview failed",
                message = ex.Message
            });
        }
    }

    private static List<string>? ParseMetricsArg(string argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson))
            return null;

        using var doc = JsonDocument.Parse(argsJson);

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return null;

        if (!doc.RootElement.TryGetProperty("metrics", out var m))
            return null;

        if (m.ValueKind != JsonValueKind.Array)
            return null;

        var list = new List<string>();
        foreach (var it in m.EnumerateArray())
        {
            if (it.ValueKind == JsonValueKind.String)
                list.Add(it.GetString() ?? "");
        }

        return list;
    }

    private static HashSet<string> NormalizeMetrics(List<string>? metrics)
    {
        // default = all
        if (metrics == null || metrics.Count == 0)
            return new HashSet<string>(new[] { "temperature", "humidity", "airQuality" }, StringComparer.OrdinalIgnoreCase);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in metrics)
        {
            var v = (m ?? "").Trim().ToLowerInvariant();

            if (v is "temperature" or "temp") set.Add("temperature");
            else if (v is "humidity" or "hum") set.Add("humidity");
            else if (v is "airquality" or "air_quality" or "co2") set.Add("airQuality");
        }

        // if user passed only invalid strings, fallback to all
        if (set.Count == 0)
            set = new HashSet<string>(new[] { "temperature", "humidity", "airQuality" }, StringComparer.OrdinalIgnoreCase);

        return set;
    }

    private static object BuildFlags(SensorStatsResponse? t, SensorStatsResponse? h, SensorStatsResponse? aq)
    {
        var flags = new Dictionary<string, object>();

        if (t != null)
        {
            flags["temperature"] = new
            {
                avg = t.Avg,
                tooCold = t.Avg < 20,
                tooWarm = t.Avg > 26
            };
        }

        if (h != null)
        {
            flags["humidity"] = new
            {
                avg = h.Avg,
                tooDry = h.Avg < 35,
                tooHumid = h.Avg > 60
            };
        }

        if (aq != null)
        {
            flags["airQuality"] = new
            {
                avg = aq.Avg,
                elevated = aq.Avg >= 1000,
                high = aq.Avg >= 1500,
                veryHigh = aq.Avg >= 2000
            };
        }

        return flags;
    }
}