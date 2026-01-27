using System;

namespace Application.Dto.Ventilation;

public sealed class VentilationStatusDto
{
    public DateTime? Dt { get; set; }
    public double? Co2Ppm { get; set; }
    public bool? WindowOpen { get; set; }
    public string Level { get; set; } = "Ok";
    public string Action { get; set; } = "None";
    public string Message { get; set; } = string.Empty;
}