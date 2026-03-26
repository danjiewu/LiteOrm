using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;

namespace LiteOrm
{
    /// <summary>
    /// 提供常用的数据访问操作基类
    /// </summary>
    /// <remarks>
    /// DAOBase 是一个抽象基类，为各种数据访问对象(DAO)提供通用的操作方法。
    /// 它封装了与数据库交互的常见操作，如生成SQL语句、创建数据库命令、处理参数等。
    /// 通过 AutoRegister 特性自动注册为 Scoped 实例，方便在应用程序中使用依赖注入框架进行管理。
    /// 
    /// 主要功能包括：
    /// 1. SQL语句和命令构建 - 根据对象类型和表定义生成SQL语句
    /// 2. 参数处理 - 将对象属性值转换为数据库参数，并将数据库值转换回对象属性值
    /// 3. 错误处理 - 提供方法检查主键定义和类型匹配，并抛出相应的异常
    /// 4. 预定义变量 - 定义了一些常用的SQL标记，如{Where}、{Table}、{From}和{AllFields}，方便在SQL模板中使用
    /// 5. 扩展性 - 通过虚方法和抽象属性，允许子类根据具体需求重写和扩展功能，如处理视图、添加更多的SQL替换标记等。
    /// 
    /// </remarks>
    [AutoRegister(Lifetime = Lifetime.Scoped)]
    public abstract class DAOBase : IExprStringBuildContext
    {
        #region 预定义变量
        /// <summary>
        /// 表示SQL查询中条件语句的标记
        /// </summary>
        public const string ParamWhere = "{Where}";
        /// <summary>
        /// 表示SQL查询中表名的标记
        /// </summary>
        public const string ParamTable = "{Table}";
        /// <summary>
        /// 表示SQL查询中多表连接的标记
        /// </summary>
        public const string ParamFrom = "{From}";
        /// <summary>
        /// 表示SQL查询中所有字段的标记
        /// </summary>
        public const string ParamAllFields = "{AllFields}";

        /// <summary>
        /// 时间戳参数的内部名称。
        /// </summary>
        protected const string TimestampParamName = "0";
        #endregion

        #region 私人变量
        private string _allFields = null;
        private string _factTableName = null;
        private string _fromTable = null;
        private ArgumentOutOfRangeException _exceptionWrongKeys;
        private Dictionary<SqlColumn, string> _columnSqlCache = new Dictionary<SqlColumn, string>();
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
        /// 是否为视图，默认为 false。对于视图，某些操作（如插入、更新、删除）可能不适用，因此在生成 SQL 语句时需要特殊处理。
        /// </summary>
        protected virtual bool IsView => false;

        /// <summary>
        /// 批量插入提供程序工厂
        /// </summary>
        public BulkProviderFactory BulkFactory { get; set; }

        /// <summary>
        /// 构建SQL语句的SQLBuilder
        /// </summary>
        public virtual SqlBuilder SqlBuilder
        {
            get { return SqlBuilderFactory.Instance.GetSqlBuilder(TableDefinition.DataProviderType, TableDefinition.DataSource); }
        }

        ISqlBuilder IExprStringBuildContext.SqlBuilder => SqlBuilder;

        /// <summary>
        /// 获取当前会话管理器
        /// </summary>
        public virtual SessionManager CurrentSession => SessionManager.Current;

        /// <summary>
        /// 获取当前数据访问对象上下文
        /// </summary>
        public virtual DAOContext DAOContext => CurrentSession.GetDaoContext(TableDefinition.DataSource, IsView);

        /// <summary>
        /// 表名参数
        /// </summary>
        public string[] TableArgs { get; internal set; }

        /// <summary>
        /// 创建 SQL 执行上下文。
        /// </summary>
        /// <param name="initTable">是否在上下文中初始化表信息，默认为 false。对于某些操作（如生成 SQL 语句），可能需要在上下文中包含表信息以正确解析列和别名等细节。</param>
        /// <returns>SQL 构建上下文实例。</returns>
        public virtual SqlBuildContext CreateSqlBuildContext(bool initTable = false)
        {
            if (initTable) return new SqlBuildContext(Table, Constants.DefaultTableAlias, TableArgs) { SingleTable = !IsView };
            else return new SqlBuildContext() { TableArgs = TableArgs, SingleTable = !IsView };
        }

        private SqlBuildContext _initSqlBuildContext;
        /// <summary>
        /// 用于初始化表信息的 SQL 构建上下文，只有在需要初始化表信息时才创建，并且在整个 DAO 生命周期内保持不变，以提高性能。对于某些操作（如生成 SQL 语句），可能需要在上下文中包含表信息以正确解析列和别名等细节，因此提供了这个属性来避免重复创建上下文。子类可以根据需要重写 CreateSqlBuildContext 方法来修改上下文的内容，但 InitSqlBuildContext 将始终返回一个包含表信息的上下文实例。
        /// </summary>
        protected SqlBuildContext InitSqlBuildContext
        {
            get
            {
                if (_initSqlBuildContext == null)
                {
                    _initSqlBuildContext = CreateSqlBuildContext(true);
                }
                return _initSqlBuildContext;
            }
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
        /// 实际表名
        /// </summary>
        public string FactTableName
        {
            get
            {
                if (_factTableName is null)
                {
                    if (TableArgs != null && TableArgs.Length > 0)
                    {
                        _factTableName = String.Format(Table.Name, TableArgs);
                    }
                    else
                    {
                        _factTableName = Table.Name;
                    }
                }
                return _factTableName;
            }
        }

        /// <summary>
        /// 查询时使用的相关联的多个表
        /// </summary>
        protected virtual string From
        {
            get
            {
                if (_fromTable is null)
                {
                    _fromTable = Table.ToSql(InitSqlBuildContext, SqlBuilder);
                }
                return _fromTable;
            }
        }

        /// <summary>
        /// 查询时需要获取的所有列
        /// </summary>
        protected virtual SqlColumn[] SelectColumns => Table.SelectColumns;


        /// <summary>
        /// 查询时需要获取的所有字段的 SQL
        /// </summary>
        protected string AllFields
        {
            get
            {
                if (_allFields is null)
                {
                    _allFields = GetSelectFieldsSql(SelectColumns);
                }
                return _allFields;
            }
        }

        /// <summary>
        /// 将条件字符串转换为 SQL WHERE 子句。
        /// </summary>
        /// <param name="where">条件字符串。</param>
        /// <returns>生成的 WHERE 子句。</returns>
        protected static string ToWhereSql(string where) => string.IsNullOrEmpty(where) ? string.Empty : $"\nWHERE {where}";
        #endregion

        #region 方法

        /// <summary>
        /// 创建 IDbCommand
        /// </summary>
        /// <returns>初始化好的数据库命令代理实例。</returns>
        public virtual DbCommandProxy NewCommand()
        {
            DAOContext.EnsureTable(ObjectType, TableArgs);
            return DAOContext.CreateCommand();
        }

        /// <summary>
        /// 生成 select 部分的 SQL
        /// </summary>
        /// <param name="selectColumns">需要 select 的列集合</param>
        /// <returns>生成的 SQL</returns>
        protected string GetSelectFieldsSql(IEnumerable<SqlColumn> selectColumns)
        {
            Span<char> initialBuffer = stackalloc char[256];
            var strAllFields = new ValueStringBuilder(initialBuffer);
            SqlColumn[] columns = selectColumns as SqlColumn[] ?? selectColumns.ToArray();
            int len = columns.Length;
            for (int i = 0; i < len; i++)
            {
                SqlColumn column = columns[i];
                if (i > 0) strAllFields.Append(",");
                strAllFields.Append(column.ToSql(InitSqlBuildContext, SqlBuilder));
                if (!String.Equals(column.Name, column.PropertyName, StringComparison.OrdinalIgnoreCase))
                {
                    strAllFields.Append(" AS ");
                    strAllFields.Append(SqlBuilder.ToSqlName(column.PropertyName));
                }
            }
            string result = strAllFields.ToString();
            strAllFields.Dispose();
            return result;
        }

        /// <summary>
        /// 获取预定义的 DbCommand
        /// </summary>
        /// <param name="methodName">方法名称</param>
        /// <param name="newCommandHandler">新的命令处理器</param>
        /// <returns>与方法名称关联的已缓存或新建的数据库命令代理实例。</returns>
        protected DbCommandProxy GetPreparedCommand(string methodName, Func<DbCommandProxy> newCommandHandler)
        {
            if (TableArgs != null && Table.Columns.Length > 0) methodName += String.Join("_", TableArgs);
            return DAOContext.PreparedCommands.GetOrAdd((ObjectType, methodName), _ => newCommandHandler());
        }

        /// <summary>
        /// 根据 SQL 语句和命名的参数建立 <see cref="IDbCommand"/>。
        /// </summary>
        /// <param name="sql">SQL 语句，SQL 中可以包含已命名的参数。</param>
        /// <param name="paramValues">参数列表，为空时表示没有参数。Key 需要与 SQL 中的参数名称对应。</param>
        /// <param name="replaceHandler">自定义替换方法，返回替换值或null表示使用默认替换。为空时使用默认替换。</param>
        /// <returns>IDbCommand 实例。</returns>
        protected DbCommandProxy MakeNamedParamCommand(string sql, IEnumerable<KeyValuePair<string, object>> paramValues, Func<string, string> replaceHandler = null)
        {
            DbCommandProxy command = NewCommand();
            command.CommandText = MutiReplacerInstance.Replace(sql, replaceHandler);
            if (paramValues is not null)
                foreach (KeyValuePair<string, object> para in paramValues)
                {
                    DbParameter param = command.CreateParameter();
                    param.ParameterName = ToParamName(ToNativeName(para.Key));
                    param.Value = SqlBuilder.ConvertToDbValue(para.Value);
                    command.Parameters.Add(param);
                }
            return command;
        }

        /// <summary>
        /// 根据表达式创建查询命令
        /// </summary>
        /// <param name="expr">查询条件表达式</param>
        /// <returns>生成的查询命令</returns>
        /// <exception cref="ArgumentException"></exception>
        protected DbCommandProxy MakeSelectExprCommand(Expr expr)
        {
            SelectExpr selectExpr;
            if (expr is SelectExpr selectExpr1)
            {
                selectExpr = selectExpr1;
            }
            else
            {
                selectExpr = new SelectExpr()
                {
                    Source = expr.ToSource(ObjectType),
                    Selects = SelectColumns.Select((col, i) => new SelectItemExpr(Expr.Prop(col.PropertyName), col.PropertyName)).ToList()
                };
            }
            return MakeExprCommand(selectExpr);
        }

        /// <summary>
        /// 根据表达式创建命令
        /// </summary>
        /// <param name="expr">表达式</param>
        /// <returns>根据表达式生成的数据库命令代理实例。</returns>
        /// <exception cref="ArgumentNullException"></exception>
        protected DbCommandProxy MakeExprCommand(Expr expr)
        {
            if (expr is null) throw new ArgumentNullException(nameof(expr));
            List<KeyValuePair<string, object>> paramList = new List<KeyValuePair<string, object>>();
            var context = CreateSqlBuildContext();
            return MakeNamedParamCommand(expr.ToSql(context, SqlBuilder, paramList), paramList);
        }

        /// <summary>
        /// 执行带有命名参数的 SQL 语句，并返回结果值。SQL 语句可以包含 Expr 或变量值。
        /// </summary>
        /// <typeparam name="T">结果类型</typeparam>
        /// <param name="sqlBody">查询SQL，使用插值字符串格式，可插入普通变量或 Expr。<see cref="LiteOrm.Common.ExprString"/></param>
        /// <returns>包含查询结果的值结果对象。</returns>
        public virtual ValueResult<T> GetValue<T>([InterpolatedStringHandlerArgument("")] ref ExprString sqlBody)
        {
            var command = MakeNamedParamCommand(sqlBody.GetSqlResult(), sqlBody.GetParams());
            return new ValueResult<T>(command);
        }

        /// <summary>
        /// 执行带有命名参数的 SQL 语句，并返回受影响的行数。SQL 语句可以包含 Expr 或变量值。
        /// </summary>
        /// <param name="sqlBody">查询SQL，使用插值字符串格式，可插入普通变量或 Expr。<see cref="LiteOrm.Common.ExprString"/></param>
        /// <returns>包含受影响行数的非查询结果对象。</returns>
        public virtual NonQueryResult Execute([InterpolatedStringHandlerArgument("")] ref ExprString sqlBody)
        {
            var command = MakeNamedParamCommand(sqlBody.GetSqlResult(), sqlBody.GetParams());
            return new NonQueryResult(command);
        }

        /// <summary>
        /// 执行带有命名参数的 SQL 语句，并返回结果集。SQL 语句可以包含 Expr 或变量值。
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="sqlBody">查询SQL，使用插值字符串格式，可插入普通变量或 Expr。<see cref="LiteOrm.Common.ExprString"/></param>
        /// <param name="readerFunc">用于从 DbDataReader 读取结果的函数，为空时默认使用 <see cref="DataReaderConverter.GetConverter{TResult}()"/></param>
        /// <returns>包含查询结果集的可枚举结果对象。</returns>
        public virtual EnumerableResult<TResult> Query<TResult>([InterpolatedStringHandlerArgument("")] ref ExprString sqlBody, Func<DbDataReader, TResult> readerFunc = null)
        {
            var command = MakeNamedParamCommand(sqlBody.GetSqlResult(), sqlBody.GetParams());
            return new EnumerableResult<TResult>(command, readerFunc);
        }

        /// <summary>
        /// 生成替换标记的默认字符串的字典，标记为以下之一： {Table}、{From} 和 {AllFields}，子类可以重写此方法添加更多的替换标记或修改现有标记的值。
        /// </summary>
        /// <returns>包含标记与对应替换值的字典。</returns>
        protected virtual Dictionary<string, string> GetReplacements()
        {
            return new Dictionary<string, string>
            {
                { ParamTable, FactTableName },
                { ParamFrom, From },
                { ParamAllFields, AllFields }
            };
        }

        /// <summary>
        /// MutiReplacer实例
        /// </summary>
        private MultiReplacer _mutiReplacer;
        private MultiReplacer MutiReplacerInstance
        {
            get
            {
                if (_mutiReplacer == null)
                {
                    _mutiReplacer = new MultiReplacer();
                    foreach (var replacement in GetReplacements())
                    {
                        _mutiReplacer.Insert(replacement.Key, replacement.Value);
                    }
                }
                return _mutiReplacer;
            }
        }

        /// <summary>
        /// 为command创建根据主键查询的条件，在command中添加参数并返回where条件的语句
        /// </summary>
        /// <param name="command">要创建条件的数据库命令</param>
        /// <returns>where条件的语句</returns>
        protected string MakeKeyCondition(DbCommand command)
        {
            ThrowExceptionIfNoKeys();
            var strConditions = ValueStringBuilder.Create(128);
            var keys = Table.Keys;
            int count = keys.Length;
            for (int i = 0; i < count; i++)
            {
                ColumnDefinition key = keys[i];
                if (i > 0) strConditions.Append(" AND ");
                strConditions.Append(ToColumnSql(key));
                strConditions.Append(" = ");
                strConditions.Append(ToSqlParam(key.PropertyName));

                if (!command.Parameters.Contains(key.PropertyName))
                {
                    DbParameter param = command.CreateParameter();
                    param.Size = key.Length;
                    param.DbType = key.DbType;
                    param.ParameterName = ToParamName(key.PropertyName);
                    command.Parameters.Add(param);
                }
            }
            string result = strConditions.ToString();
            strConditions.Dispose();
            return result;
        }


        /// <summary>
        /// 为command创建根据时间戳的条件，在command中添加参数并返回where条件的语句
        /// </summary>
        /// <param name="command">要创建条件的数据库命令</param>
        /// <param name="timestamp">时间戳</param>
        /// <returns>where条件的语句</returns>
        protected string MakeTimestampCondition(DbCommand command, object timestamp)
        {
            foreach (var column in Table.Columns)
            {
                var columnDef = column.Definition;
                if (columnDef.IsTimestamp)
                {
                    DbParameter param = command.CreateParameter();
                    param.Size = columnDef.Length;
                    param.DbType = columnDef.DbType;
                    param.ParameterName = ToParamName(TimestampParamName);
                    param.Value = ConvertToDbValue(timestamp, columnDef.DbType);
                    command.Parameters.Add(param);
                    return $"{ToColumnSql(column)} = {ToSqlParam(TimestampParamName)}";
                }
            }
            return null;
        }

        /// <summary>
        /// 数据列转换为 SQL 格式名称
        /// </summary>
        /// <param name="column">数据列</param>
        /// <returns>数据列对应的 SQL 名称</returns>
        protected string ToColumnSql(SqlColumn column)
        {
            if (_columnSqlCache.TryGetValue(column, out string cachedSql))
            {
                return cachedSql;
            }
            string sql = column.ToSql(InitSqlBuildContext, SqlBuilder);
            _columnSqlCache[column] = sql;
            return sql;
        }

        /// <summary>
        /// 获取对象的主键值
        /// </summary>
        /// <param name="o">对象</param>
        /// <returns>主键值，多个主键按照属性名称顺序排列</returns>
        protected virtual object[] GetKeyValues<T>(T o)
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
        protected virtual object ConvertFromDbValue(object dbValue, Type objectType)
        {
            return SqlBuilder.ConvertFromDbValue(dbValue, objectType);
        }

        /// <summary>
        /// 将对象的属性值转化为数据库中的值，根据 DbType 进行转换
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="dbType">数据库类型</param>
        /// <returns>数据库中的值</returns>
        protected virtual object ConvertToDbValue(object value, DbType dbType)
        {
            return SqlBuilder.ConvertToDbValue(value, dbType);
        }

        /// <summary>
        /// 如果没有定义主键则抛出异常
        /// </summary>
        /// <exception cref="Exception"></exception>
        protected void ThrowExceptionIfNoKeys()
        {
            if (TableDefinition.Keys.Length == 0)
            {
                throw new Exception($"No key definition found in type \"{Table.DefinitionType.FullName}\", please set the value of property \"IsPrimaryKey\" of key column to true.");
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
                throw new Exception($"Type {type.FullName} not match object type {ObjectType.FullName}.");
            }
        }

        /// <summary>
        /// 如果主键数量不匹配则抛出异常
        /// </summary>
        /// <param name="keys"></param>
        protected void ThrowExceptionIfWrongKeys(params object[] keys)
        {
            if (keys is null) throw new ArgumentNullException("keys");
            if (keys.Length != TableDefinition.Keys.Length)
            {
                if (_exceptionWrongKeys is null)
                {
                    List<string> strKeys = new List<string>();
                    foreach (ColumnDefinition key in TableDefinition.Keys) strKeys.Add(key.Name);
                    _exceptionWrongKeys = new ArgumentOutOfRangeException(nameof(keys), $"Wrong keys' number. Type \"{Table.DefinitionType.FullName}\" has {strKeys.Count} key(s):'{String.Join("','", strKeys.ToArray())}'.");
                }
                throw _exceptionWrongKeys;
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
}
