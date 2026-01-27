using Application.Abstractions.Telegram;
using Application.Dto.Telegram;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Application.Services.Telegram;

public class TelegramPollingService : ITelegramPollingService
{
    private readonly ITelegramBotClient _client;
    private readonly ITelegramUpdateService _updateService;

    public TelegramPollingService(ITelegramBotClient client, ITelegramUpdateService updateService)
    {
        _client = client;
        _updateService = updateService;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _client.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: ct);

        var me = await _client.GetMe(cancellationToken: ct);
        Console.WriteLine($"Telegram bot started: @{me.Username ?? "unknown"}");

        await Task.Delay(Timeout.Infinite, ct);
    }
    
    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Type != UpdateType.Message) return;
        if (update.Message?.Text is null) return;

        var dto = new TelegramMessageInDto
        {
            ChatId = update.Message.Chat.Id,
            Username = update.Message.From?.Username,
            Text = update.Message.Text,
            ReceivedAtUtc = DateTime.UtcNow
        };

        await _updateService.HandleMessageAsync(dto, ct);
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        Console.WriteLine(ex.ToString());
        return Task.CompletedTask;
    }
}