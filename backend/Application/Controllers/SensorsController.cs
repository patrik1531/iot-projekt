using Application.Abstractions;
using Application.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

[ApiController]
[Route("api/sensors")]
public class SensorsController : ControllerBase
{
    private readonly ISensorService _sensorService;

    public SensorsController(ISensorService sensorService)
    {
        _sensorService = sensorService;
    }

    // ===== LATEST =====

    [HttpGet("temperature/latest")]
    public async Task<ActionResult<SensorLatestDto>> GetTemperatureLatest()
    {
        var res = await _sensorService.GetTemperatureLatestAsync();
        return res is null ? NotFound() : Ok(res);
    }

    [HttpGet("humidity/latest")]
    public async Task<ActionResult<SensorLatestDto>> GetHumidityLatest()
    {
        var res = await _sensorService.GetHumidityLatestAsync();
        return res is null ? NotFound() : Ok(res);
    }

    [HttpGet("air-quality/latest")]
    public async Task<ActionResult<SensorLatestDto>> GetAirQualityLatest()
    {
        var res = await _sensorService.GetAirQualityLatestAsync();
        return res is null ? NotFound() : Ok(res);
    }

    [HttpGet("air-quality-percent/latest")]
    public async Task<ActionResult<SensorLatestDto>> GetAirQualityPercentLatest()
    {
        var res = await _sensorService.GetAirQualityPercentLatestAsync();
        return res is null ? NotFound() : Ok(res);
    }

    [HttpGet("motion/latest")]
    public async Task<ActionResult<SensorLatestDto>> GetMotionLatest()
    {
        var res = await _sensorService.GetMotionLatestAsync();
        return res is null ? NotFound() : Ok(res);
    }

    [HttpGet("window/latest")]
    public async Task<ActionResult<SensorLatestDto>> GetWindowLatest()
    {
        var res = await _sensorService.GetWindowLatestAsync();
        return res is null ? NotFound() : Ok(res);
    }

    // ===== TEMPERATURE =====
    [HttpGet("temperature/last-24h")]
    public async Task<ActionResult<SensorStatsResponse>> GetTemperatureLast24Hours()
        => await _sensorService.GetTemperatureLast24HoursAsync();

    [HttpGet("temperature/last-week")]
    public async Task<ActionResult<SensorStatsResponse>> GetTemperatureLastWeek()
        => await _sensorService.GetTemperatureLastWeekAsync();

    [HttpGet("temperature/last-month")]
    public async Task<ActionResult<SensorStatsResponse>> GetTemperatureLastMonth()
        => await _sensorService.GetTemperatureLastMonthAsync();

    [HttpGet("temperature/last-year")]
    public async Task<ActionResult<SensorStatsResponse>> GetTemperatureLastYear()
        => await _sensorService.GetTemperatureLastYearAsync();

    [HttpGet("temperature/range")]
    public async Task<ActionResult<SensorStatsResponse>> GetTemperatureByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => await _sensorService.GetTemperatureByDateRangeAsync(from, to);

    // ===== HUMIDITY =====
    
    [HttpGet("humidity/last-24h")]
    public async Task<ActionResult<SensorStatsResponse>> GetHumidityLast24Hours()
        => await _sensorService.GetHumidityLast24HoursAsync();

    [HttpGet("humidity/last-week")]
    public async Task<ActionResult<SensorStatsResponse>> GetHumidityLastWeek()
        => await _sensorService.GetHumidityLastWeekAsync();

    [HttpGet("humidity/last-month")]
    public async Task<ActionResult<SensorStatsResponse>> GetHumidityLastMonth()
        => await _sensorService.GetHumidityLastMonthAsync();

    [HttpGet("humidity/last-year")]
    public async Task<ActionResult<SensorStatsResponse>> GetHumidityLastYear()
        => await _sensorService.GetHumidityLastYearAsync();

    [HttpGet("humidity/range")]
    public async Task<ActionResult<SensorStatsResponse>> GetHumidityByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => await _sensorService.GetHumidityByDateRangeAsync(from, to);

    // ===== AIR QUALITY =====
    
    [HttpGet("air-quality/last-24h")]
    public async Task<ActionResult<SensorStatsResponse>> GetAirQualityLast24Hours()
        => await _sensorService.GetAirQualityLast24HoursAsync();

    [HttpGet("air-quality/last-week")]
    public async Task<ActionResult<SensorStatsResponse>> GetAirQualityLastWeek()
        => await _sensorService.GetAirQualityLastWeekAsync();

    [HttpGet("air-quality/last-month")]
    public async Task<ActionResult<SensorStatsResponse>> GetAirQualityLastMonth()
        => await _sensorService.GetAirQualityLastMonthAsync();

    [HttpGet("air-quality/last-year")]
    public async Task<ActionResult<SensorStatsResponse>> GetAirQualityLastYear()
        => await _sensorService.GetAirQualityLastYearAsync();

    [HttpGet("air-quality/range")]
    public async Task<ActionResult<SensorStatsResponse>> GetAirQualityByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => await _sensorService.GetAirQualityByDateRangeAsync(from, to);

    // ===== AIR QUALITY PERCENT =====
    
    [HttpGet("air-quality-percent/last-24h")]
    public async Task<ActionResult<SensorStatsResponse>> GetAirQualityPercentLast24Hours()
        => await _sensorService.GetAirQualityPercentLast24HoursAsync();

    [HttpGet("air-quality-percent/last-week")]
    public async Task<ActionResult<SensorStatsResponse>> GetAirQualityPercentLastWeek()
        => await _sensorService.GetAirQualityPercentLastWeekAsync();

    [HttpGet("air-quality-percent/last-month")]
    public async Task<ActionResult<SensorStatsResponse>> GetAirQualityPercentLastMonth()
        => await _sensorService.GetAirQualityPercentLastMonthAsync();

    [HttpGet("air-quality-percent/last-year")]
    public async Task<ActionResult<SensorStatsResponse>> GetAirQualityPercentLastYear()
        => await _sensorService.GetAirQualityPercentLastYearAsync();

    [HttpGet("air-quality-percent/range")]
    public async Task<ActionResult<SensorStatsResponse>> GetAirQualityPercentByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => await _sensorService.GetAirQualityPercentByDateRangeAsync(from, to);

    // ===== MOTION =====
    
    [HttpGet("motion/last-24h")]
    public async Task<ActionResult<SensorStatsResponse>> GetMotionLast24Hours()
        => await _sensorService.GetMotionLast24HoursAsync();

    [HttpGet("motion/last-week")]
    public async Task<ActionResult<SensorStatsResponse>> GetMotionLastWeek()
        => await _sensorService.GetMotionLastWeekAsync();

    [HttpGet("motion/last-month")]
    public async Task<ActionResult<SensorStatsResponse>> GetMotionLastMonth()
        => await _sensorService.GetMotionLastMonthAsync();

    [HttpGet("motion/last-year")]
    public async Task<ActionResult<SensorStatsResponse>> GetMotionLastYear()
        => await _sensorService.GetMotionLastYearAsync();

    [HttpGet("motion/range")]
    public async Task<ActionResult<SensorStatsResponse>> GetMotionByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => await _sensorService.GetMotionByDateRangeAsync(from, to);

    // ===== WINDOW =====
    
    [HttpGet("window/last-24h")]
    public async Task<ActionResult<SensorStatsResponse>> GetWindowLast24Hours()
        => await _sensorService.GetWindowLast24HoursAsync();

    [HttpGet("window/last-week")]
    public async Task<ActionResult<SensorStatsResponse>> GetWindowLastWeek()
        => await _sensorService.GetWindowLastWeekAsync();

    [HttpGet("window/last-month")]
    public async Task<ActionResult<SensorStatsResponse>> GetWindowLastMonth()
        => await _sensorService.GetWindowLastMonthAsync();

    [HttpGet("window/last-year")]
    public async Task<ActionResult<SensorStatsResponse>> GetWindowLastYear()
        => await _sensorService.GetWindowLastYearAsync();

    [HttpGet("window/range")]
    public async Task<ActionResult<SensorStatsResponse>> GetWindowByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => await _sensorService.GetWindowByDateRangeAsync(from, to);
}
