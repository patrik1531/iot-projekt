using Application.Dto.Telegram;

namespace Application.Abstractions.Telegram;

public interface ITelegramBotRepository
{
    Task SendMessageAsync(TelegramSendMessageDto dto, CancellationToken ct);
}