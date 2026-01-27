namespace Application.Dto.Telegram;

public class TelegramMessageInDto
{
    public long ChatId { get; set; }
    public string? Username { get; set; }
    public string Text { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}