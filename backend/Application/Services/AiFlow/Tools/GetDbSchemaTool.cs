using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Application.Data;

namespace Application.Services.AiFlow.Tools;

public sealed class GetDbSchemaTool : IAiTool
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GetDbSchemaTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public string Name => "GetDbSchema";

    public string Description => "Returns allowed DB schema (tables+columns) for building safe SELECT SQL. Args: {}";

    public async Task<string> InvokeAsync(string argsJson, AiToolContext ctx, CancellationToken ct)
    {
        await Task.Yield();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var schema = SchemaBuilder.FromEfModel(db);

        var handle = ctx.Memory.Put(ctx.ChatId, schema);

        var outObj = new
        {
            handle = handle.ToString(),
            tables = schema.Tables.Select(t => new { name = t.Name, columns = t.Columns })
        };

        return JsonSerializer.Serialize(outObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}
