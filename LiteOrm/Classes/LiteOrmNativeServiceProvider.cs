using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LiteOrm
{
    internal sealed class LiteOrmInterceptedServiceRegistration
    {
        public Type ServiceType { get; set; }
        public Type ImplementationType { get; set; }
        public Type[] InterceptorTypes { get; set; } = Array.Empty<Type>();
        public ServiceLifetime Lifetime { get; set; }
    }

    internal sealed class LiteOrmKeyedServiceRegistry
    {
        private readonly Dictionary<(Type ServiceType, object Key), Type> _registrations = new();

        public void Add(Type serviceType, object key, Type implementationType)
        {
            _registrations[(serviceType, key)] = implementationType;
        }

        public bool TryGetImplementationType(Type serviceType, object key, out Type implementationType)
        {
            return _registrations.TryGetValue((serviceType, key), out implementationType);
        }
    }

    internal sealed class LiteOrmRuntimeRegistry
    {
        private readonly Dictionary<Type, LiteOrmInterceptedServiceRegistration> _interceptedServices = new();

        public LiteOrmKeyedServiceRegistry KeyedServices { get; } = new LiteOrmKeyedServiceRegistry();

        public List<Type> AutoActivateTypes { get; } = new List<Type>();

        public ProxyGenerator ProxyGenerator { get; } = new ProxyGenerator();

        public void AddInterceptedService(LiteOrmInterceptedServiceRegistration registration)
        {
            _interceptedServices[registration.ServiceType] = registration;
        }

        public bool TryGetInterceptedService(Type serviceType, out LiteOrmInterceptedServiceRegistration registration)
        {
            if (_interceptedServices.TryGetValue(serviceType, out registration))
                return true;

            if (serviceType.IsConstructedGenericType &&
                _interceptedServices.TryGetValue(serviceType.GetGenericTypeDefinition(), out var openGenericRegistration))
            {
                registration = openGenericRegistration;
                return true;
            }

            registration = null;
            return false;
        }
    }

    internal sealed class LiteOrmServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
    {
        private readonly DefaultServiceProviderFactory _innerFactory = new DefaultServiceProviderFactory();
        private readonly LiteOrmServiceExtensions.LiteOrmOptions _options;
        private readonly LiteOrmRuntimeRegistry _runtimeRegistry = new LiteOrmRuntimeRegistry();

        public LiteOrmServiceProviderFactory(LiteOrmServiceExtensions.LiteOrmOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public IServiceCollection CreateBuilder(IServiceCollection services)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));

            var logger = _options.LoggerFactory?.CreateLogger(nameof(LiteOrmServiceExtensions));
            services.AddSingleton(_runtimeRegistry);
            services.AddSingleton(_runtimeRegistry.KeyedServices);

            if (_options.Assemblies != null && _options.Assemblies.Length > 0)
                services.RegisterAutoService(_runtimeRegistry, logger, _options.Assemblies);
            else
                services.RegisterAutoService(_runtimeRegistry, logger);

            return _innerFactory.CreateBuilder(services);
        }

        public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
        {
            var innerProvider = _innerFactory.CreateServiceProvider(containerBuilder);
            var provider = new LiteOrmServiceProvider(innerProvider, _runtimeRegistry, ownsInnerProvider: true);
            InitializeLiteOrm(provider);
            return provider;
        }

        private void InitializeLiteOrm(LiteOrmServiceProvider provider)
        {
            foreach (var kvp in _options.SqlBuilders)
            {
                SqlBuilderFactory.Instance.RegisterSqlBuilder(kvp.Key, kvp.Value);
            }

            foreach (var kvp in _options.SqlBuildersByType)
            {
                SqlBuilderFactory.Instance.RegisterSqlBuilder(kvp.Key, kvp.Value);
            }

            if (_runtimeRegistry.AutoActivateTypes.Count == 0)
            {
                using var scope = provider.CreateScope();
                foreach (var initializer in scope.ServiceProvider.GetServices<ILiteOrmInitializer>())
                {
                    initializer.Start();
                }
                return;
            }

            using (var scope = provider.CreateScope())
            {
                foreach (var type in _runtimeRegistry.AutoActivateTypes)
                {
                    _ = scope.ServiceProvider.GetRequiredService(type);
                }

                foreach (var initializer in scope.ServiceProvider.GetServices<ILiteOrmInitializer>())
                {
                    initializer.Start();
                }
            }
        }
    }

    internal sealed class LiteOrmServiceProvider : IServiceProvider, ISupportRequiredService, IServiceScopeFactory, IDisposable, IAsyncDisposable
    {
        private readonly IServiceProvider _innerProvider;
        private readonly ISupportRequiredService _requiredServiceProvider;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly LiteOrmRuntimeRegistry _runtimeRegistry;
        private readonly bool _ownsInnerProvider;
        private readonly ConcurrentDictionary<Type, object> _proxyCache = new();

        public LiteOrmServiceProvider(IServiceProvider innerProvider, LiteOrmRuntimeRegistry runtimeRegistry, bool ownsInnerProvider)
        {
            _innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
            _runtimeRegistry = runtimeRegistry ?? throw new ArgumentNullException(nameof(runtimeRegistry));
            _requiredServiceProvider = innerProvider as ISupportRequiredService;
            _scopeFactory = innerProvider.GetRequiredService<IServiceScopeFactory>();
            _ownsInnerProvider = ownsInnerProvider;
        }

        public object GetRequiredService(Type serviceType)
        {
            if (serviceType == typeof(IServiceScopeFactory))
                return this;

            if (_runtimeRegistry.TryGetInterceptedService(serviceType, out var registration))
            {
                return ResolveInterceptedService(serviceType, registration);
            }

            if (_requiredServiceProvider != null)
                return _requiredServiceProvider.GetRequiredService(serviceType);

            var service = GetService(serviceType);
            if (service is null)
                throw new InvalidOperationException($"No service for type '{serviceType}' has been registered.");
            return service;
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IServiceScopeFactory))
                return this;

            if (_runtimeRegistry.TryGetInterceptedService(serviceType, out var registration))
            {
                return ResolveInterceptedService(serviceType, registration);
            }

            return _innerProvider.GetService(serviceType);
        }

        public IServiceScope CreateScope()
        {
            return new LiteOrmServiceScope(_scopeFactory.CreateScope(), _runtimeRegistry);
        }

        public void Dispose()
        {
            if (_ownsInnerProvider && _innerProvider is IDisposable disposable)
                disposable.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            if (_ownsInnerProvider && _innerProvider is IAsyncDisposable asyncDisposable)
                return asyncDisposable.DisposeAsync();

            Dispose();
            return default;
        }

        private object ResolveInterceptedService(Type serviceType, LiteOrmInterceptedServiceRegistration registration)
        {
            if (registration.Lifetime == ServiceLifetime.Transient)
                return CreateInterceptedService(serviceType, registration);

            return _proxyCache.GetOrAdd(serviceType, _ => CreateInterceptedService(serviceType, registration));
        }

        private object CreateInterceptedService(Type serviceType, LiteOrmInterceptedServiceRegistration registration)
        {
            var implementationType = GetImplementationType(registration.ImplementationType, serviceType);
            var target = GetRequiredService(implementationType);
            var interceptors = ResolveInterceptors(registration.InterceptorTypes);
            return _runtimeRegistry.ProxyGenerator.CreateInterfaceProxyWithTarget(serviceType, target, interceptors);
        }

        private IInterceptor[] ResolveInterceptors(Type[] interceptorTypes)
        {
            return interceptorTypes.Select(interceptorType =>
            {
                var interceptor = GetRequiredService(interceptorType);
                if (interceptor is IAsyncInterceptor asyncInterceptor)
                    return asyncInterceptor.ToInterceptor();
                if (interceptor is IInterceptor syncInterceptor)
                    return syncInterceptor;
                throw new InvalidOperationException($"Interceptor '{interceptorType.FullName}' must implement IInterceptor or IAsyncInterceptor.");
            }).ToArray();
        }

        private static Type GetImplementationType(Type implementationType, Type requestedServiceType)
        {
            if (!implementationType.IsGenericTypeDefinition)
                return implementationType;

            if (!requestedServiceType.IsConstructedGenericType)
                return implementationType;

            return implementationType.MakeGenericType(requestedServiceType.GenericTypeArguments);
        }
    }

    internal sealed class LiteOrmServiceScope : IServiceScope, IAsyncDisposable
    {
        private readonly IServiceScope _innerScope;
        private readonly IDisposable _sessionScope;

        public LiteOrmServiceScope(IServiceScope innerScope, LiteOrmRuntimeRegistry runtimeRegistry)
        {
            _innerScope = innerScope ?? throw new ArgumentNullException(nameof(innerScope));
            ServiceProvider = new LiteOrmServiceProvider(innerScope.ServiceProvider, runtimeRegistry, ownsInnerProvider: false);
            _sessionScope = SessionManager.PushCurrentFactory(() => ServiceProvider.GetRequiredService<SessionManager>());
        }

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
            _sessionScope.Dispose();
            _innerScope.Dispose();
            if (ServiceProvider is IDisposable disposable)
                disposable.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            _sessionScope.Dispose();
            if (_innerScope is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            else
                _innerScope.Dispose();

            if (ServiceProvider is IAsyncDisposable providerAsyncDisposable)
                await providerAsyncDisposable.DisposeAsync().ConfigureAwait(false);
            else if (ServiceProvider is IDisposable providerDisposable)
                providerDisposable.Dispose();
        }
    }
}
