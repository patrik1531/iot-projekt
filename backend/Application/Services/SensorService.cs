using System.Globalization;
using Application.Abstractions;
using Application.Dto;
using Application.Models.Sensors;

namespace Application.Services;

public class SensorService : ISensorService
{
    private readonly ISensorRepository _sensorRepository;

    public SensorService(ISensorRepository sensorRepository)
    {
        _sensorRepository = sensorRepository;
    }

    // ===== HELPER METHODS =====


    private static SensorStatsResponse BuildHourlyStats<T>(List<T> data, Func<T, double> valueSelector, Func<T, DateTime> dtSelector)
    {
        if (!data.Any())
            return new SensorStatsResponse { Avg = 0, Min = 0, Max = 0, Data = new List<SensorDataPoint>() };

        var values = data.Select(valueSelector).ToList();
        var grouped = data
            .GroupBy(x => dtSelector(x).Hour)
            .OrderBy(g => g.Key)
            .Select(g => new SensorDataPoint
            {
                Label = $"{g.Key:D2}:00",
                AvgValue = Math.Round(g.Average(valueSelector), 2)
            }).ToList();

        return new SensorStatsResponse
        {
            Avg = Math.Round(values.Average(), 2),
            Min = Math.Round(values.Min(), 2),
            Max = Math.Round(values.Max(), 2),
            Data = grouped
        };
    }

    private static SensorStatsResponse BuildDailyStats<T>(List<T> data, Func<T, double> valueSelector, Func<T, DateTime> dtSelector)
    {
        if (!data.Any())
            return new SensorStatsResponse { Avg = 0, Min = 0, Max = 0, Data = new List<SensorDataPoint>() };

        var values = data.Select(valueSelector).ToList();
        var grouped = data
            .GroupBy(x => dtSelector(x).Date)
            .OrderBy(g => g.Key)
            .Select(g => new SensorDataPoint
            {
                Label = g.Key.ToString("d.M."),
                AvgValue = Math.Round(g.Average(valueSelector), 2)
            }).ToList();

        return new SensorStatsResponse
        {
            Avg = Math.Round(values.Average(), 2),
            Min = Math.Round(values.Min(), 2),
            Max = Math.Round(values.Max(), 2),
            Data = grouped
        };
    }

    private static SensorStatsResponse BuildWeeklyStats<T>(List<T> data, Func<T, double> valueSelector, Func<T, DateTime> dtSelector)
    {
        if (!data.Any())
            return new SensorStatsResponse { Avg = 0, Min = 0, Max = 0, Data = new List<SensorDataPoint>() };

        var skCulture = new CultureInfo("sk-SK");
        var values = data.Select(valueSelector).ToList();
        var grouped = data
            .GroupBy(x => dtSelector(x).Date)
            .OrderBy(g => g.Key)
            .Select(g => 
            {
                var dayName = skCulture.DateTimeFormat.GetDayName(g.Key.DayOfWeek);
                return new SensorDataPoint
                {
                    Label = char.ToUpper(dayName[0]) + dayName.Substring(1),
                    AvgValue = Math.Round(g.Average(valueSelector), 2)
                };
            }).ToList();

        return new SensorStatsResponse
        {
            Avg = Math.Round(values.Average(), 2),
            Min = Math.Round(values.Min(), 2),
            Max = Math.Round(values.Max(), 2),
            Data = grouped
        };
    }

    private static SensorStatsResponse BuildMonthlyStats<T>(List<T> data, Func<T, double> valueSelector, Func<T, DateTime> dtSelector)
    {
        if (!data.Any())
            return new SensorStatsResponse { Avg = 0, Min = 0, Max = 0, Data = new List<SensorDataPoint>() };

        var skCulture = new System.Globalization.CultureInfo("sk-SK");
        var values = data.Select(valueSelector).ToList();
        var grouped = data
            .GroupBy(x => new { dtSelector(x).Year, dtSelector(x).Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => 
            {
                var monthName = skCulture.DateTimeFormat.GetMonthName(g.Key.Month);
                return new SensorDataPoint
                {
                    Label = char.ToUpper(monthName[0]) + monthName.Substring(1),
                    AvgValue = Math.Round(g.Average(valueSelector), 2)
                };
            }).ToList();

        return new SensorStatsResponse
        {
            Avg = Math.Round(values.Average(), 2),
            Min = Math.Round(values.Min(), 2),
            Max = Math.Round(values.Max(), 2),
            Data = grouped
        };
    }

    // ===== TEMPERATURE =====
    
    public async Task<SensorStatsResponse> GetTemperatureLast24HoursAsync()
    {
        var data = await _sensorRepository.GetLast24HoursAsync<Temperature>();
        return BuildHourlyStats(data, x => x.Value, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetTemperatureLastWeekAsync()
    {
        var data = await _sensorRepository.GetLastWeekAsync<Temperature>();
        return BuildWeeklyStats(data, x => x.Value, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetTemperatureLastMonthAsync()
    {
        var data = await _sensorRepository.GetLastMonthAsync<Temperature>();
        return BuildDailyStats(data, x => x.Value, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetTemperatureLastYearAsync()
    {
        var data = await _sensorRepository.GetLastYearAsync<Temperature>();
        return BuildMonthlyStats(data, x => x.Value, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetTemperatureByDateRangeAsync(DateTime from, DateTime to)
    {
        var data = await _sensorRepository.GetByDateRangeAsync<Temperature>(from, to);
        return BuildDailyStats(data, x => x.Value, x => x.Dt);
    }

    // ===== HUMIDITY =====
    
    public async Task<SensorStatsResponse> GetHumidityLast24HoursAsync()
    {
        var data = await _sensorRepository.GetLast24HoursAsync<Humidity>();
        return BuildHourlyStats(data, x => x.Value, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetHumidityLastWeekAsync()
    {
        var data = await _sensorRepository.GetLastWeekAsync<Humidity>();
        return BuildWeeklyStats(data, x => x.Value, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetHumidityLastMonthAsync()
    {
        var data = await _sensorRepository.GetLastMonthAsync<Humidity>();
        return BuildDailyStats(data, x => x.Value, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetHumidityLastYearAsync()
    {
        var data = await _sensorRepository.GetLastYearAsync<Humidity>();
        return BuildMonthlyStats(data, x => x.Value, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetHumidityByDateRangeAsync(DateTime from, DateTime to)
    {
        var data = await _sensorRepository.GetByDateRangeAsync<Humidity>(from, to);
        return BuildDailyStats(data, x => x.Value, x => x.Dt);
    }

    // ===== AIR QUALITY =====
    
    public async Task<SensorStatsResponse> GetAirQualityLast24HoursAsync()
    {
        var data = await _sensorRepository.GetLast24HoursAsync<AirQuality>();
        return BuildHourlyStats(data, x => x.Value, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetAirQualityLastWeekAsync()
    {
        var data = await _sensorRepository.GetLastWeekAsync<AirQuality>();
        return BuildWeeklyStats(data, x => x.Value, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetAirQualityLastMonthAsync()
    {
        var data = await _sensorRepository.GetLastMonthAsync<AirQuality>();
        return BuildDailyStats(data, x => x.Value, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetAirQualityLastYearAsync()
    {
        var data = await _sensorRepository.GetLastYearAsync<AirQuality>();
        return BuildMonthlyStats(data, x => x.Value, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetAirQualityByDateRangeAsync(DateTime from, DateTime to)
    {
        var data = await _sensorRepository.GetByDateRangeAsync<AirQuality>(from, to);
        return BuildDailyStats(data, x => x.Value, x => x.Dt);
    }

    // ===== AIR QUALITY PERCENT =====
    
    public async Task<SensorStatsResponse> GetAirQualityPercentLast24HoursAsync()
    {
        var data = await _sensorRepository.GetLast24HoursAsync<AirQualityPercent>();
        return BuildHourlyStats(data, x => x.Value, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetAirQualityPercentLastWeekAsync()
    {
        var data = await _sensorRepository.GetLastWeekAsync<AirQualityPercent>();
        return BuildWeeklyStats(data, x => x.Value, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetAirQualityPercentLastMonthAsync()
    {
        var data = await _sensorRepository.GetLastMonthAsync<AirQualityPercent>();
        return BuildDailyStats(data, x => x.Value, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetAirQualityPercentLastYearAsync()
    {
        var data = await _sensorRepository.GetLastYearAsync<AirQualityPercent>();
        return BuildMonthlyStats(data, x => x.Value, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetAirQualityPercentByDateRangeAsync(DateTime from, DateTime to)
    {
        var data = await _sensorRepository.GetByDateRangeAsync<AirQualityPercent>(from, to);
        return BuildDailyStats(data, x => x.Value, x => x.Dt);
    }

    // ===== MOTION (bool -> 1/0) =====
    
    public async Task<SensorStatsResponse> GetMotionLast24HoursAsync()
    {
        var data = await _sensorRepository.GetLast24HoursAsync<Motion>();
        return BuildHourlyStats(data, x => x.Value ? 1.0 : 0.0, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetMotionLastWeekAsync()
    {
        var data = await _sensorRepository.GetLastWeekAsync<Motion>();
        return BuildWeeklyStats(data, x => x.Value ? 1.0 : 0.0, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetMotionLastMonthAsync()
    {
        var data = await _sensorRepository.GetLastMonthAsync<Motion>();
        return BuildDailyStats(data, x => x.Value ? 1.0 : 0.0, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetMotionLastYearAsync()
    {
        var data = await _sensorRepository.GetLastYearAsync<Motion>();
        return BuildMonthlyStats(data, x => x.Value ? 1.0 : 0.0, x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetMotionByDateRangeAsync(DateTime from, DateTime to)
    {
        var data = await _sensorRepository.GetByDateRangeAsync<Motion>(from, to);
        return BuildDailyStats(data, x => x.Value ? 1.0 : 0.0, x => x.Dt);
    }

    // ===== WINDOW (string -> parse or 0) =====
    
    private static double ParseWindowValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        if (double.TryParse(value, out var result)) return result;
        if (value.ToLower() == "open" || value.ToLower() == "true") return 1;
        return 0;
    }

    public async Task<SensorStatsResponse> GetWindowLast24HoursAsync()
    {
        var data = await _sensorRepository.GetLast24HoursAsync<Window>();
        return BuildHourlyStats(data, x => ParseWindowValue(x.Value), x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetWindowLastWeekAsync()
    {
        var data = await _sensorRepository.GetLastWeekAsync<Window>();
        return BuildWeeklyStats(data, x => ParseWindowValue(x.Value), x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetWindowLastMonthAsync()
    {
        var data = await _sensorRepository.GetLastMonthAsync<Window>();
        return BuildDailyStats(data, x => ParseWindowValue(x.Value), x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetWindowLastYearAsync()
    {
        var data = await _sensorRepository.GetLastYearAsync<Window>();
        return BuildMonthlyStats(data, x => ParseWindowValue(x.Value), x => x.Dt);
    }

    public async Task<SensorStatsResponse> GetWindowByDateRangeAsync(DateTime from, DateTime to)
    {
        var data = await _sensorRepository.GetByDateRangeAsync<Window>(from, to);
        return BuildDailyStats(data, x => ParseWindowValue(x.Value), x => x.Dt);
    }

    // ===== LATEST =====

    public async Task<SensorLatestDto?> GetTemperatureLatestAsync()
    {
        var latest = await _sensorRepository.GetLatestAsync<Temperature>();
        if (latest == null) return null;
        return new SensorLatestDto
        {
            Value = latest.Value,
            Name = nameof(Temperature),
            Dt = latest.Dt,
            Units = latest.Units
        };
    }

    public async Task<SensorLatestDto?> GetHumidityLatestAsync()
    {
        var latest = await _sensorRepository.GetLatestAsync<Humidity>();
        if (latest == null) return null;
        return new SensorLatestDto { Value = latest.Value, Name = nameof(Humidity), Dt = latest.Dt, Units = latest.Units };
    }

    public async Task<SensorLatestDto?> GetAirQualityLatestAsync()
    {
        var latest = await _sensorRepository.GetLatestAsync<AirQuality>();
        if (latest == null) return null;
        return new SensorLatestDto { Value = latest.Value, Name = nameof(AirQuality), Dt = latest.Dt, Units = latest.Units };
    }

    public async Task<SensorLatestDto?> GetAirQualityPercentLatestAsync()
    {
        var latest = await _sensorRepository.GetLatestAsync<AirQualityPercent>();
        if (latest == null) return null;
        return new SensorLatestDto { Value = latest.Value, Name = nameof(AirQualityPercent), Dt = latest.Dt, Units = latest.Units };
    }

    public async Task<SensorLatestDto?> GetMotionLatestAsync()
    {
        var latest = await _sensorRepository.GetLatestAsync<Motion>();
        if (latest == null) return null;
        return new SensorLatestDto { Value = latest.Value, Name = nameof(Motion), Dt = latest.Dt, Units = latest.Units };
    }

    public async Task<SensorLatestDto?> GetWindowLatestAsync()
    {
        var latest = await _sensorRepository.GetLatestAsync<Window>();
        if (latest == null) return null;
        return new SensorLatestDto { Value = latest.Value, Name = nameof(Window), Dt = latest.Dt, Units = latest.Units };
    }

}
