using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LiteOrm
{
    internal sealed class LiteOrmScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceScopeFactory _innerScopeFactory;
        private readonly LiteOrmRuntimeRegistry _runtimeRegistry;

        public LiteOrmScopeFactory(IServiceScopeFactory innerScopeFactory, LiteOrmRuntimeRegistry runtimeRegistry)
        {
            _innerScopeFactory = innerScopeFactory ?? throw new ArgumentNullException(nameof(innerScopeFactory));
            _runtimeRegistry = runtimeRegistry ?? throw new ArgumentNullException(nameof(runtimeRegistry));
        }

        public IServiceScope CreateScope()
        {
            return new LiteOrmServiceScope(_innerScopeFactory.CreateScope(), _runtimeRegistry);
        }
    }

    internal static class LiteOrmServiceScopeFactoryPatcher
    {
        private static readonly ConditionalWeakTable<IServiceScopeFactory, LiteOrmScopeFactory> _scopeFactoryCache = new();
        private static readonly object _patchLock = new();

        public static void Patch(IServiceProvider innerProvider, LiteOrmRuntimeRegistry runtimeRegistry)
        {
            if (innerProvider is null) throw new ArgumentNullException(nameof(innerProvider));
            if (runtimeRegistry is null) throw new ArgumentNullException(nameof(runtimeRegistry));

            var providerType = innerProvider.GetType();
            if (!string.Equals(providerType.FullName, "Microsoft.Extensions.DependencyInjection.ServiceProvider", StringComparison.Ordinal))
                return;

            lock (_patchLock)
            {
                // Built-in DI hardcodes IServiceScopeFactory, so registration replacement is ineffective.
                // Force the accessor to materialize first, then swap its realized delegate in place.
                _ = innerProvider.GetRequiredService<IServiceScopeFactory>();

                var serviceAccessorsField = providerType.GetField("_serviceAccessors", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Failed to locate built-in DI service accessor cache.");
                var serviceAccessors = serviceAccessorsField.GetValue(innerProvider) as IDictionary
                    ?? throw new InvalidOperationException("Built-in DI service accessor cache has unexpected type.");

                foreach (DictionaryEntry entry in serviceAccessors)
                {
                    if (!string.Equals(entry.Key?.ToString(), typeof(IServiceScopeFactory).FullName, StringComparison.Ordinal))
                        continue;

                    var accessor = entry.Value ?? throw new InvalidOperationException("Built-in DI accessor entry is null.");
                    var accessorType = accessor.GetType();
                    var realizedServiceField = accessorType.GetField("<RealizedService>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? throw new InvalidOperationException("Failed to locate built-in DI realized service delegate.");
                    realizedServiceField.SetValue(accessor, BuildScopeFactoryAccessor(realizedServiceField.FieldType, runtimeRegistry));
                    return;
                }

                throw new InvalidOperationException("Failed to patch IServiceScopeFactory accessor in built-in DI provider.");
            }
        }

        private static object BuildScopeFactoryAccessor(Type delegateType, LiteOrmRuntimeRegistry runtimeRegistry)
        {
            var scopeType = delegateType.GenericTypeArguments[0];
            var scopeParameter = Expression.Parameter(scopeType, "scope");
            var factoryMethod = typeof(LiteOrmServiceScopeFactoryPatcher).GetMethod(nameof(CreateScopeFactory), BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Failed to locate scope factory patch helper.");
            // The accessor delegate must match the internal ServiceProviderEngineScope signature exactly,
            // so build it via expressions instead of referencing internal DI types directly.
            var body = Expression.Call(
                factoryMethod,
                Expression.Convert(scopeParameter, typeof(object)),
                Expression.Constant(runtimeRegistry));
            return Expression.Lambda(delegateType, body, scopeParameter).Compile();
        }

        private static object CreateScopeFactory(object scopeFactory, LiteOrmRuntimeRegistry runtimeRegistry)
        {
            if (scopeFactory is not IServiceScopeFactory innerScopeFactory)
                throw new InvalidOperationException($"Built-in DI scope object '{scopeFactory?.GetType().FullName}' does not implement IServiceScopeFactory.");

            return _scopeFactoryCache.GetValue(innerScopeFactory, factory => new LiteOrmScopeFactory(factory, runtimeRegistry));
        }
    }
}
