using System;
using System.Collections.Generic;
using System.Text;
using LiteOrm.Common;
using System.Collections;
using System.Data;
using System.Text.RegularExpressions;

namespace LiteOrm.Oracle
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
            sqlLikeEscapeReg = new Regex(@"([%_/])");
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
        /// 初始化类型到 DbType 的映射。
        /// </summary>
        protected override void InitTypeToDbType()
        {
            base.InitTypeToDbType();
            RegisterDbType(typeof(Boolean), DbType.Byte);
        }

        /// <summary>
        /// 初始化函数映射关系。
        /// </summary>
        /// <param name="functionMappings">函数映射字典。</param>
        protected override void InitializeFunctionMappings(Dictionary<string, string> functionMappings)
        {
            functionMappings["Length"] = "LENGTH";
            functionMappings["IndexOf"] = "INSTR";       // INSTR(str, substr)
            functionMappings["Substring"] = "SUBSTR";
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
        public override string BuildIdentityInsertSQL(IDbCommand command, ColumnDefinition identityColumn, string tableName, string strColumns, string strValues)
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
        /// 将 Oracle 参数名转换为通用名称（去掉冒号前缀）。
        /// </summary>
        /// <param name="paramName">参数名称。</param>
        /// <returns>通用名称。</returns>
        public override string ToNativeName(string paramName)
        {
            if (paramName is null) throw new ArgumentNullException("paramName");
            return paramName.TrimStart(':');
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
