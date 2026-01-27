using Application.Dto.Telegram;

namespace Application.Abstractions.Telegram;

public interface ITelegramUpdateService
{
    Task HandleMessageAsync(TelegramMessageInDto dto, CancellationToken ct);
}