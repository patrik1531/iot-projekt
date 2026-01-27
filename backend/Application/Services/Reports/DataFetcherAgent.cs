using Application.Abstractions.AI;
using Application.Abstractions;
using Application.Dto;

namespace Application.Services.Reports;

public class DataFetcherAgent : IDataFetcherAgent
{
    private readonly ISensorRepository _sensorRepository;

    public DataFetcherAgent(ISensorRepository sensorRepository)
    {
        _sensorRepository = sensorRepository;
    }

    public async Task<object?> FetchAsync(ReportRequest req, CancellationToken ct)
    {
        var to = req.To ?? System.DateTime.UtcNow;
        System.DateTime from;
        if (req.From.HasValue)
            from = req.From.Value;
        else if (!string.IsNullOrEmpty(req.Preset) && req.Preset.StartsWith("last-"))
        {
            // e.g. last-2h, last-8h, last-7d, last-30d, last-365d, last-2w
            var parts = req.Preset.Split('-', System.StringSplitOptions.RemoveEmptyEntries);
            var token = parts.Length >= 2 ? parts[1] : null;
            if (token != null)
            {
                // ends with unit
                var lower = token.ToLowerInvariant();
                // number part
                var numPart = new string(lower.TakeWhile(c => char.IsDigit(c)).ToArray());
                if (int.TryParse(numPart, out var n) && n > 0)
                {
                    if (lower.EndsWith("h"))
                        from = to.AddHours(-n);
                    else if (lower.EndsWith("d"))
                        from = to.AddDays(-n);
                    else if (lower.EndsWith("w"))
                        from = to.AddDays(-7 * n);
                    else
                        from = to.AddHours(-n); // default assume hours
                }
                else
                {
                    from = to.AddHours(-2);
                }
            }
            else
            {
                from = to.AddHours(-2);
            }
        }
        else
        {
            from = to.AddHours(-2);
        }

        var metric = (req.Metric ?? string.Empty).ToLowerInvariant();

        var type = metric switch
        {
            "temperature" => typeof(Application.Models.Sensors.Temperature),
            "humidity" => typeof(Application.Models.Sensors.Humidity),
            "air_quality" => typeof(Application.Models.Sensors.AirQuality),
            "air_quality_percent" => typeof(Application.Models.Sensors.AirQualityPercent),
            "motion" => typeof(Application.Models.Sensors.Motion),
            "window" => typeof(Application.Models.Sensors.Window),
            _ => null
        };

        if (type == null) return null;

        var method = _sensorRepository.GetType().GetMethod("GetByDateRangeAsync")?.MakeGenericMethod(type);
        if (method == null) return null;

        var task = (Task)method.Invoke(_sensorRepository, new object[] { from, to })!;
        await task.ConfigureAwait(false);
        var resultProperty = task.GetType().GetProperty("Result");
        var listObj = resultProperty?.GetValue(task);
        if (listObj == null) return new List<object>();

        return ((IEnumerable<object>)listObj).ToList();
    }
}
