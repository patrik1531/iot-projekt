namespace Application.Abstractions.Telegram;

public interface ITelegramPollingService
{
    Task RunAsync(CancellationToken ct);
}