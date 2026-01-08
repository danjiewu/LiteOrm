using System;
using System.Collections.Generic;
using System.Text;
using MyOrm.Common;
using System.Text.RegularExpressions;
using System.Collections;
using System.Data;
using System.Collections.Concurrent;

namespace MyOrm
{
    /// <summary>
    /// 生成Sql语句的辅助类
    /// </summary>
    public class SqlBuilder : IConditionSqlBuilder, ISqlBuilder
    {
        public static readonly SqlBuilder Instance = new SqlBuilder();

        private ConcurrentDictionary<Type, DbType> typeToDbTypeCache = new ConcurrentDictionary<Type, DbType>();
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

        private ConcurrentDictionary<Type, IConditionSqlBuilder> extCondtionBuilders = new ConcurrentDictionary<Type, IConditionSqlBuilder>();
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
            else
            {
                if (extCondtionBuilders.ContainsKey(conditon.GetType()))
                {
                    return extCondtionBuilders[conditon.GetType()].BuildConditionSql(context, conditon, outputParams);
                }
                else throw new Exception($"Unsupported condition type \"{conditon.GetType().FullName}\"! Please register ConditionBuilder before call BuildConditionSql method.");
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
                if (joinedColumn == null) throw new ArgumentException($"Property {condition.JoinedProperty} not exists.", condition.JoinedProperty);
                if (joinedColumn.ForeignType == null) throw new ArgumentException($"Property {condition.JoinedProperty} does not point to a foreign type.", condition.JoinedProperty);
                foreignType = joinedColumn.ForeignType;
            }

            TableDefinition foreignTable = context.TableInfoProvider.GetTableDefinition(foreignType);
            ColumnDefinition foreignColumn = foreignTable.GetColumn(condition.ForeignProperty);

            if (!String.Equals(foreignTable.DataSource, tableDefinition.DataSource, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"ForeignCondition between different data source is not supported. Type {context.Table.DefinitionType.FullName}'s data source is {foreignTable.DataSource} and type {foreignTable.ObjectType.FullName}'s data source is {tableDefinition.DataSource}. ");

            if (joinedColumn == null && foreignColumn == null)
            {
                foreach (ColumnDefinition column in tableDefinition.Columns)
                {
                    if (column.ForeignType == foreignType)
                    {
                        if (joinedColumn != null || foreignColumn != null) throw new ArgumentException($"Undefined relation between Type {context.Table.DefinitionType.FullName} and Type {foreignTable.ObjectType.FullName}. Please specify the ForeignCondition.JoinedProperty.", "condition");
                        joinedColumn = column;
                    }
                }

                foreach (ColumnDefinition column in foreignTable.Columns)
                {
                    if (column.ForeignType == context.Table.DefinitionType)
                    {
                        if (joinedColumn != null || foreignColumn != null) throw new ArgumentException($"Uncertain relation between Type {context.Table.DefinitionType.FullName} and Type {foreignTable.ObjectType.FullName}. Please specify the ForeignCondition.JoinedProperty.", "condition");
                        foreignColumn = column;
                    }
                }
                if (joinedColumn == null && foreignColumn == null) throw new ArgumentException($"No relation between Type {context.Table.DefinitionType.FullName} and Type {foreignTable.ObjectType.FullName}", "condition");
            }

            if (foreignColumn == null)
            {
                if (foreignTable.Keys.Count != 1) throw new ArgumentException($"Type \"{foreignType.FullName}\" does not support foreign condition,which only take effect on type with one and only key column.", "condition");
                foreignColumn = foreignTable.Keys[0];
            }
            else if (joinedColumn == null)
            {
                if (context.Table.Definition.Keys.Count != 1) throw new ArgumentException($"Type \"{context.Table.DefinitionType.FullName}\" does not support foreign condition,which only take effect on type with one and only key column.", "condition");
                joinedColumn = context.Table.Definition.Keys[0];
            }

            string tableAlias = context.TableAliasName ?? context.GetTableNameWithArgs(context.Table.Name);
            string foreignTableAlias = "T" + context.Sequence;
            string opposite = condition.Opposite ? "not " : "";
            string innerCondition = BuildConditionSql(new SqlBuildContext() { TableInfoProvider = context.TableInfoProvider, TableAliasName = foreignTableAlias, Sequence = context.Sequence + 1, Table = foreignTable, TableNameArgs = context.TableNameArgs }, condition.Condition, outputParams);

            return $"{opposite}exists (select 1 \nfrom {foreignTable.FormattedName(this)} {ToSqlName(foreignTableAlias)} \nwhere {ToSqlName(tableAlias)}.{joinedColumn.FormattedName(this)} = {ToSqlName(foreignTableAlias)}.{foreignColumn.FormattedName(this)} and ({innerCondition}))";
        }

        /// <summary>
        /// 根据查询条件集合生成SQL语句与SQL参数
        /// </summary>
        /// <param name="context">用来生成SQL的上下文</param>
        /// <param name="conditionSet">查询条件的集合</param>
        /// <param name="outputParams">供输出的参数列表，在该列表中添加SQL参数</param>
        /// <returns>生成的SQL语句，null表示无条件</returns>
        protected string BuildConditionSetSql(SqlBuildContext context, ConditionSet conditionSet, ICollection<KeyValuePair<string, object>> outputParams)
        {
            List<string> conditions = new List<string>();
            foreach (Condition subConditon in conditionSet.SubConditions)
            {
                string str = BuildConditionSql(context, subConditon, outputParams);
                if (!String.IsNullOrEmpty(str)) conditions.Add(str);
            }
            if (conditions.Count == 0) return null;
            string joiner = " " + conditionSet.JoinType + " ";
            return $"{(conditionSet.Opposite ? "not" : "")} ({String.Join(joiner, conditions.ToArray())})";
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
            if (column == null) throw new Exception($"Property \"{simpleCondition.Property}\" does not exist in type \"{context.Table.DefinitionType.FullName}\".");
            string tableAlias = context.TableAliasName;
            string columnName = tableAlias == null ? (context.SingleTable ? column.FormattedName(this) : column.FormattedExpression(this)) : $"[{tableAlias}].[{column.Name}]";

            string expression = columnName;
            object value = simpleCondition.Value;
            string strOpposite = simpleCondition.Opposite ? "not " : "";

            if ((simpleCondition.Value == null || simpleCondition.Value == DBNull.Value) && simpleCondition.Operator == BinaryOperator.Equal)
                return $"{expression} is {strOpposite}null";

            BinaryOperator positiveOp = simpleCondition.Operator;
            if (positiveOp == BinaryOperator.Contains || positiveOp == BinaryOperator.EndsWith || positiveOp == BinaryOperator.StartsWith)
                value = ToSqlLikeValue(Convert.ToString(value));
            int paramIndex = outputParams.Count;
            switch (simpleCondition.Operator)
            {
                case BinaryOperator.Equal:
                    outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), value));
                    {
                        string p = ToSqlParam(paramIndex.ToString());
                        return simpleCondition.Opposite ? $"{expression} <> {p}" : $"{expression} = {p}";
                    }
                case BinaryOperator.GreaterThan:
                    outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), value));
                    {
                        string p = ToSqlParam(paramIndex.ToString());
                        return simpleCondition.Opposite ? $"{expression} <= {p}" : $"{expression} > {p}";
                    }
                case BinaryOperator.LessThan:
                    outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), value));
                    {
                        string p = ToSqlParam(paramIndex.ToString());
                        return simpleCondition.Opposite ? $"{expression} >= {p}" : $"{expression} < {p}";
                    }
                case BinaryOperator.Like:
                    outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), value));
                    return $"{expression} {strOpposite} like {ToSqlParam(paramIndex.ToString())}";
                case BinaryOperator.StartsWith:
                    outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), value));
                    string strlike = ConcatSql(ToSqlParam(paramIndex.ToString()), "'%'");
                    return $"{expression} {strOpposite} like {strlike} escape '{LikeEscapeChar}'";
                case BinaryOperator.EndsWith:
                    outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), value));
                    strlike = ConcatSql("'%'", ToSqlParam(paramIndex.ToString()));       
                    return $"{expression} {strOpposite} like {strlike} escape '{LikeEscapeChar}'";
                case BinaryOperator.Contains:
                    outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), value));
                    strlike = ConcatSql("'%'", ToSqlParam(paramIndex.ToString()), "'%'");
                    return $"{expression} {strOpposite} like {strlike} escape '{LikeEscapeChar}'";
                case BinaryOperator.RegexpLike:
                    outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), value));
                    return $"{strOpposite}regexp_like({expression},{ToSqlParam(paramIndex.ToString())})";
                case BinaryOperator.In:
                    List<string> paramNames = new List<string>();
                    foreach (object item in value as IEnumerable)
                    {
                        outputParams.Add(new KeyValuePair<string, object>(paramIndex.ToString(), item));
                        paramNames.Add(ToSqlParam(paramIndex.ToString()));
                        paramIndex++;
                    }
                    return $"{expression} {strOpposite}in ({String.Join(",", paramNames.ToArray())})";
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
            return $"BEGIN insert into {ToSqlName(tableName)} ({strColumns}) \nvalues ({strValues}); select @@IDENTITY as [ID]; END;";
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
            if (!String.IsNullOrEmpty(where)) where = "\nwhere " + where;
            int endIndex = startIndex + sectionSize;
            return $"select * from (\nselect {select}, Row_Number() over (Order by {orderBy}) as Row_Number \nfrom {from} {where}) TempTable \nwhere Row_Number > {startIndex} and Row_Number <= {endIndex}";
        }

        /// <summary>
        /// 名称转化为数据库合法名称
        /// </summary>
        /// <param name="name">字符串名称</param>
        /// <returns>数据库合法名称</returns>
        public virtual string ToSqlName(string name)
        {
            if (name == null) throw new ArgumentNullException("name");
            return String.Join(".", Array.ConvertAll(name.Split('.'), n => $"[{n}]"));
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


