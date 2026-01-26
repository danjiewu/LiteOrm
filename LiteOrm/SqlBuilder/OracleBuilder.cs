using System;
using System.Collections.Generic;
using System.Text;
using LiteOrm.Common;
using System.Collections;
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
            return $"insert into {ToSqlName(tableName)} \n({strColumns})\nvalues ({strValues}) \nreturning {ToSqlName(identityColumn.Name)} into {ToSqlParam(identityColumn.PropertyName)}";
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
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("INSERT ALL");
            string sqlTableName = ToSqlName(tableName);
            foreach (var values in valuesList)
            {
                sb.AppendLine($"  INTO {sqlTableName} ({columns}) VALUES {values}");
            }
            sb.Append("SELECT * FROM DUAL");
            return sb.ToString();
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
        /// 生成更新或插入（Upsert）的 SQL 语句 (Oracle MERGE 风格)。
        /// 使用 MERGE INTO ... USING DUAL 语法实现，但不能更新插入节点的标识列。
        /// </summary>
        /// <param name="command">数据库命令。</param>
        /// <param name="tableName">目标表名。</param>
        /// <param name="insertColumns">插入列。</param>
        /// <param name="insertValues">插入值。</param>
        /// <param name="updateSets">更新集。</param>
        /// <param name="keyColumns">关键关联列。</param>
        /// <param name="identityColumn">标识列。</param>
        /// <returns>返回 Oracle Upsert SQL 字符串。</returns>
        public override string BuildUpsertSql(IDbCommand command, string tableName, string insertColumns, string insertValues, string updateSets, IEnumerable<ColumnDefinition> keyColumns, ColumnDefinition identityColumn)
        {
            string table = ToSqlName(tableName);
            string where = string.Join(" AND ", keyColumns.Select(c => $"t.{ToSqlName(c.Name)} = {ToSqlParam(c.PropertyName)}"));

            if (identityColumn != null)
            {
                IDbDataParameter param = command.CreateParameter();
                param.Direction = ParameterDirection.Output;
                param.Size = identityColumn.Length;
                param.DbType = identityColumn.DbType;
                param.ParameterName = ToParamName("0");
                if (!command.Parameters.Contains(param.ParameterName))
                {
                    command.Parameters.Add(param);
                }

                if (IdentitySource == OracleIdentitySourceType.Sequence)
                {
                    insertColumns += "," + ToSqlName(identityColumn.Name);
                    insertValues += "," + (String.IsNullOrEmpty(identityColumn.IdentityExpression) ? tableName + "_seq.nextval" : identityColumn.IdentityExpression + ".nextval");
                }
                else if (IdentitySource == OracleIdentitySourceType.Expression)
                {
                    insertColumns += "," + ToSqlName(identityColumn.Name);
                    insertValues += "," + identityColumn.IdentityExpression;
                }
            }

            return $@"MERGE INTO {table} t USING DUAL ON ({where})
WHEN MATCHED THEN UPDATE SET {updateSets}
WHEN NOT MATCHED THEN INSERT ({insertColumns}) VALUES ({insertValues})";
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
        /// 生成 Oracle 专用的批量更新 SQL 语句（使用匿名 PL/SQL 块）。
        /// 针对 Oracle 优化：不直接使用全局默认的 CASE WHEN 方式，而是采用更稳定的 BEGIN...END 块驱动多条 UPDATE 语句。
        /// </summary>
        /// <param name="tableName">目标表名。</param>
        /// <param name="updatableColumns">可更新列集合。</param>
        /// <param name="keyColumns">主键列集合。</param>
        /// <param name="batchSize">批次大小。</param>
        /// <returns>返回 Oracle 可执行的批量更新 SQL 字符串。</returns>
        public override string BuildBatchUpdateSql(string tableName, ColumnDefinition[] updatableColumns, ColumnDefinition[] keyColumns, int batchSize)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("BEGIN");

            int paramsPerRecord = updatableColumns.Length + keyColumns.Length;

            for (int b = 0; b < batchSize; b++)
            {
                sb.AppendFormat("  UPDATE {0} SET ", ToSqlName(tableName));
                for (int i = 0; i < updatableColumns.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    var col = updatableColumns[i];
                    string valParam = "p" + (b * paramsPerRecord + i);
                    sb.AppendFormat("{0} = {1}", ToSqlName(col.Name), ToSqlParam(valParam));
                }

                sb.Append(" WHERE ");
                for (int k = 0; k < keyColumns.Length; k++)
                {
                    if (k > 0) sb.Append(" AND ");
                    var key = keyColumns[k];
                    string keyParam = "p" + (b * paramsPerRecord + updatableColumns.Length + k);
                    sb.AppendFormat("{0} = {1}", ToSqlName(key.Name), ToSqlParam(keyParam));
                }
                sb.AppendLine(";");
            }

            sb.Append("END;");
            return sb.ToString();
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
