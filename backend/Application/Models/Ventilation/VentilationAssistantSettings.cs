namespace Application.Models.Ventilation;

public sealed class VentilationAssistantSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>How often to evaluate (seconds).</summary>
    public int CheckPeriodSeconds { get; set; } = 30;

    /// <summary>Debounce time (seconds) - condition must persist this long before emitting a new event.</summary>
    public int DebounceSeconds { get; set; } = 120;
    
    public double OkThresholdPpm { get; set; } = 800;

    /// <summary>Open window suggestion threshold (ppm).</summary>
    public double OpenThresholdPpm { get; set; } = 1200;

    /// <summary>Close window suggestion threshold (ppm). Used as hysteresis (ppm).</summary>
    public double CloseThresholdPpm { get; set; } = 900;

    /// <summary>Urgent ventilation threshold (ppm).</summary>
    public double UrgentThresholdPpm { get; set; } = 1500;

    public long? TelegramChatId { get; set; }
    public bool DisableNotification { get; set; } = true;
}
