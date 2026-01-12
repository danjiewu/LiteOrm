using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Data;
using MyOrm.Common;
using System.Data.Common;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using MyOrm.Service;
using System.Linq;

namespace MyOrm
{
    /// <summary>
    /// 提供常用的数据访问操作基类
    /// </summary>
    /// <remarks>
    /// ObjectDAOBase 是一个抽象基类，为各种数据访问对象(DAO)提供通用的操作方法。
    /// 它封装了与数据库交互的常见操作，如生成SQL语句、创建数据库命令、处理参数等。
    /// 
    /// 主要功能包括：
    /// 1. SQL语句和命令构建 - 根据对象类型和表定义生成SQL语句
    /// 2. 参数处理 - 处理带参数的SQL语句和命名参数
    /// 3. 条件构建 - 基于条件对象生成WHERE子句
    /// 4. 数据转换 - 在数据库值和对象属性值之间进行转换
    /// 5. 排序和字段选择 - 处理ORDER BY子句和SELECT字段列表
    /// 
    /// 该类通过依赖注入框架自动注册为单例。
    /// </remarks>
    [AutoRegister(Lifetime = ServiceLifetime.Singleton)]
    public abstract class ObjectDAOBase
    {
        #region 预定义变量
        /// <summary>
        /// 表示SQL查询中条件语句的标记
        /// </summary>
        public const string ParamCondition = "@Condition@";
        /// <summary>
        /// 表示SQL查询中表名的标记
        /// </summary>
        public const string ParamTable = "@Table@";
        /// <summary>
        /// 表示SQL查询中多表连接的标记
        /// </summary>
        public const string ParamFromTable = "@FromTable@";
        /// <summary>
        /// 表示SQL查询中所有字段的标记
        /// </summary>
        public const string ParamAllFields = "@AllFields@";

        /// <summary>
        /// 时间戳参数的内部名称。
        /// </summary>
        protected const string TimestampParamName = "0";
        #endregion

        #region 私有变量
        private ReadOnlyCollection<SqlColumn> selectColumns;
        private string allFieldsSql = null;
        private string tableName = null;
        private string fromTable = null;
        private ArgumentOutOfRangeException ExceptionWrongKeys;
        #endregion

        #region 属性
        /// <summary>
        /// 实体对象类型
        /// </summary>
        public abstract Type ObjectType
        {
            get;
        }

        /// <summary>
        /// 表信息
        /// </summary>
        public abstract SqlTable Table
        {
            get;
        }

        /// <summary>
        /// 表定义
        /// </summary>
        public TableDefinition TableDefinition
        {
            get { return Table.Definition; }
        }

        /// <summary>
        /// 构建SQL语句的SQLBuilder
        /// </summary>
        protected internal virtual SqlBuilder SqlBuilder
        {
            get { return SqlBuilderFactory.Instance.GetSqlBuilder(TableDefinition.DataProviderType); }
        }

        /// <summary>
        /// 获取当前会话管理器
        /// </summary>
        public SessionManager CurrentSession => SessionManager.Current;

        /// <summary>
        /// 获取当前数据访问对象上下文
        /// </summary>
        public DAOContext DAOContext { get => CurrentSession.GetDaoContext(TableDefinition.DataSource); }

        /// <summary>
        /// 表名参数
        /// </summary>
        public string[] TableNameArgs { get; set; }

        /// <summary>
        /// 使用指定的参数创建新的DAO实例
        /// </summary>
        /// <param name="args">表名参数</param>
        /// <returns>新的DAO实例</returns>
        public object WithArgs(params string[] args)
        {
            ObjectDAOBase newDAO = MemberwiseClone() as ObjectDAOBase;
            newDAO.TableNameArgs = args;
            return newDAO;
        }

        private SqlBuildContext sqlBuildContext;
        /// <summary>
        /// 创建SQL执行上下文
        /// </summary>
        /// <returns></returns>
        protected virtual SqlBuildContext SqlBuildContext
        {
            get
            {
                if (sqlBuildContext is null) sqlBuildContext = new SqlBuildContext() { Table = Table, TableInfoProvider = TableInfoProvider, TableNameArgs = TableNameArgs };
                return sqlBuildContext;

            }
            set { sqlBuildContext = value; }
        }

        /// <summary>
        /// 表信息提供者
        /// </summary>
        public TableInfoProvider TableInfoProvider
        {
            get; set;
        }

        /// <summary>
        /// 数据库连接
        /// </summary>
        public DbConnection Connection
        {
            get { return DAOContext.DbConnection; }
        }

        /// <summary>
        /// 表名
        /// </summary>
        public string TableName
        {
            get
            {
                if (tableName is null) tableName = Table.Name;
                return tableName;
            }
        }

        /// <summary>
        /// 实际表名
        /// </summary>
        public string FactTableName { get { return SqlBuildContext.GetTableNameWithArgs(TableDefinition.Name); } }

        /// <summary>
        /// 查询时使用的相关联的多个表
        /// </summary>
        protected virtual string From
        {
            get
            {
                if (fromTable is null)
                {
                    fromTable = SqlBuildContext.GetTableNameWithArgs(Table.FormattedExpression(SqlBuilder));
                }
                return fromTable;
            }
        }

        /// <summary>
        /// 查询时需要获取的所有列
        /// </summary>
        protected virtual ReadOnlyCollection<SqlColumn> SelectColumns
        {
            get
            {
                if (selectColumns is null)
                {
                    selectColumns = Table.Columns.Where(column =>
                    {
                        while (column is ForeignColumn foreignColumn) column = foreignColumn.TargetColumn.Column;
                        if (column is ColumnDefinition columnDefinition)
                            return columnDefinition.Mode.CanRead();
                        else
                            return true;
                    }).ToList().AsReadOnly();
                }
                return selectColumns;
            }
        }

        /// <summary>
        /// 查询时需要获取的所有字段的Sql
        /// </summary>
        protected string AllFieldsSql
        {
            get
            {
                if (allFieldsSql is null)
                {
                    allFieldsSql = GetSelectFieldsSQL(SelectColumns);
                }
                return allFieldsSql;
            }
        }
        #endregion

        #region 方法

        /// <summary>
        /// 创建IDbCommand
        /// </summary>
        /// <returns></returns>
        public virtual IDbCommand NewCommand()
        {
            return new DbCommandProxy(DAOContext, SqlBuilder);
        }

        /// <summary>
        /// 生成select部分的sql
        /// </summary>
        /// <param name="selectColumns">需要select的列集合</param>
        /// <returns>生成的sql</returns>
        protected string GetSelectFieldsSQL(IEnumerable<SqlColumn> selectColumns)
        {
            StringBuilder strAllFields = new StringBuilder();
            foreach (SqlColumn column in selectColumns)
            {
                if (strAllFields.Length != 0) strAllFields.Append(",");
                strAllFields.Append(column.FormattedExpression(SqlBuilder));
                if (!String.Equals(column.Name, column.PropertyName, StringComparison.OrdinalIgnoreCase)) strAllFields.Append(" " + SqlBuilder.ToSqlName(column.PropertyName));
            }
            return strAllFields.ToString();
        }

        /// <summary>
        /// 生成orderby部分的sql
        /// </summary>
        /// <param name="orders">排序项的集合，按优先级顺序排列</param>
        /// <returns></returns>
        protected string GetOrderBySQL(IList<Sorting> orders)
        {
            StringBuilder orderBy = new StringBuilder();
            if (orders is null || orders.Count == 0)
            {
                if (TableDefinition.Keys.Count != 0)
                {
                    foreach (ColumnDefinition key in TableDefinition.Keys)
                    {
                        if (orderBy.Length != 0) orderBy.Append(",");
                        orderBy.AppendFormat("{0}.{1}", Table.FormattedName(SqlBuilder), key.FormattedName(SqlBuilder));
                    }
                }
                else
                {
                    //TODO: OrderBy one column or all columns?
                    throw new Exception("No columns or keys to sort by.");
                }
            }
            else
            {
                foreach (Sorting sorting in orders)
                {
                    SqlColumn column = Table.GetColumn(sorting.PropertyName);
                    if (column is null) throw new ArgumentException(String.Format("Type \"{0}\" does not have property \"{1}\"", ObjectType.Name, sorting.PropertyName), "section");
                    if (orderBy.Length > 0) orderBy.Append(",");
                    orderBy.Append(column.FormattedExpression(SqlBuilder));
                    orderBy.Append(sorting.Direction == ListSortDirection.Ascending ? " asc" : " desc");
                }
            }
            return orderBy.ToString();
        }

        /// <summary>
        /// 根据SQL语句和参数建立IDbCommand
        /// </summary>
        /// <param name="SQL">SQL语句，SQL中可以包含参数信息，参数名为以0开始的递增整数，对应paramValues中值的下标</param>
        /// <param name="paramValues">参数值，需要与SQL中的参数一一对应，为空时表示没有参数</param>
        /// <returns>IDbCommand</returns>
        public IDbCommand MakeParamCommand(string SQL, IEnumerable paramValues)
        {
            int paramIndex = 0;
            List<KeyValuePair<string, object>> paramList = new List<KeyValuePair<string, object>>();
            if (paramValues is not null)
                foreach (object paramValue in paramValues)
                {
                    paramList.Add(Convert.ToString(paramIndex++), paramValue);

                }
            return MakeNamedParamCommand(SQL, paramList);
        }

        /// <summary>
        /// 根据SQL语句和参数建立IDbCommand
        /// </summary>
        /// <param name="SQL">SQL语句，SQL中可以包含参数信息，参数名为以0开始的递增整数，对应paramValues中值的下标</param>
        /// <param name="paramValues">参数值，需要与SQL中的参数一一对应，为空时表示没有参数</param>
        /// <returns>IDbCommand</returns>
        public IDbCommand MakeParamCommand(string SQL, params object[] paramValues)
        {
            return MakeParamCommand(SQL, (IEnumerable)paramValues);
        }

        /// <summary>
        /// 根据 SQL 语句和命名的参数建立 <see cref="IDbCommand"/>。
        /// </summary>
        /// <param name="SQL">SQL 语句，SQL 中可以包含已命名的参数。</param>
        /// <param name="paramValues">参数列表，为空时表示没有参数。Key 需要与 SQL 中的参数名称对应。</param>
        /// <param name="context">SQL 构建上下文。</param>
        /// <returns>IDbCommand 实例。</returns>
        public IDbCommand MakeNamedParamCommand(string SQL, IEnumerable<KeyValuePair<string, object>> paramValues, SqlBuildContext context = null)   
        {
            IDbCommand command = NewCommand();
            command.CommandText = ReplaceParam(SQL, context);
            AddParamsToCommand(command, paramValues);
            return command;
        }

        /// <summary>
        /// 将参数添加到IDbCommand中
        /// </summary>
        /// <param name="command">需要添加参数的IDbCommand</param>
        /// <param name="paramValues">参数列表，包括参数名称和值，为空时表示没有参数</param>
        public void AddParamsToCommand(IDbCommand command, IEnumerable<KeyValuePair<string, object>> paramValues)
        {
            if (paramValues is not null)
                foreach (KeyValuePair<string, object> paramSet in paramValues)
                {
                    IDbDataParameter param = command.CreateParameter();
                    param.ParameterName = ToParamName(ToNativeName(paramSet.Key));
                    object value = paramSet.Value ?? DBNull.Value;
                    if (value is Enum || value.GetType() == typeof(bool))
                    {
                        value = Convert.ToInt32(value);
                    }
                    param.Value = value;
                    command.Parameters.Add(param);
                }
        }

        /// <summary>
        /// 根据SQL语句和条件建立IDbCommand
        /// </summary>
        /// <param name="SQLWithParam">带参数的SQL语句
        /// <example>"select @AllFields@ from @FromTable@ where @Condition@"表示从表中查询所有符合条件的记录</example>
        /// <example>"select count(*) from @FromTable@ "表示从表中所有记录的数量，expr参数需为空</example>
        /// <example>"delete from @Table@ where @Condition@"表示从表中删除所有符合条件的记录</example>
        /// </param>
        /// <param name="expr">条件，为null时表示无条件</param>
        /// <returns>IDbCommand</returns>
        public IDbCommand MakeConditionCommand(string SQLWithParam, Expr expr)
        {
            List<KeyValuePair<string, object>> paramList = new List<KeyValuePair<string, object>>();
            var context = SqlBuildContext;
            string strCondition = null;
            if (expr is not null)
            {
                strCondition = expr.ToSql(context, SqlBuilder, paramList);
            }

            if (String.IsNullOrEmpty(strCondition)) strCondition = " 1 = 1 ";
            return MakeNamedParamCommand(SQLWithParam.Replace(ParamCondition, strCondition), paramList);
        }

        /// <summary>
        /// 替换Sql中的标记为实际Sql
        /// </summary>
        /// <param name="SQLWithParam">包含标记的Sql语句</param>
        /// <param name="context">Sql生成的上下文</param>
        /// <returns></returns>
        protected virtual string ReplaceParam(string SQLWithParam, SqlBuildContext context = null)
        {
            if (context is null)
            {
                context = SqlBuildContext;
            }
            string tableName = context.GetTableNameWithArgs(Table.FormattedName(SqlBuilder));
            return SQLWithParam.Replace(ParamTable, tableName).Replace(ParamFromTable, From);
        }

        /// <summary>
        /// 为command创建根据主键查询的条件，在command中添加参数并返回where条件的语句
        /// </summary>
        /// <param name="command">要创建条件的数据库命令</param>
        /// <returns>where条件的语句</returns>
        protected string MakeIsKeyCondition(IDbCommand command)
        {
            ThrowExceptionIfNoKeys();
            StringBuilder strConditions = new StringBuilder();
            foreach (ColumnDefinition key in TableDefinition.Keys)
            {
                if (strConditions.Length != 0) strConditions.Append(" and ");
                string columnName = SqlBuildContext.SingleTable ? key.FormattedName(SqlBuilder) : key.FormattedExpression(SqlBuilder);
                strConditions.AppendFormat("{0} = {1}", columnName, ToSqlParam(key.PropertyName));
                if (!command.Parameters.Contains(key.PropertyName))
                {
                    IDbDataParameter param = command.CreateParameter();
                    param.Size = key.Length;
                    param.DbType = key.DbType;
                    param.ParameterName = ToParamName(key.PropertyName);
                    command.Parameters.Add(param);
                }
            }
            return strConditions.ToString();
        }

        /// <summary>
        /// 为command创建根据时间戳的条件，在command中添加参数并返回where条件的语句
        /// </summary>
        /// <param name="command">要创建条件的数据库命令</param>
        /// <param name="isView">是否是查询，若是查询则关联多个表</param>
        /// <returns>where条件的语句</returns>
        protected string MakeTimestampCondition(IDbCommand command, bool isView = true)
        {
            foreach (ColumnDefinition column in TableDefinition.Columns)
            {
                if (column.IsTimestamp)
                {
                    IDbDataParameter param = command.CreateParameter();
                    param.Size = column.Length;
                    param.DbType = column.DbType;
                    param.ParameterName = ToParamName(TimestampParamName);
                    command.Parameters.Add(param);
                    return String.Format("{0}.{1} = {2}", ToSqlName(isView ? FactTableName : TableDefinition.Name), ToSqlName(column.Name), ToSqlParam(TimestampParamName)); ;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取对象的主键值
        /// </summary>
        /// <param name="o">对象</param>
        /// <returns>主键值，多个主键按照属性名称顺序排列</returns>
        protected virtual object[] GetKeyValues(object o)
        {
            List<object> values = new List<object>();
            foreach (ColumnDefinition key in TableDefinition.Keys)
            {
                values.Add(key.GetValue(o));
            }
            return values.ToArray();
        }

        /// <summary>
        /// 将数据库取得的值转化为对象属性类型所对应的值
        /// </summary>
        /// <param name="dbValue">数据库取得的值</param>
        /// <param name="objectType">对象属性的类型</param>
        /// <returns>对象属性类型所对应的值</returns>
        protected virtual object ConvertValue(object dbValue, Type objectType)
        {
            if (dbValue is null || dbValue == DBNull.Value)
                return null;

            objectType = Nullable.GetUnderlyingType(objectType) ?? objectType;

            if (objectType.IsInstanceOfType(dbValue))
                return dbValue;
            //else if (objectType == typeof(TimeSpan)) return TimeSpan.FromTicks(Convert.ToInt64(dbValue));
            else if (objectType.IsEnum && (dbValue.GetType().IsValueType || dbValue.GetType() == typeof(string))) return Enum.ToObject(objectType, Convert.ToInt32(dbValue));
            else if (objectType == typeof(bool) && dbValue is string && bool.TryParse((string)dbValue, out bool result))
            {
                // 处理字符串类型的布尔值
                return result;
            }
            if (objectType == typeof(bool))
            {
                // 处理整数类型的布尔值
                return Convert.ToInt32(dbValue) != 0;
            }
            return Convert.ChangeType(dbValue, objectType);
        }

        /// <summary>
        /// 将对象的属性值转化为数据库中的值
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="column">列定义</param>
        /// <returns>数据库中的值</returns>
        protected virtual object ConvertToDBValue(object value, ColumnDefinition column)//TODO:
        {
            if (value is null) return DBNull.Value;
            //Type objectType = column.PropertyType;
            //objectType = Nullable.GetUnderlyingType(objectType) ?? objectType;
            //if (objectType == typeof(TimeSpan)) return ((TimeSpan)value).Ticks;
            return value;
        }


        /// <summary>
        /// 如果没有定义主键则抛出异常
        /// </summary>
        /// <exception cref="Exception"></exception>
        protected void ThrowExceptionIfNoKeys()
        {
            if (TableDefinition.Keys.Count == 0)
            {
                throw new Exception(String.Format("No key definition found in type \"{0}\", please set the value of property \"IsPrimaryKey\" of key column to true.", Table.DefinitionType.FullName));
            }
        }

        /// <summary>
        /// 如果类型不匹配则抛出异常
        /// </summary>
        /// <param name="type"></param>
        /// <exception cref="Exception"></exception>
        protected void ThrowExceptionIfTypeNotMatch(Type type)
        {
            if (!ObjectType.IsAssignableFrom(type))
            {
                throw new Exception(String.Format("Type {0} not match object type {1}.", type.FullName, ObjectType.FullName));
            }
        }

        /// <summary>
        /// 如果主键数量不匹配则抛出异常
        /// </summary>
        /// <param name="keys"></param>
        protected void ThrowExceptionIfWrongKeys(params object[] keys)
        {
            if (keys.Length != TableDefinition.Keys.Count)
            {
                if (ExceptionWrongKeys is null)
                {
                    List<string> strKeys = new List<string>();
                    foreach (ColumnDefinition key in TableDefinition.Keys) strKeys.Add(key.Name);
                    ExceptionWrongKeys = new ArgumentOutOfRangeException("keys", String.Format("Wrong keys' number. Type \"{0}\" has {1} key(s):'{2}'.", Table.DefinitionType.FullName, strKeys.Count, String.Join("','", strKeys.ToArray())));
                }
                throw ExceptionWrongKeys;
            }
        }

        /// <summary>
        /// 将名称转换为SQL中的名称格式
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        protected string ToSqlName(string name)
        {
            return SqlBuilder.ToSqlName(name);
        }

        /// <summary>
        /// 将名称转换为SQL参数的名称格式
        /// </summary>
        /// <param name="nativeName"></param>
        /// <returns></returns>
        protected string ToSqlParam(string nativeName)
        {
            return SqlBuilder.ToSqlParam(nativeName);
        }

        /// <summary>
        /// 将名称转换为SQL参数的名称格式
        /// </summary>
        /// <param name="nativeName"></param>
        /// <returns></returns>
        protected string ToParamName(string nativeName)
        {
            return SqlBuilder.ToParamName(nativeName);
        }

        /// <summary>
        /// 将参数名称转换为本地名称格式
        /// </summary>
        /// <param name="paramName"></param>
        /// <returns></returns>
        protected string ToNativeName(string paramName)
        {
            return SqlBuilder.ToNativeName(paramName);
        }
        #endregion
    }

    /// <summary>
    /// ObjectDAOBase 的扩展方法类
    /// </summary>
    /// <remarks>
    /// ObjectDAOExt 提供了 IObjectDAO&lt;T&gt; 和 IObjectViewDAO&lt;T&gt; 接口的扩展方法。
    /// 
    /// 主要功能：
    /// 1. WithArgs 扩展方法 - 为DAO对象设置动态表名参数
    /// 
    /// 这些扩展方法提供了一种流畅的API来处理参数化的表名，
    /// 允许在运行时动态指定表名或其他参数。
    /// 
    /// 使用示例：
    /// <code>
    /// var dao = serviceProvider.GetService&lt;IObjectDAO&lt;User&gt;&gt;();
    /// // 为表名参数创建新的DAO实例
    /// var specificTableDao = dao.WithArgs("User_2024");
    /// // 进行数据操作
    /// var users = await specificTableDao.SearchAsync(Expr.Property("Age") &gt; 18);
    /// </code>
    /// </remarks>
    public static class ObjectDAOExt
    {
        /// <summary>
        /// 使用指定的参数创建新的DAO实例
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dao"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static IObjectDAO<T> WithArgs<T>(this IObjectDAO<T> dao, params string[] args)
        {
            if (args is null || args.Length == 0) return dao;
            ObjectDAOBase dAOBase = dao as ObjectDAOBase;
            return dAOBase.WithArgs(args) as IObjectDAO<T>;
        }
        /// <summary>
        /// 使用指定的参数创建新的DAO实例
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dao"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static IObjectViewDAO<T> WithArgs<T>(this IObjectViewDAO<T> dao, params string[] args)
        {
            if (args is null || args.Length == 0) return dao;
            ObjectDAOBase dAOBase = dao as ObjectDAOBase;
            return dAOBase.WithArgs(args) as IObjectViewDAO<T>;
        }
    }
}