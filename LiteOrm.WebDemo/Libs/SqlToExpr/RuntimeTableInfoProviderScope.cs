using LiteOrm.Common;
using System;
using System.Collections;
using System.Collections.Generic;

namespace LiteOrm.SqlToExpr;

internal sealed class RuntimeTableInfoProviderScope : IDisposable
{
    private readonly TableInfoProvider? _previous;

    public RuntimeTableInfoProviderScope(ISqlBuilder sqlBuilder)
    {
        _previous = TableInfoProvider.Default;
        TableInfoProvider.Default = new AttributeTableInfoProvider(
            new StaticSqlBuilderFactory(sqlBuilder),
            new StaticDataSourceProvider(sqlBuilder));
    }

    public void Dispose()
    {
        TableInfoProvider.Default = _previous;
    }

    private sealed class StaticSqlBuilderFactory : ISqlBuilderFactory
    {
        private readonly ISqlBuilder _sqlBuilder;

        public StaticSqlBuilderFactory(ISqlBuilder sqlBuilder)
        {
            _sqlBuilder = sqlBuilder;
        }

        public ISqlBuilder GetSqlBuilder(Type providerType, string? dataSourceName = null)
        {
            return _sqlBuilder;
        }
    }

    private sealed class StaticDataSourceProvider : IDataSourceProvider
    {
        private readonly DataSourceConfig _config;

        public StaticDataSourceProvider(ISqlBuilder sqlBuilder)
        {
            _config = new DataSourceConfig
            {
                Name = "default",
                Provider = sqlBuilder.GetType().AssemblyQualifiedName ?? typeof(object).AssemblyQualifiedName!
            };
        }

        public string DefaultDataSourceName => "default";

        public DataSourceConfig GetDataSource(string? name)
        {
            return _config;
        }

        public IEnumerator<DataSourceConfig> GetEnumerator()
        {
            yield return _config;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}