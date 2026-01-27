using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Application.Data;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Application.Services.AiFlow;

public static class SchemaBuilder
{
    /// <summary>
    /// Builds a lightweight schema from EF Core model metadata.
    /// Works offline without querying information_schema.
    /// </summary>
    public static DbSchema FromEfModel(ApplicationDbContext db)
    {
        var schema = new DbSchema();

        // Prefer EF model, but fall back to DbSet properties if needed.
        var model = db.Model;
        foreach (var entityType in model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName() ?? entityType.ClrType.Name;
            var cols = entityType.GetProperties()
                .Select(p => p.GetColumnName(StoreObjectIdentifier.Table(tableName, entityType.GetSchema() ?? "public")) ?? p.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            schema.Tables.Add(new DbTable
            {
                Name = tableName,
                Columns = cols
            });
        }

        schema.Tables = schema.Tables
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return schema;
    }
}
