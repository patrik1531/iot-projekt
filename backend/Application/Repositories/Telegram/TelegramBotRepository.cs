using Application.Abstractions.Telegram;
using Application.Dto.Telegram;
using Telegram.Bot;

namespace Application.Repositories.Telegram;

public class TelegramBotRepository : ITelegramBotRepository
{
    private readonly ITelegramBotClient _client;
    
    public TelegramBotRepository(ITelegramBotClient client)
    {
        _client = client;
    }
    
    public async Task SendMessageAsync(TelegramSendMessageDto dto, CancellationToken ct)
    {
        await _client.SendMessage(
            chatId: dto.ChatId,
            text: dto.Text,
            disableNotification: dto.DisableNotification,
            cancellationToken: ct);
    }
}