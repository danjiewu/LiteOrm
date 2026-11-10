using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// 通用表信息提供程序。源生成器在编译期收集 <see cref="TableInfo"/> / <see cref="ColumnInfo"/>，
    /// 并在模块初始化器中注册到本类的全局单例；多个程序集可共享同一个提供程序实例，
    /// 避免各程序集生成独立 Provider 时相互覆盖 <see cref="TableInfoProvider.Instance"/>。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="LiteOrm.AttributeTableInfoProvider"/> 相对：后者在运行期读特性建模型，
    /// 本类只消费编译期生成的元数据，不读取任何特性，因此可在 NativeAOT / 裁剪下工作。
    /// 两者构建出的 <see cref="TableDefinition"/> 与 <see cref="TableView"/>（含 <see cref="JoinedTable"/>、
    /// <see cref="ForeignColumn"/>）结构一致，业务代码无需区分。
    /// </remarks>
    public class CommonTableInfoProvider : TableInfoProvider
    {
        /// <summary>
        /// 全局单例实例。
        /// </summary>
        public new static CommonTableInfoProvider Instance { get; } = new CommonTableInfoProvider();

        private readonly ConcurrentDictionary<Type, TableInfo> _tableInfos = new();
        private readonly ConcurrentDictionary<Type, TableDefinition> _tables = new();
        private readonly ConcurrentDictionary<Type, TableView> _views = new();
        private readonly object _syncLock = new object();

        /// <summary>
        /// 正在构建视图的类型。用于阻断 <see cref="GetTableView"/> 与 <see cref="GetTableDefinition"/>
        /// 之间的重入：构建 A 的视图时若顺带请求 A 的表定义，表定义会尝试回填视图，形成环。
        /// 仅在持有 <see cref="_syncLock"/> 时访问。
        /// </summary>
        private readonly HashSet<Type> _buildingViews = new();

        /// <summary>
        /// 将当前提供程序安装为 <see cref="TableInfoProvider.Instance"/> 的工厂。
        /// 该方法可被多个程序集生成的模块初始化器重复调用，始终指向同一单例。
        /// </summary>
        public static void Install()
        {
            TableInfoProvider.Set(() => Instance);
        }

        /// <summary>
        /// 注册一个表信息。若同类型已注册，则覆盖旧信息并清除已构建的缓存。
        /// </summary>
        /// <param name="tableInfo">表信息。</param>
        public void Register(TableInfo tableInfo)
        {
            if (tableInfo is null) throw new ArgumentNullException(nameof(tableInfo));

            _tableInfos[tableInfo.ObjectType] = tableInfo;
            _tables.TryRemove(tableInfo.ObjectType, out _);
            _views.TryRemove(tableInfo.ObjectType, out _);
        }

        /// <inheritdoc />
        public override TableDefinition? GetTableDefinition(
            [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
            Type objectType)
        {
            if (objectType is null) return null;
            if (_tables.TryGetValue(objectType, out var tableDef))
            {
                EnsureViewAttached(objectType, tableDef);
                return tableDef;
            }

            lock (_syncLock)
            {
                if (_tables.TryGetValue(objectType, out tableDef)) return tableDef;

                tableDef = BuildTableDefinitionOnly(objectType);
                if (tableDef is null) return null;

                // 与 AttributeTableInfoProvider 一致：表定义构建完成后回填视图，
                // 使计算列等在渲染时能通过 Definition.View 找到列。
                EnsureViewAttached(objectType, tableDef);
                return tableDef;
            }
        }

        /// <inheritdoc />
        public override TableView? GetTableView(
            [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
            Type objectType)
        {
            if (objectType is null) return null;
            if (_views.TryGetValue(objectType, out var cachedView)) return cachedView;

            lock (_syncLock)
            {
                if (_views.TryGetValue(objectType, out cachedView)) return cachedView;

                // 构建过程中被再次请求（如关联表的表定义回填视图）时返回 null，由调用方跳过。
                if (!_buildingViews.Add(objectType)) return null;
                try
                {
                    var tableDef = BuildTableDefinitionOnly(objectType);
                    if (tableDef is null) return null;
                    if (!_tableInfos.TryGetValue(objectType, out var tableInfo)) return null;

                    var view = BuildTableView(tableInfo, tableDef);
                    _views[objectType] = view;
                    tableDef.SetView(view);
                    return view;
                }
                finally
                {
                    _buildingViews.Remove(objectType);
                }
            }
        }

        private void EnsureViewAttached(
            [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
            Type objectType,
            TableDefinition tableDef)
        {
            if (tableDef.View != null) return;
            var view = GetTableView(objectType);
            if (view != null && tableDef.View == null) tableDef.SetView(view);
        }

        // ──────────────────────────────────────────────────────────────
        // 表定义
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 构建（并缓存）表定义，不触发视图构建，避免与 <see cref="GetTableView"/> 相互递归。
        /// </summary>
        private TableDefinition? BuildTableDefinitionOnly(
            [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
            Type objectType)
        {
            if (_tables.TryGetValue(objectType, out var tableDef)) return tableDef;
            if (!_tableInfos.TryGetValue(objectType, out var tableInfo)) return null;

            var newDef = BuildTableDefinition(tableInfo);
            _tables[objectType] = newDef;
            return newDef;
        }

        private static TableDefinition BuildTableDefinition(TableInfo tableInfo)
        {
            var columns = new List<ColumnDefinition>(tableInfo.Columns.Count);

            foreach (var columnInfo in tableInfo.Columns)
            {
                if (string.IsNullOrEmpty(columnInfo.PropertyName)) continue;

                // [ForeignColumn] 投影列不是真实表列，不写入表定义
                if (columnInfo.ForeignColumn != null) continue;

                var property = tableInfo.ObjectType.Find(
                    columnInfo.PropertyName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property is null)
                    throw new InvalidOperationException(
                        $"Property '{columnInfo.PropertyName}' was not found on type '{tableInfo.ObjectType.FullName}'.");

                var column = new ColumnDefinition(property)
                {
                    Name = string.IsNullOrEmpty(columnInfo.ColumnName) ? property.Name : columnInfo.ColumnName,
                    IsPrimaryKey = columnInfo.IsPrimaryKey,
                    IsIdentity = columnInfo.IsIdentity,
                    IsTimestamp = columnInfo.IsTimestamp,
                    IsIndex = columnInfo.IsIndex,
                    IsUnique = columnInfo.IsUnique,
                    AllowNull = columnInfo.AllowNull,
                    Length = columnInfo.Length,
                    DbType = columnInfo.DbType,
                    Expression = columnInfo.Expression,
                    Mode = columnInfo.Mode,
                    DefaultValue = columnInfo.DefaultValue,
                    Constant = MetaDataHelper.ParseConstant(property, columnInfo.Constant),
                    IdentityExpression = columnInfo.IdentityExpression,
                    IdentityStart = columnInfo.IdentityStart,
                    IdentityIncreasement = columnInfo.IdentityIncreasement
                };
                column.DbValueConverter = columnInfo.ValueConverter;
                if (columnInfo.ForeignTables is { Count: > 0 })
                    column.ForeignTables = columnInfo.ForeignTables;

                columns.Add(column);
            }

            return new TableDefinition(tableInfo.ObjectType, columns)
            {
                Name = tableInfo.Name,
                DataSource = tableInfo.DataSource,
                SyncTable = tableInfo.SyncTable,
                ConstFilter = MetaDataHelper.BuildConstFilter(columns)
            };
        }

        // ──────────────────────────────────────────────────────────────
        // 视图（关联元数据）
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 按编译期收集的关联元数据构建视图，输出结构与 <see cref="LiteOrm.AttributeTableInfoProvider"/>
        /// 运行期读特性得到的结构一致：先建 <see cref="TableJoinAttribute"/> 声明的关联表，再按
        /// <see cref="ForeignTypeAttribute"/> 把外键列扩展到关联表，最后解析 <see cref="ForeignColumn"/> 的目标列。
        /// AutoExpand 关联表（含类级关联）统一走展开队列递归处理。
        /// </summary>
        private TableView BuildTableView(TableInfo tableInfo, TableDefinition tableDef)
        {
            var joinedTables = new Dictionary<string, JoinedTable>(StringComparer.OrdinalIgnoreCase);
            var joinDefs = new List<TableJoinDef>();
            var expandQueue = new Queue<JoinedTable>();
            var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. 类级 [TableJoin] 声明的关联表
            AddTableJoins(tableInfo.ObjectType, tableInfo.JoinedTables, null, joinedTables, joinDefs, expandQueue);

            // 2. 视图列：真实表列 + [ForeignColumn] 投影列，保持属性声明顺序（与源生成读取映射的顺序一致）
            var columns = new List<SqlColumn>(tableInfo.Columns.Count);
            foreach (var columnInfo in tableInfo.Columns)
            {
                if (columnInfo.ForeignColumn != null)
                {
                    var foreignColumn = CreateForeignColumn(tableInfo.ObjectType, columnInfo);
                    if (foreignColumn != null) columns.Add(foreignColumn);
                }
                else
                {
                    var column = tableDef.GetColumn(columnInfo.PropertyName);
                    if (column != null) columns.Add(column);
                }
            }

            // 3. 把列上的 [ForeignType] 关联展开成关联表，AutoExpand 表连同类级关联逐级展开
            var columnRefs = new Queue<ColumnRef>(columns.Select(column => new ColumnRef(column)));
            while (expandQueue.Count > 0 || columnRefs.Count > 0)
            {
                while (expandQueue.Count > 0)
                    ExpandAutoExpandTable(tableInfo.ObjectType, expandQueue.Dequeue(), joinedTables, joinDefs, columnRefs, expandQueue, expanded);
                if (columnRefs.Count > 0)
                    JoinColumn(joinedTables, columnRefs, expandQueue);
            }

            // 4. 解析 [ForeignColumn] 的目标列（可沿 ForeignColumn 链逐层递归）
            foreach (var foreignColumn in columns.OfType<ForeignColumn>())
            {
                foreignColumn.TargetColumn = GetTargetColumn(joinedTables, tableInfo.ObjectType, foreignColumn);
                if (foreignColumn.DbValueConverter is null && foreignColumn.TargetColumn?.Column is SqlColumn targetColumn)
                {
                    foreignColumn.DbValueConverter = targetColumn.DbValueConverter
                        ?? (targetColumn as ForeignColumn)?.DbValueConverter;
                }
            }

            // 5. 创建视图
            var tableView = new TableView(tableDef, columns, joinedTables.Values) { Name = tableInfo.ObjectType.Name };

            // 6. 回填 [TableJoin] 关联表的外键
            foreach (var joinDef in joinDefs)
            {
                var info = joinDef.Info;
                // Source 显式指定时取指定表，否则取声明该关联的表（主表或上级关联表）
                JoinedTable? sourceTable = info.SourceAlias is null && info.SourceType is null
                    ? joinDef.Owner
                    : ResolveSourceTable(joinedTables, info.SourceAlias, info.SourceType);
                var foreignKeys = new List<ColumnRef>();
                foreach (string keyName in info.ForeignKeys.Split(','))
                {
                    ColumnRef? foreignKey;
                    if (sourceTable is null)
                    {
                        var column = tableView.GetColumn(keyName);
                        foreignKey = column is null ? null : new ColumnRef(column);
                    }
                    else
                    {
                        foreignKey = sourceTable.GetColumn(keyName);
                    }
                    if (foreignKey is null)
                        throw new ArgumentException($"Foreign key column \"{keyName}\" does not exist in table \"{sourceTable?.Name ?? tableInfo.ObjectType.Name}\".");
                    foreignKeys.Add(foreignKey);
                }
                joinDef.JoinedTable.ForeignKeys = foreignKeys.AsReadOnly();
            }

            // 7. 关联链上被使用到的表要一并标记，保证 JOIN 依赖顺序正确
            var queue = new Queue<JoinedTable>(joinedTables.Values);
            while (queue.Count > 0)
            {
                var table = queue.Dequeue();
                if (table.ForeignKeys is null || table.ForeignKeys.Count == 0)
                    throw new ArgumentException($"Foreign keys not defined for joined table \"{table.Name}\".");
                if (table.Used && table.ForeignKeys[0].Table is JoinedTable dependency && !dependency.Used)
                {
                    dependency.Used = true;
                    queue.Enqueue(dependency);
                }
            }

            return tableView;
        }

        /// <summary>
        /// 把类级关联元数据加入关联表集合，AutoExpand 表进入展开队列。
        /// </summary>
        private void AddTableJoins(
            Type rootType,
            IReadOnlyList<JoinedTableInfo>? joinInfos,
            JoinedTable? owner,
            Dictionary<string, JoinedTable> joinedTables,
            List<TableJoinDef> joinDefs,
            Queue<JoinedTable> expandQueue)
        {
            if (joinInfos is null) return;
            foreach (var joinInfo in joinInfos)
            {
                // 递归展开不再把主表自身作为外表引入，避免关联环形成自连接
                if (owner is not null && joinInfo.TargetType == rootType) continue;
                var targetTableDef = BuildTableDefinitionOnly(joinInfo.TargetType);
                if (targetTableDef is null) continue;

                string alias = string.IsNullOrEmpty(joinInfo.Alias) ? joinInfo.TargetType.Name : joinInfo.Alias!;
                if (joinedTables.TryGetValue(alias, out var existing))
                {
                    // 递归展开时遇到已存在的同名表即停止，避免关联环无限展开
                    if (owner is not null && existing.TableDefinition.ObjectType == joinInfo.TargetType) continue;
                    throw new ArgumentException($"Duplicate table alias name \"{alias}\"");
                }

                var joinedTable = new JoinedTable(targetTableDef, MetaDataHelper.GetJoinPrimeKeys(targetTableDef, joinInfo.PrimeKeys))
                {
                    JoinType = joinInfo.JoinType,
                    AutoExpand = joinInfo.AutoExpand,
                    Name = alias,
                    ConstFilter = MetaDataHelper.AliasConstFilter(targetTableDef.ConstFilter, alias)
                };
                joinedTables[alias] = joinedTable;
                joinDefs.Add(new TableJoinDef { Info = joinInfo, Owner = owner, JoinedTable = joinedTable });
                if (joinedTable.AutoExpand) expandQueue.Enqueue(joinedTable);
            }
        }

        /// <summary>
        /// 展开一个 AutoExpand 表：其列进入连接队列以继续解析 [ForeignType]，其类级关联也一并展开。
        /// </summary>
        private void ExpandAutoExpandTable(
            Type rootType,
            JoinedTable table,
            Dictionary<string, JoinedTable> joinedTables,
            List<TableJoinDef> joinDefs,
            Queue<ColumnRef> columnRefs,
            Queue<JoinedTable> expandQueue,
            HashSet<string> expanded)
        {
            if (table.Name is null || !expanded.Add(table.Name)) return;
            foreach (var column in table.TableDefinition.Columns)
                columnRefs.Enqueue(new ColumnRef(table, column));
            if (_tableInfos.TryGetValue(table.TableDefinition.ObjectType, out var nestedInfo))
                AddTableJoins(rootType, nestedInfo.JoinedTables, table, joinedTables, joinDefs, expandQueue);
        }

        /// <summary>
        /// 按 SourceAlias / SourceType 查找外键所在的关联表。
        /// </summary>
        private static JoinedTable ResolveSourceTable(
            Dictionary<string, JoinedTable> joinedTables,
            string? sourceAlias,
            Type? sourceType)
        {
            if (sourceAlias is not null)
            {
                if (!joinedTables.TryGetValue(sourceAlias, out var source))
                    throw new ArgumentException($"Source table \"{sourceAlias}\" does not exist in joined tables.");
                return source;
            }

            JoinedTable? matched = null;
            foreach (var candidate in joinedTables.Values)
            {
                if (candidate.TableDefinition.ObjectType != sourceType) continue;
                if (matched is not null)
                    throw new ArgumentException($"Undeterminate table. More than one table of type {sourceType} joined.");
                matched = candidate;
            }
            if (matched is null)
                throw new ArgumentException($"Source table type {sourceType} does not exist in joined tables.");
            return matched;
        }

        private sealed class TableJoinDef
        {
            public JoinedTableInfo Info { get; set; } = null!;
            public JoinedTable? Owner { get; set; }
            public JoinedTable JoinedTable { get; set; } = null!;
        }

        /// <summary>
        /// 由编译期元数据创建 <see cref="ForeignColumn"/>，等价于运行期读取 <see cref="ForeignColumnAttribute"/>。
        /// </summary>
        private static ForeignColumn? CreateForeignColumn(
            [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)] Type objectType,
            ColumnInfo columnInfo)
        {
            var property = objectType.Find(
                columnInfo.PropertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property is null) return null;

            var foreignColumn = new ForeignColumn(property)
            {
                DbValueConverter = columnInfo.ValueConverter
            };
            if (columnInfo.ForeignTables is { Count: > 0 })
                foreignColumn.ForeignTables = columnInfo.ForeignTables;
            return foreignColumn;
        }

        /// <summary>
        /// 解析 <see cref="ForeignColumn"/> 的目标列。目标列本身若也是 <see cref="ForeignColumn"/>，
        /// 用目标类型的元数据递归解析，从而支持链式引用。
        /// </summary>
        private ColumnRef GetTargetColumn(
            Dictionary<string, JoinedTable> joinedTables,
            [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)] Type objectType,
            ForeignColumn column)
        {
            var columnInfo = FindColumnInfo(objectType, column.PropertyName)
                ?? throw new ArgumentException($"Column metadata for property {column.PropertyName} was not found.");

            var targetColumn = ResolveTargetColumn(joinedTables, columnInfo, column.PropertyName, objectType);
            if (targetColumn is null)
                throw new ArgumentException($"Target column of property {column.PropertyName} does not exist in joined tables.");
            return targetColumn;
        }

        /// <summary>
        /// 按编译期元数据在已连接的表中查找目标列引用。
        /// </summary>
        private ColumnRef? ResolveTargetColumn(
            Dictionary<string, JoinedTable> joinedTables,
            ColumnInfo columnInfo,
            string ownPropertyName,
            [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)] Type ownerType)
        {
            var foreignColumnInfo = columnInfo.ForeignColumn;
            if (foreignColumnInfo is null) return null;

            string primeProperty = string.IsNullOrEmpty(foreignColumnInfo.Property)
                ? ownPropertyName
                : foreignColumnInfo.Property!;
            Type? primeType = foreignColumnInfo.ForeignType;
            string foreignTableName = primeType is null
                ? foreignColumnInfo.ForeignAlias ?? string.Empty
                : primeType.Name;

            JoinedTable? joinedTable;
            ColumnRef? targetColumn = null;
            if (joinedTables.TryGetValue(foreignTableName, out var namedTable))
            {
                joinedTable = namedTable;
                targetColumn = namedTable.GetColumn(primeProperty);
            }
            else
            {
                joinedTable = null;
                foreach (var candidate in joinedTables.Values)
                {
                    if (candidate.TableDefinition.ObjectType != primeType) continue;
                    if (joinedTable is not null)
                        throw new ArgumentException($"Undeterminate table. More than one table of type {primeType} joined.");
                    joinedTable = candidate;
                    targetColumn = candidate.GetColumn(primeProperty);
                }
            }

            if (joinedTable is null)
                throw new ArgumentException($"Foreign table name {foreignTableName} of property {ownPropertyName} does not exist in joined tables.");

            if (targetColumn is null)
            {
                // 目标属性本身也可能是 [ForeignColumn]：用目标类型的元数据继续往下解析
                var targetType = joinedTable.TableDefinition.ObjectType;
                var targetColumnInfo = FindColumnInfo(targetType, primeProperty);
                if (targetColumnInfo?.ForeignColumn != null)
                    targetColumn = ResolveTargetColumn(joinedTables, targetColumnInfo, primeProperty, targetType);
            }

            if (targetColumn is null)
                throw new ArgumentException($"Foreign table name {foreignTableName} of property {ownPropertyName} does not exist in joined tables.");

            joinedTable.Used = true;
            return targetColumn;
        }

        /// <summary>
        /// 在已注册的元数据中按属性名查找列信息（含继承的元数据）。
        /// </summary>
        private ColumnInfo? FindColumnInfo(
            [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)] Type objectType,
            string propertyName)
        {
            if (!_tableInfos.TryGetValue(objectType, out var tableInfo)) return null;
            foreach (var columnInfo in tableInfo.Columns)
            {
                if (string.Equals(columnInfo.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase))
                    return columnInfo;
            }
            return null;
        }

        /// <summary>
        /// 处理一列上的全部 <see cref="ForeignTable"/> 关联，把尚未连接的外表加入关联表集合。
        /// </summary>
        private void JoinColumn(Dictionary<string, JoinedTable> joinedTables, Queue<ColumnRef> columnRefs, Queue<JoinedTable> expandQueue)
        {
            var columnRef = columnRefs.Dequeue();
            foreach (var foreignTableInfo in columnRef.Column.ForeignTables)
            {
                JoinColumn(joinedTables, columnRefs, expandQueue, columnRef, foreignTableInfo);
            }
        }

        private void JoinColumn(
            Dictionary<string, JoinedTable> joinedTables,
            Queue<ColumnRef> columnRefs,
            Queue<JoinedTable> expandQueue,
            ColumnRef columnRef,
            ForeignTable foreignTableInfo)
        {
            string joinedTableName = string.IsNullOrEmpty(foreignTableInfo.Alias)
                ? foreignTableInfo.ForeignType?.Name ?? string.Empty
                : foreignTableInfo.Alias!;

            if (joinedTables.TryGetValue(joinedTableName, out var existingJoinedTable))
            {
                if (existingJoinedTable.TableDefinition.ObjectType != foreignTableInfo.ForeignType)
                    throw new ArgumentException($"Duplicate table alias name \"{joinedTableName}\"");
                if (existingJoinedTable.ForeignKeys is not null
                    && existingJoinedTable.ForeignKeys.Count > 0
                    && !existingJoinedTable.ForeignKeys[0].Equals(columnRef))
                    throw new ArgumentException($"Duplicate table alias name \"{joinedTableName}\"");
                return;
            }

            var foreignTable = BuildTableDefinitionOnly(foreignTableInfo.ForeignType!);
            if (foreignTable is null) return;

            var joinedTable = new JoinedTable(foreignTable)
            {
                JoinType = foreignTableInfo.JoinType,
                AutoExpand = foreignTableInfo.AutoExpand,
                Name = joinedTableName,
                ConstFilter = MetaDataHelper.AliasConstFilter(foreignTable.ConstFilter, joinedTableName)
            };
            joinedTable.ForeignKeys = new List<ColumnRef> { columnRef }.AsReadOnly();
            if (joinedTables.ContainsKey(joinedTableName))
                throw new ArgumentException($"Duplicate table alias name \"{joinedTable.Name}\"");
            joinedTables[joinedTableName] = joinedTable;

            // AutoExpand 表交给统一展开队列，继续展开其列与类级关联
            if (joinedTable.AutoExpand) expandQueue.Enqueue(joinedTable);
        }
    }
}
