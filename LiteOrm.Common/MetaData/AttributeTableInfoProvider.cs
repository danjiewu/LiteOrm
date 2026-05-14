using LiteOrm.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;

namespace LiteOrm
{
    /// <summary>
    /// 根据Attribute的表信息提供者
    /// </summary>
    [AutoRegister(Lifetime.Singleton, serviceTypes: typeof(TableInfoProvider))]
    public class AttributeTableInfoProvider : TableInfoProvider
    {
        private readonly ConcurrentDictionary<Type, TableDefinition> _tableInfoCache = new ConcurrentDictionary<Type, TableDefinition>();
        private readonly ConcurrentDictionary<Type, TableView> _tableViewCache = new ConcurrentDictionary<Type, TableView>();
        private readonly ISqlBuilderFactory _sqlBuilderFactory;
        private readonly IDataSourceProvider _dataSourceProvider;
        private readonly object _syncLock = new object();

        /// <summary>
        /// 初始化 <see cref="AttributeTableInfoProvider"/> 类的新实例。
        /// </summary>
        /// <param name="sqlBuilderFactory">SQL 构建器工厂。</param>
        /// <param name="dataSourceProvider">数据源提供者。</param>
        public AttributeTableInfoProvider(ISqlBuilderFactory sqlBuilderFactory, IDataSourceProvider dataSourceProvider)
        {
            _sqlBuilderFactory = sqlBuilderFactory ?? throw new ArgumentNullException(nameof(sqlBuilderFactory));
            _dataSourceProvider = dataSourceProvider ?? throw new ArgumentNullException(nameof(dataSourceProvider));
        }

        /// <summary>
        /// 根据对象类型得到对应的数据库表定义
        /// </summary>
        /// <param name="objectType">对象类型</param>
        /// <returns>表定义</returns>
        public override TableDefinition GetTableDefinition(Type objectType)
        {
            if (objectType is null) return null;
            if (_tableInfoCache.TryGetValue(objectType, out var tableDef)) return tableDef;

            lock (_syncLock)
            {
                if (_tableInfoCache.TryGetValue(objectType, out tableDef)) return tableDef;
                tableDef = GenerateTableDefinition(objectType);
                if (tableDef != null) _tableInfoCache[objectType] = tableDef;
                return tableDef;
            }
        }

        /// <summary>
        /// 根据对象类型得到表以及关联信息
        /// </summary>
        /// <param name="objectType">对象类型</param>
        /// <returns>表信息</returns>
        public override TableView GetTableView(Type objectType)
        {
            if (objectType is null) return null;
            if (_tableViewCache.TryGetValue(objectType, out var tableView)) return tableView;

            lock (_syncLock)
            {
                if (_tableViewCache.TryGetValue(objectType, out tableView)) return tableView;
                tableView = GenerateTableView(objectType);
                if (tableView != null) _tableViewCache[objectType] = tableView;
                return tableView;
            }
        }

        #region
        private TableDefinition GenerateTableDefinition(Type objectType)
        {
            TableAttribute tableAttribute = objectType.GetAttribute<TableAttribute>();
            if (tableAttribute is null) return null;

            string tableName = tableAttribute.TableName;
            if (String.IsNullOrEmpty(tableName)) tableName = objectType.Name;

            var dsConfig = _dataSourceProvider.GetDataSource(tableAttribute.DataSource);
            if (dsConfig == null)
            {
                throw new InvalidOperationException($"Data source '{tableAttribute.DataSource ?? "default"}' not found for type '{objectType.FullName}'. Check your configuration.");
            }

            var sqlBuilder = _sqlBuilderFactory.GetSqlBuilder(dsConfig.ProviderType, tableAttribute.DataSource);

            List<ColumnDefinition> columns = new List<ColumnDefinition>();

            var properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList().SortProperty();
            foreach (PropertyInfo property in properties)
            {
                ColumnDefinition column = GenerateColumnDefinition(property, sqlBuilder);
                if (column is not null)
                {
                    columns.Add(column);
                }
            }
            return new TableDefinition(objectType, columns)
            {
                Name = tableName,
                DataProviderType = dsConfig.ProviderType,
                DataSource = tableAttribute.DataSource ?? _dataSourceProvider.DefaultDataSourceName,
                ConstFilter = BuildConstFilter(columns)
            };
        }

        private ColumnDefinition GenerateColumnDefinition(PropertyInfo property, ISqlBuilder sqlBuilder)
        {
            if (property.GetIndexParameters().Length != 0) return null;
            ForeignTable[] foreignTables = GetForeignTables(property);

            if (property.GetAttribute<ForeignColumnAttribute>() is not null) return null;

            ColumnAttribute columnAttribute = property.GetAttribute<ColumnAttribute>();
            if (columnAttribute is not null)
            {
                if (!columnAttribute.IsColumn)
                {
                    return null;
                }
                else
                {
                    ColumnDefinition column = new ColumnDefinition(property);
                    if (!String.IsNullOrEmpty(columnAttribute.ColumnName)) column.Name = columnAttribute.ColumnName;
                    column.IsPrimaryKey = columnAttribute.IsPrimaryKey;
                    column.IsIdentity = columnAttribute.IsIdentity;
                    column.IdentityExpression = columnAttribute.IdentityExpression;
                    column.IsUnique = columnAttribute.IsUnique;
                    column.IsIndex = columnAttribute.IsIndex;
                    column.DbType = columnAttribute.DbType == DbType.Object ? sqlBuilder.GetDbType(property.PropertyType) : columnAttribute.DbType;
                    column.Length = columnAttribute.Length == 0 ? DbTypeMap.GetDefaultLength(column.DbType) : columnAttribute.Length;
                    column.AllowNull = columnAttribute.AllowNull && (property.PropertyType.IsValueType ? Nullable.GetUnderlyingType(property.PropertyType) is not null : true);
                    column.DefaultValue = columnAttribute.DefaultValue;
                    column.Constant = ParseConstant(property, columnAttribute.Constant);
                    column.IdentityIncreasement = columnAttribute.IdentityIncreasement;
                    column.Mode = columnAttribute.ColumnMode & ((property.CanRead ? ColumnMode.Write : ColumnMode.None) | (property.CanWrite ? ColumnMode.Read : ColumnMode.None));
                    column.ForeignTables = foreignTables;
                    return column;
                }
            }
            else
            {
                DbType dbType = sqlBuilder.GetDbType(property.PropertyType);
                if (dbType == DbType.Object) return null;

                ColumnDefinition column = new ColumnDefinition(property);
                column.Name = property.Name;
                column.Mode = (property.CanRead ? ColumnMode.Write : ColumnMode.None) | (property.CanWrite ? ColumnMode.Read : ColumnMode.None);
                column.DbType = dbType;
                column.Length = DbTypeMap.GetDefaultLength(column.DbType);
                column.AllowNull = property.PropertyType.IsValueType ? Nullable.GetUnderlyingType(column.PropertyType) is not null : true;
                column.ForeignTables = foreignTables;
                return column;
            }
        }

        private static ForeignColumn GenerateForeignColumn(PropertyInfo property)
        {
            if (property.GetIndexParameters().Length != 0) return null;

            ForeignColumnAttribute foreignColumnAttribute = property.GetAttribute<ForeignColumnAttribute>();
            if (foreignColumnAttribute is not null)
            {
                ForeignColumn foreignColumn = new ForeignColumn(property);
                foreignColumn.ForeignTables = GetForeignTables(property);
                return foreignColumn;
            }
            else
            {
                return null;
            }
        }

        private TableView GenerateTableView(Type objectType)
        {
            var tableDef = GetTableDefinition(objectType);
            if (tableDef == null) return null;

            var sqlBuilder = _sqlBuilderFactory.GetSqlBuilder(tableDef.DataProviderType, tableDef.DataSource);

            TableJoinAttribute[] atts = (TableJoinAttribute[])objectType.GetCustomAttributes(typeof(TableJoinAttribute), true);
            ConcurrentDictionary<string, JoinedTable> joinedTables = new ConcurrentDictionary<string, JoinedTable>(StringComparer.OrdinalIgnoreCase);

            // 首先根据TableJoinAttribute连接表，生成JoinedTable对象，并加入joinedTables字典
            foreach (TableJoinAttribute tableJoin in atts)
            {
                var targetTableDef = GetTableDefinition(tableJoin.TargetType);
                if (targetTableDef == null) continue;

                JoinedTable joinedTable = new JoinedTable(targetTableDef, GetJoinPrimeKeys(targetTableDef, tableJoin))
                {
                    JoinType = tableJoin.JoinType,
                    AutoExpand = tableJoin.AutoExpand
                };
                if (String.IsNullOrEmpty(tableJoin.Alias))
                    tableJoin.Alias = joinedTable.Name = tableJoin.TargetType.Name;
                else
                    joinedTable.Name = tableJoin.Alias;
                joinedTable.ConstFilter = AliasConstFilter(targetTableDef.ConstFilter, joinedTable.Name);
                if (joinedTables.ContainsKey(joinedTable.Name)) throw new ArgumentException($"Duplicate table alias name \"{joinedTable.Name}\"");
                joinedTables[joinedTable.Name] = joinedTable;
            }

            // 根据属性连接表，生成ColumnRef对象，并加入连接队列
            List<SqlColumn> columns = new List<SqlColumn>();
            var properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList().SortProperty();
            foreach (PropertyInfo property in properties)
            {
                ColumnDefinition column = GenerateColumnDefinition(property, sqlBuilder);
                if (column is not null)
                {
                    columns.Add(column);
                }
                else
                {
                    // 根据属性上的ForeignColumnAttribute连接表，生成ForeignColumn对象，并加入连接队列
                    ForeignColumn foreignColumn = GenerateForeignColumn(property);
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
            // 根据连接结果设置ForeignColumn的TargetColumn属性
            foreach (SqlColumn column in columns)
            {
                if (column is ForeignColumn foreignColumn)
                {
                    foreignColumn.TargetColumn = GetTargetColumn(joinedTables, foreignColumn);
                }
            }

            // 创建TableView对象

            TableView tableView = new TableView(tableDef, joinedTables.Values, columns) { Name = objectType.Name };
            // 根据TableJoinAttribute设置JoinedTable的ForeignKeys属性
            foreach (TableJoinAttribute tableJoin in atts)
            {
                // 如果Source为null，表示外键来自主表，根据ForeignKeys属性指定的列名从主表中找到对应的ColumnRef作为外键列
                if (tableJoin.Source is null)
                {
                    List<ColumnRef> foreignKeys = new List<ColumnRef>();
                    foreach (string keyName in tableJoin.ForeignKeys.Split(','))
                    {
                        foreignKeys.Add(new ColumnRef(tableView.GetColumn(keyName)));
                    }
                    joinedTables[tableJoin.Alias].ForeignKeys = foreignKeys.AsReadOnly();
                }
                //如果Source不为null，表示外键来自其他已连接的表，根据Source指定的表名或表类型找到对应的JoinedTable，再根据ForeignKeys属性指定的列名从该JoinedTable中找到对应的ColumnRef作为外键列
                else
                {
                    JoinedTable sourceTable = null;
                    if (tableJoin.Source is string)
                    {
                        string sourceName = (string)tableJoin.Source;
                        if (!joinedTables.ContainsKey(sourceName))
                            throw new ArgumentException($"Source table \"{sourceName}\" does not exist in joined tables.");
                        sourceTable = joinedTables[sourceName];
                    }
                    else
                    {
                        Type sourceType = (Type)tableJoin.Source;
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
                        foreignKeys.Add(sourceTable.GetColumn(keyName));
                    }
                    joinedTables[tableJoin.Alias].ForeignKeys = foreignKeys.AsReadOnly();
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
            ForeignColumnAttribute foreignColumnAttribute = column.Property.GetAttribute<ForeignColumnAttribute>();
            string primeProperty = String.IsNullOrEmpty(foreignColumnAttribute.Property) ? column.PropertyName : foreignColumnAttribute.Property;
            Type primeType = foreignColumnAttribute.Foreign as Type;
            string foreignTable = primeType is null ? (string)foreignColumnAttribute.Foreign : primeType.Name;
            ColumnRef targetColumn = null;
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
                var property = joinedTable.TableDefinition.ObjectType.GetProperty(primeProperty);
                if (property is not null)
                {
                    ForeignColumn foreignColumn = GenerateForeignColumn(property);
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
        /// 连接表的核心方法，根据ColumnRef对象中的外键信息在已连接的表中查找是否存在对应的外键表，如果存在则跳过连接操作，如果不存在则根据外键表类型生成JoinedTable对象加入已连接的表，并将该JoinedTable对象中的ColumnRef对象加入连接队列，以便进行后续的连接操作。
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

        private static ColumnDefinition[] GetJoinPrimeKeys(TableDefinition targetTableDef, TableJoinAttribute tableJoin)
        {
            if (String.IsNullOrWhiteSpace(tableJoin.PrimeKeys))
            {
                return targetTableDef.Keys;
            }

            string[] primeKeyNames = tableJoin.PrimeKeys.Split(',');
            ColumnDefinition[] primeKeys = new ColumnDefinition[primeKeyNames.Length];
            for (int i = 0; i < primeKeyNames.Length; i++)
            {
                string primeKeyName = primeKeyNames[i].Trim();
                ColumnDefinition primeKey = targetTableDef.GetColumn(primeKeyName);
                if (primeKey == null)
                    throw new ArgumentException($"Prime key column \"{primeKeyName}\" does not exist in target table \"{targetTableDef.ObjectType.Name}\".");
                primeKeys[i] = primeKey;
            }
            return primeKeys;
        }

        private void JoinColumn(ConcurrentDictionary<string, JoinedTable> joinedTables, Queue<ColumnRef> columnRefs, ColumnRef columnRef, ForeignTable foreignTableInfo)
        {
            string joinedTableName = String.IsNullOrEmpty(foreignTableInfo.Alias) ? foreignTableInfo.ForeignType.Name : foreignTableInfo.Alias;
            if (joinedTables.TryGetValue(joinedTableName, out JoinedTable existingJoinedTable))
            {
                if (existingJoinedTable.TableDefinition.ObjectType != foreignTableInfo.ForeignType)
                    throw new ArgumentException($"Duplicate table alias name \"{joinedTableName}\"");
                if (existingJoinedTable.ForeignKeys is not null
                    && existingJoinedTable.ForeignKeys.Count > 0
                    && !existingJoinedTable.ForeignKeys[0].Equals(columnRef))
                    throw new ArgumentException($"Duplicate table alias name \"{joinedTableName}\"");
                return;
            }

            TableDefinition foreignTable = GetTableDefinition(foreignTableInfo.ForeignType);
            JoinedTable joinedTable = new JoinedTable(foreignTable)
            {
                JoinType = foreignTableInfo.JoinType,
                AutoExpand = foreignTableInfo.AutoExpand,
                Name = joinedTableName,
                ConstFilter = AliasConstFilter(foreignTable.ConstFilter, joinedTableName)
            };
            List<ColumnRef> foreignKeys = new List<ColumnRef>();
            foreignKeys.Add(columnRef);
            joinedTable.ForeignKeys = foreignKeys.AsReadOnly();
            if (!joinedTables.TryAdd(joinedTable.Name, joinedTable))
                throw new ArgumentException($"Duplicate table alias name \"{joinedTable.Name}\"");

            // 如果所引用外表的AutoExpand为true，将该外表中的列加入连接队列，自动扩展连接的外表
            if (joinedTable.AutoExpand)
            {
                foreach (SqlColumn lcolumn in foreignTable.Columns)
                {
                    columnRefs.Enqueue(new ColumnRef(joinedTable, lcolumn));
                }
            }
        }

        private static LogicExpr BuildConstFilter(IEnumerable<ColumnDefinition> columns)
        {
            LogicExpr constFilter = null;
            foreach (ColumnDefinition column in columns)
            {
                if (column.Constant is null) continue;
                constFilter = constFilter.And(Expr.PropEqual(column.PropertyName, column.Constant));
            }
            return constFilter;
        }

        private static LogicExpr AliasConstFilter(LogicExpr constFilter, string tableAlias)
        {
            if (constFilter is null) return null;
            LogicExpr aliasedFilter = (LogicExpr)constFilter.Clone();
            ExprVisitor.Visit(node =>
            {
                if (node is PropertyExpr propertyExpr && String.IsNullOrEmpty(propertyExpr.TableAlias))
                {
                    propertyExpr.TableAlias = tableAlias;
                }
                return true;
            }, aliasedFilter);
            return aliasedFilter;
        }

        private static object ParseConstant(PropertyInfo property, object constant)
        {
            if (constant is null) return null;

            Type propType = property.PropertyType.GetUnderlyingType();
            Type constantType = constant.GetType();
            if (constantType == propType) return constant;

            if (propType.IsEnum)
            {
                if (constant is string text)
                {
                    text = text.Trim();
                    if (text.Length == 0)
                        throw new ArgumentException($"Column.Constant cannot be empty for enum property \"{property.DeclaringType?.FullName}.{property.Name}\".");
                    if (Int32.TryParse(text, out int intValue))
                        return Enum.ToObject(propType, intValue);
                    return EnumUtil.Parse(propType, text) ?? Enum.Parse(propType, text);
                }
                else if (IsIntegerType(constantType))
                {
                    object numericValue = Convert.ChangeType(constant, Enum.GetUnderlyingType(propType));
                    return Enum.ToObject(propType, numericValue);
                }
            }
            try
            {
                return Convert.ChangeType(constant, Enum.GetUnderlyingType(propType));
            }
            catch { }
            return constant;
        }

        private static bool IsIntegerType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type == typeof(byte)
                || type == typeof(sbyte)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(int)
                || type == typeof(uint)
                || type == typeof(long)
                || type == typeof(ulong);
        }

        #endregion
    }
}
