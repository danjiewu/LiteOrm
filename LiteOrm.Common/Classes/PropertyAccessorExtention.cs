using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

/// <summary>
/// 属性访问器扩展方法，使用表达式树优化属性访问性能
/// </summary>
public static class PropertyAccessorExtension
{
    private static readonly ConcurrentDictionary<PropertyInfo, Func<object, object>> _getterCache = new ConcurrentDictionary<PropertyInfo, Func<object, object>>();
    private static readonly ConcurrentDictionary<PropertyInfo, Action<object, object>> _setterCache = new ConcurrentDictionary<PropertyInfo, Action<object, object>>();

    /// <summary>
    /// 注册预编译的属性访问器委托，用于 NativeAOT 场景替代运行时 <see cref="Expression.Compile"/>。
    /// 注册后，<see cref="GetValueFast"/> 和 <see cref="SetValueFast"/> 将直接使用注册的委托。
    /// </summary>
    /// <param name="property">属性信息</param>
    /// <param name="getter">属性读取委托（可为 null 表示属性只读）</param>
    /// <param name="setter">属性设置委托（可为 null 表示属性只读）</param>
    public static void RegisterAccessor(PropertyInfo property, Func<object, object>? getter, Action<object, object>? setter)
    {
        if (property is null) throw new ArgumentNullException(nameof(property));
        if (getter != null) _getterCache[property] = getter;
        if (setter != null) _setterCache[property] = setter;
    }

    /// <summary>
    /// 快速获取属性值，使用表达式树缓存委托以提高性能
    /// </summary>
    /// <param name="property">属性信息</param>
    /// <param name="instance">对象实例</param>
    /// <returns>属性值</returns>
    /// <exception cref="ArgumentNullException">当property为null时抛出</exception>
    public static object? GetValueFast(this PropertyInfo property, object instance)
    {
        if (property is null) throw new ArgumentNullException(nameof(property));
        if (instance is null) return null;

        // 若已预注册委托则直接使用
        if (_getterCache.TryGetValue(property, out var precompiled))
            return precompiled(instance);

        // AOT 时走 PropertyInfo.GetValue
        if (!RuntimeFeature.IsDynamicCodeSupported)
            return property.GetValue(instance);

        var getter = _getterCache.GetOrAdd(property, p =>
        {
            // 使用表达式树创建强类型委托
            var instanceParam = Expression.Parameter(typeof(object), "instance");

            // 转换实例类型
            var declaringType = p.DeclaringType ?? throw new ArgumentNullException(nameof(property));
            var instanceCast = Expression.Convert(instanceParam, declaringType);

            // 属性访问
            var propertyAccess = Expression.Property(instanceCast, p);

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
        if (property is null) throw new ArgumentNullException(nameof(property));
        if (instance is null) throw new ArgumentNullException(nameof(instance));

        // 若已预注册委托则直接使用
        if (_setterCache.TryGetValue(property, out var precompiled))
        {
            precompiled(instance, value);
            return;
        }

        // AOT 时走 PropertyInfo.SetValue
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            property.SetValue(instance, value);
            return;
        }

        var setter = _setterCache.GetOrAdd(property, p =>
        {
            // 表达式树创建Setter
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var valueParam = Expression.Parameter(typeof(object), "value");

            var declaringType = p.DeclaringType ?? throw new ArgumentNullException(nameof(property));
            var instanceCast = Expression.Convert(instanceParam, declaringType);
            var valueCast = Expression.Convert(valueParam, p.PropertyType);

            var propertyAccess = Expression.Property(instanceCast, p);
            var assign = Expression.Assign(propertyAccess, valueCast);

            var lambda = Expression.Lambda<Action<object, object>>(assign, instanceParam, valueParam);
            return lambda.Compile();
        });

        setter(instance, value);
    }
}
