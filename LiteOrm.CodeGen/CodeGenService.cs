using System;
using System.Collections.Generic;

namespace LiteOrm.CodeGen;

public sealed class CodeGenService
{
    private readonly DatabaseSchemaReader _schemaReader;
    private readonly EntityCodeGenerator _entityGenerator;
    private readonly SelectArtifactsGenerator _selectGenerator;

    public CodeGenService(DatabaseSchemaReader schemaReader, EntityCodeGenerator entityGenerator, SelectArtifactsGenerator selectGenerator)
    {
        _schemaReader = schemaReader ?? throw new ArgumentNullException(nameof(schemaReader));
        _entityGenerator = entityGenerator ?? throw new ArgumentNullException(nameof(entityGenerator));
        _selectGenerator = selectGenerator ?? throw new ArgumentNullException(nameof(selectGenerator));
    }

    public EntityGenerationResult GenerateEntities(string? dataSource, IEnumerable<string>? tables, EntityGenerationOptions options)
    {
        var schema = _schemaReader.ReadSchema(dataSource, tables);
        return _entityGenerator.Generate(schema, options);
    }

    public SelectGenerationResult GenerateSelectArtifacts(string? dataSource, string sql, SelectGenerationOptions options)
    {
        var schema = _schemaReader.ReadSchema(dataSource, null);
        return _selectGenerator.Generate(schema, sql, options);
    }
}
