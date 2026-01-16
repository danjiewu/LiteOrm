using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Data;
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using LiteOrm.Common;
using System.Collections.Concurrent;

namespace LiteOrm
{
    /// <summary>
    /// 根据Attribute的表信息提供者
    /// </summary>
    [AutoRegister(ServiceLifetime.Singleton, serviceTypes: typeof(TableInfoProvider))]
    public class AttributeTableInfoProvider : TableInfoProvider
    {
        private readonly ConcurrentDictionary<Type, TableDefinition> _tableInfoCache = new ConcurrentDictionary<Type, TableDefinition>();
        private readonly ConcurrentDictionary<PropertyInfo, ColumnDefinition> _columnCache = new ConcurrentDictionary<PropertyInfo, ColumnDefinition>();
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
            if (!_tableInfoCache.ContainsKey(objectType))
            {
                lock (_syncLock)
                {
                    if (!_tableInfoCache.ContainsKey(objectType))
                        _tableInfoCache[objectType] = GenerateTableDefinition(objectType);
                }
            }
            return _tableInfoCache[objectType];
        }

        /// <summary>
        /// 根据对象类型得到表以及关联信息
        /// </summary>
        /// <param name="objectType">对象类型</param>
        /// <returns>表信息</returns>
        public override TableView GetTableView(Type objectType)
        {
            if (objectType is null) return null;
            if (!_tableViewCache.ContainsKey(objectType))
            {
                lock (_syncLock)
                {
                    if (!_tableViewCache.ContainsKey(objectType))
                        _tableViewCache[objectType] = GenerateTableView(objectType);
                }
            }
            return _tableViewCache[objectType];
        }

        #region

        /// <summary>
        /// 根据属性得到对应字段的数据库列定义
        /// </summary>
        /// <param name="property">对象的属性</param>
        /// <param name="objectType">对象类型</param>
        /// <returns>数据库列定义</returns>
        private ColumnDefinition GetColumnDefinition(PropertyInfo property, Type objectType)
        {
            if (property is null) return null;
            if (!_columnCache.ContainsKey(property))
            {
                lock (_syncLock)
                {
                    if (!_columnCache.ContainsKey(property))
                    {
                        TableAttribute tableAttribute = objectType.GetAttribute<TableAttribute>();
                        _columnCache[property] = GenerateColumnDefinition(property, _sqlBuilderFactory.GetSqlBuilder(_dataSourceProvider.GetDataSource(tableAttribute.DataSource).ProviderType));
                    }
                }
            }
            return _columnCache[property];
        }

        private TableDefinition GenerateTableDefinition(Type objectType)
        {
            TableAttribute tableAttribute = objectType.GetAttribute<TableAttribute>();
            if (tableAttribute is not null)
            {
                string tableName = tableAttribute.TableName;
                if (String.IsNullOrEmpty(tableName)) tableName = objectType.Name;
                List<ColumnDefinition> columns = new List<ColumnDefinition>();
                foreach (PropertyInfo property in objectType.GetProperties())
                {
                    ColumnDefinition column = GetColumnDefinition(property, objectType);
                    if (column is not null)
                    {
                        columns.Add(column);
                    }
                }
                return new TableDefinition(objectType, columns) { Name = tableName, DataProviderType = _dataSourceProvider.GetDataSource(tableAttribute.DataSource).ProviderType, DataSource = tableAttribute.DataSource ?? _dataSourceProvider.DefaultDataSourceName };
            }
            return null;
        }

        private ColumnDefinition GenerateColumnDefinition(PropertyInfo property, ISqlBuilder sqlBuilder)
        {
            if (property.GetIndexParameters().Length != 0) return null;
            ForeignTypeAttribute foreignTypeAttr = property.GetAttribute<ForeignTypeAttribute>();

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
                    column.Mode = columnAttribute.ColumnMode & ((property.CanRead ? ColumnMode.Write : ColumnMode.None) | (property.CanWrite ? ColumnMode.Read : ColumnMode.None));
                    if (foreignTypeAttr is not null)
                    {
                        column.ForeignTable = new ForeignTable() { ForeignType = foreignTypeAttr.ObjectType, FilterExpression = foreignTypeAttr.FilterExpression };
                    }
                    column.ForeignAlias = foreignTypeAttr is null ? null : foreignTypeAttr.Alias;
                    return column;
                }
            }
            else
            {
                ColumnDefinition column = new ColumnDefinition(property);
                column.Name = property.Name;
                column.Mode = (property.CanRead ? ColumnMode.Write : ColumnMode.None) | (property.CanWrite ? ColumnMode.Read : ColumnMode.None);
                column.DbType = sqlBuilder.GetDbType(property.PropertyType);
                column.Length = DbTypeMap.GetDefaultLength(column.DbType);
                column.AllowNull = property.PropertyType.IsValueType ? Nullable.GetUnderlyingType(column.PropertyType) is not null : true;
                if (foreignTypeAttr is not null)
                {
                    column.ForeignTable = new ForeignTable() { ForeignType = foreignTypeAttr.ObjectType, FilterExpression = foreignTypeAttr.FilterExpression };
                }
                column.ForeignAlias = foreignTypeAttr is null ? null : foreignTypeAttr.Alias;
                return column;
            }
        }

        private static ForeignColumn GenerateForeignColumn(PropertyInfo property)
        {
            if (property.GetIndexParameters().Length != 0) return null;

            ForeignColumnAttribute foreignColumnAttribute = property.GetAttribute<ForeignColumnAttribute>();
            if (foreignColumnAttribute is not null)
            {
                ForeignTypeAttribute foreignTypeAttr = property.GetAttribute<ForeignTypeAttribute>();

                ForeignColumn foreignColumn = new ForeignColumn(property);
                if (foreignTypeAttr is not null)
                {
                    foreignColumn.ForeignTable = new ForeignTable() { ForeignType = foreignTypeAttr.ObjectType, FilterExpression = foreignTypeAttr.FilterExpression };
                    foreignColumn.ForeignAlias = foreignTypeAttr.Alias;
                }
                return foreignColumn;
            }
            else
            {
                return null;
            }
        }

        private TableView GenerateTableView(Type objectType)
        {
            TableJoinAttribute[] atts = (TableJoinAttribute[])objectType.GetCustomAttributes(typeof(TableJoinAttribute), true);
            ConcurrentDictionary<string, JoinedTable> joinedTables = new ConcurrentDictionary<string, JoinedTable>(StringComparer.OrdinalIgnoreCase);

            foreach (TableJoinAttribute tableJoin in atts)
            {
                JoinedTable joinedTable = new JoinedTable(GetTableDefinition(tableJoin.TargetType));
                if (String.IsNullOrEmpty(tableJoin.AliasName))
                    tableJoin.AliasName = joinedTable.Name = tableJoin.TargetType.Name;
                else
                    joinedTable.Name = tableJoin.AliasName;
                joinedTable.FilterExpression = tableJoin.FilterExpression;
                if (joinedTables.ContainsKey(joinedTable.Name)) throw new ArgumentException(String.Format("Duplicate table alias name \"{0}\"", joinedTable.Name));
                joinedTables[joinedTable.Name] = joinedTable;
            }

            List<SqlColumn> columns = new List<SqlColumn>();
            foreach (PropertyInfo property in objectType.GetProperties())
            {
                ColumnDefinition column = GetColumnDefinition(property, objectType);
                if (column is not null)
                {
                    columns.Add(column);
                }
            }

            foreach (PropertyInfo property in objectType.GetProperties())
            {
                ForeignColumn foreignColumn = GenerateForeignColumn(property);
                if (foreignColumn is not null) columns.Add(foreignColumn);
            }

            Queue<ColumnRef> columnRefs = new Queue<ColumnRef>();
            foreach (SqlColumn column in columns)
            {
                columnRefs.Enqueue(new ColumnRef(column));
            }
            while (columnRefs.Count > 0)
            {
                JoinColumn(joinedTables, columnRefs);
            }

            HashSet<JoinedTable> usedTables = new HashSet<JoinedTable>();

            foreach (SqlColumn column in columns)
            {
                if (column is ForeignColumn)
                {
                    ForeignColumn foreignColumn = column as ForeignColumn;
                    foreignColumn.TargetColumn = GetTargetColumn(joinedTables, foreignColumn, usedTables);
                }
            }

            TableView tableView = new TableView(GetTableDefinition(objectType), usedTables, columns) { Name = objectType.Name };

            foreach (TableJoinAttribute tableJoin in atts)
            {
                if (tableJoin.Source is null)
                {
                    List<ColumnRef> foreignKeys = new List<ColumnRef>();
                    foreach (string keyName in tableJoin.ForeignKeys.Split(','))
                    {
                        foreignKeys.Add(new ColumnRef(tableView.GetColumn(keyName)));
                    }
                    joinedTables[tableJoin.AliasName].ForeignKeys = foreignKeys.AsReadOnly();
                }
                else
                {
                    JoinedTable sourceTable = null;
                    if (tableJoin.Source is string)
                    {
                        string sourceName = (string)tableJoin.Source;
                        if (!joinedTables.ContainsKey(sourceName))
                            throw new ArgumentException(String.Format("Source table \"{0}\" does not exist in joined tables.", sourceName));
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
                                    throw new ArgumentException(String.Format("Undeterminate table. More than one table of type {0} joined.", sourceType));
                            }
                        }
                        if (sourceTable is null)
                            throw new ArgumentException(String.Format("Source table type {0} does not exist in joined tables.", sourceType));
                    }
                    List<ColumnRef> foreignKeys = new List<ColumnRef>();
                    foreach (string keyName in tableJoin.ForeignKeys.Split(','))
                    {
                        foreignKeys.Add(sourceTable.GetColumn(keyName));
                    }
                    joinedTables[tableJoin.AliasName].ForeignKeys = foreignKeys.AsReadOnly();
                }
            }

            return tableView;
        }

        private static ColumnRef GetTargetColumn(ConcurrentDictionary<string, JoinedTable> joinedTables, ForeignColumn column, HashSet<JoinedTable> usedTables)
        {
            ForeignColumnAttribute foreignColumnAttribute = column.Property.GetAttribute<ForeignColumnAttribute>();
            string primeProperty = String.IsNullOrEmpty(foreignColumnAttribute.Property) ? column.PropertyName : foreignColumnAttribute.Property;
            Type primeType = foreignColumnAttribute.Foreign as Type;
            string foreignTable = primeType is null ? (string)foreignColumnAttribute.Foreign : primeType.Name;
            ColumnRef targetColumn = null;
            if (!joinedTables.ContainsKey(foreignTable))
                throw new ArgumentException(String.Format("Foreign table name {0} of property {1} does not exist in joined tables.", foreignColumnAttribute.Foreign, column.PropertyName));

            targetColumn = joinedTables[foreignTable].GetColumn(primeProperty);
            if (targetColumn is null)
            {
                var property = joinedTables[foreignTable].TableDefinition.ObjectType.GetProperty(primeProperty);
                if (property is not null)
                {
                    ForeignColumn foreignColumn = GenerateForeignColumn(property);
                    if (foreignColumn is not null) targetColumn = GetTargetColumn(joinedTables, foreignColumn, usedTables);
                }
            }

            JoinedTable usedTable = joinedTables[foreignTable];
            while (usedTable is not null)
            {
                usedTables.Add(usedTable);
                if (usedTable.ForeignKeys.Count > 0)
                    usedTable = usedTable.ForeignKeys[0].Table as JoinedTable;
                else
                    usedTable = null;
            }

            if (targetColumn is null)
                throw new ArgumentException(String.Format("Foreign property {0} does not exist in type {1}.", primeProperty, joinedTables[foreignTable].TableDefinition.ObjectType));

            return targetColumn;
        }

        private void JoinColumn(ConcurrentDictionary<string, JoinedTable> joinedTables, Queue<ColumnRef> columnRefs)
        {
            ColumnRef columnRef = columnRefs.Dequeue();
            SqlColumn column = columnRef.Column;
            if (column.ForeignType is not null)
            {
                bool foreignTypeExists = false;
                foreach (JoinedTable joinedTable in joinedTables.Values)
                {
                    if (joinedTable.TableDefinition.ObjectType == column.ForeignType && (joinedTable.Name == column.ForeignAlias || String.IsNullOrEmpty(column.ForeignAlias)))
                    {
                        foreignTypeExists = true;
                        break;
                    }
                }

                if (!foreignTypeExists)
                {
                    TableDefinition foreignTable = GetTableDefinition(column.ForeignType);
                    JoinedTable joinedTable = new JoinedTable(foreignTable);
                    if (String.IsNullOrEmpty(column.ForeignAlias))
                        joinedTable.Name = column.ForeignType.Name;
                    else
                        joinedTable.Name = column.ForeignAlias;
                    List<ColumnRef> foreignKeys = new List<ColumnRef>();
                    foreignKeys.Add(columnRef);
                    joinedTable.FilterExpression = column.ForeignTable.FilterExpression;
                    joinedTable.ForeignKeys = foreignKeys.AsReadOnly();
                    joinedTables[joinedTable.Name] = joinedTable;

                    foreach (SqlColumn lcolumn in foreignTable.Columns)
                    {
                        columnRefs.Enqueue(new ColumnRef(joinedTable, lcolumn));
                    }
                }
            }
        }

        #endregion
    }
}
