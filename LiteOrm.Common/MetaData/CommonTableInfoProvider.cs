using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// 通用表信息提供程序。源生成器在编译期收集 <see cref="TableInfo"/> / <see cref="ColumnInfo"/>，
    /// 并在模块初始化器中注册到本类的全局单例；多个程序集可共享同一个提供程序实例，
    /// 避免各程序集生成独立 Provider 时相互覆盖 <see cref="TableInfoProvider.Instance"/>。
    /// </summary>
    public class CommonTableInfoProvider : TableInfoProvider
    {
        /// <summary>
        /// 全局单例实例。
        /// </summary>
        public new static CommonTableInfoProvider Instance { get; } = new CommonTableInfoProvider();

        private readonly ConcurrentDictionary<Type, TableInfo> _tableInfos = new();
        private readonly ConcurrentDictionary<Type, TableDefinition> _tables = new();
        private readonly ConcurrentDictionary<Type, TableView> _views = new();
        private readonly object _syncLock = new();

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
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
            Type objectType)
        {
            if (objectType is null) return null;
            if (_tables.TryGetValue(objectType, out var tableDef)) return tableDef;

            lock (_syncLock)
            {
                if (_tables.TryGetValue(objectType, out tableDef)) return tableDef;
                if (!_tableInfos.TryGetValue(objectType, out var tableInfo)) return null;

                tableDef = BuildTableDefinition(tableInfo);
                if (tableDef is not null) _tables[objectType] = tableDef;
                return tableDef;
            }
        }

        /// <inheritdoc />
        public override TableView? GetTableView(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
            Type objectType)
        {
            if (objectType is null) return null;
            if (_views.TryGetValue(objectType, out var cachedView)) return cachedView;

            lock (_syncLock)
            {
                if (_views.TryGetValue(objectType, out cachedView)) return cachedView;

                var tableDef = GetTableDefinition(objectType);
                if (tableDef is null) return null;

                var columns = new List<SqlColumn>();
                foreach (var column in tableDef.Columns)
                    columns.Add(column);

                var view = new TableView(tableDef, columns, new List<JoinedTable>()) { Name = objectType.Name };
                _views[objectType] = view;
                return view;
            }
        }

        private static TableDefinition BuildTableDefinition(TableInfo tableInfo)
        {
            var columns = new List<ColumnDefinition>(tableInfo.Columns.Count);

            foreach (var columnInfo in tableInfo.Columns)
            {
                if (string.IsNullOrEmpty(columnInfo.PropertyName)) continue;

                var property = tableInfo.ObjectType.GetProperty(
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
                    IdentityExpression = columnInfo.IdentityExpression,
                    IdentityStart = columnInfo.IdentityStart,
                    IdentityIncreasement = columnInfo.IdentityIncreasement
                };
                column.DbValueConverter = columnInfo.ValueConverter;

                columns.Add(column);
            }

            return new TableDefinition(tableInfo.ObjectType, columns)
            {
                Name = tableInfo.Name,
                DataSource = tableInfo.DataSource,
                SyncTable = tableInfo.SyncTable
            };
        }
    }
}
