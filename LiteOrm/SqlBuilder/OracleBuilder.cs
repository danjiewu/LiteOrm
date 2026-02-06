using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;


namespace LiteOrm
{
    /// <summary>
    /// Oracle 生成 SQL 语句的辅助类。
    /// </summary>
    public class OracleBuilder : SqlBuilder
    {
        /// <summary>
        /// 初始化 <see cref="OracleBuilder"/> 类的新实例。
        /// </summary>
        public OracleBuilder()
        {
            _sqlLikeEscapeReg = new Regex(@"([%_/])");
        }

        /// <summary>
        /// 获取或设置 Oracle 自增值的来源类型。
        /// </summary>
        public OracleIdentitySourceType IdentitySource { get; set; } = OracleIdentitySourceType.Identity;

        /// <summary>
        /// 获取 <see cref="OracleBuilder"/> 的单例实例。
        /// </summary>
        public static readonly new OracleBuilder Instance = new OracleBuilder();

        /// <summary>
        /// 返回指定类型对应的Oracle数据库类型。
        /// </summary>
        /// <param name="type">要转换的类型。</param>
        /// <returns></returns>
        /// <remarks>Oracle不支持布尔类型，布尔类型将被映射为字节类型</remarks>
        public override DbType GetDbType(Type type)
        {
            if (type == typeof(bool)) return DbType.Byte; // Oracle 不支持布尔类型，使用数字代替
            return base.GetDbType(type);
        }

        /// <summary>
        /// 构建带有标识列或序列插入的 SQL 语句。
        /// </summary>
        /// <param name="command">数据库命令对象。</param>
        /// <param name="identityColumn">标识列或含有序列的列定义。</param>
        /// <param name="tableName">表名。</param>
        /// <param name="strColumns">插入列名部分。</param>
        /// <param name="strValues">插入值名部分。</param>
        /// <returns>构建后的 SQL 语句。</returns>
        public override string BuildIdentityInsertSql(IDbCommand command, ColumnDefinition identityColumn, string tableName, string strColumns, string strValues)
        {
            IDbDataParameter param = command.CreateParameter();
            param.Direction = ParameterDirection.Output;
            param.Size = identityColumn.Length;
            param.DbType = identityColumn.DbType;
            param.ParameterName = ToParamName(identityColumn.PropertyName);
            command.Parameters.Add(param);
            if (IdentitySource == OracleIdentitySourceType.Sequence)
            {
                strColumns += "," + ToSqlName(identityColumn.Name);
                strValues += "," + (String.IsNullOrEmpty(identityColumn.IdentityExpression) ? tableName + "_seq.nextval" : identityColumn.IdentityExpression + ".nextval");
            }
            else if (IdentitySource == OracleIdentitySourceType.Expression)
            {
                strColumns += "," + ToSqlName(identityColumn.Name);
                strValues += "," + identityColumn.IdentityExpression;
            }
            return $"INSERT INTO {ToSqlName(tableName)} \n({strColumns})\nVALUES ({strValues}) \nRETURNING {ToSqlName(identityColumn.Name)} INTO {ToSqlParam(identityColumn.PropertyName)}";
        }

        /// <summary>
        /// 生成 Oracle 专用的批量插入 SQL 语句 (INSERT ALL)。
        /// </summary>
        /// <param name="tableName">目标表名。</param>
        /// <param name="columns">插入的列名集合（逗号分隔的 SQL 名称）。</param>
        /// <param name="valuesList">每个实体的占位符集合（例如 "(:p0,:p1,:p2)"）。</param>
        /// <returns>返回 Oracle 可执行的批量插入 SQL 字符串。</returns>
        public override string BuildBatchInsertSql(string tableName, string columns, List<string> valuesList)
        {
            var sb = ValueStringBuilder.Create(1024);
            sb.Append("INSERT ALL\n");
            string sqlTableName = ToSqlName(tableName);
            foreach (var values in valuesList)
            {
                sb.Append("  INTO ");
                sb.Append(sqlTableName);
                sb.Append(" (");
                sb.Append(columns);
                sb.Append(") VALUES ");
                sb.Append(values);
                sb.Append("\n");
            }
            sb.Append("SELECT * FROM DUAL");
            string result = sb.ToString();
            sb.Dispose();
            return result;
        }

        /// <summary>
        /// 连接各字符串的SQL语句
        /// </summary>
        /// <param name="strs">需要连接的sql字符串</param>
        /// <returns>SQL语句</returns>
        public override string BuildConcatSql(params string[] strs)
        {
            return String.Join("||", strs);
        }

        /// <summary>
        /// 将列名、表名等替换为数据库合法名称
        /// </summary>
        /// <param name="sql">sql语句</param>
        /// <returns></returns>
        public override string ReplaceSqlName(string sql)
        {
            return ReplaceSqlName(sql, '"', '"', Char.ToUpper);
        }

        /// <summary>
        /// 将名称转换为 Oracle 兼容的 SQL 名称（加双引号并转大写）。
        /// </summary>
        /// <param name="name">原始名称。</param>
        /// <returns>Oracle 兼容的 SQL 名称。</returns>
        public override string ToSqlName(string name)
        {
            if (name is null) throw new ArgumentNullException("name");
            return String.Join(".", Array.ConvertAll(name.Split('.'), n => $"\"{n.ToUpper()}\""));
        }

        /// <summary>
        /// 将原始名称转换为数据库参数形式（加冒号前缀）。
        /// </summary>
        /// <param name="nativeName">原始名称。</param>
        /// <returns>数据库参数。</returns>
        public override string ToSqlParam(string nativeName)
        {
            if (nativeName is null) throw new ArgumentNullException("nativeName");
            return $":{nativeName}";
        }

        /// <summary>
        /// 将通用名称转换为 Oracle 限定的参数名称。
        /// </summary>
        /// <param name="nativeName">通用名称。</param>
        /// <returns>参数名称。</returns>
        public override string ToParamName(string nativeName)
        {
            if (nativeName is null) throw new ArgumentNullException("nativeName");
            return nativeName;
        }

        /// <summary>
        /// 获取自增标识 SQL 片段。
        /// </summary>
        protected override string GetAutoIncrementSql() => "GENERATED AS IDENTITY";

        /// <summary>
        /// 获取 Oracle 列 type。
        /// </summary>
        protected override string GetSqlType(ColumnDefinition column)
        {
            switch (column.DbType)
            {
                case DbType.String:
                case DbType.AnsiString:
                    return column.Length > 0 && column.Length <= 4000 ? $"VARCHAR2({column.Length})" : "CLOB";
                case DbType.Int16:
                case DbType.Int32:
                case DbType.Int64:
                    return "NUMBER";
                case DbType.DateTime:
                    return "TIMESTAMP";
                case DbType.Boolean:
                    return "NUMBER(1)";
                default:
                    return base.GetSqlType(column);
            }
        }

        /// <summary>
        /// 将 Oracle 参数名转换为通用名称（去掉冒号前缀）。
        /// </summary>
        /// <param name="paramName">参数名称。</param>
        /// <returns>通用名称。</returns>
        public override string ToNativeName(string paramName)
        {
            if (paramName is null) throw new ArgumentNullException("paramName");
            return paramName.TrimStart(':');
        }

        /// <summary>
        /// 生成添加多个列的 SQL 语句。
        /// </summary>
        public override string BuildAddColumnsSql(string tableName, IEnumerable<ColumnDefinition> columns)
        {
            var colSqls = columns.Select(c => $"{ToSqlName(c.Name)} {GetSqlType(c)}{(c.AllowNull ? " NULL" : (c.IsIdentity ? "" : " NOT NULL"))}");
            return $"ALTER TABLE {ToSqlName(tableName)} ADD ({string.Join(", ", colSqls)})";
        }

        /// <summary>
        /// 生成 Oracle 专用的批量更新 SQL 语句（使用 MERGE 语句）。
        /// 针对 Oracle 优化：使用 MERGE 语句批量处理更新操作，提高性能。
        /// </summary>
        /// <param name="tableName">目标表名。</param>
        /// <param name="updatableColumns">可更新列集合。</param>
        /// <param name="keyColumns">主键列集合。</param>
        /// <param name="batchSize">批次大小。</param>
        /// <returns>返回 Oracle 可执行的批量更新 SQL 字符串。</returns>
        public override string BuildBatchUpdateSql(string tableName, ColumnDefinition[] updatableColumns, ColumnDefinition[] keyColumns, int batchSize)
        {
            if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0");
            if (keyColumns.Length == 0) throw new ArgumentException("At least one key column is required", nameof(keyColumns));
            if (updatableColumns.Length == 0) throw new ArgumentException("At least one updatable column is required", nameof(updatableColumns));

            
            int paramsPerRecord = updatableColumns.Length + keyColumns.Length;
            var sb = ValueStringBuilder.Create(128 + paramsPerRecord * batchSize * 16);
            
            // 构建 MERGE INTO 语句
            sb.Append("MERGE INTO ");
            sb.Append(ToSqlName(tableName));
            sb.Append(" t\n");
            
            // 构建 USING 子句
            sb.Append("USING (\n");
            
            for (int b = 0; b < batchSize; b++)
            {
                if (b > 0) sb.Append("    UNION ALL\n");
                sb.Append("    SELECT ");
                
                // 添加可更新列的参数
                for (int i = 0; i < updatableColumns.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    string valParam = ":p" + (b * paramsPerRecord + i);
                    sb.Append(valParam);
                    sb.Append(" AS ");
                    sb.Append(ToSqlName(updatableColumns[i].Name));
                }
                
                // 添加主键列的参数
                for (int k = 0; k < keyColumns.Length; k++)
                {
                    if (updatableColumns.Length > 0 || k > 0) sb.Append(", ");
                    string keyParam = ":p" + (b * paramsPerRecord + updatableColumns.Length + k);
                    sb.Append(keyParam);
                    sb.Append(" AS ");
                    sb.Append(ToSqlName(keyColumns[k].Name));
                }
                
                sb.Append(" FROM DUAL\n");
            }
            
            sb.Append(") s\n");
            
            // 构建 ON 子句
            sb.Append("ON (");
            for (int k = 0; k < keyColumns.Length; k++)
            {
                if (k > 0) sb.Append(" AND ");
                sb.Append("t.");
                sb.Append(ToSqlName(keyColumns[k].Name));
                sb.Append(" = s.");
                sb.Append(ToSqlName(keyColumns[k].Name));
            }
            sb.Append(")\n");
            
            // 构建 WHEN MATCHED THEN UPDATE SET 子句
            sb.Append("WHEN MATCHED THEN\n");
            sb.Append("    UPDATE SET ");
            for (int i = 0; i < updatableColumns.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("t.");
                sb.Append(ToSqlName(updatableColumns[i].Name));
                sb.Append(" = s.");
                sb.Append(ToSqlName(updatableColumns[i].Name));
            }
            
            string result = sb.ToString();
            sb.Dispose();
            return result;
        }
    }

    /// <summary>
    /// Oracle 标识列（自增值）来源类型。
    /// </summary>
    public enum OracleIdentitySourceType
    {
        /// <summary>
        /// 使用 Identity 字段 (Oracle 12c+)。
        /// </summary>
        Identity,
        /// <summary>
        /// 使用序列 (Sequence)。
        /// </summary>
        Sequence,
        /// <summary>
        /// 使用 SQL 表达式。
        /// </summary>
        Expression
    }
}
