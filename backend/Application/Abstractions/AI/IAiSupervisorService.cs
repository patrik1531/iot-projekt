namespace Application.Abstractions.AI;

public interface IAiSupervisorService
{
    Task<string> ReplyAsync(long chatId, string userText, CancellationToken ct = default);
}
