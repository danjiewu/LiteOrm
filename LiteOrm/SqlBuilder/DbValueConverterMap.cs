using LiteOrm.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm
{
    internal class DbValueConverterMap
    {
        // 注册表：Key = 注册(源类型, 目标类型) ，Value = 转换委托
        private readonly ConcurrentDictionary<(Type Source, Type Target), Delegate> _registedReadConverters
            = new ConcurrentDictionary<(Type, Type), Delegate>();

        // 非泛型缓存：Key = 注册(源类型, 目标类型) ，Value = 转换委托
        private readonly ConcurrentDictionary<(Type Source, Type Target), Func<object, object>> _readAdapterCache
            = new ConcurrentDictionary<(Type, Type), Func<object, object>>();

        private readonly ConcurrentDictionary<(Type Source, DbValueType Target), Delegate> _registedWriteConverters
    = new ConcurrentDictionary<(Type, DbValueType), Delegate>();

        // 非泛型缓存：Key = 注册(源类型, 目标类型) ，Value = 转换委托
        private readonly ConcurrentDictionary<(Type Source, DbValueType Target), Func<object, object>> _writeAdapterCache
            = new ConcurrentDictionary<(Type, DbValueType), Func<object, object>>();

        public void RegisterReadConverter<TSource, TTarget>(Func<TSource, TTarget> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var key = (typeof(TSource), typeof(TTarget));
            _registedReadConverters[key] = handler;
            _readAdapterCache[key] = obj => handler((TSource)obj)!;
        }
        public bool TryGetReadConverter<TSource, TTarget>(out Func<TSource, TTarget>? handler)
        {
            Delegate? handlerDelegate;
            if (_registedReadConverters.TryGetValue((typeof(TSource), typeof(TTarget)), out handlerDelegate))
            {
                handler = (Func<TSource, TTarget>)handlerDelegate;
                return true;
            }
            else
            {
                handler = null;
                return false;
            }
        }

        public bool TryGetReadConverter((Type Source, Type Target) key, out Func<object, object>? handler)
        {
            return _readAdapterCache.TryGetValue(key, out handler);
        }

        public void RegisterWriteConverter<TSource>(DbValueType Target, Func<TSource, object> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var key = (typeof(TSource), Target);
            _registedWriteConverters[key] = handler;
            _writeAdapterCache[key] = obj => handler((TSource)obj);
        }

        public bool TryGetWriteConverter<TSource>((Type Source, DbValueType Target) key, out Func<TSource, object>? handler)
        {
            Delegate? handlerDelegate;
            if (_registedWriteConverters.TryGetValue(key, out handlerDelegate))
            {
                handler = (Func<TSource, object>)handlerDelegate;
                return true;
            }
            else
            {
                handler = null;
                return false;
            }
        }

        /// <summary>
        /// 按 (源类型, DbValueType) 查找写入转换器的非泛型适配器。
        /// </summary>
        public bool TryGetWriteConverter((Type Source, DbValueType Target) key, out Func<object, object>? handler)
        {
            return _writeAdapterCache.TryGetValue(key, out handler);
        }

    }
}
