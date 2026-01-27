namespace Application.Services.AiFlow;

public sealed class DbSchema
{
    public List<DbTable> Tables { get; set; } = new();
}

public sealed class DbTable
{
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
}
