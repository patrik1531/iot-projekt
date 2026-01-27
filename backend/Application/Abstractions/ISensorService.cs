using Application.Dto;

namespace Application.Abstractions;

public interface ISensorService
{
    // Latest
    Task<SensorLatestDto?> GetTemperatureLatestAsync();
    Task<SensorLatestDto?> GetHumidityLatestAsync();
    Task<SensorLatestDto?> GetAirQualityLatestAsync();
    Task<SensorLatestDto?> GetAirQualityPercentLatestAsync();
    Task<SensorLatestDto?> GetMotionLatestAsync();
    Task<SensorLatestDto?> GetWindowLatestAsync();

    // Temperature
    Task<SensorStatsResponse> GetTemperatureLast24HoursAsync();
    Task<SensorStatsResponse> GetTemperatureLastWeekAsync();
    Task<SensorStatsResponse> GetTemperatureLastMonthAsync();
    Task<SensorStatsResponse> GetTemperatureLastYearAsync();
    Task<SensorStatsResponse> GetTemperatureByDateRangeAsync(DateTime from, DateTime to);

    // Humidity
    Task<SensorStatsResponse> GetHumidityLast24HoursAsync();
    Task<SensorStatsResponse> GetHumidityLastWeekAsync();
    Task<SensorStatsResponse> GetHumidityLastMonthAsync();
    Task<SensorStatsResponse> GetHumidityLastYearAsync();
    Task<SensorStatsResponse> GetHumidityByDateRangeAsync(DateTime from, DateTime to);

    // AirQuality
    Task<SensorStatsResponse> GetAirQualityLast24HoursAsync();
    Task<SensorStatsResponse> GetAirQualityLastWeekAsync();
    Task<SensorStatsResponse> GetAirQualityLastMonthAsync();
    Task<SensorStatsResponse> GetAirQualityLastYearAsync();
    Task<SensorStatsResponse> GetAirQualityByDateRangeAsync(DateTime from, DateTime to);

    // AirQualityPercent
    Task<SensorStatsResponse> GetAirQualityPercentLast24HoursAsync();
    Task<SensorStatsResponse> GetAirQualityPercentLastWeekAsync();
    Task<SensorStatsResponse> GetAirQualityPercentLastMonthAsync();
    Task<SensorStatsResponse> GetAirQualityPercentLastYearAsync();
    Task<SensorStatsResponse> GetAirQualityPercentByDateRangeAsync(DateTime from, DateTime to);

    // Motion
    Task<SensorStatsResponse> GetMotionLast24HoursAsync();
    Task<SensorStatsResponse> GetMotionLastWeekAsync();
    Task<SensorStatsResponse> GetMotionLastMonthAsync();
    Task<SensorStatsResponse> GetMotionLastYearAsync();
    Task<SensorStatsResponse> GetMotionByDateRangeAsync(DateTime from, DateTime to);

    // Window
    Task<SensorStatsResponse> GetWindowLast24HoursAsync();
    Task<SensorStatsResponse> GetWindowLastWeekAsync();
    Task<SensorStatsResponse> GetWindowLastMonthAsync();
    Task<SensorStatsResponse> GetWindowLastYearAsync();
    Task<SensorStatsResponse> GetWindowByDateRangeAsync(DateTime from, DateTime to);
}