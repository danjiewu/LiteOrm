﻿﻿﻿using LiteOrm.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
        private static readonly ConcurrentDictionary<Type, Type> _enumUnderlyingTypeCache = new ConcurrentDictionary<Type, Type>();

        /// <summary>
        /// 获取默认的 <see cref="SqlBuilder"/> 实例。
        /// </summary>
        public static readonly SqlBuilder Instance = new SqlBuilder();

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
        /// 构建函数调用的 SQL 片段。
        /// </summary>
        /// <param name="functionName">函数名。</param>
        /// <param name="args">参数列表。</param>
        /// <returns>构建后的 SQL 片段。</returns>
        public string BuildFunctionSql(string functionName, IList<KeyValuePair<string, Expr>> args)
        {
            if (string.IsNullOrWhiteSpace(functionName))
                throw new ArgumentNullException(nameof(functionName));
            Type type = this.GetType();
            while (typeof(SqlBuilder).IsAssignableFrom(type))
            {
                if (GetSqlHandlerMap(type).TryGetFunctionSqlHandler(functionName, out var handler))
                    return handler(functionName, args);
                type = type.BaseType;
            }
            if (_functionMappings.TryGetValue(functionName, out string mappedName))
            {
                functionName = mappedName;
            }

            Span<char> initialBuffer = stackalloc char[128];
            var sb = new ValueStringBuilder(initialBuffer);
            sb.Append(functionName);
            sb.Append("(");
            int count = args.Count;
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(args[i].Key);
            }
            sb.Append(")");
            string result = sb.ToString();
            sb.Dispose();
            return result;
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
        /// 将字符串内容转义为适合 LIKE 查询的值。
        /// </summary>
        /// <param name="value">要转义的字符串。</param>
        /// <returns>转义后的字符串。</returns>
        public virtual string ToSqlLikeValue(string value)
        {
            return _sqlLikeEscapeReg.Replace(value, $"{Const.LikeEscapeChar}$1");
        }

        /// <summary>
        /// 生成带标识列的插入 SQL。
        /// </summary>
        /// <param name="command">数据库命令对象。</param>
        /// <param name="identityColumn">标识列定义。</param>
        /// <param name="tableName">目标表名。</param>
        /// <param name="strColumns">插入的列名集合。</param>
        /// <param name="strValues">值参数集合。</param>
        /// <returns>生成的 SQL 语句。</returns>
        public virtual string BuildIdentityInsertSql(IDbCommand command, ColumnDefinition identityColumn, string tableName, string strColumns, string strValues)
        {
            return $"INSERT INTO {ToSqlName(tableName)} ({strColumns}) \nVALUES ({strValues}); SELECT @@IDENTITY AS [ID];";
        }


        /// <summary>
        /// 连接各字符串的 SQL 语句
        /// </summary>
        /// <param name="strs">需要连接的sql字符串</param>
        /// <returns>SQL语句</returns>
        public virtual string BuildConcatSql(params string[] strs)
        {
            return $"CONCAT({String.Join(",", strs)})";
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
        /// 将简单名称转换为数据库合法名称。
        /// </summary>
        /// <param name="sb">用于构建数据库合法名称的字符串构建器。</param>
        /// <param name="simpleName">简单名称（未包含方括号）。</param>
        public virtual void ToSqlName(ref ValueStringBuilder sb, ReadOnlySpan<char> simpleName)
        {
            simpleName = simpleName.Trim();
            if (simpleName.IsEmpty) return;
            if (simpleName[0] != '[') sb.Append('[');
            sb.Append(simpleName);
            if (simpleName[simpleName.Length - 1] != ']') sb.Append(']');
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
            return sql;
        }

        /// <summary>
        /// 将列名、表名等替换为数据库合法名称
        /// </summary>
        /// <param name="sql">sql语句</param>
        /// <param name="left">左定界符</param>
        /// <param name="right">右定界符</param>
        /// <param name="handler"></param>
        /// <returns></returns>
        protected string ReplaceSqlName(string sql, char left, char right, Func<char, char> handler = null)
        {
            if (sql is null) return null;
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
        /// 将数据库取得的值转化为对象属性类型所对应的值。
        /// </summary>
        /// <param name="dbValue">数据库取得的值。</param>
        /// <param name="objectType">目标属性类型。</param>
        /// <returns>转换后的对象值。</returns>
        public object ConvertFromDbValue(object dbValue, Type objectType = null)
        {
            if (objectType == null)
            {
                return (dbValue == null || dbValue == DBNull.Value) ? null : dbValue;
            }

            bool nullable = !objectType.IsValueType || Nullable.GetUnderlyingType(objectType) is not null;

            if (dbValue is null || dbValue == DBNull.Value)
                return nullable ? null : Activator.CreateInstance(objectType);

            Type underlyingType = objectType.GetUnderlyingType();

            if (underlyingType.IsInstanceOfType(dbValue))
                return dbValue;

            if (dbValue is string s && s == string.Empty)
                return nullable ? null : Activator.CreateInstance(objectType);

            if (underlyingType.IsEnum)
            {
                if (dbValue is string strEnum) return Enum.Parse(underlyingType, strEnum, true);
                return Enum.ToObject(underlyingType, Convert.ChangeType(dbValue, Enum.GetUnderlyingType(underlyingType)));
            }

            if (underlyingType == typeof(bool))
            {
                if (dbValue is string strBool)
                {
                    if (bool.TryParse(strBool, out bool result)) return result;
                    if (strBool == "1" || strBool.Equals("Y", StringComparison.OrdinalIgnoreCase) || strBool.Equals("T", StringComparison.OrdinalIgnoreCase)) return true;
                    if (strBool == "0" || strBool.Equals("N", StringComparison.OrdinalIgnoreCase) || strBool.Equals("F", StringComparison.OrdinalIgnoreCase)) return false;
                }
                return Convert.ToInt64(dbValue) != 0;
            }

            if (underlyingType == typeof(Guid))
            {
                if (dbValue is string strGuid && Guid.TryParse(strGuid, out Guid guid)) return guid;
                if (dbValue is byte[] bytesGuid && bytesGuid.Length == 16) return new Guid(bytesGuid);
            }

            if (underlyingType == typeof(TimeSpan))
            {
                if (dbValue is long ticks) return TimeSpan.FromTicks(ticks);
                if (dbValue is string strTs && TimeSpan.TryParse(strTs, out TimeSpan ts)) return ts;
            }

            if (underlyingType == typeof(DateTimeOffset))
            {
                if (dbValue is DateTime dt) return new DateTimeOffset(dt);
                if (dbValue is string strDto && DateTimeOffset.TryParse(strDto, out DateTimeOffset dto)) return dto;
            }

            return Convert.ChangeType(dbValue, underlyingType);

        }

        /// <summary>
        /// 将对象的属性值转化为数据库中的值，根据列的 DbType 进行转换
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="dbType">数据字段类型</param>
        /// <returns>数据库中的值</returns>
        public virtual object ConvertToDbValue(object value, DbType dbType = DbType.Object)
        {
            if (value is null) return DBNull.Value;
            if (dbType == DbType.Object)
                dbType = GetDbType(value.GetType());

            Type type = value.GetType();

            // 处理枚举：优先根据基础类型转换，除非 DbType 要求字符串
            if (type.IsEnum)
            {
                if (dbType == DbType.String || dbType == DbType.AnsiString ||
                    dbType == DbType.StringFixedLength || dbType == DbType.AnsiStringFixedLength)
                {
                    return value.ToString();
                }
                Type underlyingType = _enumUnderlyingTypeCache.GetOrAdd(type, t => Enum.GetUnderlyingType(t));
                return Convert.ChangeType(value, underlyingType);
            }

            // 根据 dbType 进行特定转换
            switch (dbType)
            {
                case DbType.Boolean:
                    return Convert.ToBoolean(value);

                case DbType.Int16:
                case DbType.Int32:
                case DbType.Int64:
                case DbType.Byte:
                case DbType.SByte:
                case DbType.UInt16:
                case DbType.UInt32:
                case DbType.UInt64:
                    if (value is bool b) return b ? 1 : 0;
                    return Convert.ChangeType(value, dbType.ToType());

                case DbType.Guid:
                    if (value is Guid guid) return guid;
                    if (value is string s && Guid.TryParse(s, out Guid g)) return g;
                    if (value is byte[] bytes && bytes.Length == 16) return new Guid(bytes);
                    break;

                case DbType.Binary:
                    if (value is Guid g2) return g2.ToByteArray();
                    break;

                case DbType.Date:
                case DbType.DateTime:
                case DbType.DateTime2:
                case DbType.Time:
                    if (value is DateTimeOffset dto) return dto.DateTime;
                    if (value is DateTime date) return date;
                    break;

                case DbType.DateTimeOffset:
                    if (value is DateTime dt) return new DateTimeOffset(dt);
                    break;

                case DbType.String:
                case DbType.AnsiString:
                case DbType.StringFixedLength:
                case DbType.AnsiStringFixedLength:
                    if (value is Guid g3) return g3.ToString();
                    if (value is TimeSpan ts) return ts.ToString();
                    return value.ToString();
            }

            // 兜底通用逻辑
            if (value is bool bv) return bv ? 1 : 0;
            if (value is DateTimeOffset dtov) return dtov.DateTime;
            if (value is TimeSpan tsv) return tsv.Ticks;

            return value;
        }

        /// <summary>
        /// 获取对应的数据库类型
        /// </summary>
        /// <param name="type">要转换的 .NET 类型</param>
        /// <returns>对应的数据库类型</returns>
        public virtual DbType GetDbType(Type type)
        {
            return DbTypeMap.GetDbType(type);
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
        /// <param name="command">数据库命令对象。</param>
        /// <param name="identityColumn">标识列定义。</param>
        /// <param name="tableName">目标表名。</param>
        /// <param name="columns">插入的列名集合。</param>
        /// <param name="valuesList">占位符集合。</param>
        /// <returns>生成的 SQL 语句。</returns>
        public virtual string BuildBatchIdentityInsertSql(IDbCommand command, ColumnDefinition identityColumn, string tableName, string columns, List<string> valuesList)
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
            var sb = ValueStringBuilder.Create(512);
            sb.Append("CREATE TABLE ");
            sb.Append(ToSqlName(tableName));
            sb.Append(" (");
            bool first = true;
            foreach (var column in columns)
            {
                if (!first) sb.Append(",");
                sb.Append("\n  ");
                sb.Append(ToSqlName(column.Name));
                sb.Append(" ");
                sb.Append(GetSqlType(column));
                if (column.IsIdentity)
                {
                    sb.Append(" ");
                    sb.Append(GetAutoIncrementSql());
                }
                if (column.IsPrimaryKey) sb.Append(" PRIMARY KEY");
                if (!column.AllowNull) sb.Append(" NOT NULL");
                first = false;
            }
            sb.Append("\n)");
            string result = sb.ToString();
            sb.Dispose();
            return result;
        }


        /// <summary>
        /// 生成检查表是否存在的 SQL 语句。
        /// </summary>
        public virtual string BuildTableExistsSql(string tableName)
        {
            return $"SELECT 1 FROM {ToSqlName(tableName)} WHERE 1=0";
        }

        /// <summary>
        /// 生成添加列的 SQL 语句。
        /// </summary>
        public virtual string BuildAddColumnSql(string tableName, ColumnDefinition column)
        {
            string sqlType = GetSqlType(column);
            string nullSql = column.AllowNull ? " NULL" : (column.IsIdentity ? "" : " NOT NULL");
            return $"ALTER TABLE {ToSqlName(tableName)} ADD {ToSqlName(column.Name)} {sqlType}{nullSql}";
        }

        /// <summary>
        /// 生成添加多个列的 SQL 语句。
        /// </summary>
        /// <param name="tableName">表名。</param>
        /// <param name="columns">列定义集合。</param>
        public virtual string BuildAddColumnsSql(string tableName, IEnumerable<ColumnDefinition> columns)
        {
            var colSqls = columns.Select(c => $"{ToSqlName(c.Name)} {GetSqlType(c)}{(c.AllowNull ? " NULL" : (c.IsIdentity ? "" : " NOT NULL"))}");
            return $"ALTER TABLE {ToSqlName(tableName)} ADD {string.Join(", ", colSqls)}";
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
            return $"CREATE {unique}INDEX {ToSqlName(indexName)} ON {ToSqlName(tableName)} ({ToSqlName(column.Name)})";
        }

        /// <summary>
        /// 生成批量检查 ID 是否存在的 SQL 语句。
        /// </summary>
        /// <param name="tableName">目标表名。</param>
        /// <param name="keyColumns">主键列定义数组。</param>
        /// <param name="batchSize">批量大小。</param>
        /// <returns>生成的 SQL 语句。</returns>
        public virtual string BuildBatchIDExistsSql(string tableName, ColumnDefinition[] keyColumns, int batchSize)
        {
            var sb = ValueStringBuilder.Create(1024);
            for (int i = 0; i < keyColumns.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(ToSqlName(keyColumns[i].Name));
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
                if (keyColumns.Length > 1) sb.Append("(");
                for (int i = 0; i < keyColumns.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    string keyParam = "p" + (b * keyColumns.Length + i);
                    sb.Append(ToSqlParam(keyParam));
                }
                if (keyColumns.Length > 1) sb.Append(")");
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
                sb.Append(ToSqlName(key.Name));
                sb.Append(" IN (");
                for (int b = 0; b < batchSize; b++)
                {
                    if (b > 0) sb.Append(", ");
                    sb.Append(ToSqlParam("p" + b));
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
                        string keyParam = "p" + (b * keyColumns.Length + k);
                        sb.Append(ToSqlName(key.Name));
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
                    sb.Append(ToSqlName(updatableColumns[i].Name));
                    sb.Append(" = ");
                    sb.Append(ToSqlParam("p" + (b * paramsPerRecord + i)));
                }

                // 构建 WHERE 子句
                sb.Append("\nWHERE ");
                for (int k = 0; k < keyColumns.Length; k++)
                {
                    if (k > 0) sb.Append(" AND ");
                    sb.Append(ToSqlName(keyColumns[k].Name));
                    sb.Append(" = ");
                    sb.Append(ToSqlParam("p" + (b * paramsPerRecord + updatableColumns.Length + k)));
                }
            }

            string result = sb.ToString();
            sb.Dispose();
            return result;
        }

        /// <summary>
        /// 获取自增标识的 SQL 片段。
        /// </summary>
        protected virtual string GetAutoIncrementSql() => "IDENTITY(1,1)";

        /// <summary>
        /// 根据列定义获取数据库列类型。
        /// </summary>
        protected virtual string GetSqlType(ColumnDefinition column)
        {
            switch (column.DbType)
            {
                case DbType.String:
                case DbType.AnsiString:
                    return column.Length > 0 && column.Length <= 4000 ? $"VARCHAR({column.Length})" : "TEXT";
                case DbType.Int32: return "INT";
                case DbType.Int64: return "BIGINT";
                case DbType.Boolean: return "BIT";
                case DbType.DateTime: return "DATETIME";
                case DbType.Decimal: return "DECIMAL(18,2)";
                case DbType.Double: return "DOUBLE";
                case DbType.Single: return "FLOAT";
                case DbType.Byte: return "TINYINT";
                case DbType.Int16: return "SMALLINT";
                case DbType.Guid: return "GUID";
                case DbType.Binary: return "BLOB";
                default: return "VARCHAR(255)";
            }
        }

        private static readonly ConcurrentDictionary<Type, SqlHandlerMap> _sqlHandlerMaps = new ConcurrentDictionary<Type, SqlHandlerMap>();
        internal static SqlHandlerMap GetSqlHandlerMap<T>() where T : SqlBuilder
        {
            return _sqlHandlerMaps.GetOrAdd(typeof(T), t => new SqlHandlerMap());
        }

        internal static SqlHandlerMap GetSqlHandlerMap(Type type)
        {
            return _sqlHandlerMaps.GetOrAdd(type, t => new SqlHandlerMap());
        }

        /// <summary>
        /// 将结构化的 SQL 片段组装成最终的 SELECT 语句。
        /// 基类实现使用标准的 SQL OFFSET/FETCH 语法进行分页。
        /// </summary>
        /// <param name="subSelect">包含 SELECT 各个子句片段的结构体。</param>
        /// <param name="result">输出 SQL 语句的缓冲区。</param>
        public virtual void BuildSelectSql(ref SqlValueStringBuilder subSelect, ref ValueStringBuilder result)
        {
            if (subSelect.Select.Length == 0) result.Append("SELECT *");
            else
            {
                result.Append("SELECT ");
                result.Append(subSelect.Select.AsSpan());
            }

            if (subSelect.From.Length > 0)
            {
                result.Append(" \nFROM ");
                result.Append(subSelect.From.AsSpan());
            }

            if (subSelect.Where.Length > 0)
            {
                result.Append(" \nWHERE ");
                result.Append(subSelect.Where.AsSpan());
            }

            if (subSelect.GroupBy.Length > 0)
            {
                result.Append(" \nGROUP BY ");
                result.Append(subSelect.GroupBy.AsSpan());
            }

            if (subSelect.Having.Length > 0)
            {
                result.Append(" \nHAVING ");
                result.Append(subSelect.Having.AsSpan());
            }

            if (subSelect.OrderBy.Length > 0)
            {
                result.Append(" \nORDER BY ");
                result.Append(subSelect.OrderBy.AsSpan());
            }

            if (subSelect.Take > 0)
            {
                if (subSelect.OrderBy.Length == 0)
                {
                    result.Append(" \nORDER BY 1"); 
                }
                result.Append($" \nOFFSET {subSelect.Skip} ROWS");
                result.Append($" \nFETCH NEXT {subSelect.Take} ROWS ONLY");
            }
        }
    }
}

