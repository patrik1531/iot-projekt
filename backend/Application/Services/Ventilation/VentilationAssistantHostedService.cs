using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Models.Ventilation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Services.Ventilation;

public sealed class VentilationAssistantHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly VentilationAssistantSettings _settings;
    private readonly ILogger<VentilationAssistantHostedService> _logger;

    public VentilationAssistantHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<VentilationAssistantSettings> settings,
        ILogger<VentilationAssistantHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("VentilationAssistant is disabled (VentilationAssistant:Enabled=false).");
            return;
        }

        var period = TimeSpan.FromSeconds(Math.Max(5, _settings.CheckPeriodSeconds));
        _logger.LogInformation("VentilationAssistant started. period={Period}s open={Open} close={Close} urgent={Urgent} debounce={Debounce}s",
            period.TotalSeconds, _settings.OpenThresholdPpm, _settings.CloseThresholdPpm, _settings.UrgentThresholdPpm, _settings.DebounceSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var assistant = scope.ServiceProvider.GetRequiredService<VentilationAssistant>();
                await assistant.EvaluateAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // ignore
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VentilationAssistant tick failed.");
            }

            try
            {
                await Task.Delay(period, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
