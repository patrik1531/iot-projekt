using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Models.Sensors;

public class AirQualityPercent
{
    [Key]
    [JsonPropertyName("dt")]
    public DateTime Dt { get; set; }
    
    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("units")]
    public string Units { get; set; }
}