using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Application.Dto;

public class ReportBucket
{
    public string Label { get; set; } = string.Empty;
    public Dictionary<string, double> Values { get; set; } = new();
}

public class ReadingDto
{
    [JsonPropertyName("dt")]
    public string Dt { get; set; } = string.Empty;
    [JsonPropertyName("value")]
    public double Value { get; set; }
}

public class ReportResponse
{
    public List<ReportBucket> Buckets { get; set; } = new();
    public Dictionary<string,double>? Overall { get; set; }

    // optional computed stats requested by user
    public Dictionary<string,double>? Stats { get; set; }

    // top N peak readings
    public List<ReadingDto>? TopReadings { get; set; }
}
