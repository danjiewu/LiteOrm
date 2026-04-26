using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LiteOrm.CodeGen;

public sealed class SelectArtifactsGenerator
{
    private readonly SelectSqlParser _parser = new();
    private readonly EntityCodeGenerator _entityGenerator = new();

    public SelectGenerationResult Generate(DatabaseSchema schema, string sql, SelectGenerationOptions? options = null)
    {
        if (schema == null) throw new ArgumentNullException(nameof(schema));
        options ??= new SelectGenerationOptions();

        var result = new SelectGenerationResult();
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
            var bound = Bind(schema, query, options, result.Diagnostics);
            if (result.Diagnostics.Any(d => d.Severity == CodeGenDiagnosticSeverity.Error))
                return result;

            result.RelatedEntityCode = _entityGenerator.Generate(schema, new EntityGenerationOptions
            {
                Namespace = options.Namespace,
                Tables = bound.InvolvedTables.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            }).CombinedCode;
            result.ViewCode = GenerateViewCode(bound, options);
            result.QueryCode = GenerateQueryCode(bound, options);
        }
        catch (Exception ex)
        {
            result.Diagnostics.Add(new CodeGenDiagnostic(CodeGenDiagnosticSeverity.Error, ex.Message, "请检查 SQL 中的表名、列名和 JOIN 关系是否与数据库结构一致。"));
        }

        return result;
    }

    private static BoundSelectModel Bind(DatabaseSchema schema, ParsedSelectQuery query, SelectGenerationOptions options, List<CodeGenDiagnostic> diagnostics)
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
                var left = ResolveColumn(aliasMap, model.MainTable, condition.Left, diagnostics);
                var right = ResolveColumn(aliasMap, model.MainTable, condition.Right, diagnostics, joinTable, join.Table.Alias);
                if (left == null || right == null)
                    continue;

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
        {
            model.Projections.Add(BindProjection(aliasMap, model, projection, diagnostics));
        }

        model.Where = BindFilter(aliasMap, model.MainTable, query.Where, diagnostics);
        model.GroupBy.AddRange(query.GroupBy.Select(g => ResolveColumn(aliasMap, model.MainTable, g, diagnostics)!).Where(c => c != null));
        model.OrderBy.AddRange(query.OrderBy.Select(o => new BoundOrderBy(ResolveColumn(aliasMap, model.MainTable, o.Column, diagnostics)!, o.Ascending)).Where(o => o.Column != null));
        model.ViewName = options.ViewName;

        return model;
    }

    private static BoundProjection BindProjection(Dictionary<string, BoundTableSource> aliasMap, BoundSelectModel model, ParsedProjection projection, List<CodeGenDiagnostic> diagnostics)
    {
        if (projection.Kind == ProjectionKind.Wildcard)
        {
            return new BoundProjection { Kind = ProjectionKind.Wildcard, WildcardAlias = projection.WildcardAlias };
        }

        if (projection.Kind == ProjectionKind.Column)
        {
            var column = ResolveColumn(aliasMap, model.MainTable, projection.Column!, diagnostics);
            if (column == null)
                throw new InvalidOperationException($"无法解析投影列 {projection.Column?.ColumnName}。");

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

        var argument = projection.Function.Argument == null ? null : ResolveColumn(aliasMap, model.MainTable, projection.Function.Argument, diagnostics);
        return new BoundProjection
        {
            Kind = ProjectionKind.Function,
            Function = new BoundFunction(projection.Function.Name, argument, projection.Function.IsDistinct, projection.Function.IsStarArgument),
            Alias = projection.Alias
        };
    }

    private static BoundFilterNode? BindFilter(Dictionary<string, BoundTableSource> aliasMap, BoundTableSource mainTable, FilterNode? node, List<CodeGenDiagnostic> diagnostics)
    {
        if (node == null)
            return null;

        if (node is FilterLogicalNode logical)
        {
            return new BoundFilterLogicalNode
            {
                Operator = logical.Operator,
                Left = BindFilter(aliasMap, mainTable, logical.Left, diagnostics)!,
                Right = BindFilter(aliasMap, mainTable, logical.Right, diagnostics)!
            };
        }

        var cmp = (FilterComparisonNode)node;
        var left = ResolveColumn(aliasMap, mainTable, cmp.Left, diagnostics);
        if (left == null)
            return null;

        return new BoundFilterComparisonNode
        {
            Left = left,
            Operator = cmp.Operator,
            RightColumn = cmp.RightColumn == null ? null : ResolveColumn(aliasMap, mainTable, cmp.RightColumn, diagnostics),
            RightLiteral = cmp.RightLiteral,
            RightValues = cmp.RightValues
        };
    }

    private static BoundColumnReference? ResolveColumn(Dictionary<string, BoundTableSource> aliasMap, BoundTableSource mainTable, ParsedColumnReference reference, List<CodeGenDiagnostic> diagnostics, TableSchema? fallbackJoinTable = null, string? fallbackAlias = null)
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

    private static string GenerateViewCode(BoundSelectModel model, SelectGenerationOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using LiteOrm.Common;");
        sb.AppendLine();
        sb.AppendLine($"namespace {options.Namespace};");
        sb.AppendLine();

        foreach (var join in model.Joins)
        {
            var joinType = join.JoinType switch
            {
                SqlJoinType.Inner => "TableJoinType.Inner",
                SqlJoinType.Right => "TableJoinType.Right",
                _ => "TableJoinType.Left"
            };

            if (string.Equals(join.SourceAlias, model.MainTable.Alias, StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"[TableJoin(typeof({join.Table.ClassName}), nameof({model.MainTable.Table.ClassName}.{join.ForeignKeys[0]}), Alias = \"{join.Alias}\", JoinType = {joinType})]");
            }
            else
            {
                var sourceTable = model.Joins.First(j => string.Equals(j.Alias, join.SourceAlias, StringComparison.OrdinalIgnoreCase)).Table.ClassName;
                sb.AppendLine($"[TableJoin(\"{join.SourceAlias}\", typeof({join.Table.ClassName}), nameof({sourceTable}.{join.ForeignKeys[0]}), Alias = \"{join.Alias}\", JoinType = {joinType})]");
            }
        }

        sb.AppendLine($"public class {model.ViewName} : {model.MainTable.Table.ClassName}");
        sb.AppendLine("{");

        foreach (var property in BuildViewProperties(model))
        {
            foreach (var attribute in property.Attributes)
                sb.AppendLine("    " + attribute);

            sb.AppendLine($"    public {property.TypeName} {property.Name} {{ get; set; }}");
            sb.AppendLine();
        }

        if (BuildViewProperties(model).Count == 0)
            sb.AppendLine("}");
        else
        {
            sb.Length -= Environment.NewLine.Length * 2;
            sb.AppendLine();
            sb.AppendLine("}");
        }

        return sb.ToString().TrimEnd();
    }

    private static List<ViewPropertyModel> BuildViewProperties(BoundSelectModel model)
    {
        var properties = new List<ViewPropertyModel>();
        foreach (var projection in model.Projections)
        {
            if (projection.Kind == ProjectionKind.Wildcard)
                continue;

            if (projection.Kind == ProjectionKind.Column && projection.Column != null)
            {
                bool fromMain = string.Equals(projection.Column.TableAlias, model.MainTable.Alias, StringComparison.OrdinalIgnoreCase);
                string propertyName = projection.Alias ?? projection.Column.Column.PropertyName;
                if (fromMain && string.Equals(propertyName, projection.Column.Column.PropertyName, StringComparison.Ordinal))
                    continue;

                var attributes = new List<string>();
                if (!fromMain)
                    attributes.Add($"[ForeignColumn(\"{projection.Column.TableAlias}\", Property = nameof({projection.Column.Table.ClassName}.{projection.Column.Column.PropertyName}))]");
                else if (!string.Equals(propertyName, projection.Column.Column.PropertyName, StringComparison.Ordinal))
                    attributes.Add($"[Column(\"{projection.Alias}\")]");

                properties.Add(new ViewPropertyModel(propertyName, CodeGenNaming.ToCSharpTypeName(projection.Column.Column.ClrType, projection.Column.Column.IsNullable), attributes));
                continue;
            }

            if (projection.Kind == ProjectionKind.Function && projection.Function != null)
            {
                Type resultType = projection.Function.Name.ToUpperInvariant() switch
                {
                    "COUNT" => typeof(int),
                    "AVG" => typeof(double),
                    "SUM" => projection.Function.Argument?.Column.ClrType ?? typeof(decimal),
                    "MAX" => projection.Function.Argument?.Column.ClrType ?? typeof(string),
                    "MIN" => projection.Function.Argument?.Column.ClrType ?? typeof(string),
                    _ => typeof(string)
                };
                properties.Add(new ViewPropertyModel(projection.Alias!, CodeGenNaming.ToCSharpTypeName(resultType, false), []));
            }
        }

        return properties
            .GroupBy(p => p.Name, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();
    }

    private static string GenerateQueryCode(BoundSelectModel model, SelectGenerationOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using LiteOrm.Common;");
        sb.AppendLine();
        sb.AppendLine("// 查询构建代码");
        sb.AppendLine($"var from = new FromExpr(new TableExpr(typeof({model.MainTable.Table.ClassName})) {{ Alias = \"{model.MainTable.Alias}\" }});");
        foreach (var join in model.Joins)
        {
            string joinType = join.JoinType switch
            {
                SqlJoinType.Inner => "TableJoinType.Inner",
                SqlJoinType.Right => "TableJoinType.Right",
                _ => "TableJoinType.Left"
            };
            string onCode = string.Join(" & ", join.Conditions.Select(c => $"{ToExprProp(c.Source)} == {ToExprProp(c.Target)}"));
            sb.AppendLine($"from.Joins.Add(new TableJoinExpr(new TableExpr(typeof({join.Table.ClassName})) {{ Alias = \"{join.Alias}\" }}, {onCode}) {{ JoinType = {joinType} }});");
        }
        sb.AppendLine();
        sb.AppendLine("SqlSegment source = from;");
        if (model.Where != null)
            sb.AppendLine($"source = new WhereExpr(source, {BuildFilterCode(model.Where)});");
        if (model.GroupBy.Count > 0)
            sb.AppendLine($"source = new GroupByExpr(source, {string.Join(", ", model.GroupBy.Select(ToExprProp))});");
        if (model.OrderBy.Count > 0)
            sb.AppendLine($"source = new OrderByExpr(source, {string.Join(", ", model.OrderBy.Select(o => $"new OrderByItemExpr({ToExprProp(o.Column)}, {(o.Ascending ? "true" : "false")})"))});");
        sb.AppendLine();
        sb.AppendLine("var select = new SelectExpr(source,");
        for (int i = 0; i < model.Projections.Count; i++)
        {
            var projection = model.Projections[i];
            string code = projection.Kind switch
            {
                ProjectionKind.Wildcard when string.IsNullOrWhiteSpace(projection.WildcardAlias) => "    Expr.Prop(\"*\")",
                ProjectionKind.Wildcard => throw new InvalidOperationException("暂不支持生成 alias.* 的 SelectExpr，请显式列出字段。"),
                ProjectionKind.Column => "    " + BuildProjectionCode(projection, options.ViewName),
                ProjectionKind.Function => "    " + BuildFunctionProjectionCode(projection),
                _ => throw new InvalidOperationException("不支持的投影类型。")
            };
            if (i < model.Projections.Count - 1)
                code += ",";
            sb.AppendLine(code);
        }
        sb.AppendLine(");");
        sb.AppendLine();
        sb.AppendLine($"// 结果模型：{options.Namespace}.{options.ViewName}");
        return sb.ToString().TrimEnd();
    }

    private static string BuildProjectionCode(BoundProjection projection, string viewName)
    {
        var code = ToExprProp(projection.Column!);
        if (!string.IsNullOrWhiteSpace(projection.Alias))
            code += $".As(nameof({viewName}.{projection.Alias}))";
        return code;
    }

    private static string BuildFunctionProjectionCode(BoundProjection projection)
    {
        var function = projection.Function!;
        string baseCode = function.Name.ToUpperInvariant() switch
        {
            "COUNT" when function.IsStarArgument => "Expr.Const(1).Count()",
            "COUNT" => $"{ToExprProp(function.Argument!)}.Count({(function.IsDistinct ? "true" : "false")})",
            "SUM" => $"{ToExprProp(function.Argument!)}.Sum()",
            "AVG" => $"{ToExprProp(function.Argument!)}.Avg()",
            "MAX" => $"{ToExprProp(function.Argument!)}.Max()",
            "MIN" => $"{ToExprProp(function.Argument!)}.Min()",
            _ => throw new InvalidOperationException($"暂不支持函数 {function.Name} 的代码生成。")
        };
        return $"{baseCode}.As(\"{projection.Alias}\")";
    }

    private static string BuildFilterCode(BoundFilterNode node)
    {
        if (node is BoundFilterLogicalNode logical)
        {
            var op = logical.Operator == FilterLogicalOperator.And ? "&" : "|";
            return $"({BuildFilterCode(logical.Left)} {op} {BuildFilterCode(logical.Right)})";
        }

        var cmp = (BoundFilterComparisonNode)node;
        string left = ToExprProp(cmp.Left);
        return cmp.Operator switch
        {
            FilterComparisonOperator.Equal => cmp.RightColumn != null ? $"{left} == {ToExprProp(cmp.RightColumn)}" : $"{left} == {ToLiteralCode(cmp.RightLiteral)}",
            FilterComparisonOperator.NotEqual => cmp.RightColumn != null ? $"{left} != {ToExprProp(cmp.RightColumn)}" : $"{left} != {ToLiteralCode(cmp.RightLiteral)}",
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

    private static string ToExprProp(BoundColumnReference column)
    {
        return $"Expr.Prop(\"{column.TableAlias}\", nameof({column.Table.ClassName}.{column.Column.PropertyName}))";
    }

    private static string ToLiteralCode(object? value)
    {
        return value switch
        {
            null => "null",
            string text => "@\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"",
            bool b => b ? "true" : "false",
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture) + "f",
            decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "null"
        };
    }

    private sealed record ViewPropertyModel(string Name, string TypeName, List<string> Attributes);

    private sealed class BoundSelectModel
    {
        public BoundTableSource MainTable { get; set; } = null!;

        public string ViewName { get; set; } = string.Empty;

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

    private sealed record BoundFunction(string Name, BoundColumnReference? Argument, bool IsDistinct, bool IsStarArgument);

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

        public BoundColumnReference? RightColumn { get; set; }

        public object? RightLiteral { get; set; }

        public List<object?>? RightValues { get; set; }
    }

    private sealed record BoundOrderBy(BoundColumnReference Column, bool Ascending);
}
