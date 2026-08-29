using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace LiteOrm.Converter
{
    /// <summary>列读取规范：一条被读取列到目标成员的映射（列序号、核心类型、读取转换委托，以及「属性 setter」或「构造函数参数」目标之一）。</summary>
    internal struct ColumnReadSpec
    {
        public int Ordinal;
        public Type CoreType;
        public DbConvertHandler? ReadHandler;
        /// <summary>普通类型的属性 setter 目标；匿名/构造类型的构造函数参数目标使用 <see cref="ParameterIndex"/>。</summary>
        public PropertyInfo? Property;
        /// <summary>匿名/构造类型的构造函数参数下标；普通类型为 -1。</summary>
        public int ParameterIndex;
    }

    /// <summary>
    /// 在 NativeAOT（<see cref="System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported"/> 为 false）下，
    /// 通过纯反射将 <see cref="AutoLockDataReader"/> 当前行映射为 <typeparamref name="TResult"/> 实例的通用转换类。
    /// </summary>
    /// <remarks>
    /// 同时支持两种目标类型：
    /// <list type="bullet">
    /// <item>普通类型（公开无参构造函数 + 可写属性）：<c>ctor.Invoke(null)</c> 建实例后按属性 setter 赋值；</item>
    /// <item>匿名/构造类型（仅带参公开构造函数）：按构造函数参数名匹配的列填充参数数组后 <c>ctor.Invoke(object?[])</c> 一次性建实例。</item>
    /// </list>
    /// 读取转换委托由构造方（<see cref="DataReaderConverter"/>）在构建 <see cref="ColumnReadSpec"/> 时一次解析好；<see cref="Map"/>
    /// 逐行读取并为属性赋值或填充构造参数，方法组可直接作为 <see cref="Func{T, TResult}"/> 委托挂载。
    /// 全程不使用 <c>Expression.Compile</c>，不开动态代码；目标类型元数据由构造方通过
    /// <see cref="DynamicallyAccessedMembersAttribute"/> 保证在裁剪后保留。
    /// </remarks>
    internal sealed class AotMapper<[DynamicallyAccessedMembers(Constants.RegistedMemberTypes)] TResult>
    {
        private readonly ConstructorInfo _ctor;
        private readonly bool _useCtorArgs;
        private readonly int[] _ordinals;
        private readonly Type[] _cores;
        private readonly DbConvertHandler?[] _handlers;
        private readonly PropertyInfo[]? _props;   // !_useCtorArgs：普通类型属性 setter 目标
        private readonly int[]? _paramIndexes;     // _useCtorArgs：匿名/构造类型构造参数下标
        private readonly object?[]? _defaults;    // _useCtorArgs：各构造参数默认值

        /// <summary>
        /// 初始化映射器：检测 <typeparamref name="TResult"/> 是否具有公开无参构造函数，来决定普通类型（属性 setter）或
        /// 匿名/构造类型（构造函数参数）映射模式，并将 <paramref name="specs"/> 固化为并行数组。
        /// </summary>
        public AotMapper(IReadOnlyList<ColumnReadSpec> specs)
        {
            var paramless = typeof(TResult).GetConstructor(Type.EmptyTypes);
            _ctor = paramless ?? typeof(TResult).GetConstructors()[0]; // 匿名类型通常仅一个公开构造函数
            _useCtorArgs = paramless == null;

            int count = specs.Count;
            _ordinals = new int[count];
            _cores = new Type[count];
            _handlers = new DbConvertHandler?[count];
            if (_useCtorArgs)
            {
                _paramIndexes = new int[count];
                for (int i = 0; i < count; i++)
                {
                    _ordinals[i] = specs[i].Ordinal;
                    _cores[i] = specs[i].CoreType;
                    _handlers[i] = specs[i].ReadHandler;
                    _paramIndexes[i] = specs[i].ParameterIndex;
                }

                ParameterInfo[] parameters = _ctor.GetParameters();
                _defaults = new object?[parameters.Length];
                for (int p = 0; p < parameters.Length; p++)
                {
                    Type core = Nullable.GetUnderlyingType(parameters[p].ParameterType) ?? parameters[p].ParameterType;
                    _defaults[p] = CreateDefaultValue(core);
                }
            }
            else
            {
                _props = new PropertyInfo[count];
                for (int i = 0; i < count; i++)
                {
                    _ordinals[i] = specs[i].Ordinal;
                    _cores[i] = specs[i].CoreType;
                    _handlers[i] = specs[i].ReadHandler;
                    _props[i] = specs[i].Property!;
                }
            }
        }

        /// <summary>
        /// 将 <paramref name="reader"/> 当前行映射为新的 <typeparamref name="TResult"/> 实例。
        /// 列值为 DBNull 时保持默认值（普通类型属性保持初值 / 构造参数取该参数类型默认值），不执行转换。
        /// </summary>
        public TResult Map(AutoLockDataReader reader)
        {
            object? instance = null;
            object?[]? args = null;
            if (_useCtorArgs) args = (object?[])_defaults!.Clone();
            else instance = _ctor.Invoke(null);

            int count = _handlers.Length;
            for (int i = 0; i < count; i++)
            {
                if (reader.IsDBNull(_ordinals[i])) continue; // 保持默认值                
                if (_handlers[i] == null)
                {
                    var converter = reader.DbConverter;
                    Type fieldType = reader.GetFieldType(_ordinals[i]);
                    Type coreType = _cores[i];
                    var valueConverter = converter.GetDbValueConverter(coreType, converter.GetDbValueType(fieldType));
                    if (valueConverter?.DbReadConverter != null)
                        _handlers[i] = valueConverter?.DbReadConverter;
                    else if (coreType.IsAssignableFrom(fieldType))
                        _handlers[i] = new DbConvertHandler(o => o);
                    else if (coreType.IsEnum)
                    {
                        if (fieldType.IsPrimitive)
                            _handlers[i] = new DbConvertHandler(o => Enum.ToObject(coreType, o));
                        else
                            _handlers[i] = new DbConvertHandler(o => Enum.Parse(coreType, o.ToString()!, ignoreCase: true));
                    }
                    else
                        _handlers[i] = new DbConvertHandler(o => Convert.ChangeType(o, coreType));
                }
                DbConvertHandler handler = _handlers[i]!;
                object raw = reader.GetValue(_ordinals[i]);
                object value = handler(raw);
                if (_useCtorArgs) args![_paramIndexes![i]] = value;
                else _props![i].SetValue(instance, value);
            }

            return _useCtorArgs ? (TResult)_ctor.Invoke(args!)! : (TResult)instance!;
        }

        /// <summary>返回类型 <paramref name="type"/> 的默认值：值类型装箱零值，引用类型 null。</summary>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2072",
            Justification = "Activator.CreateInstance is only called for value types (reference types use null); the default constructor of a value type requires no extra trim-preservation.")]
        [UnconditionalSuppressMessage("Trimming", "IL2067",
            Justification = "Activator.CreateInstance(Type) requires PublicParameterlessConstructor; it is only called for value types (reference types return null), whose default constructor needs no extra trim preservation.")]
#endif
        private static object? CreateDefaultValue(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}