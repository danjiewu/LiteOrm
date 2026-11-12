using LiteOrm.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace LiteOrm
{
    /// <summary>
    /// 根据Attribute的表信息提供者
    /// </summary>
    public class AttributeTableInfoProvider : TableInfoProvider
    {
        private readonly ConcurrentDictionary<Type, TableDefinition> _tableInfoCache = new ConcurrentDictionary<Type, TableDefinition>();
        private readonly ConcurrentDictionary<Type, TableView> _tableViewCache = new ConcurrentDictionary<Type, TableView>();
        private readonly object _syncLock = new object();
        /// <summary>
        /// 正在构建视图的类型。构建 A 的视图时若顺带请求 A 的表定义，表定义会尝试回填视图，
        /// 若不拦截会再次进入视图构建，产生两个 TableView 实例（表定义持有其中一个，缓存持有另一个）。
        /// 仅在持有 <see cref="_syncLock"/> 时访问。
        /// </summary>
        private readonly HashSet<Type> _buildingViews = new HashSet<Type>();

        /// <summary>
        /// 初始化 <see cref="AttributeTableInfoProvider"/> 类的新实例。
        /// </summary>
        /// <param name="serviceProvider">DI 服务提供者（保留兼容性，当前实现不再依赖此参数）。</param>
        public AttributeTableInfoProvider(IServiceProvider serviceProvider) { }

        /// <summary>
        /// 使用默认构造函数初始化 <see cref="AttributeTableInfoProvider"/> 类的新实例。
        /// </summary>
        public AttributeTableInfoProvider() { }

        /// <summary>
        /// 根据对象类型得到对应的数据库表定义
        /// </summary>
        /// <param name="objectType">对象类型</param>
        /// <returns>表定义</returns>
        public override TableDefinition? GetTableDefinition(
            [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
            Type objectType)
        {
            if (objectType is null) return null;
            if (_tableInfoCache.TryGetValue(objectType, out var tableDef))
            {
                EnsureViewAttached(objectType, tableDef);
                return tableDef;
            }

            lock (_syncLock)
            {
                if (_tableInfoCache.TryGetValue(objectType, out tableDef))
                {
                    EnsureViewAttached(objectType, tableDef);
                    return tableDef;
                }
                tableDef = GenerateTableDefinition(objectType);
                if (tableDef != null)
                {
                    _tableInfoCache[objectType] = tableDef;
                    EnsureViewAttached(objectType, tableDef);
                }
                return tableDef;
            }
        }

        /// <summary>
        /// 根据对象类型得到表以及关联信息
        /// </summary>
        /// <param name="objectType">对象类型</param>
        /// <returns>表信息</returns>
        public override TableView? GetTableView(
            [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
            Type objectType)
        {
            if (objectType is null) return null;
            if (_tableViewCache.TryGetValue(objectType, out var tableView)) return tableView;

            lock (_syncLock)
            {
                if (_tableViewCache.TryGetValue(objectType, out tableView)) return tableView;

                // 同一类型正在构建视图（如 A 关联 B、B 又回指 A）时返回 null，
                // 由最外层构建完成后统一发布，避免无限递归和重复生成。
                if (!_buildingViews.Add(objectType)) return null;
                try
                {
                    if (!_tableInfoCache.TryGetValue(objectType, out var tableDef))
                    {
                        tableDef = GenerateTableDefinition(objectType);
                        if (tableDef is null) return null;
                        _tableInfoCache[objectType] = tableDef;
                    }

                    tableView = GenerateTableView(objectType, tableDef);
                    if (tableView != null)
                    {
                        _tableViewCache[objectType] = tableView;
                        tableDef.SetView(tableView);
                    }
                    return tableView;
                }
                finally
                {
                    _buildingViews.Remove(objectType);
                }
            }
        }

        /// <summary>
        /// 为表定义回填视图；视图已构建时直接复用，正在构建时由最外层负责回填。
        /// </summary>
        private void EnsureViewAttached(
            [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
            Type objectType,
            TableDefinition tableDef)
        {
            if (tableDef.View != null) return;
            var view = GetTableView(objectType);
            if (view != null && tableDef.View == null) tableDef.SetView(view);
        }

        private TableDefinition? GenerateTableDefinition(
            [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
            Type objectType)
        {
            TableAttribute? tableAttribute = objectType.GetAttribute<TableAttribute>();
            if (tableAttribute is null) return null;

            string? tableName = tableAttribute.TableName;
            if (String.IsNullOrEmpty(tableName)) tableName = objectType.Name;

            List<ColumnDefinition> columns = new List<ColumnDefinition>();
            bool autoGenerateColumns = tableAttribute.AutoGenerateColumns;

            var properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList().SortProperty() ?? new List<PropertyInfo>();
            foreach (PropertyInfo property in properties)
            {
                ColumnDefinition? column = GenerateColumnDefinition(property, autoGenerateColumns);
                if (column is not null)
                {
                    columns.Add(column);
                }
            }
            return new TableDefinition(objectType, columns)
            {
                Name = tableName ?? objectType.Name,
                DataSource = tableAttribute.DataSource,
                SyncTable = tableAttribute.SyncTable,
                ConstFilter = MetaDataHelper.BuildConstFilter(columns)
            };
        }

        private ColumnDefinition? GenerateColumnDefinition(PropertyInfo property, bool autoGenerateColumns)
        {
            if (property.GetIndexParameters().Length != 0) return null;
            ForeignTable[] foreignTables = GetForeignTables(property);

            if (property.GetAttribute<ForeignColumnAttribute>() is not null) return null;

            ColumnAttribute? columnAttribute = property.GetAttribute<ColumnAttribute>();
            if (columnAttribute is not null)
            {
                if (!columnAttribute.IsColumn)
                {
                    return null;
                }
                else
                {
                    ColumnDefinition column = new ColumnDefinition(property);
                    if (!String.IsNullOrEmpty(columnAttribute.ColumnName)) column.Name = columnAttribute.ColumnName ?? column.Name;
                    column.IsPrimaryKey = columnAttribute.IsPrimaryKey;
                    column.IsIdentity = columnAttribute.IsIdentity;
                    column.IsTimestamp = columnAttribute.IsTimestamp;
                    column.IdentityExpression = columnAttribute.IdentityExpression;
                    column.IsUnique = columnAttribute.IsUnique;
                    column.IsIndex = columnAttribute.IsIndex;
                    column.DbType = columnAttribute.DbType;
                    column.Expression = columnAttribute.Expression;
                    column.Length = columnAttribute.Length;
                    column.AllowNull = columnAttribute.AllowNull && (property.PropertyType.IsValueType ? Nullable.GetUnderlyingType(property.PropertyType) is not null : true);
                    column.DefaultValue = columnAttribute.DefaultValue;
                    column.Constant = MetaDataHelper.ParseConstant(property, columnAttribute.Constant);
                    column.IdentityStart = columnAttribute.IdentityStart;
                    column.IdentityIncreasement = columnAttribute.IdentityIncreasement;
                    ColumnMode accessMask = (property.CanRead ? ColumnMode.Write : ColumnMode.None) | (property.CanWrite ? ColumnMode.Read : ColumnMode.None);
                    if (columnAttribute.ColumnMode == ColumnMode.None)
                    {
                        // 未显式声明列模式：按属性的读写可访问性推导；
                        // 设置表达式即视为计算列，可写属性推导为 Read | Computed（按表达式取值并回填），
                        // 只读属性推导为 Computed（只用于查询条件，不回填）。
                        column.Mode = String.IsNullOrEmpty(columnAttribute.Expression)
                            ? ColumnMode.Full & accessMask
                            : (property.CanWrite ? ColumnMode.Read | ColumnMode.Computed : ColumnMode.Computed);
                    }
                    else
                    {
                        // 显式声明：与可访问性求交，计算列位与读写位正交需单独保留；
                        // Read 位只按声明值生效，计算列不自动可读。
                        column.Mode = (columnAttribute.ColumnMode & accessMask) | (columnAttribute.ColumnMode & ColumnMode.Computed);
                    }
                    column.ForeignTables = foreignTables;
                    column.DbValueConverter = CreateDbValueConverter(columnAttribute.ConverterType, property);
                    return column;
                }
            }
            else
            {
                // 未标注 [Column] 的属性：受 TableAttribute.AutoGenerateColumns 控制。
                // 该标记为 false 时一律不生成列，只有显式标注 [Column] 的属性才映射为列。
                if (!autoGenerateColumns) return null;

                // 复杂类型（数组/集合 → Array 掩码、自定义类 → Object）
                // 不自动生成列元信息，必须手动加 [Column] 特性才会作为列；仅已知标量类型（数值、string、byte[]、Guid、日期等）及 Json/Jsonb 映射类型自动映射为列。
                DbValueType inferred = DbValueTypeMap.InferFromPropertyType(property.PropertyType);
                if (inferred.HasArray() || inferred is DbValueType.Object)
                    return null;

                ColumnDefinition column = new ColumnDefinition(property);
                column.Name = property.Name;
                column.Mode = (property.CanRead ? ColumnMode.Write : ColumnMode.None) | (property.CanWrite ? ColumnMode.Read : ColumnMode.None);
                column.DbType = DbValueType.Default;
                column.Length = 0;
                column.AllowNull = property.PropertyType.IsValueType ? Nullable.GetUnderlyingType(column.PropertyType) is not null : true;
                column.ForeignTables = foreignTables;
                return column;
            }
        }

        private static ForeignColumn? GenerateForeignColumn(PropertyInfo property)
        {
            if (property.GetIndexParameters().Length != 0) return null;

            ForeignColumnAttribute? foreignColumnAttribute = property.GetAttribute<ForeignColumnAttribute>();
            if (foreignColumnAttribute is not null)
            {
                ForeignColumn foreignColumn = new ForeignColumn(property);
                foreignColumn.ForeignTables = GetForeignTables(property);
                foreignColumn.DbValueConverter = CreateDbValueConverter(foreignColumnAttribute.ConverterType, property);
                return foreignColumn;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 依据表定义构建视图。表定义由调用方创建或取自缓存，本方法不再调用
        /// <see cref="GetTableDefinition(Type)"/>，避免“表定义回填视图 → 视图再取表定义”的循环导致同一类型生成两个视图。
        /// </summary>
        private TableView? GenerateTableView(
            [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
            Type objectType,
            TableDefinition tableDef)
        {
            TableJoinAttribute[] atts = (TableJoinAttribute[])objectType.GetCustomAttributes(typeof(TableJoinAttribute), true);
            ConcurrentDictionary<string, JoinedTable> joinedTables = new ConcurrentDictionary<string, JoinedTable>(StringComparer.OrdinalIgnoreCase);

            // 首先根据TableJoinAttribute连接表，生成JoinedTable对象，并加入joinedTables字典
            foreach (TableJoinAttribute tableJoin in atts)
            {
                var targetTableDef = GetTableDefinition(tableJoin.TargetType);
                if (targetTableDef == null) continue;

                JoinedTable joinedTable = new JoinedTable(targetTableDef, MetaDataHelper.GetJoinPrimeKeys(targetTableDef, tableJoin.PrimeKeys))
                {
                    JoinType = tableJoin.JoinType,
                    AutoExpand = tableJoin.AutoExpand
                };
                if (String.IsNullOrEmpty(tableJoin.Alias))
                    tableJoin.Alias = joinedTable.Name = tableJoin.TargetType.Name;
                else
                    joinedTable.Name = tableJoin.Alias;
                joinedTable.ConstFilter = MetaDataHelper.AliasConstFilter(targetTableDef.ConstFilter, joinedTable.Name ?? string.Empty);
                if (joinedTables.ContainsKey(joinedTable.Name ?? string.Empty)) throw new ArgumentException($"Duplicate table alias name \"{joinedTable.Name}\"");
                joinedTables[joinedTable.Name ?? string.Empty] = joinedTable;
            }
            List<SqlColumn> columns = new List<SqlColumn>();
            var properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList().SortProperty() ?? new List<PropertyInfo>();
            foreach (PropertyInfo property in properties)
            {
                ColumnDefinition? column = tableDef.GetColumn(property.Name);
                if (column is not null)
                {
                    columns.Add(column);
                }
                else
                {
                    // 根据属性上的ForeignColumnAttribute连接表，生成ForeignColumn对象，并加入连接队列
                    ForeignColumn? foreignColumn = GenerateForeignColumn(property);
                    if (foreignColumn is not null) columns.Add(foreignColumn);
                }
            }

            // 将ColumnRef对象加入队列，准备进行连接操作
            Queue<ColumnRef> columnRefs = new Queue<ColumnRef>();
            foreach (SqlColumn column in columns)
            {
                columnRefs.Enqueue(new ColumnRef(column));
            }
            // 进行连接操作，直到连接队列为空
            while (columnRefs.Count > 0)
            {
                JoinColumn(joinedTables, columnRefs);
            }

            // 递归展开 AutoExpand 的关联表：先展开其列的 ForeignType 关联，再展开该类型自身的类级 TableJoin 关联；
            // 新加入的关联表若也 AutoExpand，会继续参与后续展开。
            HashSet<JoinedTable> columnExpandedTables = new HashSet<JoinedTable>();
            HashSet<JoinedTable> joinExpandedTables = new HashSet<JoinedTable>();
            while (true)
            {
                bool enqueued = false;
                foreach (JoinedTable joinedTable in joinedTables.Values)
                {
                    if (!joinedTable.AutoExpand || !columnExpandedTables.Add(joinedTable)) continue;
                    foreach (ColumnDefinition joinedColumn in joinedTable.TableDefinition.Columns)
                    {
                        columnRefs.Enqueue(new ColumnRef(joinedTable, joinedColumn));
                    }
                    enqueued = true;
                }
                if (enqueued)
                {
                    while (columnRefs.Count > 0)
                    {
                        JoinColumn(joinedTables, columnRefs);
                    }
                    continue;
                }

                JoinedTable? pendingTable = null;
                foreach (JoinedTable joinedTable in joinedTables.Values)
                {
                    if (joinedTable.AutoExpand && joinExpandedTables.Add(joinedTable))
                    {
                        pendingTable = joinedTable;
                        break;
                    }
                }
                if (pendingTable is null) break;

                foreach (TableJoinAttribute tableJoin in (TableJoinAttribute[])pendingTable.TableDefinition.ObjectType.GetCustomAttributes(typeof(TableJoinAttribute), true))
                {
                    JoinTable(joinedTables, pendingTable, tableJoin);
                }
            }
            // 根据连接结果设置ForeignColumn的TargetColumn属性
            foreach (SqlColumn column in columns)
            {
                if (column is ForeignColumn foreignColumn)
                {
                    foreignColumn.TargetColumn = GetTargetColumn(joinedTables, foreignColumn);
                    if (foreignColumn.DbValueConverter is null && foreignColumn.TargetColumn?.Column is SqlColumn targetCol)
                    {
                        foreignColumn.DbValueConverter = targetCol.DbValueConverter ?? (targetCol as ForeignColumn)?.DbValueConverter;
                    }
                }
            }

            // 创建TableView对象

            TableView tableView = new TableView(tableDef, columns, joinedTables.Values) { Name = objectType.Name };
            // 根据TableJoinAttribute设置JoinedTable的ForeignKeys属性
            foreach (TableJoinAttribute tableJoin in atts)
            {
                // 如果Source为null，表示外键来自主表，根据ForeignKeys属性指定的列名从主表中找到对应的ColumnRef作为外键列
                if (tableJoin.Source is null)
                {
                    List<ColumnRef> foreignKeys = new List<ColumnRef>();
                    foreach (string keyName in tableJoin.ForeignKeys.Split(','))
                    {
                        foreignKeys.Add(new ColumnRef(tableView.GetColumn(keyName)!));
                    }
                    joinedTables[tableJoin.Alias ?? tableJoin.TargetType.Name ?? string.Empty].ForeignKeys = foreignKeys.AsReadOnly();
                }
                //如果Source不为null，表示外键来自其他已连接的表，根据Source指定的表名或表类型找到对应的JoinedTable，再根据ForeignKeys属性指定的列名从该JoinedTable中找到对应的ColumnRef作为外键列
                else
                {
                    JoinedTable? sourceTable = null;
                    if (tableJoin.Source is string)
                    {
                        string sourceName = (string)tableJoin.Source;
                        if (!joinedTables.ContainsKey(sourceName))
                            throw new ArgumentException($"Source table \"{sourceName}\" does not exist in joined tables.");
                        sourceTable = joinedTables[sourceName];
                    }
                    else
                    {
                        Type sourceType = (Type)tableJoin.Source!;
                        foreach (JoinedTable joinedTable in joinedTables.Values)
                        {
                            if (joinedTable.TableDefinition.ObjectType == sourceType)
                            {
                                if (sourceTable is null)
                                    sourceTable = joinedTable;
                                else
                                    throw new ArgumentException($"Undeterminate table. More than one table of type {sourceType} joined.");
                            }
                        }
                        if (sourceTable is null)
                            throw new ArgumentException($"Source table type {sourceType} does not exist in joined tables.");
                    }
                    List<ColumnRef> foreignKeys = new List<ColumnRef>();
                    foreach (string keyName in tableJoin.ForeignKeys.Split(','))
                    {
                        foreignKeys.Add(sourceTable.GetColumn(keyName)!);
                    }
                    joinedTables[tableJoin.Alias ?? tableJoin.TargetType.Name ?? string.Empty].ForeignKeys = foreignKeys.AsReadOnly();
                }
            }

            // 标记所有被使用的表为已使用状态
            Queue<JoinedTable> queue = new Queue<JoinedTable>(joinedTables.Values);
            while (queue.Count > 0)
            {
                JoinedTable table = queue.Dequeue();
                if (table.ForeignKeys is null || table.ForeignKeys.Count == 0)
                    throw new ArgumentException($"Foreign keys not defined for joined table \"{table.Name}\".");
                if (table.Used && table.ForeignKeys[0].Table is JoinedTable jt && !jt.Used)
                {
                    jt.Used = true;
                    queue.Enqueue(jt);
                }
            }

            return tableView;
        }

        private static ColumnRef GetTargetColumn(ConcurrentDictionary<string, JoinedTable> joinedTables, ForeignColumn column)
        {
            ForeignColumnAttribute? foreignColumnAttribute = column.Property.GetAttribute<ForeignColumnAttribute>();
            string primeProperty = String.IsNullOrEmpty(foreignColumnAttribute!.Property) ? column.PropertyName ?? string.Empty : foreignColumnAttribute.Property ?? string.Empty;
            Type? primeType = foreignColumnAttribute.Foreign as Type;
            string foreignTable = primeType is null ? (string)foreignColumnAttribute.Foreign! : primeType.Name!;
            ColumnRef? targetColumn = null;
            // 首先尝试直接通过外键表名找到目标列，如果找不到再通过外键表类型在已连接的表中查找目标列
            if (joinedTables.TryGetValue(foreignTable, out var joinedTable))
            {
                targetColumn = joinedTable.GetColumn(primeProperty);
            }
            else
            {
                foreach (JoinedTable jt in joinedTables.Values)
                {
                    if (jt.TableDefinition.ObjectType == primeType)
                    {
                        if (targetColumn is not null) throw new ArgumentException($"Undeterminate table. More than one table of type {primeType} joined.");
                        joinedTable = jt;
                        targetColumn = jt.GetColumn(primeProperty);
                    }
                }
            }

            if (joinedTable is null)
            {
                throw new ArgumentException($"Foreign table name {foreignTable} of property {column.PropertyName} does not exist in joined tables.");
            }

            // 如果通过外键表名和外键表类型都找不到目标列，则尝试通过外键表类型在已连接的表中查找属性，再生成目标列
            if (targetColumn is null)
            {
                var property = joinedTable.TableDefinition.ObjectType.Find(primeProperty);
                if (property is not null)
                {
                    ForeignColumn? foreignColumn = GenerateForeignColumn(property);
                    if (foreignColumn is not null) targetColumn = GetTargetColumn(joinedTables, foreignColumn);
                }
            }

            // 如果找不到目标列，则抛出异常
            if (targetColumn is null)
                throw new ArgumentException($"Foreign table name {foreignTable} of property {column.PropertyName} does not exist in joined tables.");

            // 标记外键表为已使用
            joinedTable.Used = true;

            return targetColumn;
        }

        /// <summary>
        /// 连接表的核心方法，根据ColumnRef对象中的外键信息在已连接的表中查找是否存在对应的外键表，如果存在则跳过连接操作，如果不存在则根据外键表类型生成JoinedTable对象加入已连接的表。
        /// </summary>
        /// <param name="joinedTables"></param>
        /// <param name="columnRefs"></param>
        private void JoinColumn(ConcurrentDictionary<string, JoinedTable> joinedTables, Queue<ColumnRef> columnRefs)
        {
            ColumnRef columnRef = columnRefs.Dequeue();
            SqlColumn column = columnRef.Column;
            foreach (ForeignTable foreignTableInfo in column.ForeignTables)
            {
                JoinColumn(joinedTables, columnRefs, columnRef, foreignTableInfo);
            }
        }

        /// <summary>
        /// 将指定关联表类型上声明的类级 <see cref="TableJoinAttribute"/> 关联加入连接。
        /// </summary>
        /// <param name="joinedTables">已连接的关联表。</param>
        /// <param name="sourceTable">声明该关联的源关联表，外键默认取自该表。</param>
        /// <param name="tableJoin">源关联表类型上声明的关联定义。</param>
        private void JoinTable(
            ConcurrentDictionary<string, JoinedTable> joinedTables,
            JoinedTable sourceTable,
            TableJoinAttribute tableJoin)
        {
            JoinedTable actualSource = sourceTable;
            if (tableJoin.Source is string sourceName)
            {
                if (!joinedTables.TryGetValue(sourceName, out var namedSource))
                    throw new ArgumentException($"Source table \"{sourceName}\" does not exist in joined tables.");
                actualSource = namedSource;
            }
            else if (tableJoin.Source is Type sourceType)
            {
                JoinedTable? matched = null;
                foreach (JoinedTable candidate in joinedTables.Values)
                {
                    if (candidate.TableDefinition.ObjectType != sourceType) continue;
                    if (matched is not null)
                        throw new ArgumentException($"Undeterminate table. More than one table of type {sourceType} joined.");
                    matched = candidate;
                }
                if (matched is null)
                    throw new ArgumentException($"Source table type {sourceType} does not exist in joined tables.");
                actualSource = matched;
            }

            TableDefinition? targetTableDef = GetTableDefinition(tableJoin.TargetType);
            if (targetTableDef is null) return;

            string alias = String.IsNullOrEmpty(tableJoin.Alias) ? tableJoin.TargetType.Name : tableJoin.Alias!;
            if (joinedTables.TryGetValue(alias, out JoinedTable? existingJoinedTable))
            {
                if (existingJoinedTable.TableDefinition.ObjectType != tableJoin.TargetType)
                    throw new ArgumentException($"Duplicate table alias name \"{alias}\"");
                existingJoinedTable.AutoExpand |= tableJoin.AutoExpand;
                return;
            }

            JoinedTable joinedTable = new JoinedTable(targetTableDef, MetaDataHelper.GetJoinPrimeKeys(targetTableDef, tableJoin.PrimeKeys))
            {
                JoinType = tableJoin.JoinType,
                AutoExpand = tableJoin.AutoExpand,
                Name = alias,
                ConstFilter = MetaDataHelper.AliasConstFilter(targetTableDef.ConstFilter, alias)
            };
            List<ColumnRef> foreignKeys = new List<ColumnRef>();
            foreach (string keyName in tableJoin.ForeignKeys.Split(','))
            {
                ColumnRef? keyColumn = actualSource.GetColumn(keyName);
                if (keyColumn is null)
                    throw new ArgumentException($"Foreign key column \"{keyName}\" does not exist in source table \"{actualSource.Name}\".");
                foreignKeys.Add(keyColumn);
            }
            joinedTable.ForeignKeys = foreignKeys.AsReadOnly();
            if (!joinedTables.TryAdd(alias, joinedTable))
                throw new ArgumentException($"Duplicate table alias name \"{alias}\"");
        }

        private static ForeignTable[] GetForeignTables(PropertyInfo property)
        {
            ForeignTypeAttribute[] foreignTypeAttrs = (ForeignTypeAttribute[])property.GetCustomAttributes(typeof(ForeignTypeAttribute), true);
            if (foreignTypeAttrs.Length == 0) return Array.Empty<ForeignTable>();

            ForeignTable[] foreignTables = new ForeignTable[foreignTypeAttrs.Length];
            for (int i = 0; i < foreignTypeAttrs.Length; i++)
            {
                foreignTables[i] = CreateForeignTable(foreignTypeAttrs[i]);
            }
            return foreignTables;
        }

        private static ForeignTable CreateForeignTable(ForeignTypeAttribute foreignTypeAttr)
        {
            return new ForeignTable()
            {
                ForeignType = foreignTypeAttr.ObjectType,
                JoinType = foreignTypeAttr.JoinType,
                Alias = foreignTypeAttr.Alias,
                AutoExpand = foreignTypeAttr.AutoExpand
            };
        }

        private void JoinColumn(ConcurrentDictionary<string, JoinedTable> joinedTables, Queue<ColumnRef> columnRefs, ColumnRef columnRef, ForeignTable foreignTableInfo)
        {
            string joinedTableName = String.IsNullOrEmpty(foreignTableInfo.Alias) ? foreignTableInfo.ForeignType?.Name ?? string.Empty : foreignTableInfo.Alias ?? string.Empty;
            if (joinedTables.TryGetValue(joinedTableName, out JoinedTable? existingJoinedTable))
            {
                if (existingJoinedTable.TableDefinition.ObjectType != foreignTableInfo.ForeignType)
                    throw new ArgumentException($"Duplicate table alias name \"{joinedTableName}\"");
                if (existingJoinedTable.ForeignKeys is not null
                    && existingJoinedTable.ForeignKeys.Count > 0
                    && !existingJoinedTable.ForeignKeys[0].Equals(columnRef))
                    throw new ArgumentException($"Duplicate table alias name \"{joinedTableName}\"");
                // 同一关联表可能同时被 TableJoin 与 ForeignType 声明，任一声明要求 AutoExpand 都应生效
                existingJoinedTable.AutoExpand |= foreignTableInfo.AutoExpand;
                return;
            }

            TableDefinition? foreignTable = GetTableDefinition(foreignTableInfo.ForeignType!);
            JoinedTable joinedTable = new JoinedTable(foreignTable!)
            {
                JoinType = foreignTableInfo.JoinType,
                AutoExpand = foreignTableInfo.AutoExpand,
                Name = joinedTableName,
                ConstFilter = MetaDataHelper.AliasConstFilter(foreignTable!.ConstFilter, joinedTableName)
            };
            List<ColumnRef> foreignKeys = new List<ColumnRef>();
            foreignKeys.Add(columnRef);
            joinedTable.ForeignKeys = foreignKeys.AsReadOnly();
            if (!joinedTables.TryAdd(joinedTable.Name, joinedTable))
                throw new ArgumentException($"Duplicate table alias name \"{joinedTable.Name}\"");
        }

        /// <summary>
        /// 根据 <see cref="ColumnAttribute.ConverterType"/> 创建列级数据库值转换器实例。
        /// </summary>
        /// <param name="converterType">转换器类型，可为 null。</param>
        /// <param name="property">转换器所应用的属性（用于异常提示）。</param>
        /// <returns>转换器实例；未指定类型时返回 null。</returns>
        private static IDbValueConverter? CreateDbValueConverter(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type? converterType,
            PropertyInfo property)
        {
            if (converterType is null) return null;
            if (!typeof(IDbValueConverter).IsAssignableFrom(converterType))
                throw new ArgumentException($"The ValueConverterType '{converterType.FullName}' on property '{property.DeclaringType?.FullName}.{property.Name}' does not implement IDbValueConverter.");
            object? instance = Activator.CreateInstance(converterType);
            if (instance is not IDbValueConverter converter)
                throw new ArgumentException($"The ValueConverterType '{converterType.FullName}' on property '{property.DeclaringType?.FullName}.{property.Name}' could not be instantiated as IDbValueConverter (a public parameterless constructor is required).");
            return converter;
        }
    }
}
