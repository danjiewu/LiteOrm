using LiteOrm.CodeGen;
using LiteOrm.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace LiteOrm.SqlToExpr;

public sealed class SqlConversionService
{
    private readonly SelectSqlParser _parser = new();
    private readonly HelperModelCodeGenerator _helperModelCodeGenerator = new();
    public SqlConversionResult Convert(string sql, SqlConversionOptions? options = null)
    {
        options ??= new SqlConversionOptions();
        var result = new SqlConversionResult();

        ParsedSelectQuery query;
        try
        {
            query = _parser.Parse(sql);
        }
        catch (Exception ex)
        {
            result.Diagnostics.Add(new CodeGenDiagnostic(CodeGenDiagnosticSeverity.Error, ex.Message, "请改写为单条 SELECT，并限制在基础 JOIN/WHERE/GROUP BY/ORDER BY 范围内。"));
            return result;
        }

        try
        {
            var schema = InferSchema(query);
            var bound = Bind(schema, query, result.Diagnostics);
            if (!result.Succeeded)
                return result;

            var viewName = ResolveViewName(options, bound);
            var viewProperties = BuildViewProperties(bound);
            result.HelperCode = _helperModelCodeGenerator.GenerateEntityCode(schema, options.Namespace, bound.MainTable.Table.Name);
            result.ViewCode = GenerateViewCode(bound, options, viewName, viewProperties);
            result.ExprCode = GenerateQueryCode(bound, options, viewName, viewProperties);

            if (bound.Projections.Any(p => p.Kind == ProjectionKind.Wildcard && !string.IsNullOrWhiteSpace(p.WildcardAlias)))
            {
                result.Diagnostics.Add(new CodeGenDiagnostic(CodeGenDiagnosticSeverity.Warning, "alias.* 暂不支持回生为 SelectExpr。", "请显式列出字段，或只使用 *。"));
                return result;
            }

            using var providerScope = new RuntimeTableInfoProviderScope(options.Dialect.GetSqlBuilder());
            var compiled = CompileAndBuildExpr(result.HelperCode, result.ViewCode, result.ExprCode, options.Namespace);
            var expr = compiled.Expr;
            var viewType = compiled.Assembly.GetType($"{options.Namespace}.{viewName}", throwOnError: true)!;
            var sqlGen = new SqlGen(viewType);
            if (expr is not SelectExpr) expr = new SelectExpr()
            {
                Source = expr.ToSource(viewType),
                Selects = TableInfoProvider.Default.GetTableView(viewType).SelectColumns.Select((col, i) => new SelectItemExpr(Expr.Prop(col.PropertyName), col.PropertyName)).ToList()
            };
            result.RegeneratedSql = sqlGen.ToSql(expr).Sql;
        }
        catch (Exception ex)
        {
            result.Diagnostics.Add(new CodeGenDiagnostic(CodeGenDiagnosticSeverity.Error, ex.Message, "请检查 SQL 结构是否在当前支持范围内。"));
        }

        return result;
    }

    private static (Expr Expr, Assembly Assembly) CompileAndBuildExpr(string? helperCode, string? viewCode, string? exprCode, string targetNamespace)
    {
        var sourceTexts = new[] { helperCode, viewCode, exprCode }
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
        if (sourceTexts.Length == 0)
            throw new InvalidOperationException("缺少可编译的生成代码。");

        var syntaxTrees = sourceTexts
            .Select(text => CSharpSyntaxTree.ParseText(text!))
            .ToArray();

        var referencePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trustedPlatformAssemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            ?? [];
        foreach (var path in trustedPlatformAssemblies)
            referencePaths.Add(path);

        foreach (var referencedAssembly in new[]
                 {
                     typeof(object).Assembly,
                     typeof(Enumerable).Assembly,
                     typeof(Expr).Assembly,
                     typeof(ObjectBase).Assembly,
                     typeof(LiteOrm.CodeGen.SqlGen).Assembly
                 })
        {
            if (!referencedAssembly.IsDynamic && !string.IsNullOrWhiteSpace(referencedAssembly.Location))
                referencePaths.Add(referencedAssembly.Location);
        }

        var references = System.Linq.Enumerable.Select(referencePaths, path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToList();

        var compilation = CSharpCompilation.Create(
            "LiteOrm.SqlToExpr.Generated." + Guid.NewGuid().ToString("N"),
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        if (!emitResult.Success)
        {
            var diagnostics = string.Join(Environment.NewLine, emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));
            throw new InvalidOperationException("动态编译生成代码失败：" + Environment.NewLine + diagnostics);
        }

        stream.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
        var generatedType = assembly.GetType($"{targetNamespace}.GeneratedExprQuery", throwOnError: true)!;
        var buildMethod = generatedType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("未找到 GeneratedExprQuery.Build()。");
        var expr = buildMethod.Invoke(null, null) as Expr;
        return (expr ?? throw new InvalidOperationException("GeneratedExprQuery.Build() 未返回 Expr。"), assembly);
    }

    private static string ResolveViewName(SqlConversionOptions options, BoundSelectModel model)
    {
        if (!string.IsNullOrWhiteSpace(options.ViewName))
            return options.ViewName;

        return model.MainTable.Table.ClassName + "View";
    }

    private static DatabaseSchema InferSchema(ParsedSelectQuery query)
    {
        if (query.MainSource == null)
            throw new InvalidOperationException("缺少 FROM 主表。");

        var schema = new DatabaseSchema();
        var aliasMap = new Dictionary<string, TableSchema>(StringComparer.OrdinalIgnoreCase);

        var mainTable = EnsureTable(schema, query.MainSource.TableName);
        aliasMap[query.MainSource.Alias] = mainTable;
        MarkConventionPrimaryKey(mainTable);

        foreach (var join in query.Joins)
        {
            var table = EnsureTable(schema, join.Table.TableName);
            aliasMap[join.Table.Alias] = table;
            MarkConventionPrimaryKey(table);

            foreach (var condition in join.Conditions)
            {
                AddColumn(aliasMap, mainTable, condition.Left, typeof(object));
                AddColumn(aliasMap, mainTable, condition.Right, typeof(object));

                if (!string.IsNullOrWhiteSpace(condition.Left.Alias) &&
                    !string.IsNullOrWhiteSpace(condition.Right.Alias) &&
                    aliasMap.TryGetValue(condition.Left.Alias!, out var leftTable) &&
                    aliasMap.TryGetValue(condition.Right.Alias!, out var rightTable))
                {
                    leftTable.ForeignKeys.Add(new ForeignKeySchema
                    {
                        SourceColumn = condition.Left.ColumnName,
                        TargetTable = rightTable.Name,
                        TargetColumn = condition.Right.ColumnName
                    });
                    MarkPrimaryKey(rightTable, condition.Right.ColumnName);
                }
            }
        }

        foreach (var projection in query.Projections)
        {
            switch (projection.Kind)
            {
                case ProjectionKind.Column when projection.Column != null:
                    AddColumn(aliasMap, mainTable, projection.Column, typeof(object));
                    break;
                case ProjectionKind.Function when projection.Function != null:
                    AddFunctionArgumentColumns(aliasMap, mainTable, projection.Function);
                    break;
            }
        }

        if (query.Where != null)
            InferFilterColumns(aliasMap, mainTable, query.Where);

        foreach (var group in query.GroupBy)
            AddColumn(aliasMap, mainTable, group, typeof(object));

        foreach (var orderBy in query.OrderBy)
            AddColumn(aliasMap, mainTable, orderBy.Column, typeof(object));

        MarkSelectColumns(aliasMap, mainTable, query);

        return schema;
    }

    private static void MarkSelectColumns(Dictionary<string, TableSchema> aliasMap, TableSchema mainTable, ParsedSelectQuery query)
    {
        var selectCount = 0;
        foreach (var projection in query.Projections)
        {
            switch (projection.Kind)
            {
                case ProjectionKind.Column when projection.Column != null:
                {
                    var table = ResolveReferenceTable(aliasMap, mainTable, projection.Column);
                    var column = table.GetColumn(projection.Column.ColumnName);
                    if (column != null)
                    {
                        column.IsInSelect = true;
                        column.SelectOrdinal = selectCount++;
                    }
                    break;
                }
                case ProjectionKind.Wildcard:
                {
                    string? wildcardAlias = projection.WildcardAlias;
                    if (!string.IsNullOrWhiteSpace(wildcardAlias))
                    {
                        if (aliasMap.TryGetValue(wildcardAlias, out var wildcardTable))
                        {
                            foreach (var col in wildcardTable.Columns)
                            {
                                col.IsInSelect = true;
                                col.SelectOrdinal = selectCount++;
                            }
                        }
                    }
                    else
                    {
                        foreach (var table in aliasMap.Values.DistinctBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            foreach (var col in table.Columns)
                            {
                                col.IsInSelect = true;
                                col.SelectOrdinal = selectCount++;
                            }
                        }
                    }
                    break;
                }
                case ProjectionKind.Function when projection.Function != null:
                {
                    selectCount = MarkFunctionSelectColumns(aliasMap, mainTable, projection.Function, selectCount);
                    break;
                }
            }
        }
    }

    private static void MarkConventionPrimaryKey(TableSchema table)
    {
        var idColumn = table.GetColumn("Id");
        if (idColumn != null)
            idColumn.IsPrimaryKey = true;
    }

    private static void MarkPrimaryKey(TableSchema table, string columnName)
    {
        var column = table.GetColumn(columnName);
        if (column != null)
            column.IsPrimaryKey = true;
    }

    private static void InferFilterColumns(Dictionary<string, TableSchema> aliasMap, TableSchema mainTable, FilterNode node)
    {
        if (node is FilterLogicalNode logical)
        {
            InferFilterColumns(aliasMap, mainTable, logical.Left);
            InferFilterColumns(aliasMap, mainTable, logical.Right);
            return;
        }

        var comparison = (FilterComparisonNode)node;
        AddColumn(aliasMap, mainTable, comparison.Left, InferLiteralType(comparison.RightLiteral));

        if (comparison.RightColumn != null)
            AddColumn(aliasMap, mainTable, comparison.RightColumn, typeof(object));
    }

    private static TableSchema EnsureTable(DatabaseSchema schema, string tableName)
    {
        var existing = schema.GetTable(tableName);
        if (existing != null)
            return existing;

        var created = new TableSchema
        {
            Name = tableName,
            ClassName = CodeGenNaming.ToClassName(tableName)
        };
        schema.Tables.Add(created);
        return created;
    }

    private static void AddColumn(Dictionary<string, TableSchema> aliasMap, TableSchema mainTable, ParsedColumnReference reference, Type suggestedType)
    {
        var table = ResolveReferenceTable(aliasMap, mainTable, reference);
        var column = table.GetColumn(reference.ColumnName);
        if (column != null)
            return;

        table.Columns.Add(new ColumnSchema
        {
            Name = reference.ColumnName,
            PropertyName = CodeGenNaming.ToPropertyName(reference.ColumnName),
            ClrType = suggestedType == typeof(DBNull) ? typeof(object) : suggestedType,
            IsNullable = true,
            Ordinal = table.Columns.Count
        });
    }

    private static TableSchema ResolveReferenceTable(Dictionary<string, TableSchema> aliasMap, TableSchema mainTable, ParsedColumnReference reference)
    {
        if (!string.IsNullOrWhiteSpace(reference.Alias))
            return aliasMap[reference.Alias!];

        return mainTable;
    }

    private static void AddFunctionArgumentColumns(Dictionary<string, TableSchema> aliasMap, TableSchema mainTable, ParsedFunctionCall function)
    {
        for (int i = 0; i < function.Arguments.Count; i++)
        {
            switch (function.Arguments[i])
            {
                case ParsedColumnExpression column:
                    AddColumn(aliasMap, mainTable, column.Column, InferFunctionArgumentType(function.Name, i));
                    break;
                case ParsedFunctionExpression nested:
                    AddFunctionArgumentColumns(aliasMap, mainTable, nested.Function);
                    break;
            }
        }

        foreach (var whenClause in function.WhenClauses)
        {
            AddFilterColumns(aliasMap, mainTable, whenClause.Condition);
            AddColumns(aliasMap, mainTable, whenClause.Result);
        }

        if (function.ElseArgument != null)
            AddColumns(aliasMap, mainTable, function.ElseArgument);
    }

    private static int MarkFunctionSelectColumns(Dictionary<string, TableSchema> aliasMap, TableSchema mainTable, ParsedFunctionCall function, int selectCount)
    {
        foreach (var columnReference in EnumerateFunctionColumns(function))
        {
            var table = ResolveReferenceTable(aliasMap, mainTable, columnReference);
            var column = table.GetColumn(columnReference.ColumnName);
            if (column == null)
                continue;

            column.IsInSelect = true;
            column.SelectOrdinal = selectCount++;
        }

        return selectCount;
    }

    private static IEnumerable<ParsedColumnReference> EnumerateFunctionColumns(ParsedFunctionCall function)
    {
        foreach (var argument in function.Arguments)
        {
            foreach (var column in EnumerateColumns(argument))
                yield return column;
        }

        foreach (var whenClause in function.WhenClauses)
        {
            foreach (var column in EnumerateFilterColumns(whenClause.Condition))
                yield return column;
            foreach (var column in EnumerateColumns(whenClause.Result))
                yield return column;
        }

        if (function.ElseArgument != null)
        {
            foreach (var column in EnumerateColumns(function.ElseArgument))
                yield return column;
        }
    }

    private static IEnumerable<ParsedColumnReference> EnumerateColumns(ParsedScalarExpression expression)
    {
        switch (expression)
        {
            case ParsedColumnExpression column:
                yield return column.Column;
                break;
            case ParsedFunctionExpression function:
                foreach (var columnReference in EnumerateFunctionColumns(function.Function))
                    yield return columnReference;
                break;
        }
    }

    private static IEnumerable<ParsedColumnReference> EnumerateFilterColumns(FilterNode node)
    {
        if (node is FilterLogicalNode logical)
        {
            foreach (var column in EnumerateFilterColumns(logical.Left))
                yield return column;
            foreach (var column in EnumerateFilterColumns(logical.Right))
                yield return column;
            yield break;
        }

        var comparison = (FilterComparisonNode)node;
        yield return comparison.Left;
        if (comparison.RightColumn != null)
            yield return comparison.RightColumn;
    }

    private static void AddColumns(Dictionary<string, TableSchema> aliasMap, TableSchema mainTable, ParsedScalarExpression expression)
    {
        switch (expression)
        {
            case ParsedColumnExpression column:
                AddColumn(aliasMap, mainTable, column.Column, typeof(object));
                break;
            case ParsedFunctionExpression function:
                AddFunctionArgumentColumns(aliasMap, mainTable, function.Function);
                break;
        }
    }

    private static void AddFilterColumns(Dictionary<string, TableSchema> aliasMap, TableSchema mainTable, FilterNode node)
    {
        if (node is FilterLogicalNode logical)
        {
            AddFilterColumns(aliasMap, mainTable, logical.Left);
            AddFilterColumns(aliasMap, mainTable, logical.Right);
            return;
        }

        var comparison = (FilterComparisonNode)node;
        AddColumn(aliasMap, mainTable, comparison.Left, InferLiteralType(comparison.RightLiteral));
        if (comparison.RightColumn != null)
            AddColumn(aliasMap, mainTable, comparison.RightColumn, typeof(object));
    }

    private static Type InferFunctionArgumentType(string functionName, int argumentIndex)
    {
        return functionName.ToUpperInvariant() switch
        {
            "COUNT" => typeof(int),
            "AVG" or "SUM" => typeof(decimal),
            "LOWER" or "UPPER" or "SUBSTRING" or "TRIM" or "TRIMSTART" or "TRIMEND" or "CONCAT" or "COALESCE" or "IFNULL" or "NVL" => typeof(string),
            "NOW" or "TODAY" or "CURRENT_DATE" or "CURRENT_TIMESTAMP" => typeof(DateTime),
            _ => typeof(object)
        };
    }

    private static Type InferLiteralType(object? value)
    {
        return value?.GetType() ?? typeof(object);
    }

    private static BoundSelectModel Bind(DatabaseSchema schema, ParsedSelectQuery query, List<CodeGenDiagnostic> diagnostics)
    {
        if (query.MainSource == null)
            throw new InvalidOperationException("缺少 FROM 主表。");

        var aliasMap = new Dictionary<string, BoundTableSource>(StringComparer.OrdinalIgnoreCase);
        var mainTable = ResolveTable(schema, query.MainSource.TableName);
        var model = new BoundSelectModel
        {
            MainTable = new BoundTableSource(query.MainSource.Alias, mainTable)
        };

        aliasMap[model.MainTable.Alias] = model.MainTable;
        model.InvolvedTables.Add(mainTable);

        foreach (var join in query.Joins)
        {
            var joinTable = ResolveTable(schema, join.Table.TableName);
            var boundJoin = new BoundJoin
            {
                Alias = join.Table.Alias,
                Table = joinTable,
                JoinType = join.JoinType
            };

            foreach (var condition in join.Conditions)
            {
                var left = ResolveColumn(aliasMap, model.MainTable, condition.Left);
                var right = ResolveColumn(aliasMap, model.MainTable, condition.Right, joinTable, join.Table.Alias);

                bool rightIsJoin = string.Equals(right.TableAlias, join.Table.Alias, StringComparison.OrdinalIgnoreCase);
                bool leftIsJoin = string.Equals(left.TableAlias, join.Table.Alias, StringComparison.OrdinalIgnoreCase);
                if (rightIsJoin == leftIsJoin)
                    throw new InvalidOperationException("JOIN 条件必须一边引用来源表，一边引用当前 JOIN 表。");

                if (rightIsJoin)
                {
                    boundJoin.SourceAlias = left.TableAlias;
                    boundJoin.ForeignKeys.Add(left.Column.PropertyName);
                    boundJoin.TargetKeys.Add(right.Column.PropertyName);
                    boundJoin.Conditions.Add((left, right));
                }
                else
                {
                    boundJoin.SourceAlias = right.TableAlias;
                    boundJoin.ForeignKeys.Add(right.Column.PropertyName);
                    boundJoin.TargetKeys.Add(left.Column.PropertyName);
                    boundJoin.Conditions.Add((right, left));
                }
            }

            if (string.IsNullOrWhiteSpace(boundJoin.SourceAlias))
                throw new InvalidOperationException($"无法从 JOIN {join.Table.TableName} 推断关联来源。");

            aliasMap[boundJoin.Alias] = new BoundTableSource(boundJoin.Alias, joinTable);
            model.Joins.Add(boundJoin);
            model.InvolvedTables.Add(joinTable);
        }

        foreach (var projection in query.Projections)
            model.Projections.Add(BindProjection(aliasMap, model, projection));

        model.Where = BindFilter(aliasMap, model.MainTable, query.Where);
        model.GroupBy.AddRange(query.GroupBy.Select(g => ResolveColumn(aliasMap, model.MainTable, g)));
        model.OrderBy.AddRange(query.OrderBy.Select(o => new BoundOrderBy(ResolveColumn(aliasMap, model.MainTable, o.Column), o.Ascending)));

        if (query.Projections.Any(p => p.Kind == ProjectionKind.Wildcard && !string.IsNullOrWhiteSpace(p.WildcardAlias)))
        {
            diagnostics.Add(new CodeGenDiagnostic(CodeGenDiagnosticSeverity.Warning, "alias.* 当前仅支持解析与代码输出，不支持 Expr 回生。", "请显式列出字段，以获得完整的 Expr -> SQL 对照。"));
        }

        return model;
    }

    private static BoundProjection BindProjection(Dictionary<string, BoundTableSource> aliasMap, BoundSelectModel model, ParsedProjection projection)
    {
        if (projection.Kind == ProjectionKind.Wildcard)
        {
            return new BoundProjection { Kind = ProjectionKind.Wildcard, WildcardAlias = projection.WildcardAlias };
        }

        if (projection.Kind == ProjectionKind.Column)
        {
            var column = ResolveColumn(aliasMap, model.MainTable, projection.Column!);
            return new BoundProjection
            {
                Kind = ProjectionKind.Column,
                Column = column,
                Alias = projection.Alias
            };
        }

        if (projection.Function == null)
            throw new InvalidOperationException("函数投影解析失败。");
        if (string.IsNullOrWhiteSpace(projection.Alias))
            throw new InvalidOperationException($"函数投影 {projection.Function.Name} 需要显式 AS 别名。");

        return new BoundProjection
        {
            Kind = ProjectionKind.Function,
            Function = BindFunction(aliasMap, model.MainTable, projection.Function),
            Alias = projection.Alias
        };
    }

    private static BoundFunction BindFunction(Dictionary<string, BoundTableSource> aliasMap, BoundTableSource mainTable, ParsedFunctionCall function)
    {
        var arguments = function.Arguments
            .Select(argument => BindValueExpression(aliasMap, mainTable, argument))
            .ToList();
        var whenClauses = function.WhenClauses
            .Select(whenClause => new BoundCaseWhenClause(
                BindFilter(aliasMap, mainTable, whenClause.Condition)!,
                BindValueExpression(aliasMap, mainTable, whenClause.Result)))
            .ToList();
        var elseArgument = function.ElseArgument == null ? null : BindValueExpression(aliasMap, mainTable, function.ElseArgument);
        return new BoundFunction(function.Name, arguments, whenClauses, elseArgument, function.IsDistinct, function.IsStarArgument);
    }

    private static BoundValueExpression BindValueExpression(Dictionary<string, BoundTableSource> aliasMap, BoundTableSource mainTable, ParsedScalarExpression expression)
    {
        return expression switch
        {
            ParsedColumnExpression column => new BoundColumnValueExpression(ResolveColumn(aliasMap, mainTable, column.Column)),
            ParsedLiteralExpression literal => new BoundLiteralValueExpression(literal.Value),
            ParsedFunctionExpression function => new BoundFunctionValueExpression(BindFunction(aliasMap, mainTable, function.Function)),
            ParsedSqlTypeExpression sqlType => new BoundSqlTypeValueExpression(sqlType.TypeName),
            _ => throw new InvalidOperationException("不支持的函数参数类型。")
        };
    }

    private static BoundFilterNode? BindFilter(Dictionary<string, BoundTableSource> aliasMap, BoundTableSource mainTable, FilterNode? node)
    {
        if (node == null)
            return null;

        if (node is FilterLogicalNode logical)
        {
            return new BoundFilterLogicalNode
            {
                Operator = logical.Operator,
                Left = BindFilter(aliasMap, mainTable, logical.Left)!,
                Right = BindFilter(aliasMap, mainTable, logical.Right)!
            };
        }

        var cmp = (FilterComparisonNode)node;
        return new BoundFilterComparisonNode
        {
            Left = ResolveColumn(aliasMap, mainTable, cmp.Left),
            Operator = cmp.Operator,
            RightColumn = cmp.RightColumn == null ? null : ResolveColumn(aliasMap, mainTable, cmp.RightColumn),
            LikeEscapeChar = cmp.LikeEscapeChar,
            RightLiteral = cmp.RightLiteral,
            RightValues = cmp.RightValues
        };
    }

    private static BoundColumnReference ResolveColumn(
        Dictionary<string, BoundTableSource> aliasMap,
        BoundTableSource mainTable,
        ParsedColumnReference reference,
        TableSchema? fallbackJoinTable = null,
        string? fallbackAlias = null)
    {
        if (!string.IsNullOrWhiteSpace(reference.Alias))
        {
            if (!aliasMap.TryGetValue(reference.Alias!, out var source))
            {
                if (fallbackJoinTable != null && string.Equals(reference.Alias, fallbackAlias, StringComparison.OrdinalIgnoreCase))
                    source = new BoundTableSource(fallbackAlias!, fallbackJoinTable);
                else
                    throw new InvalidOperationException($"未找到表别名 {reference.Alias}。");
            }

            var column = source.Table.GetColumn(reference.ColumnName)
                ?? throw new InvalidOperationException($"表 {source.Table.Name} 中不存在列 {reference.ColumnName}。");
            return new BoundColumnReference(source.Alias, source.Table, column);
        }

        var matches = aliasMap.Values
            .Append(mainTable)
            .DistinctBy(v => v.Alias, StringComparer.OrdinalIgnoreCase)
            .Select(v => new { Source = v, Column = v.Table.GetColumn(reference.ColumnName) })
            .Where(v => v.Column != null)
            .ToList();

        if (matches.Count == 0)
            throw new InvalidOperationException($"未找到列 {reference.ColumnName}。");
        if (matches.Count > 1)
            throw new InvalidOperationException($"列 {reference.ColumnName} 来源不明确，请在 SQL 中增加表别名。");

        return new BoundColumnReference(matches[0].Source.Alias, matches[0].Source.Table, matches[0].Column!);
    }

    private static TableSchema ResolveTable(DatabaseSchema schema, string tableName)
    {
        return schema.GetTable(tableName)
            ?? throw new InvalidOperationException($"数据库结构中不存在表 {tableName}。");
    }

    private static string GenerateViewCode(BoundSelectModel model, SqlConversionOptions options, string viewName, List<ViewPropertyModel> properties)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using LiteOrm.Common;");
        sb.AppendLine();
        sb.AppendLine($"namespace {options.Namespace};");
        sb.AppendLine();

        foreach (var join in model.Joins)
        {
            if (options.JoinMetadataMode == JoinMetadataMode.ForeignType && CanUseForeignTypeJoinMetadata(model, join))
                continue;

            var joinType = join.JoinType switch
            {
                SqlJoinType.Inner => "TableJoinType.Inner",
                SqlJoinType.Right => "TableJoinType.Right",
                _ => "TableJoinType.Left"
            };
            bool omitAlias = CanOmitJoinAlias(model, join);
            var joinTypeCode = join.JoinType == SqlJoinType.Left ? string.Empty : $", JoinType = {joinType}";

            if (string.Equals(join.SourceAlias, model.MainTable.Alias, StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine(omitAlias
                    ? $"[TableJoin(typeof({join.Table.ClassName}), nameof({model.MainTable.Table.ClassName}.{join.ForeignKeys[0]}){joinTypeCode})]"
                    : $"[TableJoin(typeof({join.Table.ClassName}), nameof({model.MainTable.Table.ClassName}.{join.ForeignKeys[0]}), Alias = \"{join.Alias}\"{joinTypeCode})]");
            }
            else
            {
                var sourceTable = model.Joins.First(j => string.Equals(j.Alias, join.SourceAlias, StringComparison.OrdinalIgnoreCase)).Table.ClassName;
                sb.AppendLine(omitAlias
                    ? $"[TableJoin(\"{join.SourceAlias}\", typeof({join.Table.ClassName}), nameof({sourceTable}.{join.ForeignKeys[0]}){joinTypeCode})]"
                    : $"[TableJoin(\"{join.SourceAlias}\", typeof({join.Table.ClassName}), nameof({sourceTable}.{join.ForeignKeys[0]}), Alias = \"{join.Alias}\"{joinTypeCode})]");
            }
        }

        sb.AppendLine($"public class {viewName} : {model.MainTable.Table.ClassName}");
        sb.AppendLine("{");

        foreach (var property in properties)
        {
            foreach (var attribute in property.Attributes)
                sb.AppendLine("    " + attribute);

            sb.AppendLine($"    public {property.TypeName} {property.Name} {{ get; set; }}");
            sb.AppendLine();
        }

        if (properties.Count > 0)
        {
            sb.Length -= Environment.NewLine.Length * 2;
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString().TrimEnd();
    }

    private static bool CanOmitJoinAlias(BoundSelectModel model, BoundJoin join)
    {
        if (!string.Equals(join.SourceAlias, model.MainTable.Alias, StringComparison.OrdinalIgnoreCase))
            return false;

        return model.Joins.Count(j => string.Equals(j.Table.Name, join.Table.Name, StringComparison.OrdinalIgnoreCase)) == 1;
    }

    private static bool CanUseForeignTypeJoinMetadata(BoundSelectModel model, BoundJoin join)
    {
        return string.Equals(join.SourceAlias, model.MainTable.Alias, StringComparison.OrdinalIgnoreCase)
               && join.ForeignKeys.Count == 1
               && join.TargetKeys.Count == 1
               && join.Conditions.Count == 1
               && CanOmitJoinAlias(model, join);
    }

    private static List<ViewPropertyModel> BuildViewProperties(BoundSelectModel model)
    {
        var properties = new List<ViewPropertyModel>();
        for (int i = 0; i < model.Projections.Count; i++)
        {
            var projection = model.Projections[i];
            if (projection.Kind == ProjectionKind.Wildcard)
                continue;

            if (projection.Kind == ProjectionKind.Column && projection.Column != null)
            {
                bool fromMain = string.Equals(projection.Column.TableAlias, model.MainTable.Alias, StringComparison.OrdinalIgnoreCase);
                string propertyName = projection.Alias ?? projection.Column.Column.PropertyName;
                if (fromMain && string.Equals(propertyName, projection.Column.Column.PropertyName, StringComparison.Ordinal))
                    continue;

                var attributes = new List<string>();
                AddPropertyOrderAttribute(attributes, i);
                if (!fromMain)
                {
                    bool useAlias = model.Joins.Any(j => string.Equals(j.Alias, projection.Column.TableAlias, StringComparison.OrdinalIgnoreCase) && !CanOmitJoinAlias(model, j));
                    attributes.Add(useAlias
                        ? $"[ForeignColumn(\"{projection.Column.TableAlias}\", Property = nameof({projection.Column.Table.ClassName}.{projection.Column.Column.PropertyName}))]"
                        : $"[ForeignColumn(typeof({projection.Column.Table.ClassName}), Property = nameof({projection.Column.Table.ClassName}.{projection.Column.Column.PropertyName}))]");
                }
                else if (!string.Equals(propertyName, projection.Column.Column.PropertyName, StringComparison.Ordinal))
                    attributes.Add($"[Column(\"{projection.Alias}\")]");

                properties.Add(new ViewPropertyModel(propertyName, CodeGenNaming.ToCSharpTypeName(projection.Column.Column.ClrType, projection.Column.Column.IsNullable), attributes, projection.Column));
                continue;
            }

            if (projection.Kind == ProjectionKind.Function && projection.Function != null)
            {
                var sourceColumn = EnumerateFunctionColumns(projection.Function).FirstOrDefault();
                var resultType = InferFunctionResultType(projection.Function);
                var attributes = new List<string>();
                AddPropertyOrderAttribute(attributes, i);
                properties.Add(new ViewPropertyModel(projection.Alias!, CodeGenNaming.ToCSharpTypeName(resultType, false), attributes, sourceColumn));
            }
        }

        return properties
            .GroupBy(p => p.Name, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();
    }

    private static string GenerateQueryCode(BoundSelectModel model, SqlConversionOptions options, string viewName, List<ViewPropertyModel> viewProperties)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using LiteOrm.Common;");
        if (options.UseStaticExpr)
            sb.AppendLine("using static LiteOrm.Common.Expr;");
        sb.AppendLine();
        sb.AppendLine($"namespace {options.Namespace};");
        sb.AppendLine();
        sb.AppendLine("public static class GeneratedExprQuery");
        sb.AppendLine("{");
        sb.AppendLine("    public static Expr Build()");
        sb.AppendLine("    {");
        if (CanUseImplicitViewChain(model, viewProperties))
        {
            sb.AppendLine($"        return {BuildImplicitRoot(model, viewName, options)}");
            if (!AppendChainClauses(sb, model, options, viewName, viewProperties, useViewProperties: true))
                sb.AppendLine("            ;");
        }
        else
        {
            sb.AppendLine($"        return {BuildExplicitRoot(model, options)}");
            if (!AppendChainClauses(sb, model, options, viewName, viewProperties, useViewProperties: false))
                sb.AppendLine("            ;");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString().TrimEnd();
    }

    private static bool AppendChainClauses(StringBuilder sb, BoundSelectModel model, SqlConversionOptions options, string viewName, List<ViewPropertyModel> viewProperties, bool useViewProperties)
    {
        if (model.Where != null)
            sb.AppendLine($"            .Where({BuildFilterCode(model.Where, options, model, viewName, viewProperties, useViewProperties)})");
        if (model.GroupBy.Count > 0)
            sb.AppendLine($"            .GroupBy({string.Join(", ", model.GroupBy.Select(g => BuildColumnCode(g, options, model, viewName, viewProperties, useViewProperties)))})");
        if (model.OrderBy.Count > 0)
            sb.AppendLine($"            .OrderBy({string.Join(", ", model.OrderBy.Select(o => $"{BuildColumnCode(o.Column, options, model, viewName, viewProperties, useViewProperties)}.{(o.Ascending ? "Asc()" : "Desc()")}"))})");

        if (!ShouldEmitSelectClause(model, options, useViewProperties))
            return false;

        sb.AppendLine("            .Select(");
        for (int i = 0; i < model.Projections.Count; i++)
        {
            var code = model.Projections[i].Kind switch
            {
                ProjectionKind.Wildcard when string.IsNullOrWhiteSpace(model.Projections[i].WildcardAlias) => BuildWildcardCode(options),
                ProjectionKind.Wildcard => "// alias.* requires explicit field expansion before Expr regeneration",
                ProjectionKind.Column => BuildProjectionCode(model.Projections[i], options, model, viewName, viewProperties, useViewProperties),
                ProjectionKind.Function => BuildFunctionProjectionCode(model.Projections[i], options, model, viewName, viewProperties, useViewProperties),
                _ => throw new InvalidOperationException("不支持的投影类型。")
            };
            if (i < model.Projections.Count - 1)
                code += ",";
            sb.AppendLine("                " + code);
        }
        sb.AppendLine("            );");
        return true;
    }

    private static bool ShouldEmitSelectClause(BoundSelectModel model, SqlConversionOptions options, bool useViewProperties)
    {
        if (options.GenerateFullSelect)
            return true;
        if (!useViewProperties)
            return true;
        if (model.Projections.Count == 0)
            return false;

        return model.Projections.Any(projection => projection.Kind != ProjectionKind.Column);
    }

    private static string BuildExplicitRoot(BoundSelectModel model, SqlConversionOptions options)
    {
        var root = BuildTableSourceCode(model.MainTable.Table.ClassName, model.MainTable.Alias, true, options);
        foreach (var join in model.Joins)
        {
            var joinType = join.JoinType == SqlJoinType.Left
                ? string.Empty
                : $", {join.JoinType switch
                {
                    SqlJoinType.Inner => "TableJoinType.Inner",
                    SqlJoinType.Right => "TableJoinType.Right",
                    _ => "TableJoinType.Left"
                }}";
            var target = BuildTableSourceCode(join.Table.ClassName, join.Alias, true, options);
            var onCode = string.Join(" & ", join.Conditions.Select(c => $"{ToExplicitColumnCode(c.Source, options)} == {ToExplicitColumnCode(c.Target, options)}"));
            root += Environment.NewLine + $"            .Join({target}, {onCode}{joinType})";
        }

        return root;
    }

    private static string BuildImplicitRoot(BoundSelectModel model, string viewName, SqlConversionOptions options)
    {
        return $"{ExprReference(options)}From<{viewName}>()";
    }

    private static string BuildTableSourceCode(string typeName, string alias, bool forceAlias, SqlConversionOptions options)
    {
        var root = $"{ExprReference(options)}From(typeof({typeName}))";
        return forceAlias ? $"{root}.As(\"{alias}\")" : root;
    }

    private static bool CanUseImplicitViewChain(BoundSelectModel model, List<ViewPropertyModel> viewProperties)
    {
        foreach (var column in EnumerateReferencedColumns(model))
        {
            if (!TryGetViewPropertyName(column, model, viewProperties, out _))
                return false;
        }

        return model.Projections.All(projection => projection.Kind != ProjectionKind.Wildcard || string.IsNullOrWhiteSpace(projection.WildcardAlias));
    }

    private static IEnumerable<BoundColumnReference> EnumerateReferencedColumns(BoundSelectModel model)
    {
        foreach (var projection in model.Projections)
        {
            if (projection.Column != null)
                yield return projection.Column;
            if (projection.Function != null)
            {
                foreach (var column in EnumerateFunctionColumns(projection.Function))
                    yield return column;
            }
        }

        if (model.Where != null)
        {
            foreach (var column in EnumerateFilterColumns(model.Where))
                yield return column;
        }

        foreach (var column in model.GroupBy)
            yield return column;
        foreach (var orderBy in model.OrderBy)
            yield return orderBy.Column;
    }

    private static IEnumerable<BoundColumnReference> EnumerateFilterColumns(BoundFilterNode node)
    {
        if (node is BoundFilterLogicalNode logical)
        {
            foreach (var column in EnumerateFilterColumns(logical.Left))
                yield return column;
            foreach (var column in EnumerateFilterColumns(logical.Right))
                yield return column;
            yield break;
        }

        var comparison = (BoundFilterComparisonNode)node;
        yield return comparison.Left;
        if (comparison.RightColumn != null)
            yield return comparison.RightColumn;
    }

    private static bool TryGetViewPropertyName(BoundColumnReference column, BoundSelectModel model, List<ViewPropertyModel> viewProperties, out string propertyName)
    {
        if (string.Equals(column.TableAlias, model.MainTable.Alias, StringComparison.OrdinalIgnoreCase))
        {
            propertyName = column.Column.PropertyName;
            return true;
        }

        var property = viewProperties.FirstOrDefault(p =>
            p.SourceColumn != null &&
            string.Equals(p.SourceColumn.TableAlias, column.TableAlias, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.SourceColumn.Column.PropertyName, column.Column.PropertyName, StringComparison.Ordinal));
        if (property != null)
        {
            propertyName = property.Name;
            return true;
        }

        propertyName = string.Empty;
        return false;
    }

    private static string BuildProjectionCode(BoundProjection projection, SqlConversionOptions options, BoundSelectModel model, string viewName, List<ViewPropertyModel> viewProperties, bool useViewProperties)
    {
        var code = BuildColumnCode(projection.Column!, options, model, viewName, viewProperties, useViewProperties);
        if (useViewProperties)
            return code;

        if (!string.IsNullOrWhiteSpace(projection.Alias))
            code += options.UseNameof ? $".As(nameof({viewName}.{projection.Alias}))" : $".As(\"{projection.Alias}\")";
        return code;
    }

    private static string BuildFunctionProjectionCode(BoundProjection projection, SqlConversionOptions options, BoundSelectModel model, string viewName, List<ViewPropertyModel> viewProperties, bool useViewProperties)
    {
        var function = projection.Function!;
        string baseCode = BuildFunctionCode(function, options, model, viewName, viewProperties, useViewProperties);
        return options.UseNameof
            ? $"{baseCode}.As(nameof({viewName}.{projection.Alias}))"
            : $"{baseCode}.As(\"{projection.Alias}\")";
    }

    private static string BuildFunctionCode(BoundFunction function, SqlConversionOptions options, BoundSelectModel model, string viewName, List<ViewPropertyModel> viewProperties, bool useViewProperties)
    {
        string upperName = function.Name.ToUpperInvariant();
        if (upperName == "CASE")
            return BuildCaseCode(function, options, model, viewName, viewProperties, useViewProperties);

        if (upperName == "COUNT" && function.IsStarArgument)
            return $"{ExprReference(options)}Const(1).Count()";

        if (upperName == "COUNT" && function.Arguments.Count == 1)
            return function.IsDistinct
                ? $"{BuildValueExpressionCode(function.Arguments[0], options, model, viewName, viewProperties, useViewProperties)}.Count(true)"
                : $"{BuildValueExpressionCode(function.Arguments[0], options, model, viewName, viewProperties, useViewProperties)}.Count()";

        if (function.Arguments.Count == 1 && upperName is "SUM" or "AVG" or "MAX" or "MIN")
        {
            var argumentCode = BuildValueExpressionCode(function.Arguments[0], options, model, viewName, viewProperties, useViewProperties);
            if (function.IsDistinct)
                return $"{ExprReference(options)}Aggregate({ToLiteralCode(upperName)}, {argumentCode}, true)";

            return upperName switch
            {
                "SUM" => $"{argumentCode}.Sum()",
                "AVG" => $"{argumentCode}.Avg()",
                "MAX" => $"{argumentCode}.Max()",
                _ => $"{argumentCode}.Min()"
            };
        }

        if (upperName is "COALESCE" or "IFNULL" or "NVL" && function.Arguments.Count == 2)
        {
            return $"{BuildValueExpressionCode(function.Arguments[0], options, model, viewName, viewProperties, useViewProperties)}.IfNull({BuildValueExpressionCode(function.Arguments[1], options, model, viewName, viewProperties, useViewProperties)})";
        }

        if (upperName == "CONCAT" && function.Arguments.Count == 2)
        {
            return $"{BuildValueExpressionCode(function.Arguments[0], options, model, viewName, viewProperties, useViewProperties)}.Concat({BuildValueExpressionCode(function.Arguments[1], options, model, viewName, viewProperties, useViewProperties)})";
        }

        if (upperName is "NOW" or "CURRENT_TIMESTAMP" && function.Arguments.Count == 0)
            return $"{ExprReference(options)}Now()";

        if (upperName is "TODAY" or "CURRENT_DATE" && function.Arguments.Count == 0)
            return $"{ExprReference(options)}Today()";

        if (upperName == "CAST" && function.Arguments.Count == 2 && TryBuildCastCode(function, options, model, viewName, viewProperties, useViewProperties, out var castCode))
            return castCode;

        var argumentCodes = function.Arguments
            .Select(argument => BuildGenericFunctionArgumentCode(argument, options, model, viewName, viewProperties, useViewProperties))
            .ToList();
        if (function.IsDistinct)
        {
            if (argumentCodes.Count == 0)
                throw new InvalidOperationException($"函数 {function.Name} 使用 DISTINCT 时缺少参数。");
            argumentCodes[0] += ".Distinct()";
        }

        return argumentCodes.Count == 0
            ? $"{ExprReference(options)}Func({ToLiteralCode(function.Name)})"
            : $"{ExprReference(options)}Func({ToLiteralCode(function.Name)}, {string.Join(", ", argumentCodes)})";
    }

    private static string BuildCaseCode(BoundFunction function, SqlConversionOptions options, BoundSelectModel model, string viewName, List<ViewPropertyModel> viewProperties, bool useViewProperties)
    {
        if (function.WhenClauses.Count == 0)
            throw new InvalidOperationException("CASE 表达式缺少 WHEN 分支。");

        var casesCode = string.Join(", ", function.WhenClauses.Select(whenClause =>
            $"new KeyValuePair<LogicExpr, ValueTypeExpr>({BuildFilterCode(whenClause.Condition, options, model, viewName, viewProperties, useViewProperties)}, {BuildValueExpressionCode(whenClause.Result, options, model, viewName, viewProperties, useViewProperties)})"));

        return function.ElseArgument == null
            ? $"{ExprReference(options)}Case(new[] {{ {casesCode} }})"
            : $"{ExprReference(options)}Case(new[] {{ {casesCode} }}, {BuildValueExpressionCode(function.ElseArgument, options, model, viewName, viewProperties, useViewProperties)})";
    }

    private static bool TryBuildCastCode(BoundFunction function, SqlConversionOptions options, BoundSelectModel model, string viewName, List<ViewPropertyModel> viewProperties, bool useViewProperties, out string code)
    {
        if (!TryMapSqlTypeArgument(function.Arguments[1], out var dbTypeCode))
        {
            code = string.Empty;
            return false;
        }

        code = $"{ExprReference(options)}Func(@\"CAST\", {BuildValueExpressionCode(function.Arguments[0], options, model, viewName, viewProperties, useViewProperties)}, {ExprReference(options)}Const({dbTypeCode}))";
        return true;
    }

    private static string BuildGenericFunctionArgumentCode(BoundValueExpression expression, SqlConversionOptions options, BoundSelectModel model, string viewName, List<ViewPropertyModel> viewProperties, bool useViewProperties)
    {
        return expression switch
        {
            BoundSqlTypeValueExpression sqlType => ToLiteralCode(sqlType.TypeName),
            _ => BuildValueExpressionCode(expression, options, model, viewName, viewProperties, useViewProperties)
        };
    }

    private static string BuildValueExpressionCode(BoundValueExpression expression, SqlConversionOptions options, BoundSelectModel model, string viewName, List<ViewPropertyModel> viewProperties, bool useViewProperties)
    {
        return expression switch
        {
            BoundColumnValueExpression column => BuildColumnCode(column.Column, options, model, viewName, viewProperties, useViewProperties),
            BoundLiteralValueExpression literal => ToLiteralCode(literal.Value),
            BoundFunctionValueExpression function => BuildFunctionCode(function.Function, options, model, viewName, viewProperties, useViewProperties),
            BoundSqlTypeValueExpression sqlType => ToLiteralCode(sqlType.TypeName),
            _ => throw new InvalidOperationException("不支持的函数参数类型。")
        };
    }

    private static string BuildFilterCode(BoundFilterNode node, SqlConversionOptions options, BoundSelectModel model, string viewName, List<ViewPropertyModel> viewProperties, bool useViewProperties, int parentPriority = 0)
    {
        if (node is BoundFilterLogicalNode logical)
        {
            int currentPriority = GetFilterPriority(logical);
            var op = logical.Operator == FilterLogicalOperator.And ? "&" : "|";
            var code = $"{BuildFilterCode(logical.Left, options, model, viewName, viewProperties, useViewProperties, currentPriority)} {op} {BuildFilterCode(logical.Right, options, model, viewName, viewProperties, useViewProperties, currentPriority)}";
            return currentPriority < parentPriority ? $"({code})" : code;
        }

        var cmp = (BoundFilterComparisonNode)node;
        string left = BuildColumnCode(cmp.Left, options, model, viewName, viewProperties, useViewProperties);
        var likeCode = TryBuildLikeCode(cmp.Operator, left, cmp.RightLiteral, cmp.LikeEscapeChar);
        if (likeCode != null)
            return likeCode;

        return cmp.Operator switch
        {
            FilterComparisonOperator.Equal => cmp.RightColumn != null ? $"{left} == {BuildColumnCode(cmp.RightColumn, options, model, viewName, viewProperties, useViewProperties)}" : $"{left} == {ToLiteralCode(cmp.RightLiteral)}",
            FilterComparisonOperator.NotEqual => cmp.RightColumn != null ? $"{left} != {BuildColumnCode(cmp.RightColumn, options, model, viewName, viewProperties, useViewProperties)}" : $"{left} != {ToLiteralCode(cmp.RightLiteral)}",
            FilterComparisonOperator.GreaterThan => $"{left} > {ToLiteralCode(cmp.RightLiteral)}",
            FilterComparisonOperator.GreaterThanOrEqual => $"{left} >= {ToLiteralCode(cmp.RightLiteral)}",
            FilterComparisonOperator.LessThan => $"{left} < {ToLiteralCode(cmp.RightLiteral)}",
            FilterComparisonOperator.LessThanOrEqual => $"{left} <= {ToLiteralCode(cmp.RightLiteral)}",
            FilterComparisonOperator.Like => $"{left}.Like({ToLiteralCode(cmp.RightLiteral)})",
            FilterComparisonOperator.NotLike => $"!{left}.Like({ToLiteralCode(cmp.RightLiteral)})",
            FilterComparisonOperator.In => $"{left}.In({string.Join(", ", cmp.RightValues!.Select(ToLiteralCode))})",
            FilterComparisonOperator.NotIn => $"!{left}.In({string.Join(", ", cmp.RightValues!.Select(ToLiteralCode))})",
            FilterComparisonOperator.IsNull => $"{left}.IsNull()",
            FilterComparisonOperator.IsNotNull => $"{left}.IsNotNull()",
            _ => throw new InvalidOperationException($"不支持的筛选操作：{cmp.Operator}")
        };
    }

    private static string BuildColumnCode(BoundColumnReference column, SqlConversionOptions options, BoundSelectModel model, string viewName, List<ViewPropertyModel> viewProperties, bool useViewProperties)
    {
        if (useViewProperties && TryGetViewPropertyName(column, model, viewProperties, out var propertyName))
            return ToViewPropCode(propertyName, viewName, options);

        return ToExplicitColumnCode(column, options);
    }

    private static string ToViewPropCode(string propertyName, string viewName, SqlConversionOptions options)
    {
        var property = options.UseNameof ? $"nameof({viewName}.{propertyName})" : $"\"{propertyName}\"";
        return $"{ExprReference(options)}Prop({property})";
    }

    private static string ToExplicitColumnCode(BoundColumnReference column, SqlConversionOptions options)
    {
        var property = options.UseNameof
            ? $"nameof({column.Table.ClassName}.{column.Column.PropertyName})"
            : $"\"{column.Column.PropertyName}\"";
        return $"{ExprReference(options)}Prop(\"{column.TableAlias}\", {property})";
    }

    private static string BuildWildcardCode(SqlConversionOptions options)
    {
        return $"{ExprReference(options)}Prop(\"*\")";
    }

    private static void AddPropertyOrderAttribute(List<string> attributes, int ordinal)
    {
        attributes.Add($"[PropertyOrder({ordinal})]");
    }

    private static int GetFilterPriority(BoundFilterNode node)
    {
        return node is BoundFilterLogicalNode logical
            ? logical.Operator == FilterLogicalOperator.And ? 2 : 1
            : 3;
    }

    private static string? TryBuildLikeCode(FilterComparisonOperator @operator, string left, object? rightLiteral, char? escapeChar)
    {
        if (rightLiteral is not string pattern)
            return null;
        if (!TryAnalyzeLikePattern(pattern, escapeChar, out var method, out var value))
            return null;

        var code = $"{left}.{method}({ToLiteralCode(value)})";
        return @operator == FilterComparisonOperator.NotLike ? "!" + code : code;
    }

    private static bool TryAnalyzeLikePattern(string pattern, char? escapeChar, out string method, out string value)
    {
        var segments = new List<(LikeSegmentKind Kind, string Value)>();
        var literal = new StringBuilder();
        for (int i = 0; i < pattern.Length; i++)
        {
            char current = pattern[i];
            if (escapeChar.HasValue && current == escapeChar.Value)
            {
                if (i == pattern.Length - 1)
                {
                    method = string.Empty;
                    value = string.Empty;
                    return false;
                }

                literal.Append(pattern[++i]);
                continue;
            }

            if (current is '%' or '_')
            {
                if (literal.Length > 0)
                {
                    segments.Add((LikeSegmentKind.Literal, literal.ToString()));
                    literal.Clear();
                }

                segments.Add((current == '%' ? LikeSegmentKind.Many : LikeSegmentKind.Single, current.ToString()));
                continue;
            }

            literal.Append(current);
        }

        if (literal.Length > 0)
            segments.Add((LikeSegmentKind.Literal, literal.ToString()));

        if (segments.Any(segment => segment.Kind == LikeSegmentKind.Single))
        {
            method = string.Empty;
            value = string.Empty;
            return false;
        }

        if (segments.Count == 2 &&
            segments[0].Kind == LikeSegmentKind.Literal &&
            segments[1].Kind == LikeSegmentKind.Many &&
            segments[0].Value.Length > 0 &&
            !ContainsLikeMetaChars(segments[0].Value))
        {
            method = "StartsWith";
            value = segments[0].Value;
            return true;
        }

        if (segments.Count == 2 &&
            segments[0].Kind == LikeSegmentKind.Many &&
            segments[1].Kind == LikeSegmentKind.Literal &&
            segments[1].Value.Length > 0 &&
            !ContainsLikeMetaChars(segments[1].Value))
        {
            method = "EndsWith";
            value = segments[1].Value;
            return true;
        }

        if (segments.Count == 3 &&
            segments[0].Kind == LikeSegmentKind.Many &&
            segments[1].Kind == LikeSegmentKind.Literal &&
            segments[2].Kind == LikeSegmentKind.Many &&
            segments[1].Value.Length > 0 &&
            !ContainsLikeMetaChars(segments[1].Value))
        {
            method = "Contains";
            value = segments[1].Value;
            return true;
        }

        method = string.Empty;
        value = string.Empty;
        return false;
    }

    private static bool ContainsLikeMetaChars(string value)
    {
        return value.Contains('%', StringComparison.Ordinal) || value.Contains('_', StringComparison.Ordinal);
    }

    private static IEnumerable<BoundColumnReference> EnumerateFunctionColumns(BoundFunction function)
    {
        foreach (var argument in function.Arguments)
        {
            foreach (var column in EnumerateColumns(argument))
                yield return column;
        }
    }

    private static IEnumerable<BoundColumnReference> EnumerateColumns(BoundValueExpression expression)
    {
        switch (expression)
        {
            case BoundColumnValueExpression column:
                yield return column.Column;
                break;
            case BoundFunctionValueExpression function:
                foreach (var columnReference in EnumerateFunctionColumns(function.Function))
                    yield return columnReference;
                break;
        }
    }

    private static Type InferFunctionResultType(BoundFunction function)
    {
        return function.Name.ToUpperInvariant() switch
        {
            "CASE" => InferCaseResultType(function),
            "COUNT" => typeof(int),
            "AVG" => typeof(double),
            "SUM" => InferValueExpressionType(function.Arguments.FirstOrDefault()) ?? typeof(decimal),
            "MAX" or "MIN" => InferValueExpressionType(function.Arguments.FirstOrDefault()) ?? typeof(object),
            "NOW" or "TODAY" or "CURRENT_DATE" or "CURRENT_TIMESTAMP" => typeof(DateTime),
            "CONCAT" or "LOWER" or "UPPER" or "SUBSTRING" or "TRIM" or "TRIMSTART" or "TRIMEND" or "FORMAT" => typeof(string),
            "COALESCE" or "IFNULL" or "NVL" => InferValueExpressionType(function.Arguments.FirstOrDefault()) ?? InferValueExpressionType(function.Arguments.Skip(1).FirstOrDefault()) ?? typeof(object),
            "CAST" when function.Arguments.Count > 1 && TryMapSqlTypeArgument(function.Arguments[1], out _, out var castType) => castType,
            _ => typeof(object)
        };
    }

    private static Type InferCaseResultType(BoundFunction function)
    {
        foreach (var whenClause in function.WhenClauses)
        {
            var branchType = InferValueExpressionType(whenClause.Result);
            if (branchType != null)
                return branchType;
        }

        return InferValueExpressionType(function.ElseArgument) ?? typeof(object);
    }

    private static Type? InferValueExpressionType(BoundValueExpression? expression)
    {
        return expression switch
        {
            null => null,
            BoundColumnValueExpression column => column.Column.Column.ClrType,
            BoundLiteralValueExpression literal => literal.Value?.GetType(),
            BoundFunctionValueExpression function => InferFunctionResultType(function.Function),
            _ => null
        };
    }

    private static bool TryMapSqlTypeArgument(BoundValueExpression expression, out string dbTypeCode)
        => TryMapSqlTypeArgument(expression, out dbTypeCode, out _);

    private static bool TryMapSqlTypeArgument(BoundValueExpression expression, out string dbTypeCode, out Type clrType)
    {
        string? typeName = expression switch
        {
            BoundSqlTypeValueExpression sqlType => sqlType.TypeName,
            BoundLiteralValueExpression literal when literal.Value is string text => text,
            _ => null
        };

        if (typeName != null && TryMapSqlTypeName(typeName, out dbTypeCode, out clrType))
            return true;

        dbTypeCode = string.Empty;
        clrType = typeof(object);
        return false;
    }

    private static bool TryMapSqlTypeName(string typeName, out string dbTypeCode, out Type clrType)
    {
        string normalized = typeName.ToUpperInvariant().Trim();
        int parenIndex = normalized.IndexOf('(');
        if (parenIndex >= 0)
            normalized = normalized[..parenIndex];
        normalized = normalized.Replace(" ", string.Empty, StringComparison.Ordinal);

        switch (normalized)
        {
            case "CHAR":
            case "NCHAR":
            case "VARCHAR":
            case "NVARCHAR":
            case "TEXT":
            case "CLOB":
            case "STRING":
                dbTypeCode = "System.Data.DbType.String";
                clrType = typeof(string);
                return true;
            case "INT":
            case "INTEGER":
            case "INT4":
                dbTypeCode = "System.Data.DbType.Int32";
                clrType = typeof(int);
                return true;
            case "BIGINT":
            case "INT8":
                dbTypeCode = "System.Data.DbType.Int64";
                clrType = typeof(long);
                return true;
            case "SMALLINT":
                dbTypeCode = "System.Data.DbType.Int16";
                clrType = typeof(short);
                return true;
            case "TINYINT":
                dbTypeCode = "System.Data.DbType.Byte";
                clrType = typeof(byte);
                return true;
            case "DECIMAL":
            case "NUMERIC":
            case "MONEY":
                dbTypeCode = "System.Data.DbType.Decimal";
                clrType = typeof(decimal);
                return true;
            case "FLOAT":
            case "DOUBLE":
            case "DOUBLEPRECISION":
                dbTypeCode = "System.Data.DbType.Double";
                clrType = typeof(double);
                return true;
            case "REAL":
                dbTypeCode = "System.Data.DbType.Single";
                clrType = typeof(float);
                return true;
            case "BIT":
            case "BOOL":
            case "BOOLEAN":
                dbTypeCode = "System.Data.DbType.Boolean";
                clrType = typeof(bool);
                return true;
            case "DATE":
                dbTypeCode = "System.Data.DbType.Date";
                clrType = typeof(DateTime);
                return true;
            case "DATETIME":
            case "DATETIME2":
            case "TIMESTAMP":
                dbTypeCode = "System.Data.DbType.DateTime";
                clrType = typeof(DateTime);
                return true;
            case "GUID":
            case "UNIQUEIDENTIFIER":
                dbTypeCode = "System.Data.DbType.Guid";
                clrType = typeof(Guid);
                return true;
        }

        dbTypeCode = string.Empty;
        clrType = typeof(object);
        return false;
    }

    private static string ExprReference(SqlConversionOptions options) => options.UseStaticExpr ? string.Empty : "Expr.";

    private static string ToLiteralCode(object? value)
    {
        return value switch
        {
            null => "null",
            string text => "@\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"",
            bool b => b ? "true" : "false",
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture) + "f",
            decimal m => m.ToString(CultureInfo.InvariantCulture) + "m",
            long l => l.ToString(CultureInfo.InvariantCulture) + "L",
            _ => System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null"
        };
    }

    private sealed record ViewPropertyModel(string Name, string TypeName, List<string> Attributes, BoundColumnReference? SourceColumn);

    private sealed class BoundSelectModel
    {
        public BoundTableSource MainTable { get; set; } = null!;

        public List<BoundJoin> Joins { get; } = new();

        public List<BoundProjection> Projections { get; } = new();

        public BoundFilterNode? Where { get; set; }

        public List<BoundColumnReference> GroupBy { get; } = new();

        public List<BoundOrderBy> OrderBy { get; } = new();

        public List<TableSchema> InvolvedTables { get; } = new();
    }

    private sealed record BoundTableSource(string Alias, TableSchema Table);

    private sealed class BoundJoin
    {
        public string Alias { get; set; } = string.Empty;

        public string SourceAlias { get; set; } = string.Empty;

        public TableSchema Table { get; set; } = null!;

        public SqlJoinType JoinType { get; set; }

        public List<string> ForeignKeys { get; } = new();

        public List<string> TargetKeys { get; } = new();

        public List<(BoundColumnReference Source, BoundColumnReference Target)> Conditions { get; } = new();
    }

    private sealed class BoundProjection
    {
        public ProjectionKind Kind { get; set; }

        public BoundColumnReference? Column { get; set; }

        public BoundFunction? Function { get; set; }

        public string? Alias { get; set; }

        public string? WildcardAlias { get; set; }
    }

    private abstract record BoundValueExpression
    {
    }

    private sealed record BoundColumnValueExpression(BoundColumnReference Column) : BoundValueExpression;

    private sealed record BoundLiteralValueExpression(object? Value) : BoundValueExpression;

    private sealed record BoundFunctionValueExpression(BoundFunction Function) : BoundValueExpression;

    private sealed record BoundSqlTypeValueExpression(string TypeName) : BoundValueExpression;

    private sealed record BoundCaseWhenClause(BoundFilterNode Condition, BoundValueExpression Result);

    private sealed record BoundFunction(string Name, IReadOnlyList<BoundValueExpression> Arguments, IReadOnlyList<BoundCaseWhenClause> WhenClauses, BoundValueExpression? ElseArgument, bool IsDistinct, bool IsStarArgument);

    private sealed record BoundColumnReference(string TableAlias, TableSchema Table, ColumnSchema Column);

    private abstract class BoundFilterNode
    {
    }

    private sealed class BoundFilterLogicalNode : BoundFilterNode
    {
        public FilterLogicalOperator Operator { get; set; }

        public BoundFilterNode Left { get; set; } = null!;

        public BoundFilterNode Right { get; set; } = null!;
    }

    private sealed class BoundFilterComparisonNode : BoundFilterNode
    {
        public BoundColumnReference Left { get; set; } = null!;

        public FilterComparisonOperator Operator { get; set; }

        public object? RightLiteral { get; set; }

        public char? LikeEscapeChar { get; set; }

        public BoundColumnReference? RightColumn { get; set; }

        public List<object?>? RightValues { get; set; }
    }

    private sealed record BoundOrderBy(BoundColumnReference Column, bool Ascending);

    private enum LikeSegmentKind
    {
        Literal,
        Many,
        Single
    }
}