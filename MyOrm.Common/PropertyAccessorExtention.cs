using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// 属性访问器扩展方法，使用表达式树优化属性访问性能
/// </summary>
public static class PropertyAccessorExtension
{
    private static readonly ConcurrentDictionary<string, Func<object, object>> _getterCache = new ConcurrentDictionary<string, Func<object, object>>();
    private static readonly ConcurrentDictionary<string, Action<object, object>> _setterCache = new ConcurrentDictionary<string, Action<object, object>>();

    /// <summary>
    /// 快速获取属性值，使用表达式树缓存委托以提高性能
    /// </summary>
    /// <param name="property">属性信息</param>
    /// <param name="instance">对象实例</param>
    /// <returns>属性值</returns>
    /// <exception cref="ArgumentNullException">当property为null时抛出</exception>
    public static object GetValueFast(this PropertyInfo property, object instance)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));
        if (instance == null) return null;

        string cacheKey = $"{property.DeclaringType.FullName}.{property.Name}";

        var getter = _getterCache.GetOrAdd(cacheKey, key =>
        {
            // 使用表达式树创建强类型委托
            var instanceParam = Expression.Parameter(typeof(object), "instance");

            // 转换实例类型
            var instanceCast = Expression.Convert(instanceParam, property.DeclaringType);

            // 属性访问
            var propertyAccess = Expression.Property(instanceCast, property);

            // 返回值转换为object
            var convertResult = Expression.Convert(propertyAccess, typeof(object));

            // 编译表达式树
            var lambda = Expression.Lambda<Func<object, object>>(convertResult, instanceParam);
            return lambda.Compile();
        });

        return getter(instance);
    }

    /// <summary>
    /// 快速设置属性值，使用表达式树缓存委托以提高性能
    /// </summary>
    /// <param name="property">属性信息</param>
    /// <param name="instance">对象实例</param>
    /// <param name="value">要设置的值</param>
    /// <exception cref="ArgumentNullException">当property或instance为null时抛出</exception>
    public static void SetValueFast(this PropertyInfo property, object instance, object value)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));
        if (instance == null) throw new ArgumentNullException(nameof(instance));

        string cacheKey = $"{property.DeclaringType.FullName}.{property.Name}.set";

        var setter = _setterCache.GetOrAdd(cacheKey, key =>
        {
            // 表达式树创建Setter
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var valueParam = Expression.Parameter(typeof(object), "value");

            var instanceCast = Expression.Convert(instanceParam, property.DeclaringType);
            var valueCast = Expression.Convert(valueParam, property.PropertyType);

            var propertyAccess = Expression.Property(instanceCast, property);
            var assign = Expression.Assign(propertyAccess, valueCast);

            var lambda = Expression.Lambda<Action<object, object>>(assign, instanceParam, valueParam);
            return lambda.Compile();
        });

        setter(instance, value);
    }
}
