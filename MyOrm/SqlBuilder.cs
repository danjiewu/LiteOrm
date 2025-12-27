using System;
using System.Collections.Generic;
using System.Text;
using MyOrm.Common;
using System.Text.RegularExpressions;
using System.Collections;
using System.Data;

namespace MyOrm
{
    /// <summary>
    /// 生成Sql语句的辅助类
    /// </summary>
    public class SqlBuilder : IConditionSqlBuilder, ISqlBuilder
    {
        public static readonly SqlBuilder Instance = new SqlBuilder();       

        private Dictionary<Type, DbType> typeToDbTypeCache = new Dictionary<Type, DbType>();
        public SqlBuilder()
        {
            InitTypeToDbType();
        }

        protected virtual void InitTypeToDbType()
        {
            typeToDbTypeCache[typeof(Enum)] = DbType.Int32;
            typeToDbTypeCache[typeof(Byte)] = DbType.Byte;
            typeToDbTypeCache[typeof(Byte[])] = DbType.Binary;
            typeToDbTypeCache[typeof(Char)] = DbType.String;
            typeToDbTypeCache[typeof(Boolean)] = DbType.Boolean;
            typeToDbTypeCache[typeof(DateTime)] = DbType.DateTime;
            typeToDbTypeCache[typeof(Decimal)] = DbType.Decimal;
            typeToDbTypeCache[typeof(Double)] = DbType.Double;
            typeToDbTypeCache[typeof(Guid)] = DbType.Guid;
            typeToDbTypeCache[typeof(Int16)] = DbType.Int16;
            typeToDbTypeCache[typeof(Int32)] = DbType.Int32;
            typeToDbTypeCache[typeof(Int64)] = DbType.Int64;
            typeToDbTypeCache[typeof(SByte)] = DbType.SByte;
            typeToDbTypeCache[typeof(Single)] = DbType.Single;
            typeToDbTypeCache[typeof(String)] = DbType.String;
            typeToDbTypeCache[typeof(TimeSpan)] = DbType.Time;
            typeToDbTypeCache[typeof(UInt16)] = DbType.UInt16;
            typeToDbTypeCache[typeof(UInt32)] = DbType.UInt32;
            typeToDbTypeCache[typeof(UInt64)] = DbType.UInt64;
            typeToDbTypeCache[typeof(DateTimeOffset)] = DbType.DateTimeOffset;
        }

        public void RegisterDbType(Type type, DbType dbType)
        {
            typeToDbTypeCache[type] = dbType;
        }

        /// <summary>
        /// 数据类型转换为DbType
        /// </summary>
        /// <param name="type">数据类型</param>
        /// <returns></returns>
        public DbType GetDbType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (!typeToDbTypeCache.ContainsKey(type) && type.IsEnum) type = typeof(Enum);
            if (typeToDbTypeCache.ContainsKey(type)) return typeToDbTypeCache[type];
            return DbType.Object;
        }

        /// <summary>
        /// 获取指定数据类型的默认长度
        /// </summary>
        /// <param name="columnType">数据库列的数据类型</param>
        /// <returns></returns>
        public int GetDefaultLength(DbType columnType)
        {
            switch (columnType)
            {
                case DbType.String:
                case DbType.AnsiString:
                case DbType.AnsiStringFixedLength:
                case DbType.StringFixedLength: return 255;
                case DbType.Byte:
                case DbType.Boolean: return 1;
                case DbType.Single:
                case DbType.Int32: return 4;
                case DbType.Double: return 8;
                case DbType.Xml: return 1 << 20;
                case DbType.Binary: return Int32.MaxValue;
                default: return 0;
            }
        }

        #region 预定义变量
        /// <summary>
        /// SQL语句中like条件中的转义符
        /// </summary>
        public const char LikeEscapeChar = '/';
        /// <summary>
        /// 对like条件的字符串内容中的转义符进行替换的正则表达
        /// </summary>
        protected Regex sqlLikeEscapeReg = new Regex(@"([_/%\[\]])");
        /// <summary>
        /// 查找列名、表名等的正则表达式
        /// </summary>
        protected static Regex sqlNameRegex = new Regex(@"\[([^\]]+)\]");

        private Dictionary<Type, IConditionSqlBuilder> extCondtionBuilders = new Dictionary<Type, IConditionSqlBuilder>();
        #endregion

        /// <summary>
        /// 注册自定义条件SQL生成类
        /// </summary>
        /// <param name="conditionType">自定义条件类型</param>
        /// <param name="conditionBuilder">自定义条件SQL生成类</param>
        public void RegisterConditionBuilder(Type conditionType, IConditionSqlBuilder conditionBuilder)
        {
            extCondtionBuilders[conditionType] = conditionBuilder;
        }

        /// <summary>
        /// 根据查询条件生成SQL语句与SQL参数
        /// </summary>
        /// <param name="context">用来生成SQL的上下文</param>
        /// <param name="conditon">查询条件，可为查询条件集合或单个条件，为空表示无条件</param>
        /// <param name="outputParams">供输出的参数列表，在该列表中添加SQL参数</param>
        /// <returns>生成的SQL语句，null表示无条件</returns>
        public virtual string BuildConditionSql(SqlBuildContext context, Condition conditon, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (conditon == null)
                return null;
            else if (conditon is SimpleCondition)
                return BuildSimpleConditionSql(context, conditon as SimpleCondition, outputParams);
            else if (conditon is ConditionSet)
                return BuildConditionSetSql(context, conditon as ConditionSet, outputParams);
            else if (conditon is ForeignCondition)
                return BuildForeignConditionSql(context, conditon as ForeignCondition, outputParams);
            else if (conditon is ExpressionCondition)
                return BuildExpressionConditionSql(context, conditon as ExpressionCondition, outputParams);
            else
            {
                if (extCondtionBuilders.ContainsKey(conditon.GetType()))
                {
                    return extCondtionBuilders[conditon.GetType()].BuildConditionSql(context, conditon, outputParams);
                }
                else throw new Exception(String.Format("Unsupported condition type \"{0}\"! Please register ConditionBuilder before call BuildConditionSql method.", conditon.GetType().FullName));
            }
        }

        /// <summary>
        /// 根据外部对象查询条件生成SQL语句与SQL参数
        /// </summary>
        /// <param name="context">用来生成SQL的上下文</param>
        /// <param name="condition">外部对象的查询条件</param>
        /// <param name="outputParams">供输出的参数列表，在该列表中添加SQL参数</param>
        /// <returns>生成的SQL语句，null表示无条件</returns>
        protected string BuildForeignConditionSql(SqlBuildContext context, ForeignCondition condition, ICollection<KeyValuePair<string, object>> outputParams)
        {
            TableDefinition tableDefinition = context.Table.Definition;
            ColumnDefinition joinedColumn = tableDefinition.GetColumn(condition.JoinedProperty);
            Type foreignType = condition.ForeignType;
            if (foreignType == null)
            {
                if (joinedColumn == null) throw new ArgumentException(String.Format("Property {0} not exists.", condition.JoinedProperty), condition.JoinedProperty);
                if (joinedColumn.ForeignType == null) throw new ArgumentException(String.Format("Property {0} does not point to a foreign type.", condition.JoinedProperty), condition.JoinedProperty);
                foreignType = joinedColumn.ForeignType;
            }

            TableDefinition foreignTable = context.TableInfoProvider.GetTableDefinition(foreignType);
            ColumnDefinition foreignColumn = foreignTable.GetColumn(condition.ForeignProperty);

            if (!String.Equals(foreignTable.DataSource, tableDefinition.DataSource, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException(String.Format("ForeignCondition between different data source is not supported. Type {0}'s data source is {1} and type {2}'s data source is {3}. ", context.Table.DefinitionType.FullName, foreignTable.DataSource, foreignTable.ObjectType.FullName, tableDefinition.DataSource));

            if (joinedColumn == null && foreignColumn == null)
            {
                foreach (ColumnDefinition column in tableDefinition.Columns)
                {
                    if (column.ForeignType == foreignType)
                    {
                        if (joinedColumn != null || foreignColumn != null) throw new ArgumentException(String.Format("Undefined relation between Type {0} and Type {1}. Please specify the ForeignCondition.JoinedProperty.", context.Table.DefinitionType.FullName, foreignTable.ObjectType.FullName), "condition");
                        joinedColumn = column;
                    }
                }

                foreach (ColumnDefinition column in foreignTable.Columns)
                {
                    if (column.ForeignType == context.Table.DefinitionType)
                    {
                        if (joinedColumn != null || foreignColumn != null) throw new ArgumentException(String.Format("Uncertain relation between Type {0} and Type {1}. Please specify the ForeignCondition.JoinedProperty.", context.Table.DefinitionType.FullName, foreignTable.ObjectType.FullName), "condition");
                        foreignColumn = column;
                    }
                }
                if (joinedColumn == null && foreignColumn == null) throw new ArgumentException(String.Format("No relation between Type {0} and Type {1}", context.Table.DefinitionType.FullName, foreignTable.ObjectType.FullName), "condition");
            }

            if (foreignColumn == null)
            {
                if (foreignTable.Keys.Count != 1) throw new ArgumentException(String.Format("Type \"{0}\" does not support foreign condition,which only take effect on type with one and only key column.", foreignType.FullName), "condition");
                foreignColumn = foreignTable.Keys[0];
            }
            else if (joinedColumn == null)
            {
                if (context.Table.Definition.Keys.Count != 1) throw new ArgumentException(String.Format("Type \"{0}\" does not support foreign condition,which only take effect on type with one and only key column.", context.Table.DefinitionType.FullName), "condition");
                joinedColumn = context.Table.Definition.Keys[0];
            }

            string tableAlias = context.TableAliasName ?? context.GetTableNameWithArgs(context.Table.Name);
            string foreignTableAlias = "T" + context.Sequence;
            return String.Format("{0}exists (select 1 \nfrom {1} {2} \nwhere {3}.{4} = {5}.{6} and ({7}))",
                condition.Opposite ? "not " : null,
                foreignTable.FormattedName(this),
                ToSqlName(foreignTableAlias),
                ToSqlName(tableAlias),
                joinedColumn.FormattedName(this),
                ToSqlName(foreignTableAlias),
                foreignColumn.FormattedName(this),
                BuildConditionSql(new SqlBuildContext() { TableInfoProvider = context.TableInfoProvider, TableAliasName = foreignTableAlias, Sequence = context.Sequence + 1, Table = foreignTable, TableNameArgs = context.TableNameArgs }, condition.Condition, outputParams));
        }

        /// <summary>
        /// 根据查询条件集合生成SQL语句与SQL参数
        /// </summary>
        /// <param name="context">用来生成SQL的上下文</param>
        /// <param name="conditionSet">查询条件的集合</param>
        /// <param name="outputParams">供输出的参数列表，在该列表中添加SQL参数</param>
        /// <returns>生成的SQL语句，null表示无条件</returns>
        protected string BuildConditionSetSql(SqlBuildContext context, ConditionSet conditionSet, ICollection<KeyValuePair<string,object>> outputParams)
        {
            List<string> conditions = new List<string>();
            foreach (Condition subConditon in conditionSet.SubConditions)
            {
                string str = BuildConditionSql(context, subConditon, outputParams);
                if (!String.IsNullOrEmpty(str)) conditions.Add(str);
            }
            if (conditions.Count == 0) return null;
            return String.Format("{0} ({1})", conditionSet.Opposite ? "not" : null, String.Join(" " + conditionSet.JoinType + " ", conditions.ToArray()));
        }

        /// <summary>
        /// 根据表达式条件生成SQL语句与SQL参数
        /// </summary>
        /// <param name="context">用来生成SQL的上下文</param>
        /// <param name="expressionCondition">表示查询条件的表达式</param>
        /// <param name="outputParams">供输出的参数列表，在该列表中添加SQL参数</param>
        /// <returns>生成的SQL语句，null表示无条件</returns>
        protected string BuildExpressionConditionSql(SqlBuildContext context, ExpressionCondition expressionCondition, ICollection<KeyValuePair<string,object>> outputParams)
        {
            List<string> conditions = new List<string>();
            ExpressionParser parser = new ExpressionParser(this, context);
            parser.ArgumentsStartIndex = outputParams.Count;
            parser.Visit(expressionCondition.Expression);
            foreach (var v in parser.Arguments)
            {
                outputParams.Add(new KeyValuePair<string, object>(v.Key, v.Value));
            }
            return parser.Result;
        }

        /// <summary>
        /// 根据简单查询条件生成SQL语句与SQL参数
        /// </summary>
        /// <param name="context">用来生成SQL的上下文</param>
        /// <param name="simpleCondition">简单查询条件</param>
        /// <param name="outputParams">参数列表，在该列表中添加SQL参数</param>
        /// <returns>生成的SQL语句</returns>
        protected string BuildSimpleConditionSql(SqlBuildContext context, SimpleCondition simpleCondition, ICollection<KeyValuePair<string, object>> outputParams)
        {
            Column column = context.Table.GetColumn(simpleCondition.Property);
            if (column == null) throw new Exception(String.Format("Property \"{0}\" does not exist in type \"{1}\".", simpleCondition.Property, context.Table.DefinitionType.FullName));
            string tableAlias = context.TableAliasName;
            string columnName = tableAlias == null ? (context.SingleTable ? column.FormattedName(this) : column.FormattedExpression(this)) : String.Format("[{0}].[{1}]", tableAlias, column.Name);

            string expression = columnName;
            object value = simpleCondition.Value;
            string strOpposite = simpleCondition.Opposite ? "not" : null;

            if ((simpleCondition.Value == null || simpleCondition.Value == DBNull.Value) && simpleCondition.Operator == ConditionOperator.Equals)
                return string.Format("{0} is {1} null", expression, strOpposite);

            ConditionOperator positiveOp = simpleCondition.Operator;
            if (positiveOp == ConditionOperator.Contains || positiveOp == ConditionOperator.EndsWith || positiveOp == ConditionOperator.StartsWith)
                value = ToSqlLikeValue(Convert.ToString(value));
            int paramIndex = outputParams.Count;
            switch (simpleCondition.Operator)
            {
                case ConditionOperator.Equals:
                    outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), value));
                    return String.Format(simpleCondition.Opposite ? "{0} <> {1}" : "{0} = {1}", expression, ToSqlParam(paramIndex.ToString()));
                case ConditionOperator.LargerThan:
                    outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), value));
                    return String.Format(simpleCondition.Opposite ? "{0} <= {1}" : "{0} > {1}", expression, ToSqlParam(paramIndex.ToString()));
                case ConditionOperator.SmallerThan:
                    outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), value));
                    return String.Format(simpleCondition.Opposite ? "{0} >= {1}" : "{0} < {1}", expression, ToSqlParam(paramIndex.ToString()));
                case ConditionOperator.Like:
                    outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), value));
                    return String.Format(@"{0} {1} like {2}", expression, strOpposite, ToSqlParam(paramIndex.ToString()));
                case ConditionOperator.StartsWith:
                    outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), value));
                    return String.Format(@"{0} {1} like {2} escape '{3}'", expression, strOpposite, ConcatSql(ToSqlParam(paramIndex.ToString()), "'%'"), LikeEscapeChar);
                case ConditionOperator.EndsWith:
                    outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), value));
                    return String.Format(@"{0} {1} like {2} escape '{3}'", expression, strOpposite, ConcatSql("'%'", ToSqlParam(paramIndex.ToString())), LikeEscapeChar);
                case ConditionOperator.Contains:
                    outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), value));
                    return String.Format(@"{0} {1} like {2} escape '{3}'", expression, strOpposite, ConcatSql("'%'", ToSqlParam(paramIndex.ToString()), "'%'"), LikeEscapeChar);
                case ConditionOperator.RegexpLike:
                    outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), value));
                    return String.Format(@"{0} regexp_like({1},{2})", strOpposite, expression, ToSqlParam(paramIndex.ToString()));
                case ConditionOperator.In:
                    List<string> paramNames = new List<string>();
                    foreach (object item in value as IEnumerable)
                    {
                        outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), item));
                        paramNames.Add(ToSqlParam(paramIndex.ToString()));
                        paramIndex++;
                    }
                    return String.Format("{0} {1} in ({2})", expression, strOpposite, String.Join(",", paramNames.ToArray()));
                default:
                    return string.Empty;
            }
        }

        public virtual string ToSqlLikeValue(string value)
        {
            return sqlLikeEscapeReg.Replace(value, LikeEscapeChar + "$1");
        }

        /// <summary>
        /// 生成带标识列的插入SQL
        /// </summary>
        /// <param name="command">SQLCommand</param>
        /// <param name="identityColumn">标识列</param>
        /// <param name="tableName">表名</param>
        /// <param name="strColumns">插入数据列名称</param>
        /// <param name="strValues">插入数据名称</param>
        /// <returns></returns>
        public virtual string BuildIdentityInsertSQL(IDbCommand command, ColumnDefinition identityColumn, string tableName, string strColumns, string strValues)
        {
            return String.Format("BEGIN insert into {0} ({1}) \nvalues ({2}); {3} END;", ToSqlName(tableName), strColumns, strValues, "select @@IDENTITY as [ID];");
        }

        /// <summary>
        /// 连接各字符串的SQL语句
        /// </summary>
        /// <param name="strs">需要连接的sql字符串</param>
        /// <returns>SQL语句</returns>
        public virtual string ConcatSql(params string[] strs)
        {
            return String.Join(" + ", strs);
        }

        /// <summary>
        /// 生成分页查询的SQL语句
        /// </summary>
        /// <param name="select">select内容</param>
        /// <param name="from">from块</param>
        /// <param name="where">where条件</param>
        /// <param name="orderBy">排序</param>
        /// <param name="startIndex">起始位置，从0开始</param>
        /// <param name="sectionSize">查询条数</param>
        /// <returns></returns>
        public virtual string GetSelectSectionSql(string select, string from, string where, string orderBy, int startIndex, int sectionSize)
        {
            if (!String.IsNullOrEmpty(where)) where= " \nwhere " + where;
            return String.Format("select * from (\nselect {0}, Row_Number() over (Order by {1}) as Row_Number \nfrom {2} {3}) TempTable \nwhere Row_Number > {4} and Row_Number <= {5}", select, orderBy, from, where, startIndex, startIndex + sectionSize);
        }

        /// <summary>
        /// 名称转化为数据库合法名称
        /// </summary>
        /// <param name="name">字符串名称</param>
        /// <returns>数据库合法名称</returns>
        public virtual string ToSqlName(string name)
        {
            if (name == null) throw new ArgumentNullException("name");
            return String.Join(".", Array.ConvertAll(name.Split('.'), n => String.Format("[{0}]", n)));
        }

        /// <summary>
        /// 原始名称转化为数据库参数
        /// </summary>
        /// <param name="nativeName">原始名称</param>
        /// <returns>数据库参数</returns>
        public virtual string ToSqlParam(string nativeName)
        {
            return String.Format("@{0}", nativeName);
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
            if (sql == null) return null;
            StringBuilder sb = new StringBuilder();
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
                            if (handler != null)
                            {
                                sb.Append(handler(ch));
                            }
                            else
                                sb.Append(ch);
                            break;
                    }
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 自定义Condition转换为sql语句的接口
    /// </summary>
    public interface IConditionSqlBuilder
    {
        /// <summary>
        /// 生成sql语句
        /// </summary>
        /// <param name="context">生成sql的上下文</param>
        /// <param name="customConditon">自定义Condition</param>
        /// <param name="outputParams">存放参数的集合</param>
        /// <returns>生成的sql字符串</returns>
        string BuildConditionSql(SqlBuildContext context, Condition customConditon, ICollection<KeyValuePair<string, object>> outputParams);
    }
}


