namespace Application.Models.Telegram;

public class TelegramIncomingMessage
{
    public long ChatId { get; set; }
    public string? Username { get; set; }
    public string Text { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}