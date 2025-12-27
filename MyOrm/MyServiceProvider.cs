using Castle.Core.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyOrm.Common;
using MyOrm.Service;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;

namespace MyOrm
{
    public class MyServiceProvider : IServiceProvider, IKeyedServiceProvider, IServiceScopeFactory, IDisposable
    {
        // 新增：标记是否已释放，防止重复Dispose
        private bool _disposed = false;
        // 每个实例独立的作用域栈（避免静态污染）
        private readonly AsyncLocal<Stack<IServiceScope>> _scopeStack = new();
        // 原生容器依赖
        private readonly IServiceProvider _defaultProvider;
        private readonly IServiceScopeFactory _defaultScopeFactory;
        private readonly IKeyedServiceProvider _defaultKeyedProvider;
        // 仅根容器维护Singleton缓存
        private readonly ConcurrentDictionary<Type, object>? _singletonCache;
        private readonly ConcurrentDictionary<(Type, object?), object>? _keyedSingletonCache;
        // 当前作用域，获取前先清理已释放的栈顶
        public IServiceScope? CurrentScope
        {
            get
            {
                CleanupDisposedScopes(); // 先清理，再返回
                return _scopeStack.Value?.Count > 0 ? _scopeStack.Value.Peek() : null;
            }
        }
        // 根容器单例（线程安全）
        public static IServiceProvider? Root { get; private set; }
        public static IServiceProvider Current
        {
            get
            {
                if (Root is MyServiceProvider rootProvider)
                {
                    return rootProvider.CurrentScope?.ServiceProvider ?? Root;
                }
                throw new InvalidOperationException("全局根容器未初始化");
            }
        }

        private readonly IServiceCollection _serviceCollection;
        private readonly ILogger<MyServiceProvider> _logger;
        // 构造函数（区分根容器/作用域容器）
        public MyServiceProvider(IServiceCollection serviceCollection, IServiceProvider defaultProvider)
        {
            _serviceCollection = serviceCollection ?? throw new ArgumentNullException(nameof(serviceCollection));
            _defaultProvider = defaultProvider ?? throw new ArgumentNullException(nameof(defaultProvider));
            _defaultScopeFactory = defaultProvider.GetRequiredService<IServiceScopeFactory>();
            _logger = defaultProvider.GetRequiredService<ILogger<MyServiceProvider>>();
            _defaultKeyedProvider = defaultProvider as IKeyedServiceProvider ??
                throw new InvalidOperationException("基础容器不支持键控服务");

            _singletonCache = new ConcurrentDictionary<Type, object>();
            _keyedSingletonCache = new ConcurrentDictionary<(Type, object?), object>();
            Root = this; // 仅根容器设置全局根实例

            // 初始化作用域栈
            if (_scopeStack.Value == null)
                _scopeStack.Value = new Stack<IServiceScope>();
        }

        // 实现IServiceScopeFactory：线程安全的作用域创建
        public IServiceScope CreateScope()
        {
            // 已释放则抛异常
            if (_disposed)
                throw new ObjectDisposedException(nameof(MyServiceProvider), "服务提供器已释放，无法创建新作用域");

            lock (_scopeStack) // 确保栈操作线程安全
            {
                var defaultScope = _defaultScopeFactory.CreateScope();
                var parentScope = CurrentScope;
                var myScope = new MyServiceScope(defaultScope, this, parentScope);

                try
                {
                    if (_scopeStack.Value == null)
                        _scopeStack.Value = new Stack<IServiceScope>();

                    _scopeStack.Value!.Push(myScope);
                    return myScope;
                }
                catch
                {
                    myScope.Dispose();
                    throw;
                }
            }
        }

        // 实现IKeyedServiceProvider（根容器）
        public object? GetKeyedService(Type serviceType, object? serviceKey)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MyServiceProvider));

            // 优先从Singleton缓存获取
            if (_keyedSingletonCache!.TryGetValue((serviceType, serviceKey), out var cached))
                return cached;

            // 原生容器获取
            var service = _defaultKeyedProvider.GetKeyedService(serviceType, serviceKey);
            if (service is ServiceBase baseService)
                service = ServiceProxyHelper.CreateServiceInvokeProxy(serviceType, service, baseService.ServiceName);

            // 缓存Singleton服务
            if (service != null)
                _keyedSingletonCache.TryAdd((serviceType, serviceKey), service);

            return service;
        }

        // 实现IServiceProvider（根容器）
        public object? GetService(Type serviceType)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MyServiceProvider));

            // 框架核心服务返回自身
            if (serviceType == typeof(IServiceProvider)) return this;
            if (serviceType == typeof(IServiceScopeFactory)) return this;
            if (serviceType == typeof(IKeyedServiceProvider)) return this;

            // 优先从Singleton缓存获取
            if (_singletonCache!.TryGetValue(serviceType, out var cached))
                return cached;

            // 原生容器获取
            var service = _defaultProvider.GetService(serviceType);
            if (service is ServiceBase baseService)
                service = ServiceProxyHelper.CreateServiceInvokeProxy(serviceType, service, baseService.ServiceName);

            // 泛型服务构建（仅根容器）
            if (service == null && serviceType.IsGenericType)
                service = BuildGenericService(serviceType);

            // 缓存Singleton服务
            if (service != null)
                _singletonCache.TryAdd(serviceType, service);

            return service;
        }

        // 泛型服务构建
        private object? BuildGenericService(Type serviceType)
        {
            var builder = _defaultProvider.GetService<IGenericServiceBuilder>();
            return builder?.BuildGenericService(serviceType);
        }

        // 显式接口实现
        public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MyServiceProvider));

            return GetKeyedService(serviceType, serviceKey) ??
                throw new InvalidOperationException($"键控服务 {serviceType.Name} (Key: {serviceKey}) 未找到");
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public object? GetKeyedService<T>(object? serviceKey) => GetKeyedService(typeof(T), serviceKey);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public T GetRequiredKeyedService<T>(object? serviceKey) => (T)GetRequiredKeyedService(typeof(T), serviceKey);

        // 提供方法供作用域容器获取服务生命周期
        public ServiceLifetime GetServiceLifetime(Type serviceType)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MyServiceProvider));

            // 查找服务类型的注册描述符
            var descriptor = _serviceCollection.FirstOrDefault(d =>
                d.ServiceType == serviceType ||
                (d.ServiceType.IsGenericTypeDefinition && serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == d.ServiceType));

            return descriptor?.Lifetime ?? ServiceLifetime.Transient;
        }

        // 清理栈顶所有已释放的作用域
        private void CleanupDisposedScopes()
        {
            lock (_scopeStack)
            {
                var stack = _scopeStack.Value;
                if (stack == null || stack.Count == 0) return;

                // 循环检查栈顶：如果栈顶已释放，则弹出，直到栈顶有效或栈空
                while (stack.Count > 0 && (stack.Peek() as MyServiceScope)?.IsDisposed == true)
                {
                    var disposedScope = stack.Pop() as MyServiceScope;
                    // 额外确认释放（防御性编程）
                    if (disposedScope != null && !disposedScope.IsDisposed)
                    {
                        disposedScope.Dispose();
                    }
                }
            }
        }

        // 优化Dispose方法：解决死循环问题
        public void Dispose()
        {
            // 新增：确保Dispose只执行一次
            if (_disposed) return;
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // 分离释放逻辑，支持析构函数
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            // 标记已释放
            _disposed = true;

            if (disposing)
            {
                // 托管资源释放
                lock (_scopeStack)
                {
                    CleanupDisposedScopes();
                    // 先复制栈内元素到临时数组，避免迭代时修改原集合
                    var scopesToDispose = _scopeStack.Value?.ToArray() ?? Array.Empty<IServiceScope>();
                    // 清空栈
                    _scopeStack.Value?.Clear();
                    foreach (var scope in scopesToDispose)
                    {
                        try
                        {
                            scope.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "释放作用域 {ScopeType} 时出错", scope.GetType().Name);
                        }
                    }
                }

                // 安全释放Singleton缓存：先复制值到临时列表
                if (_singletonCache != null)
                {
                    var singletonDisposables = _singletonCache.Values.OfType<IDisposable>().ToList();
                    foreach (var disposable in singletonDisposables)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "释放 Singleton 服务 {ServiceType} 时出错", disposable.GetType().Name);
                        }
                    }
                    _singletonCache.Clear();
                }

                if (_keyedSingletonCache != null)
                {
                    var keyedSingletonDisposables = _keyedSingletonCache.Values.OfType<IDisposable>().ToList();
                    foreach (var disposable in keyedSingletonDisposables)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "释放 Keyed Singleton 服务 {ServiceType} 时出错", disposable.GetType().Name);
                        }
                    }
                    _keyedSingletonCache.Clear();
                }

                // 释放原生容器（避免递归释放）
                if (_defaultProvider is IDisposable disposableProvider && disposableProvider != this)
                {
                    try
                    {
                        disposableProvider.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "释放原生服务提供器时出错");
                    }
                }

                // 清空根实例
                if (Root == this)
                    Root = null;
            }

        }

        // 新增：析构函数，处理未手动释放的情况
        ~MyServiceProvider()
        {
            Dispose(false);
        }

        #region 作用域实现
        private class MyServiceScope : IServiceScope
        {
            // 新增：标记作用域是否已释放
            private bool _disposed = false;
            private readonly IServiceScope _defaultScope;
            private readonly MyServiceProvider _rootProvider;
            private readonly IServiceScope? _parentScope;
            private readonly ConcurrentDictionary<Type, object> _scopedCache = new();
            private readonly ConcurrentDictionary<(Type, object?), object> _keyedScopedCache = new();

            // 新增：保存 SessionManager.EnterContext() 返回的作用域恢复句柄
            private IDisposable? _sessionContextScope;

            public IServiceProvider ServiceProvider { get; }
            public IServiceScope ParentScope => _parentScope;

            public MyServiceScope(IServiceScope defaultScope, MyServiceProvider rootProvider, IServiceScope? parentScope)
            {
                _defaultScope = defaultScope;
                _rootProvider = rootProvider;
                _parentScope = parentScope;

                // 作用域内的服务提供器
                ServiceProvider = new MyScopedServiceProvider(
                    defaultScope.ServiceProvider,
                    defaultScope.ServiceProvider as IKeyedServiceProvider ??
                        throw new InvalidOperationException("作用域容器不支持键控服务"),
                    rootProvider,
                    _scopedCache,
                    _keyedScopedCache);

                // 尝试在作用域创建时解析并进入 SessionManager 上下文（容错）
                try
                {
                    var sm = ServiceProvider.GetService<SessionManager>();
                    if (sm != null)
                    {
                        // EnterContext 会设置 SessionManager.Current = this，并返回一个 IDisposable 用于恢复之前的 Current
                        _sessionContextScope = sm.EnterContext();
                    }
                }
                catch
                {
                    // 容错：不要抛出异常，避免影响作用域创建流程
                }
            }

            public bool IsDisposed => _disposed;

            // 优化作用域Dispose：避免重复释放和栈操作冲突
            public void Dispose()
            {
                if (_disposed) return;
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (_disposed) return;

                if (disposing)
                {
                    try
                    {
                        if (_sessionContextScope != null)
                        {
                            try { _sessionContextScope.Dispose(); } catch { }
                        }
                        // 释放作用域缓存
                        _scopedCache.Values.OfType<IDisposable>().ToList().ForEach(d =>
                        {
                            try { d.Dispose(); } catch { }
                        });
                        _keyedScopedCache.Values.OfType<IDisposable>().ToList().ForEach(d =>
                        {
                            try { d.Dispose(); } catch { }
                        });
                        _scopedCache.Clear();
                        _keyedScopedCache.Clear();

                        // 从栈中移除（仅当当前是栈顶时）
                        lock (_rootProvider._scopeStack)
                        {
                            if (_rootProvider._scopeStack.Value?.Count > 0 &&
                                _rootProvider._scopeStack.Value.Peek() == this)
                            {
                                _rootProvider._scopeStack.Value.Pop();
                                _rootProvider.CleanupDisposedScopes();
                            }
                        }

                        // 释放原生作用域
                        _defaultScope.Dispose();
                    }
                    catch { }
                }

                _disposed = true;
            }

            ~MyServiceScope()
            {
                Dispose(false);
            }
        }
        #endregion

        #region 作用域内服务提供器
        private class MyScopedServiceProvider : IServiceProvider, IKeyedServiceProvider, IServiceScopeFactory
        {
            private readonly IServiceProvider _defaultScopedProvider;
            private readonly IKeyedServiceProvider _defaultKeyedScopedProvider;
            private readonly MyServiceProvider _rootProvider;
            private readonly ConcurrentDictionary<Type, object> _scopedCache;
            private readonly ConcurrentDictionary<(Type, object?), object> _keyedScopedCache;
            // 新增：标记是否已释放
            private bool _disposed = false;

            public MyScopedServiceProvider(
                IServiceProvider defaultScopedProvider,
                IKeyedServiceProvider defaultKeyedScopedProvider,
                MyServiceProvider rootProvider,
                ConcurrentDictionary<Type, object> scopedCache,
                ConcurrentDictionary<(Type, object?), object> keyedScopedCache)
            {
                _defaultScopedProvider = defaultScopedProvider;
                _defaultKeyedScopedProvider = defaultKeyedScopedProvider;
                _rootProvider = rootProvider;
                _scopedCache = scopedCache;
                _keyedScopedCache = keyedScopedCache;
            }

            // 作用域内服务解析
            public object? GetService(Type serviceType)
            {
                if (_disposed || _rootProvider._disposed)
                    throw new ObjectDisposedException(nameof(MyScopedServiceProvider));

                // 框架核心服务返回自身
                if (serviceType == typeof(IServiceProvider)) return this;
                if (serviceType == typeof(IServiceScopeFactory)) return this;
                if (serviceType == typeof(IKeyedServiceProvider)) return this;

                // 判断服务生命周期
                var lifetime = _rootProvider.GetServiceLifetime(serviceType);

                // Singleton服务处理（根缓存）
                if (lifetime == ServiceLifetime.Singleton)
                {
                    return _rootProvider.GetService(serviceType);
                }

                // 优先从作用域缓存获取
                if (_scopedCache.TryGetValue(serviceType, out var cached))
                    return cached;

                // 原生作用域容器获取
                var scopedService = _defaultScopedProvider.GetService(serviceType);
                if (scopedService is ServiceBase baseService2)
                {
                    scopedService = ServiceProxyHelper.CreateServiceInvokeProxy(serviceType, scopedService, baseService2.ServiceName);
                    _scopedCache.TryAdd(serviceType, scopedService);
                }

                // 泛型服务构建
                if (scopedService == null && serviceType.IsGenericType)
                {
                    scopedService = BuildGenericService(serviceType);
                    if (scopedService != null)
                        _scopedCache.TryAdd(serviceType, scopedService);
                }

                return scopedService;
            }

            // 作用域内键控服务解析
            public object? GetKeyedService(Type serviceType, object? serviceKey)
            {
                if (_disposed || _rootProvider._disposed)
                    throw new ObjectDisposedException(nameof(MyScopedServiceProvider));

                var key = (serviceType, serviceKey);
                var lifetime = _rootProvider.GetServiceLifetime(serviceType);

                // Singleton服务处理（根缓存）
                if (lifetime == ServiceLifetime.Singleton)
                {
                    return _rootProvider.GetKeyedService(serviceType, serviceKey);
                }

                // 优先从作用域缓存获取
                if (_keyedScopedCache.TryGetValue((serviceType, serviceKey), out var cached))
                    return cached;

                // 原生作用域容器获取
                var keyedService = _defaultKeyedScopedProvider.GetKeyedService(serviceType, serviceKey);
                if (keyedService is ServiceBase baseService2)
                {
                    keyedService = ServiceProxyHelper.CreateServiceInvokeProxy(serviceType, keyedService, baseService2.ServiceName);
                    _keyedScopedCache.TryAdd((serviceType, serviceKey), keyedService);
                }

                // 泛型服务构建
                if (keyedService == null && serviceType.IsGenericType)
                {
                    keyedService = BuildGenericService(serviceType);
                    if (keyedService != null)
                        _keyedScopedCache.TryAdd((serviceType, serviceKey), keyedService);
                }

                return keyedService;
            }

            // 泛型服务构建（作用域内）
            private object? BuildGenericService(Type serviceType)
            {
                var builder = _defaultScopedProvider.GetService<IGenericServiceBuilder>();
                return builder?.BuildGenericService(serviceType);
            }

            // 作用域内创建子作用域
            public IServiceScope CreateScope()
            {
                if (_disposed || _rootProvider._disposed)
                    throw new ObjectDisposedException(nameof(MyScopedServiceProvider));

                return _rootProvider.CreateScope();
            }

            // 显式接口实现
            public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
            {
                if (_disposed || _rootProvider._disposed)
                    throw new ObjectDisposedException(nameof(MyScopedServiceProvider));

                return GetKeyedService(serviceType, serviceKey) ??
                    throw new InvalidOperationException($"键控服务 {serviceType.Name} (Key: {serviceKey}) 未找到");
            }

            [EditorBrowsable(EditorBrowsableState.Never)]
            public object? GetKeyedService<T>(object? serviceKey) => GetKeyedService(typeof(T), serviceKey);

            [EditorBrowsable(EditorBrowsableState.Never)]
            public T GetRequiredKeyedService<T>(object? serviceKey) => (T)GetRequiredKeyedService(typeof(T), serviceKey);

            // 新增：释放作用域内服务提供器资源
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                // 清空缓存（由外层Scope统一释放实例）
                _scopedCache.Clear();
                _keyedScopedCache.Clear();
            }
        }
        #endregion
    }

    #region 泛型服务构建器
    public interface IGenericServiceBuilder
    {
        object? BuildGenericService(Type serviceType);
        Dictionary<Type, Type> ServiceTypeMap { get; }
    }

    public class GenericServiceBuilder : IGenericServiceBuilder
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<Type, Type> _serviceTypeMap = new()
        {
            { typeof(IObjectDAO<>), typeof(ObjectDAO<>) },
            { typeof(IObjectViewDAO<>), typeof(ObjectViewDAO<>) },
            { typeof(IEntityService<>), typeof(EntityService<>) },
            { typeof(IEntityViewService<>), typeof(EntityViewService<>) }
        };
        public GenericServiceBuilder(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        public Type this[Type key] { get { return _serviceTypeMap[key]; } set { _serviceTypeMap[key] = value; } }

        public Dictionary<Type, Type> ServiceTypeMap => _serviceTypeMap;

        public object? BuildGenericService(Type serviceType)
        {
            if (!serviceType.IsGenericType) return null;

            var genericTypeDef = serviceType.GetGenericTypeDefinition();
            if (!ServiceTypeMap.ContainsKey(genericTypeDef)) return null;

            var entityType = serviceType.GetGenericArguments()[0];
            var implementationType = ServiceTypeMap[genericTypeDef].MakeGenericType(entityType);

            // 使用原生容器创建实例（支持依赖注入）
            var instance = ActivatorUtilities.CreateInstance(_serviceProvider, implementationType);

            // 创建代理（如果是ServiceBase类型）
            if (instance is ServiceBase baseService)
                instance = ServiceProxyHelper.CreateServiceInvokeProxy(serviceType, instance, baseService.ServiceName);

            return instance;
        }
    }
    #endregion
}