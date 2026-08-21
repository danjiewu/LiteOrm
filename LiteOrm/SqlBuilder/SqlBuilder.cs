using LiteOrm.Common;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LiteOrm
{
    /// <summary>
    /// SQL 语句生成辅助类 - 提供数据库无关的 SQL 生成功能。
    /// SqlBuilder 是一个抽象 SQL 生成器类，提供了生成各种 SQL 语句的基础功能。
    /// 它实现了 ISqlBuilder 接口，为不同的数据库系统提供了可扩展的基础。
    /// </summary>
    /// <remarks>
    /// 主要功能包括：
    /// 1. SQL 语句生成 - 生成 SELECT、INSERT、UPDATE、DELETE 等 SQL 语句
    /// 2. 名称转换 - 将 .NET 命名转换为 SQL 数据库命名约定
    /// 3. 参数处理 - 生成数据库特定的参数名称和格式
    /// 4. 类型映射 - 将 .NET 类型映射到数据库 DbType
    /// 5. 函数映射 - 映射 .NET 函数到 SQL 函数
    /// 6. 条件生成 - 生成 WHERE 子句的条件语句
    /// 7. 表达式处理 - 将 Lambda 表达式转换为 SQL 条件
    /// 8. 子类可扩展性 - 通过虚方法允许子类自定义SQL生成逻辑
    /// 
    /// 该类有多个具体实现用于不同的数据库：
    /// - SqlServerBuilder - SQL Server 特定的实现
    /// - MySqlBuilder - MySQL 特定的实现
    /// - OracleBuilder - Oracle 特定的实现
    /// - SQLiteBuilder - SQLite 特定的实现
    /// 
    /// 使用示例：
    /// <code>
    /// // 通常由框架自动选择合适的实现
    /// ISqlBuilder builder = SqlBuilderFactory.Instance.GetSqlBuilder(typeof(SqlConnection));
    /// 
    /// // 名称转换
    /// string sqlName = builder.ToSqlName(\"UserName\"); // 可能返回 \"[UserName]\" 或 \"`UserName`\"`
    /// 
    /// // 参数名称生成
    /// string paramName = builder.ToSqlParam(\"id\"); // 可能返回 \"@id\" 或 \":id\"`
    /// 
    /// // 类型映射
    /// DbType dbType = builder.ToDbType(typeof(string)); // 返回 DbType.String
    /// </code>
    /// </remarks>
    public class SqlBuilder : ISqlBuilder
    {
        /// <summary>
        /// 获取默认的 <see cref="SqlBuilder"/> 实例。
        /// </summary>
        public static readonly SqlBuilder Instance = new SqlBuilder();

        /// <summary>
        /// 获取或设置当前 <see cref="SqlBuilder"/> 对应的数据库批量插入提供程序。
        /// 直接赋值即可生效；未设置时为 null，此时批量插入将回退为逐条或普通批量 SQL 插入。
        /// </summary>
        public virtual IBulkProvider? BulkProvider { get; set; }

        /// <summary>
        /// 静态构造函数，在首次访问 SqlBuilder 时自动触发 SQL 函数映射注册与默认值转换器注册。
        /// 这样使用方无需手动调用 <see cref="LiteOrmSqlFunctionInitializer.Initialize"/> 与 <see cref="LiteOrmConverterInitializer.Initialize"/>。
        /// </summary>
        static SqlBuilder()
        {
            LiteOrmSqlFunctionInitializer.Initialize();
            LiteOrmConverterInitializer.Initialize();
        }

        /// <summary>
        /// 获取当前数据库是否支持公共表表达式（CTE / WITH 子句）。
        /// 默认返回 <see langword="true"/>。不支持 CTE 的旧数据库版本可重写为 <see langword="false"/>，
        /// 此时 CTE 将被展开为内联子查询。
        /// </summary>
        public virtual bool SupportCteExpr => true;

        /// <summary>
        /// 获取当前数据库是否要求显式声明递归 CTE（即使用 WITH RECURSIVE 语法）。
        /// 默认返回 <see langword="false"/>，适用于 SQL Server、Oracle 等不需要显式 RECURSIVE 关键字的数据库。
        /// MySQL、PostgreSQL、SQLite 等要求递归 CTE 必须使用 WITH RECURSIVE 语法的数据库应重写为 <see langword="true"/>。
        /// 当本属性为 <see langword="true"/> 时，生成 SQL 会直接输出 WITH RECURSIVE。
        /// </summary>
        public virtual bool ExplicitRecursive => false;

        /// <summary>
        /// 当前数据库是否原生支持数组列（如 PostgreSQL / KingbaseES / GaussDB 的 <c>T[]</c>）。
        /// 为 <see langword="false"/> 时，数组列以 JSON 字符串存储（文本回退）。
        /// </summary>
        public virtual bool SupportsNativeArrays => false;

        private readonly Dictionary<string, string> _functionMappings = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["IndexOf"] = "CHARINDEX",
            ["Substring"] = "SUBSTR",
            ["ToUpper"] = "UPPER",
            ["ToLower"] = "LOWER",
            ["Now"] = "CURRENT_TIMESTAMP",
            ["Today"] = "CURRENT_DATE",
            ["Max"] = "GREATEST",
            ["Min"] = "LEAST"
        };

        /// <summary>
        /// 构建函数调用的 SQL 片段，直接写入 <paramref name="outSql"/>。
        /// 会首先根据构造器类型及继承关系的顺序查找注册的函数处理器<seealso cref="SqlBuilderExtensions"/>，如果找到则使用处理器生成 SQL；否则按照默认规则生成函数调用 SQL。
        /// </summary>
        /// <param name="outSql">接收输出 SQL 片段的字符串构建器。</param>
        /// <param name="expr">函数表达式，包含函数名及参数列表。</param>
        /// <param name="context">SQL 构建上下文。</param>
        /// <param name="outputParams">输出参数集合。</param>
        public virtual void BuildFunctionSql(ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, ICollection<Param> outputParams)
        {
            if (expr is null) throw new ArgumentNullException(nameof(expr));
            string functionName = expr.FunctionName!;
            Type type = this.GetType();
            while (typeof(SqlBuilder).IsAssignableFrom(type))
            {
                if (GetSqlHandlerMap(type).TryGetFunctionSqlHandler(functionName, out var handler) && handler is not null)
                {
                    handler(ref outSql, expr, context, this, outputParams);
                    return;
                }
                type = type.BaseType!;
            }
            if (!expr.IsAggregate && _functionMappings.TryGetValue(functionName, out string? mappedName))
            {
                functionName = mappedName;
            }

            outSql.Append(functionName);
            outSql.Append("(");
            int count = expr.Args.Count;
            for (int i = 0; i < count; i++)
            {
                if (i > 0) outSql.Append(", ");
                expr.Args[i].ToSql(ref outSql, context, this, outputParams);
            }
            outSql.Append(")");
        }


        #region 内部字段与正则表达式
        /// <summary>
        /// 用于在 LIKE 条件中转义特殊字符的正则表达式。
        /// </summary>
        protected Regex _sqlLikeEscapeReg = new Regex(@"([_/%\[\]])");

        #endregion

        /// <summary>
        /// 是否支持标识列插入。
        /// </summary>
        public virtual bool SupportIdentityInsert => true;
        /// <summary>
        /// 插入的标识值是否通过参数返回
        /// </summary>
        public virtual bool ReturnIdentityByParam => false;

        /// <summary>
        /// 将字符串内容转义为适合 LIKE 查询的值。
        /// </summary>
        /// <param name="value">要转义的字符串。</param>
        /// <returns>转义后的字符串。</returns>
        public virtual string ToSqlLikeValue(string value)
        {
            return _sqlLikeEscapeReg.Replace(value, $"{Constants.LikeEscapeChar}$1");
        }

        /// <summary>
        /// 检测是否需要对 LIKE 查询的字符串进行转义（即是否包含特殊字符）。如果返回 <see langword="true"/>，则调用 <see cref="ToSqlLikeValue"/> 进行转义并补 ESCAPE 语句。
        /// </summary>
        /// <param name="value">要检查的字符串。</param>
        /// <returns>如果字符串包含需要转义的特殊字符，则返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        public virtual bool NeedLikeEscape(string value)
        {
            return _sqlLikeEscapeReg.IsMatch(value);
        }


        /// <summary>
        /// 生成带标识列的插入 SQL。
        /// </summary>
        /// <param name="identityColumn">标识列定义。</param>
        /// <param name="tableName">目标表名。</param>
        /// <param name="strColumns">插入的列名集合。</param>
        /// <param name="strValues">值参数集合。</param>
        /// <returns>生成的 SQL 语句。</returns>
        public virtual string BuildIdentityInsertSql(ColumnDefinition identityColumn, string tableName, string strColumns, string strValues)
        {
            return $"INSERT INTO {ToSqlName(tableName)} ({strColumns}) \nVALUES ({strValues}); SELECT @@IDENTITY AS [ID];";
        }


        /// <summary>
        /// 使用传入的 <see cref="ValueStringBuilder"/> 构建字符串连接 SQL 片段。
        /// </summary>
        /// <param name="sb">用于接收 SQL 片段的字符串构建器。</param>
        /// <param name="strs">需要连接的sql字符串</param>
        /// <returns>SQL语句</returns>
        public virtual void BuildConcatSql(ref ValueStringBuilder sb, params string[] strs)
        {
            sb.Append("CONCAT(");
            for (int i = 0; i < strs.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(strs[i]);
            }
            sb.Append(')');
        }

        /// <summary>
        /// 名称转化为数据库合法名称
        /// </summary>
        /// <param name="name">字符串名称</param>
        /// <returns>数据库合法名称</returns>
        public virtual string ToSqlName(string name)
        {
            if (name is null) throw new ArgumentNullException(nameof(name));
            var sb = ValueStringBuilder.Create(name.Length + 4);
            ReadOnlySpan<char> span = name.AsSpan();
            int start = 0;
            bool first = true;
            for (int i = 0; i <= span.Length; i++)
            {
                if (i == span.Length || span[i] == '.')
                {
                    if (!first) sb.Append('.');
                    ToSqlName(ref sb, span.Slice(start, i - start));
                    start = i + 1;
                    first = false;
                }
            }
            string result = sb.ToString();
            sb.Dispose();
            return result;
        }



        /// <summary>
        /// 将集合操作类型（UNION/UNION ALL/INTERSECT/EXCEPT）转换为数据库合法的 SQL 关键字。
        /// 子类可覆盖以提供数据库特定的关键字或语法差异。
        /// </summary>
        /// <param name="selectSetType">集合操作类型。</param>
        public virtual string ToSelectSetTypeSql(SelectSetType selectSetType)
        {
            return selectSetType switch
            {
                SelectSetType.Union => "UNION",
                SelectSetType.UnionAll => "UNION ALL",
                SelectSetType.Intersect => "INTERSECT",
                SelectSetType.Except => "EXCEPT",
                _ => throw new ArgumentOutOfRangeException(nameof(selectSetType), selectSetType, null)
            };
        }

        /// <summary>
        /// 将简单名称转换为数据库合法名称。
        /// </summary>
        /// <param name="sb">用于构建数据库合法名称的字符串构建器。</param>
        /// <param name="simpleName">简单名称（未包含方括号）。</param>
        public virtual void ToSqlName(ref ValueStringBuilder sb, ReadOnlySpan<char> simpleName)
        {
            simpleName = simpleName.Trim();
            if (simpleName.IsEmpty) return;
            if (simpleName[0] != '"') sb.Append('"');
            sb.Append(simpleName);
            if (simpleName[simpleName.Length - 1] != '"') sb.Append('"');
        }

        /// <summary>
        /// 原始名称转化为数据库参数
        /// </summary>
        /// <param name="nativeName">原始名称</param>
        /// <returns>数据库参数</returns>
        public virtual string ToSqlParam(string nativeName)
        {
            return $"@{nativeName}";
        }

        /// <summary>
        /// 原始名称转化为参数名称
        /// </summary>
        /// <param name="nativeName">原始名称</param>
        /// <returns>参数名称</returns>
        public virtual string ToParamName(string nativeName)
        {
            return nativeName;
        }

        /// <summary>
        /// 参数名称转化为原始名称
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <returns>原始名称</returns>
        public virtual string ToNativeName(string paramName)
        {
            return paramName;
        }

        /// <summary>
        /// 将列名、表名等替换为数据库合法名称
        /// </summary>
        /// <param name="sql">sql语句</param>
        /// <returns></returns>
        public virtual string ReplaceSqlName(string sql)
        {
            return ReplaceSqlName(sql, '"', '"');
        }

        /// <summary>
        /// 将列名、表名等替换为数据库合法名称
        /// </summary>
        /// <param name="sql">sql语句</param>
        /// <param name="left">左定界符</param>
        /// <param name="right">右定界符</param>
        /// <param name="handler"></param>
        /// <returns></returns>
        protected string ReplaceSqlName(string sql, char left, char right, Func<char, char>? handler = null)
        {
            if (sql is null) return null!;
            var sb = ValueStringBuilder.Create(sql.Length);
            bool passNext = false;
            Stack<char> stack = new Stack<char>();
            foreach (char ch in sql)
            {
                if (passNext)
                {
                    sb.Append(ch);
                    passNext = false;
                }
                else
                {
                    switch (ch)
                    {
                        case '[': sb.Append(stack.Count == 0 ? left : ch); break;
                        case ']': sb.Append(stack.Count == 0 ? right : ch); break;
                        case '"':
                            if (stack.Count > 0 && stack.Peek() == '"') stack.Pop();
                            else stack.Push('"');
                            sb.Append(ch); break;
                        case '\'':
                            if (stack.Count > 0 && stack.Peek() == '\'') stack.Pop();
                            else stack.Push('\'');
                            sb.Append(ch); break;
                        case '\\': sb.Append(ch); passNext = true; break;
                        default:
                            if (handler is not null)
                            {
                                sb.Append(handler(ch));
                            }
                            else
                                sb.Append(ch);
                            break;
                    }
                }
            }
            string result = sb.ToString();
            sb.Dispose();
            return result;
        }

        /// <summary>
        /// 获取按 (值类型, 数据库取值类型) 沿 SqlBuilder 继承链查找注册的转换器（读取与写入共用注册表，方言注册优先于基类）。
        /// </summary>
        /// <param name="valueType">实体属性 / .NET 值类型。</param>
        /// <param name="dbValueType">数据库取值类型。</param>
        /// <returns>注册的转换器；未注册时返回 null。</returns>
        public IDbValueConverter? GetDbValueConverter(Type valueType, DbValueType dbValueType)
        {
            Type builderType = this.GetType();
            while (typeof(SqlBuilder).IsAssignableFrom(builderType))
            {
                if (GetDbValueConverterMap(builderType).TryGetConverter((valueType, dbValueType), out IDbValueConverter? converter))
                {
                    return converter;
                }
                builderType = builderType.BaseType!;
            }
            return null;
        }

        /// <summary>
        /// 将对象序列化为 JSON 字符串（Json/Jsonb 列、数组列文本回退与字符串列的复杂值均经此序列化）。
        /// 供写入转换兜底逻辑（<see cref="SqlBuilderExtensions.ConvertToDbValue(IDbConverter, object?, DbValueType?)"/>）调用，
        /// 子类可覆盖以提供方言特定的 JSON 序列化行为。
        /// </summary>
        /// <param name="value">要序列化的值。</param>
        /// <returns>JSON 字符串。</returns>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050",
            Justification = "JSON serialization is only triggered when the value is a complex object/collection; under AOT, users must provide a System.Text.Json source-gen context for complex property types, otherwise a NotSupportedException is thrown at runtime.")]
#endif
        public virtual string ToJsonString(object value)
        {
            return JsonSerializer.Serialize(value, value.GetType());
        }

        /// <summary>
        /// 获取对应的数据库类型
        /// </summary>
        /// <param name="type">要转换的 .NET 类型，支持 Nullable 类型</param>
        /// <returns>对应的数据库取值类型</returns>
        public DbValueType GetDbValueType(Type type)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));
            Type underlyingType = type.GetUnderlyingType();
            return GetDbValueTypeInternal(underlyingType);
        }
        /// <summary>
        /// 将对象值转换为数据库可接受的值，一般用于非数据列的数值转换。
        /// </summary>
        /// <param name="value">要转换的对象值</param>
        /// <param name="dbValueType">数据库取值类型</param>
        /// <returns>数据库可接受的值</returns>
        public virtual object ToDbValue(object? value, DbValueType? dbValueType = null)
        {
            if (value is null) return DBNull.Value;
            var dbType = dbValueType ?? GetDbValueType(value.GetType());
            IDbValueConverter? converterInstance = GetDbValueConverter(value.GetType(), dbType);
            return converterInstance?.ConvertToDbValue(value) ?? value;
        }

        /// <summary>
        /// 获取对应的数据库取值类型的内部实现方法，子类可覆盖以提供数据库特定的类型映射逻辑。
        /// </summary>
        /// <param name="type">要转换的 .NET 类型</param>
        /// <returns>对应的数据库取值类型</returns>
        protected virtual DbValueType GetDbValueTypeInternal(Type type)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));
            return DbValueTypeMap.GetDbValueType(type);
        }

        /// <summary>
        /// 将 <see cref="DbValueType"/> 转换为 <see cref="DbType"/>（数据库操作边界转换）。
        /// 默认实现剥离 <see cref="DbValueType.Array"/> 掩码后按标量映射；
        /// 子类可覆盖以提供方言特定的映射（如 Oracle 将 Boolean 映射为 Byte）。
        /// </summary>
        public virtual DbType ToDbType(DbValueType dbValueType)
        {
            return DbValueTypeMap.ToDbType(dbValueType);
        }

        /// <summary>
        /// 获取指定数据库取值类型的默认列长度。
        /// </summary>
        public virtual int GetDefaultLength(DbValueType dbValueType)
        {
            return dbValueType switch
            {
                DbValueType.Byte or DbValueType.SByte or DbValueType.Boolean => 1,
                DbValueType.Int16 or DbValueType.UInt16 => 2,
                DbValueType.Single or DbValueType.UInt32 or DbValueType.Int32 => 4,
                DbValueType.Int64 or DbValueType.UInt64 or DbValueType.Double => 8,
                DbValueType.String or DbValueType.AnsiString or DbValueType.AnsiStringFixedLength or DbValueType.StringFixedLength => 255,
                DbValueType.Xml => 1 << 16,
                DbValueType.Binary => Int32.MaxValue,
                _ => 0
            };
        }

        /// <summary>
        /// 将 <see cref="DbValueType"/> 转换为通用的 SQL 类型名称，用于 CAST 表达式等。
        /// </summary>
        public virtual string GetSqlTypeName(DbValueType dbValueType)
        {
            return dbValueType switch
            {
                DbValueType.String or DbValueType.AnsiString or DbValueType.AnsiStringFixedLength or DbValueType.StringFixedLength => "VARCHAR",
                DbValueType.Int16 => "SMALLINT",
                DbValueType.Int32 => "INT",
                DbValueType.Int64 => "BIGINT",
                DbValueType.Boolean => "BIT",
                DbValueType.UInt16 => "SMALLINT",
                DbValueType.UInt32 => "INT",
                DbValueType.UInt64 => "BIGINT",
                DbValueType.DateTime => "DATETIME",
                DbValueType.DateTime2 => "TIMESTAMP",
                DbValueType.DateTimeOffset => "DATETIMEOFFSET",
                DbValueType.Date => "DATE",
                DbValueType.Time => "TIME",
                DbValueType.Decimal => "DECIMAL",
                DbValueType.Double => "DOUBLE",
                DbValueType.Single => "FLOAT",
                DbValueType.Byte or DbValueType.SByte => "TINYINT",
                DbValueType.Guid => "GUID",
                DbValueType.Binary => "BLOB",
                _ => "VARCHAR"
            };
        }

        /// <summary>
        /// 是否支持带自增列的批量插入并返回首个 ID。
        /// </summary>
        public virtual bool SupportBatchInsertWithIdentity => false;

        /// <summary>
        /// 生成批量插入的 SQL 语句。
        /// </summary>
        /// <param name="tableName">目标表名。</param>
        /// <param name="columns">插入的列名集合（逗号分隔的 SQL 名称）。</param>
        /// <param name="valuesList">每个实体的占位符集合（例如 "(@0,@1,@2)"）。</param>
        /// <returns>返回目标数据库可执行的批量插入 SQL 字符串。</returns>
        public virtual string BuildBatchInsertSql(string tableName, string columns, List<string> valuesList)
        {
            var sb = ValueStringBuilder.Create(valuesList.Count * 20);
            sb.Append("INSERT INTO ");
            sb.Append(ToSqlName(tableName));
            sb.Append(" (");
            sb.Append(columns);
            sb.Append(") \nVALUES ");
            for (int i = 0; i < valuesList.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(valuesList[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 生成带标识列的批量插入 SQL，返回首个插入的 ID。
        /// </summary>
        /// <param name="identityColumn">标识列定义。</param>
        /// <param name="tableName">目标表名。</param>
        /// <param name="columns">插入的列名集合。</param>
        /// <param name="valuesList">占位符集合。</param>
        /// <returns>生成的 SQL 语句。</returns>
        public virtual string BuildBatchIdentityInsertSql(ColumnDefinition identityColumn, string tableName, string columns, List<string> valuesList)
        {
            return BuildBatchInsertSql(tableName, columns, valuesList);
        }


        /// <summary>
        /// 生成创建表的 SQL 语句。
        /// </summary>
        /// <param name="tableName">表名。</param>
        /// <param name="columns">列定义集合。</param>
        public virtual string BuildCreateTableSql(string tableName, IEnumerable<ColumnDefinition> columns)
        {
            // 计算列（非实际列）不生成物理列
            var columnList = columns.Where(c => !c.IsComputed).ToList();
            var keyColumns = columnList.Where(c => c.IsPrimaryKey).ToList();
            bool hasCompositeKeys = keyColumns.Count > 1;
            var sb = ValueStringBuilder.Create(512);
            sb.Append("CREATE TABLE ");
            sb.Append(ToSqlName(tableName));
            sb.Append(" (");
            bool first = true;
            foreach (var column in columnList)
            {
                if (!first) sb.Append(",");
                sb.Append("\n  ");
                sb.Append(BuildCreateColumnDefinitionSql(column, column.IsPrimaryKey && !hasCompositeKeys, hasCompositeKeys && column.IsPrimaryKey));
                first = false;
            }
            if (hasCompositeKeys)
            {
                sb.Append(",\n  PRIMARY KEY (");
                for (int i = 0; i < keyColumns.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(ToSqlName(keyColumns[i].Name!));
                }
                sb.Append(")");
            }
            sb.Append("\n)");
            string result = sb.ToString();
            sb.Dispose();
            return result;
        }


        /// <summary>
        /// 生成添加多个列的 SQL 语句。非空列自动附加类型相关的 DEFAULT 值，以兼容表中已有数据的场景。
        /// </summary>
        /// <param name="tableName">表名。</param>
        /// <param name="columns">列定义集合。</param>
        public virtual string BuildAddColumnsSql(string tableName, IEnumerable<ColumnDefinition> columns)
        {
            var colSqls = columns.Where(c => !c.IsComputed).Select(BuildAddColumnDefinitionSql).ToList();
            if (colSqls.Count == 0) return string.Empty;
            return $"ALTER TABLE {ToSqlName(tableName)} ADD {string.Join(", ", colSqls)}";
        }

        /// <summary>
        /// 构建 CREATE TABLE 中单个列定义的 SQL 片段。
        /// </summary>
        protected virtual string BuildCreateColumnDefinitionSql(ColumnDefinition column, bool inlinePrimaryKey, bool forceNotNull)
        {
            var sb = ValueStringBuilder.Create(64);
            sb.Append(ToSqlName(column.Name!));
            sb.Append(" ");
            sb.Append(GetSqlTypeDefinition(column));

            if (column.IsIdentity)
            {
                string autoIncrementSql = GetAutoIncrementSql(column);
                if (!string.IsNullOrEmpty(autoIncrementSql))
                {
                    sb.Append(" ");
                    sb.Append(autoIncrementSql);
                }
            }

            if (inlinePrimaryKey) sb.Append(" PRIMARY KEY");
            if (!string.IsNullOrEmpty(column.DefaultValue))
            {
                sb.Append(" DEFAULT ");
                sb.Append(column.DefaultValue);
            }
            if (forceNotNull || !column.AllowNull) sb.Append(" NOT NULL");

            string result = sb.ToString();
            sb.Dispose();
            return result;
        }

        /// <summary>
        /// 构建 ALTER TABLE ADD COLUMN 中单个列定义的 SQL 片段。
        /// </summary>
        protected virtual string BuildAddColumnDefinitionSql(ColumnDefinition column)
        {
            var sb = ValueStringBuilder.Create(64);
            sb.Append(ToSqlName(column.Name!));
            sb.Append(" ");
            sb.Append(GetSqlTypeDefinition(column));

            if (column.IsIdentity)
            {
                string autoIncrementSql = GetAutoIncrementSql(column);
                if (!string.IsNullOrEmpty(autoIncrementSql))
                {
                    sb.Append(" ");
                    sb.Append(autoIncrementSql);
                }
            }

            if (column.IsPrimaryKey || !column.AllowNull)
            {
                if (!column.IsIdentity)
                {
                    sb.Append(" DEFAULT ");
                    sb.Append(GetDefaultValueSql(column));
                    sb.Append(" NOT NULL");
                }
            }
            else if (!string.IsNullOrEmpty(column.DefaultValue))
            {
                sb.Append(" DEFAULT ");
                sb.Append(column.DefaultValue);
            }
            else
            {
                sb.Append(" NULL");
            }

            string result = sb.ToString();
            sb.Dispose();
            return result;
        }

        /// <summary>
        /// 返回列的可空约束 SQL 片段。
        /// 可空列返回 <c> NULL</c>；自增列返回空字符串；
        /// 其余非空列返回 <c> DEFAULT &lt;value&gt; NOT NULL</c>。
        /// </summary>
        /// <param name="column">列定义。</param>
        /// <returns>约束 SQL 片段。</returns>
        protected virtual string GetNotNullConstraintSql(ColumnDefinition column)
        {
            if (column.AllowNull) return " NULL";
            if (column.IsIdentity) return "";
            return $" DEFAULT {GetDefaultValueSql(column)} NOT NULL";
        }

        /// <summary>
        /// 返回指定列类型的 DEFAULT 值 SQL 字面量，用于 ADD COLUMN … NOT NULL 时为已有行填充默认值。
        /// </summary>
        /// <param name="column">列定义。</param>
        /// <returns>默认值 SQL 字面量，例如 <c>0</c>、<c>''</c>、<c>'1900-01-01'</c>。</returns>
        public virtual string GetDefaultValueSql(ColumnDefinition column)
        {
            if (!string.IsNullOrEmpty(column.DefaultValue))
            {
                return column.DefaultValue!;
            }

            DbValueType dbValueType = column.GetDbValueType(this);
            if (dbValueType.HasArray()) return "'{}'";
            var dbType = ToDbType(dbValueType);
            switch (dbType)
            {
                case DbType.Boolean:
                case DbType.Byte:
                case DbType.SByte:
                case DbType.Int16:
                case DbType.Int32:
                case DbType.Int64:
                case DbType.UInt16:
                case DbType.UInt32:
                case DbType.UInt64:
                case DbType.Decimal:
                case DbType.Double:
                case DbType.Single:
                    return "0";
                case DbType.DateTime:
                case DbType.DateTime2:
                case DbType.Date:
                    return "'1900-01-01'";
                case DbType.Time:
                    return "'00:00:00'";
                case DbType.Guid:
                    return "'00000000-0000-0000-0000-000000000000'";
                case DbType.String:
                case DbType.AnsiString:
                case DbType.StringFixedLength:
                case DbType.AnsiStringFixedLength:
                default:
                    return "''";
            }
        }

        /// <summary>
        /// 生成创建索引的 SQL 语句。
        /// </summary>
        /// <param name="tableName">表名。</param>
        /// <param name="column">列定义。</param>
        /// <returns>返回创建索引的 SQL 字符串。</returns>
        public virtual string BuildCreateIndexSql(string tableName, ColumnDefinition column)
        {
            string indexName = $"IX_{tableName}_{column.Name}";
            string unique = column.IsUnique ? "UNIQUE " : "";
            return $"CREATE {unique}INDEX {ToSqlName(indexName)} ON {ToSqlName(tableName)} ({ToSqlName(column.Name!)})";
        }

        /// <summary>
        /// 生成批量检查 ID 是否存在的 SQL 语句。
        /// </summary>
        /// <param name="tableName">目标表名。</param>
        /// <param name="keyColumns">主键列定义数组。</param>
        /// <param name="batchSize">批量大小。</param>
        /// <returns>生成的 SQL 语句。</returns>
        public virtual string BuildBatchIDExistsSql(string tableName, IList<ColumnDefinition> keyColumns, int batchSize)
        {
            var sb = ValueStringBuilder.Create(1024);
            for (int i = 0; i < keyColumns.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(ToSqlName(keyColumns[i].Name!));
            }
            string sqlKeys = sb.ToString();
            sb.Clear();

            sb.Append("SELECT ");
            sb.Append(sqlKeys);
            sb.Append(" FROM ");
            sb.Append(ToSqlName(tableName));
            sb.Append(" WHERE ");
            sb.Append(sqlKeys);
            sb.Append(" IN (");

            for (int b = 0; b < batchSize; b++)
            {
                if (b > 0) sb.Append(",");
                if (keyColumns.Count > 1) sb.Append("(");
                for (int i = 0; i < keyColumns.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    string keyParam = (b * keyColumns.Count + i).ToString();
                    sb.Append(ToSqlParam(keyParam));
                }
                if (keyColumns.Count > 1) sb.Append(")");
            }
            sb.Append(")");
            string result = sb.ToString();
            sb.Dispose();
            return result;
        }

        /// <summary>
        /// 生成批量删除的 SQL 语句。
        /// </summary>
        /// <param name="tableName">目标表名。</param>
        /// <param name="keyColumns">主键列集合。</param>
        /// <param name="batchSize">批次大小。</param>
        /// <returns>返回目标数据库可执行的批量删除 SQL 字符串。</returns>
        public virtual string BuildBatchDeleteSql(string tableName, ColumnDefinition[] keyColumns, int batchSize)
        {
            var sb = ValueStringBuilder.Create(1024);
            sb.Append("DELETE FROM ");
            sb.Append(ToSqlName(tableName));
            sb.Append(" WHERE ");

            if (keyColumns.Length == 1)
            {
                var key = keyColumns[0];
                sb.Append(ToSqlName(key.Name!));
                sb.Append(" IN (");
                for (int b = 0; b < batchSize; b++)
                {
                    if (b > 0) sb.Append(", ");
                    sb.Append(ToSqlParam(b.ToString()));
                }
                sb.Append(")");
            }
            else
            {
                for (int b = 0; b < batchSize; b++)
                {
                    if (b > 0) sb.Append(" OR ");
                    sb.Append("(");
                    for (int k = 0; k < keyColumns.Length; k++)
                    {
                        if (k > 0) sb.Append(" AND ");
                        var key = keyColumns[k];
                        string keyParam = (b * keyColumns.Length + k).ToString();
                        sb.Append(ToSqlName(key.Name!));
                        sb.Append(" = ");
                        sb.Append(ToSqlParam(keyParam));
                    }
                    sb.Append(")");
                }
            }

            string result = sb.ToString();
            sb.Dispose();
            return result;
        }

        /// <summary>
        /// 生成批量更新的 SQL 语句。采用单条 UPDATE 语句拼接的方式以保证兼容性。
        /// </summary>
        /// <param name="tableName">目标表名。</param>
        /// <param name="updatableColumns">可更新列集合。</param>
        /// <param name="keyColumns">主键列集合。</param>
        /// <param name="batchSize">批次大小。</param>
        /// <returns>返回目标数据库可执行的批量更新 SQL 字符串。</returns>
        public virtual string BuildBatchUpdateSql(string tableName, ColumnDefinition[] updatableColumns, ColumnDefinition[] keyColumns, int batchSize)
        {
            int paramsPerRecord = updatableColumns.Length + keyColumns.Length;
            var sb = ValueStringBuilder.Create(128 + paramsPerRecord * batchSize * 8);
            string sqlTableName = ToSqlName(tableName);

            // 为每条记录生成一个 UPDATE 语句
            for (int b = 0; b < batchSize; b++)
            {
                if (b > 0) sb.Append("\n");

                // 构建 UPDATE 语句
                sb.Append("UPDATE ");
                sb.Append(sqlTableName);
                sb.Append("\nSET ");

                // 构建 SET 子句
                for (int i = 0; i < updatableColumns.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(ToSqlName(updatableColumns[i].Name!));
                    sb.Append(" = ");
                    sb.Append(ToSqlParam((b * paramsPerRecord + i).ToString()));
                }

                // 构建 WHERE 子句
                sb.Append("\nWHERE ");
                for (int k = 0; k < keyColumns.Length; k++)
                {
                    if (k > 0) sb.Append(" AND ");
                    sb.Append(ToSqlName(keyColumns[k].Name!));
                    sb.Append(" = ");
                    sb.Append(ToSqlParam((b * paramsPerRecord + updatableColumns.Length + k).ToString()));
                }
            }

            string result = sb.ToString();
            sb.Dispose();
            return result;
        }

        /// <summary>
        /// 获取自增标识的 SQL 片段。
        /// </summary>
        /// <param name="column">当前列定义，可用于读取起始值与增量。</param>
        protected virtual string GetAutoIncrementSql(ColumnDefinition column) => $"IDENTITY({column.IdentityStart},{column.IdentityIncreasement})";

        /// <summary>
        /// 根据列定义获取数据库列类型。
        /// </summary>
        protected virtual string GetSqlTypeDefinition(ColumnDefinition column)
        {
            DbValueType dbValueType = column.GetDbValueType(this);
            // 非 PgSQL 方言无原生数组，回退为文本存储
            if (dbValueType.HasArray()) return "TEXT";
            if (dbValueType == DbValueType.Json || dbValueType == DbValueType.Jsonb) return "TEXT";

            var dbType = ToDbType(dbValueType);
            switch (dbType)
            {
                case DbType.String:
                case DbType.AnsiString:
                    if (column.Length <= 0) return "VARCHAR(255)";
                    else if (column.Length <= 4000) return $"VARCHAR({column.Length})";
                    else return "TEXT";
                case DbType.Int16: return "SMALLINT";
                case DbType.Int32: return "INT";
                case DbType.Int64: return "BIGINT";
                case DbType.Boolean: return "BIT";
                case DbType.UInt16: return "SMALLINT UNSIGNED";
                case DbType.UInt32: return "INT UNSIGNED";
                case DbType.UInt64: return "BIGINT UNSIGNED";
                case DbType.DateTime: return "DATETIME";
                case DbType.Decimal: return "DECIMAL(18,2)";
                case DbType.Double: return "DOUBLE";
                case DbType.Single: return "FLOAT";
                case DbType.SByte:
                case DbType.Byte: return "TINYINT";
                case DbType.Guid: return "GUID";
                case DbType.Binary: return "BLOB";
                case DbType.Date: return "DATE";
                case DbType.Time: return "TIME";
                case DbType.DateTime2: return "TIMESTAMP";
                case DbType.DateTimeOffset: return "DATETIMEOFFSET";

                default: return "VARCHAR(255)";
            }
        }

        private static readonly ConcurrentDictionary<Type, SqlHandlerMap> _sqlHandlerMaps = new ConcurrentDictionary<Type, SqlHandlerMap>();
        private static readonly ConcurrentDictionary<Type, DbValueConverterMap> _dbValueConverterMaps = new ConcurrentDictionary<Type, DbValueConverterMap>();

        internal static SqlHandlerMap GetSqlHandlerMap<T>() where T : SqlBuilder
        {
            return _sqlHandlerMaps.GetOrAdd(typeof(T), t => new SqlHandlerMap());
        }

        internal static SqlHandlerMap GetSqlHandlerMap(Type type)
        {
            return _sqlHandlerMaps.GetOrAdd(type, t => new SqlHandlerMap());
        }

        internal static DbValueConverterMap GetDbValueConverterMap<T>() where T : SqlBuilder
        {
            return _dbValueConverterMaps.GetOrAdd(typeof(T), t => new DbValueConverterMap());
        }

        internal static DbValueConverterMap GetDbValueConverterMap(Type type)
        {
            return _dbValueConverterMaps.GetOrAdd(type, t => new DbValueConverterMap());
        }

        /// <summary>
        /// 将结构化的 SQL 片段组装成最终的 SELECT 语句。
        /// 基类实现使用标准的 SQL OFFSET/FETCH 语法进行分页。
        /// </summary>
        /// <param name="subSelect">包含 SELECT 各个子句片段的结构体。</param>
        /// <param name="result">输出 SQL 语句的缓冲区。</param>
        /// <param name="indent">当前缩进字符串的长度，用于格式化输出。</param>
        public virtual void BuildSelectSql(ref SqlValueStringBuilder subSelect, ref ValueStringBuilder result, int indent)
        {
            if (subSelect.Select.Length == 0) result.Append("SELECT *");
            else
            {
                result.Append("SELECT ");
                result.Append(subSelect.Select.AsSpan());
            }

            if (subSelect.From.Length > 0)
            {
                result.NewLine(indent);
                result.Append("FROM ");
                result.Append(subSelect.From.AsSpan());
            }

            if (subSelect.Where.Length > 0)
            {
                result.NewLine(indent);
                result.Append("WHERE ");
                result.Append(subSelect.Where.AsSpan());
            }

            if (subSelect.GroupBy.Length > 0)
            {
                result.NewLine(indent);
                result.Append("GROUP BY ");
                result.Append(subSelect.GroupBy.AsSpan());
            }

            if (subSelect.Having.Length > 0)
            {
                result.NewLine(indent);
                result.Append("HAVING ");
                result.Append(subSelect.Having.AsSpan());
            }

            if (subSelect.OrderBy.Length > 0)
            {
                result.NewLine(indent);
                result.Append("ORDER BY ");
                result.Append(subSelect.OrderBy.AsSpan());
            }

            if (subSelect.Take > 0)
            {
                if (subSelect.OrderBy.Length == 0)
                {
                    result.NewLine(indent);
                    result.Append("ORDER BY 1");
                }
                result.NewLine(indent);
                result.Append($"OFFSET {subSelect.Skip} ROWS FETCH NEXT {subSelect.Take} ROWS ONLY");
            }
        }

    }
}

