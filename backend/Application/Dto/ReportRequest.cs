using System;

namespace Application.Dto;

public class ReportRequest
{
    public string Metric { get; set; } = string.Empty;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Preset { get; set; }
    public int? IntervalMinutes { get; set; }
    public string[]? Aggregations { get; set; }
    public bool Ai { get; set; } = false;
    public string Format { get; set; } = "both";

    // If true, request is to list sensors in DB (no metric time-series)
    public bool SensorsOnly { get; set; } = false;

    // Optional explicit sensor ids/names to filter
    public string[]? SensorIds { get; set; }

    // Additional stats user may request (e.g. avg,min,max,median,p90,count)
    public string[]? Stats { get; set; }

    // Top N peaks to include
    public int? TopN { get; set; }
}
