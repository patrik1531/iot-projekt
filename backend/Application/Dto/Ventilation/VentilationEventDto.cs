using System;

namespace Application.Dto.Ventilation;

public sealed class VentilationEventDto
{
    public long Id { get; set; }
    public DateTime Dt { get; set; }
    public string? DeviceId { get; set; }
    public double? Co2Ppm { get; set; }
    public bool? WindowOpen { get; set; }
    public string Level { get; set; } = "Ok";
    public string Action { get; set; } = "None";
    public string? Message { get; set; }
}
