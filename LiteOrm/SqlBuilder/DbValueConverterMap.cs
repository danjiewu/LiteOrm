using LiteOrm.Common;
using System;
using System.Collections.Concurrent;

namespace LiteOrm
{
    /// <summary>
    /// 数据库值转换器统一注册表（读取与写入共用）。
    /// Key = (值类型, 数据库取值类型枚举)：值类型为实体属性 / .NET 值类型（即 <see cref="IDbValueConverter.ValueType"/>），
    /// 枚举为列的数据库取值类型。读取按 (目标属性类型, 列取值类型) 查找，写入按 (源值类型, 目标取值类型) 查找，
    /// 同一 key 空间双向复用，一个转换器天然服务读、写两个方向。
    /// </summary>
    internal class DbValueConverterMap
    {
        // 统一注册表：Key = (值类型, 数据库取值类型枚举) ，Value = 双向转换器
        private readonly ConcurrentDictionary<(Type ValueType, DbValueType DbValueType), IDbValueConverter> _converters
            = new ConcurrentDictionary<(Type, DbValueType), IDbValueConverter>();

        /// <summary>
        /// 注册转换器。注册 key = (converter.ValueType, <paramref name="target"/>)；
        /// 同一转换器实例可注册到多个枚举目标（如 string 类的多个枚举值）。
        /// </summary>
        /// <param name="converter">双向转换器实例。</param>
        /// <param name="target">目标数据库取值类型。</param>
        public void RegisterConverter(IDbValueConverter converter, DbValueType target)
        {
            if (converter == null) throw new ArgumentNullException(nameof(converter));
            _converters[(converter.ValueType, target)] = converter;
        }

        /// <summary>
        /// 按 (值类型, 数据库取值类型) 查找注册的转换器。
        /// </summary>
        public bool TryGetConverter((Type ValueType, DbValueType DbValueType) key, out IDbValueConverter? converter)
        {
            return _converters.TryGetValue(key, out converter);
        }
    }
}
