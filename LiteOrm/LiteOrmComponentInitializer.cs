using Autofac;
using LiteOrm.Common;
using LiteOrm.SqlBuilder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;




namespace LiteOrm
{
    /// <summary>
    /// 在应用程序的依赖注入容器中为 LiteOrm 组件提供初始化逻辑。
    /// </summary>
    /// <remarks>此类通常注册为单例，负责在应用程序启动期间配置核心 LiteOrm 服务，例如会话管理和表信息提供程序。它应被用于确保 LiteOrm 组件在使用之前已正确设置。</remarks>
    [AutoRegister(Lifetime = ServiceLifetime.Singleton)]
    public class LiteOrmComponentInitializer : IComponentInitializer
    {
        private readonly ILogger<LiteOrmComponentInitializer> _logger;

        /// <summary>
        /// 初始化 <see cref="LiteOrmComponentInitializer"/> 类的新实例。
        /// </summary>
        /// <param name="logger">日志记录器。</param>
        public LiteOrmComponentInitializer(ILogger<LiteOrmComponentInitializer> logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 使用指定的组件上下文初始化应用程序范围的服务并注册自定义 SQL 函数。
        /// </summary>
        /// <remarks>此方法设置当前会话管理器和表信息提供程序，并在应用程序启动期间调用此方法以确保所需服务可用。</remarks>
        /// <param name="componentContext">用于解析和配置所需服务的组件上下文。不能为 null。</param>
        public void Initialize(IComponentContext componentContext)
        {
            // 设置全局单例引用的核心组件
            SessionManager.Current = componentContext.Resolve<SessionManager>();
            TableInfoProvider.Default = componentContext.Resolve<TableInfoProvider>();

            // 注册各数据库方言的 SQL 函数生成逻辑
            RegisterSqlFunctions();
            // 注册 Lambda 表达式转换到 Expr 对象的成员句柄 (如 DateTime.Now)
            RegisterLambdaMemberHandlers();
            // 注册 Lambda 表达式转换到 Expr 对象的方法句柄 (如 StartsWith, Contains)
            RegisterLambdaMethodHandlers();

            // 自动建表同步
            SyncTables(componentContext);
        }

        /// <summary>
        /// 自动同步数据库表结构
        /// </summary>
        private void SyncTables(IComponentContext componentContext)
        {
            var dataSourceProvider = componentContext.Resolve<IDataSourceProvider>();
            var tableInfoProvider = componentContext.Resolve<TableInfoProvider>();
            var sqlBuilderFactory = componentContext.Resolve<ISqlBuilderFactory>();
            var daoContextPoolFactory = componentContext.Resolve<DAOContextPoolFactory>();

            var syncDataSources = dataSourceProvider.Where(ds => ds.SyncTable).ToList();
            if (!syncDataSources.Any()) return;

            _logger?.LogInformation("开始自动同步数据库表结构...");

            var assemblies = AssemblyAnalyzer.GetAllReferencedAssemblies();
            var tableTypes = assemblies.SelectMany(a => a.GetTypes())
                                       .Where(t => !t.IsAbstract && t.GetCustomAttribute<TableAttribute>() != null)
                                       .ToList();


            // 按数据源和表名分组
            var tableGroups = tableTypes.GroupBy(t =>
            {
                var attr = t.GetCustomAttribute<TableAttribute>();
                return (DataSource: attr.DataSource ?? dataSourceProvider.DefaultDataSourceName, TableName: attr.TableName ?? t.Name);
            }).ToList();

            var syncTasks = syncDataSources.Select(async ds =>
            {
                var sqlBuilder = sqlBuilderFactory.GetSqlBuilder(ds.ProviderType);
                var pool = daoContextPoolFactory.GetPool(ds.Name);
                if (pool == null) return;

                var currentDsGroups = tableGroups.Where(g => string.Equals(g.Key.DataSource, ds.Name, StringComparison.OrdinalIgnoreCase)).ToList();

                try
                {
                    if (currentDsGroups.Any())
                    {
                        _logger?.LogInformation("正在同步数据源 '{DataSource}'，共 {Count} 个表定义", ds.Name, currentDsGroups.Count);

                        var context = pool.PeekContextInternal();
                        try
                        {
                            using (await context.AcquireScopeAsync())
                            {
                                foreach (var group in currentDsGroups)
                                {
                                    string tableName = group.Key.TableName;
                                    // 合并该表的所有列定义
                                    var allColumns = new Dictionary<string, ColumnDefinition>(StringComparer.OrdinalIgnoreCase);
                                    TableDefinition firstTableDef = null;

                                    foreach (var type in group)
                                    {
                                        var tableDef = tableInfoProvider.GetTableDefinition(type);
                                        if (tableDef == null) continue;
                                        if (firstTableDef == null) firstTableDef = tableDef;

                                        foreach (var col in tableDef.Columns)
                                        {
                                            if (!allColumns.ContainsKey(col.Name))
                                                allColumns[col.Name] = col;
                                        }
                                    }

                                    if (firstTableDef == null || allColumns.Count == 0) continue;

                                    if (!TableExists(context, sqlBuilder, tableName))

                                    {
                                        _logger?.LogInformation("正在数据源 '{DataSource}' 中创建表 '{TableName}'", ds.Name, tableName);
                                        // 创建表，使用合并后的列
                                        string createSql = sqlBuilder.BuildCreateTableSql(tableName, allColumns.Values);
                                        _logger?.LogInformation(createSql);
                                        ExecuteSql(context, createSql);
                                    }
                                    else

                                    {
                                        // 检查补全列
                                        var existingColumns = GetTableColumns(context, sqlBuilder.ToSqlName(tableName));
                                        var missingColumns = allColumns.Values.Where(col => !existingColumns.Contains(col.Name)).ToList();
                                        if (missingColumns.Any())
                                        {
                                            _logger?.LogInformation("正在向数据源 '{DataSource}' 的表 '{TableName}' 添加 {Count} 个新列", ds.Name, tableName, missingColumns.Count);
                                            string addColsSql = sqlBuilder.BuildAddColumnsSql(tableName, missingColumns);
                                            ExecuteSql(context, addColsSql);
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            pool.ReturnContext(context);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "同步数据源 '{DataSource}' 时发生异常", ds.Name);
                    throw; // Re-throw to be caught by Task.WhenAll
                }
                finally
                {
                    pool.MarkInitialized();
                }
            }).ToArray();


            try
            {
                Task.WhenAll(syncTasks).GetAwaiter().GetResult();
            }
            catch
            {
                // Task.WhenAll catches all exceptions but only re-throws the first one.
                // Log is already done inside each task.
                throw;
            }

            _logger?.LogInformation("数据库表结构同步完成");
        }



        private void ExecuteSql(DAOContext context, string sql)

        {
            try
            {
                using (var cmd = context.DbConnection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    context.EnsureConnectionOpen();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("执行同步 SQL 失败: {Sql} {ErrorMessage}", sql, ex.Message);
                throw;
            }
        }


        private HashSet<string> GetTableColumns(DAOContext context, string tableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            context.EnsureConnectionOpen();
            try
            {
                using (var cmd = context.DbConnection.CreateCommand())
                {
                    cmd.CommandText = $"SELECT * FROM {tableName} WHERE 1=0";
                    using (var reader = cmd.ExecuteReader())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            columns.Add(reader.GetName(i));
                        }
                    }
                }
            }
            catch
            {
                // Fallback
                var schema = context.DbConnection.GetSchema("Columns", new[] { null, null, tableName });
                foreach (System.Data.DataRow row in schema.Rows)
                {
                    columns.Add(row["COLUMN_NAME"].ToString());
                }
            }
            return columns;
        }



        private bool TableExists(DAOContext context, ISqlBuilder sqlBuilder, string tableName)
        {
            try
            {
                using (var cmd = context.DbConnection.CreateCommand())
                {
                    cmd.CommandText = sqlBuilder.BuildTableExistsSql(tableName);
                    context.EnsureConnectionOpen();
                    cmd.ExecuteScalar();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }


        /// <summary>
        /// 注册 Lambda 表达式中的成员访问处理器。
        /// </summary>
        private void RegisterLambdaMemberHandlers()
        {
            // 注册特定的属性/字段访问转换逻辑
            // DateTime.Now -> SQL Now()
            LambdaExprConverter.RegisterMemberHandler(typeof(DateTime), "Now");
            // DateTime.Today -> SQL Today()
            LambdaExprConverter.RegisterMemberHandler(typeof(DateTime), "Today");
            // String.Length -> SQL Length() 或者特定数据库的 LEN()/CHAR_LENGTH()
            LambdaExprConverter.RegisterMemberHandler(typeof(string), "Length");
        }


        /// <summary>
        /// 注册 Lambda 表达式中的方法调用处理器。
        /// </summary>
        private void RegisterLambdaMethodHandlers()
        {

            // 批量注册常用类型的公开方法（使用默认映射，即：方法名 -> SQL函数名）
            LambdaExprConverter.RegisterMethodHandler(typeof(DateTime));
            LambdaExprConverter.RegisterMethodHandler(typeof(Math));
            LambdaExprConverter.RegisterMethodHandler(typeof(string));

            // 特定字符串方法映射到 BinaryOperator，以便支持模糊查询生成特定的 SQL (LIKE)
            LambdaExprConverter.RegisterMethodHandler(typeof(string), "StartsWith", (node, converter) =>
            {
                var left = converter.Convert(node.Object);
                var right = converter.Convert(node.Arguments[0]);
                return new BinaryExpr(left, BinaryOperator.StartsWith, right);
            });

            LambdaExprConverter.RegisterMethodHandler(typeof(string), "EndsWith", (node, converter) =>
            {
                var left = converter.Convert(node.Object);
                var right = converter.Convert(node.Arguments[0]);
                return new BinaryExpr(left, BinaryOperator.EndsWith, right);
            });

            LambdaExprConverter.RegisterMethodHandler(typeof(string), "Contains", (node, converter) =>
            {
                var left = converter.Convert(node.Object);
                var right = converter.Convert(node.Arguments[0]);
                return new BinaryExpr(left, BinaryOperator.Contains, right);
            });

            // 处理集合 Contains 逻辑，映射为 SQL 的 IN 表达式
            LambdaExprConverter.RegisterMethodHandler("Contains", (node, converter) =>
            {
                if (node.Method.DeclaringType == typeof(Enumerable) || typeof(IEnumerable).IsAssignableFrom(node.Method.DeclaringType))
                {
                    // 统一处理 Enumerable.Contains(collection, value) 和 List.Contains(value)
                    Expr collection = null;
                    Expr value = null;
                    if (node.Method.IsStatic)
                    {
                        collection = converter.Convert(node.Arguments[0]);
                        value = converter.Convert(node.Arguments[1]);
                    }
                    else
                    {
                        collection = converter.Convert(node.Object);
                        value = converter.Convert(node.Arguments[0]);
                    }
                    return new BinaryExpr(value, BinaryOperator.In, collection);
                }
                return null;
            });

            // 字符串拼接映射
            LambdaExprConverter.RegisterMethodHandler(typeof(string), "Concat", (node, converter) =>
            {
                List<Expr> args = new List<Expr>();
                // 处理实例方法调用 "a".Concat("b")
                if (node.Object != null) args.Add(converter.Convert(node.Object));

                if (node.Arguments.Count == 1)
                {
                    // 处理 Concat(IEnumerable<string>) 或 Concat(params string[])
                    var arg = converter.Convert(node.Arguments[0]);
                    if (arg is IEnumerable<Expr> enumerable)
                        args.AddRange(enumerable);
                    else
                        args.Add(arg);
                }
                else
                {
                    // 处理静态多参数 Concat(a, b, c)
                    foreach (var arg in node.Arguments)
                    {
                        args.Add(converter.Convert(arg));
                    }
                }
                return new ExprSet(ExprJoinType.Concat, args);
            });

            // 相等性比较映射
            LambdaExprConverter.RegisterMethodHandler("Equals", (node, converter) =>
            {
                Expr left = null;
                Expr right = null;
                if (node.Object != null)
                {
                    left = converter.Convert(node.Object);
                    right = converter.Convert(node.Arguments[0]);
                }
                else
                {
                    left = converter.Convert(node.Arguments[0]);
                    right = converter.Convert(node.Arguments[1]);
                }
                return new BinaryExpr(left, BinaryOperator.Equal, right);
            });

            // ToString 通常在 SQL 中作为字段引用或忽略，这里默认引用对象本身
            LambdaExprConverter.RegisterMethodHandler("ToString", (node, converter) =>
            {
                return converter.Convert(node.Object);
            });

            // Compare/CompareTo 的逻辑简化：映射为相等比较
            LambdaExprConverter.RegisterMethodHandler("Compare", (node, converter) =>
            {
                var left = converter.Convert(node.Arguments[0]);
                var right = converter.Convert(node.Arguments[1]);
                return new BinaryExpr(left, BinaryOperator.Equal, right);
            });

            LambdaExprConverter.RegisterMethodHandler("CompareTo", (node, converter) =>
            {
                var left = node.Object != null ? converter.Convert(node.Object) : converter.Convert(node.Arguments[0]);
                var right = node.Object != null ? converter.Convert(node.Arguments[0]) : converter.Convert(node.Arguments[1]);
                return new BinaryExpr(left, BinaryOperator.Equal, right);
            });
        }

        /// <summary>
        /// 注册不同数据库构建器对特定函数的 SQL 生成逻辑。
        /// </summary>
        private void RegisterSqlFunctions()
        {
            // 注册跨库通用的 SQL 映射
            BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("Now", (functionName, args) => "CURRENT_TIMESTAMP");
            BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("Today", (functionName, args) => "CURRENT_DATE");
            // 特殊处理 IndexOf 和 Substring，支持 C# 到 SQL 的索引转换 (0-based -> 1-based)
            BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (functionName, args) => args.Count > 2 ?
                        $"INSTR({args[0].Key}, {args[1].Key}, {args[2].Key}+1)-1" : $"INSTR({args[0].Key}, {args[1].Key})-1");
            BaseSqlBuilder.Instance.RegisterFunctionSqlHandler("Substring", (name, args) => args.Count > 2 ?
                        $"SUBSTR({args[0].Key}, {args[1].Key}+1, {args[2].Key})" : $"SUBSTR({args[0].Key}, {args[1].Key}+1)");

            // SQLite 方言配置
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (functionName, args) => $"DATE({args[0].Key}, CAST({args[1].Key} AS TEXT)||' {functionName.Substring(3).ToLower()}')");

            // MySQL 方言配置
            MySqlBuilder.Instance.RegisterFunctionSqlHandler("LENGTH", (functionName, args) => $"CHAR_LENGTH({args[0].Key})");
            MySqlBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (functionName, args) => $"DATE_ADD({args[0].Key}, INTERVAL {args[1].Key} {functionName.Substring(3).ToUpper().TrimEnd('S')})");

            // Oracle 方言配置
            OracleBuilder.Instance.RegisterFunctionSqlHandler("IfNull", (name, args) => $"NVL({args[0].Key}, {args[1].Key})");
            OracleBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays"], (functionName, args) => $"({args[0].Key} + NUMTODSINTERVAL({args[1].Key}, '{functionName.Substring(3).ToUpper().TrimEnd('S')}'))");
            OracleBuilder.Instance.RegisterFunctionSqlHandler(["AddMonths", "AddYears"], (functionName, args) => $"({args[0].Key} + NUMTOYMINTERVAL({args[1].Key}, '{functionName.Substring(3).ToUpper().TrimEnd('S')}'))");

            // PostgreSQL 方言配置
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (functionName, args) => $"POSITION({args[1].Key} IN {args[0].Key})-1");
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler("Substring", (name, args) => args.Count > 2 ?
                        $"SUBSTRING({args[0].Key} FROM {args[1].Key}+1 FOR {args[2].Key})" : $"SUBSTRING({args[0].Key} FROM {args[1].Key}+1)");
            PostgreSqlBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (functionName, args) => $"({args[0].Key} + ({args[1].Key} || ' {functionName.Substring(3).ToLower()}')::interval)");

            // SQL Server 方言配置
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("IfNull", (name, args) => $"ISNULL({args[0].Key}, {args[1].Key})");
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("Length", (functionName, args) => $"LEN({args[0].Key})");
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("IndexOf", (functionName, args) => args.Count > 2 ?
                        $"CHARINDEX({args[1].Key}, {args[0].Key}, {args[2].Key}+1)-1" : $"CHARINDEX({args[1].Key}, {args[0].Key})-1");
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler("Substring", (name, args) => args.Count > 2 ?
                        $"SUBSTRING({args[0].Key}, {args[1].Key}+1, {args[2].Key})" : $"SUBSTRING({args[0].Key}, {args[1].Key}+1, LEN({args[0].Key}))");
            SqlServerBuilder.Instance.RegisterFunctionSqlHandler(["AddSeconds", "AddMinutes", "AddHours", "AddDays", "AddMonths", "AddYears"],
                (functionName, args) => $"DATEADD({functionName.Substring(3).ToLower().TrimEnd('s')}, {args[1].Key}, {args[0].Key})");
        }
    }
}
