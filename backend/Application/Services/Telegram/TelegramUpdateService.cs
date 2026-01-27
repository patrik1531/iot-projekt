using Application.Abstractions.AI;
using Application.Abstractions.Telegram;
using Application.Dto.Telegram;

namespace Application.Services.Telegram;

public class TelegramUpdateService : ITelegramUpdateService
{
    private readonly ITelegramBotRepository _repository;
    private readonly IAiSupervisorService _ai;

    public TelegramUpdateService(ITelegramBotRepository repository, IAiSupervisorService ai)
    {
        _repository = repository;
        _ai = ai;
    }

    public async Task HandleMessageAsync(TelegramMessageInDto dto, CancellationToken ct)
    {
        var text = dto.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return;

        var reply = await _ai.ReplyAsync(dto.ChatId, text, ct);

        await _repository.SendMessageAsync(new TelegramSendMessageDto
        {
            ChatId = dto.ChatId,
            Text = reply,
            DisableNotification = false
        }, ct);
    }
}
