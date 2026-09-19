using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// 提供常用的数据访问操作基类
    /// </summary>
    /// <param name="sessionManager">会话管理器，由依赖注入容器自动解析。</param>
    /// <remarks>
    /// DAOBase 是一个抽象基类，为各种数据访问对象(DAO)提供通用的操作方法。
    /// 它封装了与数据库交互的常见操作，如生成SQL语句、创建数据库命令、处理参数等。
    /// 
    /// 依赖注入由 LiteOrm 核心（<c>AddLiteOrm</c>）与 LiteOrm.DependencyInjection（<c>RegisterLiteOrm</c>）显式注册；
    /// 各 DAO 基类亦带 <c>[AutoRegister]</c>，使派生类可自动注册。
    /// 
    /// 主要功能包括：
    /// 1. SQL语句和命令构建 - 根据对象类型和表定义生成SQL语句
    /// 2. 参数处理 - 将对象属性值转换为数据库参数，并将数据库值转换回对象属性值
    /// 3. 错误处理 - 提供方法检查主键定义和类型匹配，并抛出相应的异常
    /// 4. 预定义变量 - 定义了一些常用的SQL标记，如{Where}、{Table}、{From}和{AllFields}，方便在SQL模板中使用
    /// 5. 扩展性 - 通过虚方法和抽象属性，允许子类根据具体需求重写和扩展功能，如处理视图、添加更多的SQL替换标记等。
    /// 
    /// </remarks>
    public abstract class DAOBase(SessionManager sessionManager) : IExprStringBuildContext
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
        #endregion

        #region 私有变量
        private string? _allFields = null;
        private string? _factTableName = null;
        private string? _fromTable = null;
        private ArgumentOutOfRangeException? _exceptionWrongKeys;
        private Dictionary<SqlColumn, string> _columnSqlCache = new Dictionary<SqlColumn, string>();
        #endregion

        #region 属性
        /// <summary>
        /// 实体对象类型
        /// </summary>
        [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
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
        /// 数据源名称，默认为表定义中的数据源名称。对于某些操作（如获取数据库连接），可能需要使用数据源名称来确定连接字符串或数据库提供程序，因此在 DAO 中提供了这个属性来方便访问。子类可以根据需要重写这个属性来修改数据源名称，例如在多租户应用中可能需要根据当前租户动态返回不同的数据源名称。
        /// </summary>
        protected virtual string? DataSource => TableDefinition.DataSource;


        /// <summary>
        /// 是否为视图，默认为 false。对于视图，某些操作（如插入、更新、删除）可能不适用，因此在生成 SQL 语句时需要特殊处理。
        /// </summary>
        protected virtual bool IsView => false;

        /// <summary>
        /// 构建SQL语句的SQLBuilder
        /// </summary>
        public virtual SqlBuilder SqlBuilder
        {
            get
            {
                SqlBuilder sqlBuilder = field ?? (field = Session.GetDAOContextPool(DataSource)!.SqlBuilder);
                // 惰性集中初始化该表各列的数据库值转换器（表内以标记去重，避免重复查找）
                Table.EnsureConverters(sqlBuilder);
                return sqlBuilder;
            }
        }

        /// <summary>
        /// 获取当前数据源对应的数据库提供程序类型。
        /// 通过 <see cref="SessionManager.GetDAOContextPool"/> 根据数据源名称查找连接池，再从连接池获取提供程序类型。
        /// </summary>
        /// <returns>数据库提供程序类型；若当前会话未设置或数据源不存在则返回 null。</returns>
        protected virtual Type? GetProviderType()
        {
            return Session.GetDAOContextPool(DataSource)?.ProviderType;
        }

        /// <summary>
        /// 会话管理器，由依赖注入容器在构造 DAO 时自动注入。
        /// </summary>
        /// <remarks>
        /// 替代原先内部依赖的 <see cref="SessionManager.Current"/>；DAO 不再依赖全局静态会话。
        /// </remarks>
        public SessionManager Session => sessionManager;

        /// <summary>
        /// 获取当前数据访问对象上下文
        /// </summary>
        public virtual DAOContext GetDaoContext()
        {
            var daoContext = Session.GetDaoContext(DataSource, IsView);
            if (!IsView)
            {
                daoContext.EnsureTable(ObjectType, TableArgs);
            }
            return daoContext;
        }

        /// <summary>
        /// 异步获取当前数据访问对象上下文
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        public virtual async Task<DAOContext> GetDaoContextAsync(CancellationToken cancellationToken = default)
        {
            var daoContext = await Session.GetDaoContextAsync(DataSource, IsView, cancellationToken);
            if (!IsView)
            {
                await daoContext.EnsureTableAsync(ObjectType, TableArgs).ConfigureAwait(false);
            }
            return daoContext;
        }

        /// <summary>
        /// 表名参数
        /// </summary>
        public string[]? TableArgs { get; internal set; }

        /// <summary>
        /// 创建 SQL 执行上下文，并绑定当前 DAO 使用的 SQL 构建器。
        /// </summary>
        /// <param name="initTable">是否在上下文中初始化表信息，默认为 false。对于某些操作（如生成 SQL 语句），可能需要在上下文中包含表信息以正确解析列和别名等细节。</param>
        /// <returns>SQL 构建上下文实例。</returns>
        public virtual SqlBuildContext CreateSqlBuildContext(bool initTable = false)
        {
            return initTable
                ? new SqlBuildContext(SqlBuilder, Table, Constants.DefaultTableAlias, TableArgs) { SingleTable = !IsView }
                : new SqlBuildContext(SqlBuilder) { TableArgs = TableArgs, SingleTable = !IsView };
        }

        private SqlBuildContext? _initSqlBuildContext;
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
        public TableInfoProvider TableInfoProvider => LiteOrm.Common.TableInfoProvider.Instance;

        /// <summary>
        /// 实际表名
        /// </summary>
        public string FactTableName
        {
            get
            {
                if (_factTableName is null)
                {
                    var tableArgs = TableArgs;
                    if (tableArgs != null && tableArgs.Length > 0)
                    {
                        _factTableName = String.Format(Table.Name!, tableArgs);
                    }
                    else
                    {
                        _factTableName = Table.Name;
                    }
                }
                return _factTableName!;
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
                    _fromTable = Table.ToSql(InitSqlBuildContext);
                }
                return _fromTable;
            }
        }

        /// <summary>
        /// 查询时需要获取的所有列
        /// </summary>
        protected virtual ReadOnlyCollection<SqlColumn> SelectColumns => Table.SelectColumns;


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
        #endregion

        #region 方法

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
                bool computed = column is ColumnDefinition cd && cd.IsComputed;
                if (i > 0) strAllFields.Append(",");
                strAllFields.Append(column.ToSql(InitSqlBuildContext));
                if (computed || !String.Equals(column.Name, column.PropertyName, StringComparison.OrdinalIgnoreCase))
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
        /// 表是否声明了固定筛选条件（<see cref="TableDefinition.ConstFilter"/>）。
        /// </summary>
        /// <remarks>
        /// 声明了固定筛选条件的表既不复用上下文缓存的命令，也不把新建的命令写入缓存：
        /// 固定筛选条件来自元数据、运行时可被替换，缓存会把首次生成的 SQL 固化下来；
        /// 而若把这类命令占用缓存槽位，运行时切换固定筛选条件（例如先无筛选、后设置筛选）时同一槽位会同时承载两种内容，
        /// 反过来污染常规（无固定筛选）命令的缓存。此时每次调用都新建命令，用完即释放，缓存里始终只有常规命令。
        /// </remarks>
        private bool HasConstFilter => TableDefinition.ConstFilter is not null;

        /// <summary>
        /// 获取缓存复用的命令代理：未命中时按当前元数据装配一次底层命令，之后每次调用都新建代理包装同一条命令。
        /// </summary>
        /// <param name="daoContext">当前上下文。</param>
        /// <param name="key">缓存键，由对象类型与方法名称组成。</param>
        /// <param name="sqlFunc">生成 PreparedSql 的方法。</param>
        /// <param name="configureCommand">用于配置 DbCommandProxy 的操作。</param>
        /// <returns><see cref="DbCommandProxy.IsReusable"/> 为 true 的命令代理实例，释放它不会影响缓存中的命令。</returns>
        private DbCommandProxy GetOrAddPreparedCommand(DAOContext daoContext, (Type, string) key, Func<PreparedSql> sqlFunc, Action<DbCommandProxy>? configureCommand)
        {
            if (!daoContext.PreparedCommands.TryGetValue(key, out var target))
            {
                // 装配用一个不拥有底层命令的代理完成，装配成功后命令交给缓存持有
                var commandProxy = daoContext.CreateCommand(false);
                target = commandProxy.Target;
                try
                {
                    var preparedSql = sqlFunc();
                    SetupCommand(commandProxy, preparedSql.Sql, preparedSql.Params);
                    configureCommand?.Invoke(commandProxy);
                }
                catch
                {
                    // 装配失败时释放尚未交给缓存的命令
                    target.Dispose();
                    throw;
                }
                var existing = daoContext.PreparedCommands.GetOrAdd(key, target);
                if (!ReferenceEquals(existing, target))
                {
                    // 并发下可能已有其他线程先写入，释放落选的新建命令，改用缓存里的那一条
                    target.Dispose();
                    return new DbCommandProxy(daoContext, existing, false);
                }
                return commandProxy;
            }
            return new DbCommandProxy(daoContext, target, false);
        }

        /// <summary>
        /// 获取预定义的 DbCommand
        /// </summary>
        /// <param name="methodName">方法名称</param>
        /// <param name="sqlFunc">生成 PreparedSql 的方法</param>
        /// <param name="configureCommand">用于配置 DbCommandProxy 的操作</param>
        /// <returns>
        /// 与方法名称关联的数据库命令代理实例。包装上下文缓存命令时为可复用代理，释放它不影响缓存；
        /// 表声明了固定筛选条件时每次调用都新建命令，此时代理拥有底层命令，调用方使用完毕后须释放。
        /// </returns>
        protected DbCommandProxy GetPreparedCommand(string methodName, Func<PreparedSql> sqlFunc, Action<DbCommandProxy>? configureCommand = null)
        {
            if (TableArgs != null && Table.Columns.Count > 0) methodName += String.Join("_", TableArgs);
            var daoContext = GetDaoContext();
            if (HasConstFilter)
            {
                var command = MakeNamedParamCommand(sqlFunc());
                configureCommand?.Invoke(command);
                return command;
            }
            daoContext.EnsureTable(ObjectType, TableArgs);
            return GetOrAddPreparedCommand(daoContext, (ObjectType, methodName), sqlFunc, configureCommand);
        }

        /// <summary>
        /// 异步获取预定义的 DbCommand。
        /// </summary>
        /// <param name="methodName">方法名称</param>
        /// <param name="sqlFunc">生成 PreparedSql 的方法</param>
        /// <param name="configureCommand">用于配置 DbCommandProxy 的操作</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>
        /// 与方法名称关联的数据库命令代理实例。包装上下文缓存命令时为可复用代理，释放它不影响缓存；
        /// 表声明了固定筛选条件时每次调用都新建命令，此时代理拥有底层命令，调用方使用完毕后须释放。
        /// </returns>
        protected async Task<DbCommandProxy> GetPreparedCommandAsync(string methodName, Func<PreparedSql> sqlFunc, Action<DbCommandProxy>? configureCommand = null, CancellationToken cancellationToken = default)
        {
            if (TableArgs != null && Table.Columns.Count > 0) methodName += String.Join("_", TableArgs);
            var daoContext = await GetDaoContextAsync(cancellationToken).ConfigureAwait(false);
            if (HasConstFilter)
            {
                var command = await MakeNamedParamCommandAsync(sqlFunc(), cancellationToken).ConfigureAwait(false);
                configureCommand?.Invoke(command);
                return command;
            }
            return GetOrAddPreparedCommand(daoContext, (ObjectType, methodName), sqlFunc, configureCommand);
        }

        /// <summary>
        /// 异步获取预定义的 DbCommand。
        /// </summary>
        /// <param name="methodName">方法名称</param>
        /// <param name="sqlFunc">生成 PreparedSql 的方法</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>与方法名称关联的已缓存或新建的数据库命令代理实例。</returns>

        protected async Task<DbCommandProxy> GetPreparedCommandAsync(string methodName, Func<PreparedSql> sqlFunc, CancellationToken cancellationToken = default)
        {
            return await GetPreparedCommandAsync(methodName, sqlFunc, null, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 根据 预处理的 SQL 语句和参数列表建立 <see cref="DbCommandProxy"/>。
        /// 预处理的 SQL 包含 SQL 语句和与之对应的命名参数列表，SQL 语句中的参数名称需要与参数列表中的键对应。
        /// SQL 语句中可以包含预定义的标记，如 {Table}、{From} 和 {AllFields}，这些标记会被替换为相应的值。
        /// 参数列表中的值会被转换为数据库参数，并添加到命令中。
        /// </summary>
        /// <param name="preparedSql"></param>
        /// <returns></returns>
        internal protected DbCommandProxy MakeNamedParamCommand(PreparedSql preparedSql)
        {
            return MakeNamedParamCommand(preparedSql.Sql, preparedSql.Params);
        }

        /// <summary>
        /// 根据 SQL 语句和 <see cref="Param"/> 参数列表建立 <see cref="DbCommandProxy"/>，
        /// 当参数指定了 <see cref="Param.DbType"/> 时将其应用到数据库参数。
        /// </summary>
        /// <param name="sql">SQL 语句。</param>
        /// <param name="paramValues">参数列表。</param>
        /// <returns>IDbCommand 实例。</returns>
        internal protected DbCommandProxy MakeNamedParamCommand(string sql, IEnumerable<Param> paramValues)
        {
            var daoContext = GetDaoContext();
            var command = daoContext.CreateCommand();
            SetupCommand(command, sql, paramValues);
            return command;
        }
        /// <summary>
        /// 异步根据 预处理的 SQL 语句和参数列表建立 <see cref="DbCommandProxy"/>。
        /// </summary>
        /// <param name="preparedSql"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        internal protected async Task<DbCommandProxy> MakeNamedParamCommandAsync(PreparedSql preparedSql, CancellationToken cancellationToken = default)
        {
            return await MakeNamedParamCommandAsync(preparedSql.Sql, preparedSql.Params, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 根据 SQL 语句和 <see cref="Param"/> 参数列表设定 <see cref="DbCommandProxy"/>，
        /// </summary>
        /// <param name="command">要设置的数据库命令代理实例。</param>
        /// <param name="sql">SQL 语句。</param>
        /// <param name="paramValues">参数列表。</param>
        private void SetupCommand(DbCommandProxy command, string sql, IEnumerable<Param> paramValues)
        {
            command.CommandText = MutiReplacerInstance.Replace(sql);
            command.Parameters.Clear();
            if (paramValues is not null)
                foreach (var para in paramValues)
                {
                    DbParameter dbParam = command.CreateParameter();
                    dbParam.ParameterName = ToParamName(ToNativeName(para.Name));
                    if (para.DbType != DbValueType.Default)
                    {
                        // 数组列不设置 DbParameter.DbType，交由驱动按 CLR 类型推断（值为 JSON 字符串）
                        if (!para.DbType.HasArray())
                        {
                            var dbType = SqlBuilder.ToDbType(para.DbType);
                            // DbType.Object 表示由驱动按值类型推断（如 Oracle 的 INTERVAL DAY TO SECOND 绑定 TimeSpan）
                            if (dbType != DbType.Object) dbParam.DbType = dbType;
                        }
                        else dbParam.DbType = DbType.String;
                    }
                    else if (para.Value is not null)
                    {
                        var dbType = SqlBuilder.ToDbType(SqlBuilder.GetDbValueType(para.Value.GetType()));
                        if (dbType != DbType.Object) dbParam.DbType = dbType;
                    }
                    dbParam.Value = ConvertDbValueForParameter(para.Value, para.DbType);
                    command.Parameters.Add(dbParam);
                }
        }

        /// <summary>
        /// 将无列上下文（裸 SQL 参数）的值转换为数据库可接受的值：
        /// 按 (值运行时类型, 目标 DbValueType) 从 SqlBuilder 注册表解析转换器并取其 <see cref="IDbValueConverter.DbWriteConverter"/> 委托执行；
        /// 未注册或委托为 null 时原样返回（严格无兜底）。
        /// </summary>
        private object ConvertDbValueForParameter(object? value, DbValueType dbType)
        {
            if (value is null) return DBNull.Value;
            DbValueType targetDbType = dbType != DbValueType.Default
                ? dbType
                : SqlBuilder.GetDbValueType(value.GetType());
            IDbValueConverter? converter = SqlBuilder.GetDbValueConverter(value.GetType(), targetDbType);
            return converter?.DbWriteConverter != null ? converter.DbWriteConverter(value) : value;
        }

        /// <summary>
        /// 异步根据 SQL 语句和 <see cref="Param"/> 参数列表建立 <see cref="DbCommandProxy"/>，
        /// 当参数指定了 <see cref="Param.DbType"/> 时将其应用到数据库参数。
        /// </summary>
        /// <param name="sql">SQL 语句。</param>
        /// <param name="paramValues">参数列表。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>IDbCommand 实例。</returns>
        internal protected async Task<DbCommandProxy> MakeNamedParamCommandAsync(string sql, IEnumerable<Param> paramValues, CancellationToken cancellationToken = default)
        {
            var daoContext = await GetDaoContextAsync(cancellationToken).ConfigureAwait(false);
            var command = daoContext.CreateCommand();
            SetupCommand(command, sql, paramValues);
            return command;
        }

        /// <summary>
        /// 根据表达式创建命令
        /// </summary>
        /// <param name="expr">表达式</param>
        /// <param name="isQuery">是否生成 select 查询</param>
        /// <returns>根据表达式生成的数据库命令代理实例。</returns>
        /// <exception cref="ArgumentNullException"></exception>
        internal protected DbCommandProxy MakeExprCommand(Expr expr, bool isQuery = false)
        {
            if (isQuery) expr = ToSelectExpr(expr);
            if (expr is null) throw new ArgumentNullException(nameof(expr));
            return MakeNamedParamCommand(expr.ToPreparedSql(CreateSqlBuildContext()));
        }
        /// <summary>
        /// 异步根据表达式创建命令
        /// </summary>
        /// <param name="expr">表达式</param>
        /// <param name="isQuery">是否生成 select 查询</param>
        /// <param name="cancellationToken"></param>
        /// <returns>根据表达式生成的数据库命令代理实例。</returns>
        /// <exception cref="ArgumentNullException"></exception>
        internal protected async Task<DbCommandProxy> MakeExprCommandAsync(Expr expr, bool isQuery = false, CancellationToken cancellationToken = default)
        {
            if (isQuery) expr = ToSelectExpr(expr);
            if (expr is null) throw new ArgumentNullException(nameof(expr));
            return await MakeNamedParamCommandAsync(expr.ToPreparedSql(CreateSqlBuildContext()), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 转换表达式为 SelectExpr，如果表达式已经是 SelectExpr 则直接返回，否则创建一个新的 SelectExpr，并将表达式转换为数据源。
        /// 这个方法主要用于将普通的表达式转换为查询表达式，以便在执行查询操作时使用。
        /// </summary>
        /// <param name="expr">待转换的表达式</param>
        /// <returns></returns>
        protected SelectExpr ToSelectExpr(Expr expr)
        {
            if (expr is null) expr = Expr.From(ObjectType, TableArgs!);
            if (expr is SelectExpr selectExpr) return selectExpr;
            return new SelectExpr()
            {
                Source = expr.ToSource(ObjectType),
                Selects = SelectColumns.Select((col, i) => new SelectItemExpr(Expr.Prop(col.PropertyName), col.PropertyName)).ToList()
            };
        }

        /// <summary>
        /// 执行带有命名参数的 SQL 语句，并返回结果值。SQL 语句可以包含 Expr 或变量值。
        /// </summary>
        /// <typeparam name="T">结果类型</typeparam>
        /// <param name="sqlBody">查询SQL，使用插值字符串格式，可插入普通变量或 Expr。<see cref="LiteOrm.Common.ExprString"/></param>
        /// <returns>包含查询结果的值结果对象。</returns>
        public virtual ValueResult<T> GetValue<T>([InterpolatedStringHandlerArgument("")] ref ExprString sqlBody)
        {
            var prepared = sqlBody.GetResult();
            return new ValueResult<T>(this, prepared);
        }

        /// <summary>
        /// 执行带有命名参数的 SQL 语句，并返回受影响的行数。SQL 语句可以包含 Expr 或变量值。
        /// </summary>
        /// <param name="sqlBody">查询SQL，使用插值字符串格式，可插入普通变量或 Expr。<see cref="LiteOrm.Common.ExprString"/></param>
        /// <returns>包含受影响行数的非查询结果对象。</returns>
        public virtual NonQueryResult Execute([InterpolatedStringHandlerArgument("")] ref ExprString sqlBody)
        {
            var prepared = sqlBody.GetResult();
            return new NonQueryResult(this, prepared);
        }

        /// <summary>
        /// 执行带有命名参数的 SQL 语句，并返回结果集。SQL 语句可以包含 Expr 或变量值。
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="sqlBody">查询SQL，使用插值字符串格式，可插入普通变量或 Expr。<see cref="LiteOrm.Common.ExprString"/></param>
        /// <param name="readerFunc">用于从 DbDataReader 读取结果的函数，为空时默认使用 <see cref="DataReaderConverter.GetConverterByTable{TResult}(IDbConverter)"/></param>
        /// <returns>包含查询结果集的可枚举结果对象。</returns>
        public virtual EnumerableResult<TResult> Query<[DynamicallyAccessedMembers(Constants.RegistedMemberTypes)] TResult>
            ([InterpolatedStringHandlerArgument("")] ref ExprString sqlBody, Func<AutoLockDataReader, TResult>? readerFunc = null)
        {
            return new EnumerableResult<TResult>(this, sqlBody.GetResult(), readerFunc);
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
        private MultiReplacer? _mutiReplacer;
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
        /// 为command创建根据主键查询的条件，在参数集合中添加参数并返回where条件的语句
        /// </summary>
        /// <param name="paramValues">参数集合</param>
        /// <returns>where条件的语句</returns>
        protected string MakeKeyCondition(ICollection<Param> paramValues)
        {
            ThrowExceptionIfNoKeys();
            var strConditions = ValueStringBuilder.Create(128);
            var keys = Table.Keys;
            int count = keys.Count;
            for (int i = 0; i < count; i++)
            {
                ColumnDefinition key = keys[i];
                var paramName = paramValues.Count.ToString();
                if (i > 0) strConditions.Append(" AND ");
                strConditions.Append(ToColumnSql(key));
                strConditions.Append(" = ");
                strConditions.Append(ToSqlParam(paramName));
                paramValues.Add(new Param(paramName, null, key.GetDbValueType(SqlBuilder)));
            }
            string result = strConditions.ToString();
            strConditions.Dispose();
            return result;
        }

        /// <summary>
        /// 生成表固定筛选条件（<see cref="TableDefinition.ConstFilter"/>，由 <see cref="ColumnAttribute.Constant"/> 收敛而来）的 SQL 片段。
        /// </summary>
        /// <remarks>
        /// 参数名称取 <paramref name="paramValues"/> 的当前元素个数，因此须在键条件参数追加之后调用，才能与命令中的参数顺序保持一致。
        /// </remarks>
        /// <param name="paramValues">参数集合，固定筛选条件产生的参数追加到该集合末尾。</param>
        /// <param name="tableAlias">
        /// 条件中列名使用的表别名。为 null 时按单表模式生成不带表名的列名，适用于语句中未给目标表起别名的单表 UPDATE / DELETE。
        /// 视图 DAO 的 FROM 子句带默认别名，故默认使用 <see cref="Constants.DefaultTableAlias"/> 限定；
        /// 批量更新语句则传入 <see cref="SqlBuilder.BatchTargetTableAlias"/> 指定的别名。
        /// </param>
        /// <returns>固定筛选条件的 SQL 片段；表中未声明固定筛选时返回 null。</returns>
        protected virtual string? MakeConstFilterCondition(ICollection<Param> paramValues, string? tableAlias = null)
        {
            LogicExpr? constFilter = TableDefinition.ConstFilter;
            if (constFilter is null) return null;
            if (tableAlias is null && IsView) tableAlias = Constants.DefaultTableAlias;
            var context = new SqlBuildContext(SqlBuilder, Table, tableAlias ?? Constants.DefaultTableAlias, TableArgs) { SingleTable = tableAlias is null, OutputParams = paramValues };
            return constFilter.ToSql(context);
        }

        /// <summary>
        /// 将固定筛选条件以 AND 追加到已有 WHERE 条件之后。
        /// </summary>
        /// <param name="where">已有 WHERE 条件，可为空。</param>
        /// <param name="paramValues">参数集合，固定筛选条件产生的参数追加到该集合末尾。</param>
        /// <param name="tableAlias">条件中列名使用的表别名，语义同 <see cref="MakeConstFilterCondition"/>。</param>
        /// <returns>合并后的 WHERE 条件；表中未声明固定筛选时原样返回 <paramref name="where"/>。</returns>
        protected string AppendConstFilter(string where, ICollection<Param> paramValues, string? tableAlias = null)
        {
            string? constFilter = MakeConstFilterCondition(paramValues, tableAlias);
            if (constFilter is null || constFilter.Length == 0) return where;
            if (String.IsNullOrEmpty(where)) return constFilter;
            return $"{where} AND {constFilter}";
        }

        /// <summary>
        /// 数据列转换为 SQL 格式名称
        /// </summary>
        /// <param name="column">数据列</param>
        /// <returns>数据列对应的 SQL 名称</returns>
        protected string ToColumnSql(SqlColumn column)
        {
            if (_columnSqlCache.TryGetValue(column, out string? cachedSql))
            {
                return cachedSql;
            }
            string sql = column.ToSql(InitSqlBuildContext);
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
                values.Add(key.GetValue(o)!);
            }
            return values.ToArray();
        }

        /// <summary>
        /// 如果没有定义主键则抛出异常
        /// </summary>
        /// <exception cref="Exception"></exception>
        protected void ThrowExceptionIfNoKeys()
        {
            if (TableDefinition.Keys.Count == 0)
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
            if (keys.Length != TableDefinition.Keys.Count)
            {
                if (_exceptionWrongKeys is null)
                {
                    List<string> strKeys = new List<string>();
                    foreach (ColumnDefinition key in TableDefinition.Keys) strKeys.Add(key.Name!);
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
