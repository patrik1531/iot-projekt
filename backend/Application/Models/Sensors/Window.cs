using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.Models.Sensors;

public class Window
{
    [Key]
    [JsonPropertyName("dt")]
    public DateTime Dt { get; set; }
    
    [JsonPropertyName("value")]
    public string Value { get; set; }

    [JsonPropertyName("units")]
    public string Units { get; set; }
}