namespace Application.Services.AiFlow;

public interface IAiTool
{
    string Name { get; }
    string Description { get; }

    /// <summary>
    /// argsJson is a raw JSON object as string.
    /// The return value must be a compact JSON string.
    /// </summary>
    Task<string> InvokeAsync(string argsJson, AiToolContext ctx, CancellationToken ct);
}
