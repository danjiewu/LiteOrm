using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;


public static class PropertyAccessorExtention
{
    // 缓存Getter委托：Key=属性唯一标识，Value=泛型委托包装的非泛型Getter
    private static readonly ConcurrentDictionary<string, Func<object, object>> _getterCache = new();
    // 缓存Setter委托：Key=属性唯一标识，Value=泛型委托包装的非泛型Setter
    private static readonly ConcurrentDictionary<string, Action<object, object>> _setterCache = new();

    #region 替代 PropertyInfo.GetValue
    /// <summary>
    /// 高性能替代 PropertyInfo.GetValue（内部泛型实现）
    /// </summary>
    public static object? GetVal(this PropertyInfo property, object instance)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));
        if (instance == null && !property.DeclaringType!.IsValueType) return null;

        // 生成唯一缓存Key
        string cacheKey = GetPropertyCacheKey(property, isSetter: false);
        // 获取/创建Getter委托（内部是泛型逻辑）
        var getter = _getterCache.GetOrAdd(cacheKey, _ => CreateGenericGetter(property));

        return getter(instance);
    }
    #endregion

    #region 替代 PropertyInfo.SetValue
    /// <summary>
    /// 高性能替代 PropertyInfo.SetValue（内部泛型实现）
    /// </summary>
    public static void SetVal(this PropertyInfo property, object instance, object value)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));
        if (instance == null && !property.DeclaringType!.IsValueType) return;

        string cacheKey = GetPropertyCacheKey(property, isSetter: true);
        var setter = _setterCache.GetOrAdd(cacheKey, _ => CreateGenericSetter(property));

        setter(instance, value);
    }
    #endregion

    #region 核心：创建泛型Getter（内部无反射）
    private static Func<object, object> CreateGenericGetter(PropertyInfo property)
    {
        MethodInfo getMethod = property.GetGetMethod(true)!;
        Type declaringType = property.DeclaringType!;
        Type propertyType = property.PropertyType;

        // 生成强类型泛型委托（Func<声明类型, 属性类型>）
        Type getterType = typeof(Func<,>).MakeGenericType(declaringType, propertyType);
        Delegate strongGetter = Delegate.CreateDelegate(getterType, getMethod, throwOnBindFailure: true);

        // 包装为非泛型接口（仅首次包装有开销）
        return instance =>
        {
            // 类型转换（仅一次装箱）
            object typedInstance = Convert.ChangeType(instance, declaringType);
            // 调用泛型委托（无反射、无DynamicInvoke）
            return InvokeGenericGetter(strongGetter, typedInstance, declaringType, propertyType);
        };
    }

    /// <summary>
    /// 泛型调用Getter（JIT生成专用代码）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static object InvokeGenericGetter(Delegate getter, object instance, Type declaringType, Type propertyType)
    {
        // 通过反射创建泛型方法（仅首次调用）
        var method = typeof(PropertyAccessorExtention)
            .GetMethod(nameof(InvokeGenericGetter_Typed), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(declaringType, propertyType);
        // 调用泛型方法（后续JIT缓存）
        return method.Invoke(null, new[] { getter, instance })!;
    }

    /// <summary>
    /// 真正的泛型Getter（无装箱、无反射）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static object InvokeGenericGetter_Typed<TInstance, TProperty>(Delegate getter, object instance)
    {
        if (getter is Func<TInstance, TProperty> typedGetter)
        {
            // 强类型调用，无装箱
            TInstance typedInstance = (TInstance)instance;
            return typedGetter(typedInstance)!;
        }
        return null!;
    }
    #endregion

    #region 核心：创建泛型Setter（内部无反射）
    private static Action<object, object> CreateGenericSetter(PropertyInfo property)
    {
        MethodInfo setMethod = property.GetSetMethod(true)!;
        Type declaringType = property.DeclaringType!;
        Type propertyType = property.PropertyType;

        Type setterType = typeof(Action<,>).MakeGenericType(declaringType, propertyType);
        Delegate strongSetter = Delegate.CreateDelegate(setterType, setMethod, throwOnBindFailure: true);

        return (instance, value) =>
        {
            object typedInstance = Convert.ChangeType(instance, declaringType);
            object typedValue = value == null ? null : Convert.ChangeType(value, propertyType.GetUnderlyingType());
            InvokeGenericSetter(strongSetter, typedInstance, typedValue, declaringType, propertyType);
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeGenericSetter(Delegate setter, object instance, object value, Type declaringType, Type propertyType)
    {
        var method = typeof(PropertyAccessorExtention)
            .GetMethod(nameof(InvokeGenericSetter_Typed), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(declaringType, propertyType);
        method.Invoke(null, new[] { setter, instance, value });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvokeGenericSetter_Typed<TInstance, TProperty>(Delegate setter, object instance, object value)
    {
        if (setter is Action<TInstance, TProperty> typedSetter)
        {
            TInstance typedInstance = (TInstance)instance;
            TProperty typedValue = (TProperty)value!;
            typedSetter(typedInstance, typedValue);
        }
    }
    #endregion

    #region 辅助方法
    /// <summary>
    /// 判断是否为可空类型（Nullable<T>）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Type GetUnderlyingType(this Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) ? Nullable.GetUnderlyingType(type)! : type;    
    }

    private static string GetPropertyCacheKey(PropertyInfo property, bool isSetter)
    {
        return $"{property.DeclaringType!.Assembly.FullName}_{property.DeclaringType.FullName}_{property.Name}_{(isSetter ? "Setter" : "Getter")}";
    }
    #endregion
}