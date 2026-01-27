using Application.Abstractions.Telegram;

namespace Application.Services.Telegram;

public class TelegramPollingHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public TelegramPollingHostedService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scope = _serviceProvider.CreateScope();
        var pollingService = scope.ServiceProvider.GetRequiredService<ITelegramPollingService>();
        var task = pollingService.RunAsync(stoppingToken);
        task.ContinueWith(_ => scope.Dispose(), TaskScheduler.Default);
        return task;
    }
}