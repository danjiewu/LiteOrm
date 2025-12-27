using System;
using System.Collections.Generic;
using System.Text;
using MyOrm.Common;
using System.Collections;
using System.Data;
using System.Text.RegularExpressions;

namespace MyOrm.Oracle
{
    /// <summary>
    /// Oracle生成Sql语句的辅助类
    /// </summary>
    public class OracleBuilder : SqlBuilder
    {
        public OracleBuilder()
        {
            sqlLikeEscapeReg = new Regex(@"([%_/])");
        }

        public OracleIdentitySourceType IdentitySource { get; set; } = OracleIdentitySourceType.Identity;

        public static readonly new OracleBuilder Instance = new OracleBuilder();
        protected override void InitTypeToDbType()
        {
            base.InitTypeToDbType();
            RegisterDbType(typeof(Boolean), DbType.Byte);
        }
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
            return String.Format("insert into {0} \n({1})\nvalues ({2}) \nreturning {3} into {4}", ToSqlName(tableName), strColumns, strValues, ToSqlName(identityColumn.Name), ToSqlParam(identityColumn.PropertyName));
        }

        /// <summary>
        /// 连接各字符串的SQL语句
        /// </summary>
        /// <param name="strs">需要连接的sql字符串</param>
        /// <returns>SQL语句</returns>
        public override string ConcatSql(params string[] strs)
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

        public override string ToSqlName(string name)
        {
            if (name == null) throw new ArgumentNullException("name");
            return String.Join(".", Array.ConvertAll(name.Split('.'), n => String.Format("\"{0}\"", n.ToUpper())));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="nativeName"></param>
        /// <returns></returns>
        public override string ToSqlParam(string nativeName)
        {
            if (nativeName == null) throw new ArgumentNullException("nativeName");
            return ":" + nativeName;
        }

        public override string ToParamName(string nativeName)
        {
            if (nativeName == null) throw new ArgumentNullException("nativeName");
            return nativeName;
        }

        public override string ToNativeName(string paramName)
        {
            if (paramName == null) throw new ArgumentNullException("paramName");
            return paramName.TrimStart(':');
        }
    }
    public enum OracleIdentitySourceType
    {
        Identity,
        Sequence,
        Expression
    }
}
