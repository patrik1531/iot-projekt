namespace Application.Models;

public class Notification
{
    public List<string> Urls { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
    public List<string>? Attachments { get; set; }
}