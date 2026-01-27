namespace Application.Dto.Telegram;

public class TelegramSendMessageDto
{
    public long ChatId { get; set; }
    public string Text { get; set; }
    public bool DisableNotification { get; set; }
}