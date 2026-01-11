using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm
{
    public interface IBulkInsertProvider
    {
        Type DbConnectionType { get; }
        int BulkInsert(DataTable dt, DAOContext context);
    }

    public static class BulkInsertProviderFactory
    {
        private static readonly ConcurrentDictionary<Type, IBulkInsertProvider> _providers = new  ConcurrentDictionary<Type, IBulkInsertProvider>();
        public static void RegisterProvider(IBulkInsertProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            _providers[provider.DbConnectionType] = provider;
        }
        public static IBulkInsertProvider? GetProvider(Type dbConnectionType)
        {
            return _providers.TryGetValue(dbConnectionType, out var provider) ? provider : null;
        }
    }
}
