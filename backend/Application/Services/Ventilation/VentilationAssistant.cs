// csharp
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Application.Data;
using Application.Models.Sensors;
using Application.Models.Ventilation;
using Application.Abstractions.Telegram;
using Application.Dto.Telegram;
using Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services.Ventilation;

/// <summary>
/// Evaluates latest CO2 + Window readings and generates ventilation events.
/// This is "AI-free" deterministic logic (threshold + hysteresis + debounce) to create real added value.
/// </summary>
public sealed class VentilationAssistant
{
    private readonly ApplicationDbContext _db;
    private readonly SensorEventService _sse;
    private readonly ITelegramBotRepository _telegram;
    private readonly VentilationAssistantSettings _opt;
    private readonly ILogger<VentilationAssistant> _log;

    // per-chat/device runtime state for hysteresis/debounce
    private readonly ConcurrentDictionary<string, RuntimeState> _stateByKey = new(StringComparer.OrdinalIgnoreCase);

    public VentilationAssistant(
        ApplicationDbContext db,
        SensorEventService sse,
        ITelegramBotRepository telegram,
        IOptions<VentilationAssistantSettings> opt,
        ILogger<VentilationAssistant> log)
    {
        _db = db;
        _sse = sse;
        _telegram = telegram;
        _opt = opt.Value;
        _log = log;
    }

    public async Task EvaluateAsync(CancellationToken ct)
    {
        if (!_opt.Enabled)
            return;

        // NOTE: current DB model has no DeviceId. We evaluate "global" latest readings.
        const string key = "default";

        var latestCo2 = await _db.AirQuality
            .OrderByDescending(x => x.Dt)
            .Select(x => new { x.Dt, x.Value, x.Units })
            .FirstOrDefaultAsync(ct);

        var latestWindow = await _db.Window
            .OrderByDescending(x => x.Dt)
            .Select(x => new { x.Dt, x.Value, x.Units })
            .FirstOrDefaultAsync(ct);

        if (latestCo2 == null)
        {
            _log.LogDebug("[Ventilation] No CO2 (AirQuality) rows.");
            return;
        }

        var co2Ppm = latestCo2.Value;
        double windowVal = 0;

        if (latestWindow != null)
        {
            var s = latestWindow.Value?.Trim();

            if (!string.IsNullOrWhiteSpace(s))
            {
                if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out windowVal))
                    double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out windowVal);
            }
        }

        var windowOpen = latestWindow != null && windowVal > 0.5;

        var desired = ComputeDesiredLevel(co2Ppm, _stateByKey.GetOrAdd(key, _ => new RuntimeState()).CurrentLevel);
        var action = ComputeAction(desired, windowOpen);

        await MaybeEmitAsync(key, co2Ppm, windowOpen, desired, action, ct);
    }

    private VentilationLevel ComputeDesiredLevel(double co2Ppm, VentilationLevel current)
    {
        if (co2Ppm >= _opt.UrgentThresholdPpm)
            return VentilationLevel.Urgent;
        if (current is VentilationLevel.Ventilate or VentilationLevel.Urgent)
        {
            if (co2Ppm <= _opt.CloseThresholdPpm)
            {
                return co2Ppm < _opt.OkThresholdPpm ? VentilationLevel.Ok : VentilationLevel.Degraded;
            }

            return VentilationLevel.Ventilate;
        }

        if (co2Ppm >= _opt.OpenThresholdPpm)
            return VentilationLevel.Ventilate;

        if (co2Ppm < _opt.OkThresholdPpm)
            return VentilationLevel.Ok;

        return VentilationLevel.Degraded;
    }

    private static VentilationAction ComputeAction(VentilationLevel level, bool windowOpen)
    {
        return level switch
        {
            VentilationLevel.Ventilate or VentilationLevel.Urgent
                => windowOpen ? VentilationAction.None : VentilationAction.SuggestOpenWindow,
            VentilationLevel.Ok
                => windowOpen ? VentilationAction.SuggestCloseWindow : VentilationAction.None,
            _ => VentilationAction.None
        };
    }

    public async Task MaybeEmitAsync(string key, double co2Ppm, bool windowOpen, VentilationLevel desired, VentilationAction action, CancellationToken ct)
    {
        try
        {
            var ev = new VentilationEvent
            {
                Dt = DateTime.UtcNow,
                Level = desired,
                Action = action,
                WindowOpen = windowOpen,
                Co2Ppm = co2Ppm,
                DeviceId = key
            };

            var last = await _db.VentilationEvent
                .OrderByDescending(x => x.Dt)
                .Select(x => new { x.Level, x.Action, x.WindowOpen, x.DeviceId })
                .FirstOrDefaultAsync(ct);

            _log.LogDebug("[Ventilation] Last DB event: {@Last}", last);
            _log.LogDebug("[Ventilation] New event candidate: {@Ev}", new { ev.Level, ev.Action, ev.WindowOpen, ev.DeviceId, ev.Co2Ppm });

            bool sameAsLast =
                last != null &&
                last.Level == ev.Level &&
                last.Action == ev.Action &&
                last.WindowOpen == ev.WindowOpen &&
                string.Equals(last.DeviceId, ev.DeviceId, StringComparison.OrdinalIgnoreCase);

            var forceInsert = false;

            if (!forceInsert && sameAsLast)
            {
                _log.LogDebug("[Ventilation] Skipping DB insert (same as last event).");
                return;
            }

            _db.VentilationEvent.Add(ev);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("[Ventilation] VentilationEvent saved (Dt={Dt}, Level={Level}, Action={Action})", ev.Dt, ev.Level, ev.Action);

            var dto = new VentilationEvent
            {
                Dt = ev.Dt,
                Level = ev.Level,
                Type = Transform(ev.Level),
                Action = ev.Action,
                Co2Ppm = ev.Co2Ppm,
                WindowOpen = ev.WindowOpen,
                DeviceId = ev.DeviceId,
                Message = BuildMessage(ev.Co2Ppm, ev.Level, ev.Action, ev.WindowOpen),
                Title = Title(Transform(ev.Level))
            };
            if(dto.Action == VentilationAction.SuggestOpenWindow || dto.Action == VentilationAction.SuggestCloseWindow)
                await _sse.SendEventAsync("event", dto);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[Ventilation] Failed to save VentilationEvent: {Message}", ex.Message);
        }
    }

    private string Title(string level)
    {
        if (level == "info")
        {
            return "Všetko je v poriadku!";
        }

        if (level == "warning")
        {
            return "Pozor!";
        }

        if (level == "error")
        {
            return "Niečo je zle!";
        }

        return "";
    }
    
    private string Transform(VentilationLevel level)
    {
        if (level == VentilationLevel.Ok)
        {
            return "info";
        }
        if (level == VentilationLevel.Degraded)
        {
            return "warning";
        }

        if (level == VentilationLevel.Urgent)
        {
            return "warning";
        }

        if (level == VentilationLevel.Ventilate)
        {
            return "error";
        }

        return "";
    }

    private static string BuildMessage(double? co2, VentilationLevel level, VentilationAction action, bool? windowOpen)
    {
        var co2Text = co2.HasValue ? Math.Round(co2.Value).ToString(CultureInfo.InvariantCulture) : "n/a";

        var baseText = level switch
        {
            VentilationLevel.Ok => $"CO₂ {co2Text} ppm: OK",
            VentilationLevel.Degraded => $"CO₂ {co2Text} ppm: zhoršené",
            VentilationLevel.Ventilate => $"CO₂ {co2Text} ppm: vetrať",
            VentilationLevel.Urgent => $"CO₂ {co2Text} ppm: URGENTNE vetrať",
            _ => $"CO₂ {co2Text} ppm"
        };

        if (action == VentilationAction.SuggestOpenWindow)
            return baseText + " (odporúčanie: otvor okno)";
        if (action == VentilationAction.SuggestCloseWindow)
            return baseText + " (odporúčanie: zavri okno)";

        // use explicit comparison to handle nullable bool
        return windowOpen == true ? baseText + " (okno: otvorené)" : baseText + " (okno: zatvorené)";
    }

    private sealed class RuntimeState
    {
        public VentilationLevel CurrentLevel { get; set; } = VentilationLevel.Ok;
        public VentilationAction CurrentAction { get; set; } = VentilationAction.None;

        public DateTime? PendingSinceUtc { get; set; }
        public VentilationLevel PendingLevel { get; set; } = VentilationLevel.Ok;
        public VentilationAction PendingAction { get; set; } = VentilationAction.None;

        public DateTime? LastEmittedUtc { get; set; }
    }
}
