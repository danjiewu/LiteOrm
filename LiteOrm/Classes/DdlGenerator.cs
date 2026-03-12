using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace LiteOrm
{
    /// <summary>
    /// DDL 语句生成工具类，通过连接数据库检查现有结构并生成所需的差量 DDL 语句。
    /// </summary>
    /// <remarks>
    /// DdlGenerator 根据实体类型的 <see cref="TableDefinition.DataSource"/> 自动从
    /// <see cref="DAOContextPoolFactory"/> 获取对应连接池，委托
    /// <see cref="DAOContextPool.ResolveEnsureTableDDL"/> 实现 DDL 解析。
    /// 支持多数据源场景：不同实体类型可分属不同数据源，各自连接正确的数据库进行对比。
    ///
    /// 主要用途：
    /// 1. 在部署前预览或导出数据库迁移脚本（差量 DDL）。
    /// 2. 为指定实体类型生成单张表的 DDL。
    /// 3. 扫描程序集自动生成全部实体类型的 DDL。
    ///
    /// 注意：调用后表会被标记为已处理，后续 <see cref="DAOContextPool.EnsureTable"/> 将跳过已标记的表。
    /// 若需重新生成，请先调用 <see cref="DAOContextPool.ClearTableCache"/>。
    ///
    /// 使用示例：
    /// <code>
    /// var generator = _serviceProvider.GetService<DDLGenerator>();
    ///
    /// // 生成单个实体的 DDL
    /// foreach (var sql in generator.GenerateDdl(typeof(User)))
    ///     Console.WriteLine(sql);
    ///
    /// // 扫描程序集生成所有实体的 DDL（自动按数据源路由到对应连接池）
    /// foreach (var sql in generator.GenerateAllDDL())
    ///     Console.WriteLine(sql);
    /// </code>
    /// </remarks>
    [AutoRegister]
    public class DDLGenerator
    {
        private readonly DAOContextPoolFactory _factory;

        /// <summary>
        /// 初始化 <see cref="DDLGenerator"/> 的新实例。
        /// </summary>
        /// <param name="factory">用于按数据源名称获取连接池的工厂。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="factory"/> 为 null 时抛出。</exception>
        public DDLGenerator(DAOContextPoolFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// 针对指定实体类型，根据其数据源获取对应连接池，连接数据库检查现有状态并返回所需的 DDL 语句列表。
        /// 若该表已被标记为已处理，则返回空列表。
        /// </summary>
        /// <param name="objectType">实体类型。</param>
        /// <param name="tableArgs">动态表名参数，适用于实现了 <see cref="IArged"/> 的类型。</param>
        /// <returns>所需的 DDL 语句列表（CREATE TABLE、ADD COLUMN、CREATE INDEX）。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="objectType"/> 为 null 时抛出。</exception>
        /// <exception cref="InvalidOperationException">当 <see cref="TableInfoProvider.Default"/> 未设置或未找到对应的连接池时抛出。</exception>
        /// <exception cref="InvalidOperationException">当 <see cref="TableDefinition.DataSource"/> 未设置或无效时抛出。</exception>
        public List<string> GenerateDDL(Type objectType, string[] tableArgs = null)
        {
            if (objectType == null) throw new ArgumentNullException(nameof(objectType));
            if (TableInfoProvider.Default == null) throw new InvalidOperationException("TableInfoProvider.Default is not set.");
            var tableDef = TableInfoProvider.Default.GetTableDefinition(objectType);
            if (tableDef == null) return new List<string>();

            var pool = _factory.GetPool(tableDef.DataSource);
            if(pool == null) throw new InvalidOperationException($"No DAOContextPool found for data source '{tableDef.DataSource}'.");
            return pool.ResolveEnsureTableDDL(objectType, tableArgs);
        }

        /// <summary>
        /// 针对多个实体类型生成所需的 DDL 语句，每个类型自动路由到其对应数据源的连接池。
        /// </summary>
        /// <param name="objectTypes">实体类型集合。</param>
        /// <returns>所有实体类型的 DDL 语句集合。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="objectTypes"/> 为 null 时抛出。</exception>
        public IEnumerable<string> GenerateDDL(IEnumerable<Type> objectTypes)
        {
            if (objectTypes == null) throw new ArgumentNullException(nameof(objectTypes));

            foreach (var type in objectTypes)
                foreach (var sql in GenerateDDL(type))
                    yield return sql;
        }

        /// <summary>
        /// 扫描指定程序集，为所有带 <see cref="TableAttribute"/> 标记的实体类型生成 DDL 语句。
        /// 每个类型会自动根据 <see cref="TableDefinition.DataSource"/> 路由到对应的连接池。
        /// 实现了 <see cref="IArged"/> 接口的动态表名类型会被跳过（因无法确定运行时表名）。
        /// 无法解析或数据源不存在的类型会被静默跳过。
        /// </summary>
        /// <param name="assemblies">
        /// 要扫描的程序集数组。若为空或未指定，则自动扫描所有已加载的相关程序集
        /// （通过 <see cref="AssemblyAnalyzer.GetAllReferencedAssemblies"/>）。
        /// </param>
        /// <returns>所有匹配实体类型的 DDL 语句集合。</returns>
        public IEnumerable<string> GenerateAllDDL(params Assembly[] assemblies)
        {
            IEnumerable<Assembly> targetAssemblies = (assemblies != null && assemblies.Length > 0)
                ? (IEnumerable<Assembly>)assemblies
                : AssemblyAnalyzer.GetAllReferencedAssemblies();

            var typesByDataSource = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);

            foreach (var assembly in targetAssemblies)
            {
                IEnumerable<Type> types;
                try { types = assembly.GetExportedTypes(); }
                catch { continue; }

                foreach (var type in types)
                {
                    if (type.GetAttribute<TableAttribute>() == null) continue;
                    if (typeof(IArged).IsAssignableFrom(type)) continue;

                    TableDefinition tableDef;
                    try { tableDef = TableInfoProvider.Default?.GetTableDefinition(type); }
                    catch { continue; }

                    if (tableDef == null) continue;

                    var dataSource = tableDef.DataSource ?? string.Empty;
                    if (!typesByDataSource.TryGetValue(dataSource, out var list))
                        typesByDataSource[dataSource] = list = new List<Type>();
                    list.Add(type);
                }
            }

            foreach (var kvp in typesByDataSource)
            {
                DAOContextPool pool;
                try { pool = _factory.GetPool(kvp.Key); }
                catch { continue; }

                foreach (var type in kvp.Value)
                {
                    List<string> statements;
                    try { statements = pool.ResolveEnsureTableDDL(type); }
                    catch { continue; }

                    foreach (var sql in statements)
                        yield return sql;
                }
            }
        }
    }
}
