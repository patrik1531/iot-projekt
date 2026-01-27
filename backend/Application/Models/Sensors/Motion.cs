using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Models.Sensors;

public class Motion
{
    [Key]
    [JsonPropertyName("dt")]
    public DateTime Dt { get; set; }

    [JsonPropertyName("value")] 
    public bool Value { get; set; }

    [JsonPropertyName("units")]
    public string Units { get; set; }
}