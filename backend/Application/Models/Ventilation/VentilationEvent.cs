using System;

namespace Application.Models.Ventilation;

public sealed class VentilationEvent
{
    public long Id { get; set; }

    /// <summary>UTC timestamp when the event was generated.</summary>
    public DateTime Dt { get; set; } = DateTime.UtcNow;

    /// <summary>Optional: device identifier (if you have multiple devices). Can be null for single-device setup.</summary>
    public string? DeviceId { get; set; }

    /// <summary>CO2 concentration in ppm (best-effort, comes from AirQuality.Value).</summary>
    public double? Co2Ppm { get; set; }

    /// <summary>Last known window state. Null if unknown.</summary>
    public bool? WindowOpen { get; set; }

    public VentilationLevel Level { get; set; } = VentilationLevel.Ok;
    
    public string? Type { get; set; }
    public VentilationAction Action { get; set; } = VentilationAction.None;

    /// <summary>Human readable message (Slovak).</summary>
    public string Message { get; set; } = string.Empty;
    
    public string? Title { get; set; } = string.Empty;
}
