using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace LiteOrm.Common
{
    /// <summary>
    /// 对象基类 - 为所有实体对象提供基础功能
    /// </summary>
    /// <remarks>
    /// ObjectBase 是一个抽象基类，为 LiteOrm 框架中的所有实体对象提供通用的功能。
    /// 所有需要与数据库交互的实体类都应该继承自此类。
    /// 
    /// 主要功能包括：
    /// 1. 对象复制 - CopyFrom() 方法用于从另一个对象复制属性值
    /// 2. 对象克隆 - Clone() 方法创建对象的深度克隆副本
    /// 3. 日志记录 - 实现 ILogable 接口以支持操作日志记录
    /// 4. 属性缓存 - 使用 ConcurrentDictionary 缓存类型的属性信息以提高性能
    /// 5. 日志属性记录 - 提供选择性的属性日志记录功能
    /// 6. 序列化支持 - 标记为 Serializable 以支持对象序列化
    /// 7. 表映射支持 - 标记 Table 特性表明这是一个数据库表实体
    /// 
    /// 该类提供了高效的属性访问机制，使用反射缓存来避免重复的性能损耗。
    /// 
    /// 使用示例：
    /// <code>
    /// public class User : ObjectBase
    /// {
    ///     public int Id { get; set; }
    ///     public string Name { get; set; }
    ///     public string Email { get; set; }
    /// }
    /// 
    /// // 复制属性
    /// var user1 = new User { Id = 1, Name = \"John\", Email = \"john@example.com\" };
    /// var user2 = new User();
    /// user2.CopyFrom(user1); // user2 现在有了与 user1 相同的属性值
    /// 
    /// // 克隆对象
    /// var user3 = (User)user1.Clone();
    /// 
    /// // 日志记录
    /// string log = user1.GetLogValue(); // 获取对象的日志表示
    /// </code>
    /// </remarks>
    [Serializable]
    [Table]
    public abstract class ObjectBase : ICopyable<ObjectBase>, ICloneable, ILogable
    {
        // 使用 ConcurrentDictionary 代替 Dictionary + lock
        private static readonly ConcurrentDictionary<Type, string[]> _logPropertiesCache =
            new ConcurrentDictionary<Type, string[]>();

        // 添加缓存来提升性能
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertiesCache =
            new ConcurrentDictionary<Type, PropertyInfo[]>();

        /// <summary>
        /// 从源对象复制属性值
        /// </summary>
        /// <param name="target">源对象</param>
        /// <exception cref="ArgumentNullException">当target为null时抛出</exception>
        public virtual void CopyFrom(ObjectBase target)
        {
            if (target is null) throw new ArgumentNullException(nameof(target));


            Type sourceType = target.GetType();
            Type targetType = this.GetType();

            // 确定要复制的属性范围
            Type copyType = targetType.IsAssignableFrom(sourceType) ? sourceType : targetType;

            foreach (var property in GetProperties(copyType))
            {
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        var value = property.GetValueFast(target);
                        property.SetValueFast(this, value);
                    }
                    catch
                    {
                        // 可选：记录日志或忽略无法复制的属性
                        continue;
                    }
                }
            }
        }

        /// <summary>
        /// 通过属性名索引器访问对象属性
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <returns>属性值</returns>
        /// <exception cref="ArgumentException">当propertyName为null或空时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">当属性不存在时抛出</exception>
        public virtual object this[string propertyName]
        {
            get
            {
                if (string.IsNullOrEmpty(propertyName))
                    throw new ArgumentException("Property name cannot be null or empty", nameof(propertyName));

                var property = this.GetType().GetProperty(propertyName);
                if (property is null)
                    throw new ArgumentOutOfRangeException(nameof(propertyName),
                        $"Property '{propertyName}' not found on type {this.GetType().Name}");

                return property.GetValueFast(this);
            }
            set
            {
                if (string.IsNullOrEmpty(propertyName))
                    throw new ArgumentException("Property name cannot be null or empty", nameof(propertyName));

                var property = this.GetType().GetProperty(propertyName);
                if (property is null)
                    throw new ArgumentOutOfRangeException(nameof(propertyName),
                        $"Property '{propertyName}' not found on type {this.GetType().Name}");

                property.SetValueFast(this, value);
            }
        }

        /// <summary>
        /// 创建当前对象的浅表副本
        /// </summary>
        /// <returns>当前对象的浅表副本</returns>
        public object Clone()
        {
            return MemberwiseClone();
        }

        /// <summary>
        /// 获取需要记录日志的属性列表
        /// </summary>
        /// <returns>需要记录日志的属性名数组</returns>
        protected virtual string[] ToLogProperties()
        {
            var type = this.GetType();
            return _logPropertiesCache.GetOrAdd(type, t =>
            {
                var properties = GetProperties(t);
                var logProperties = new List<string>();

                foreach (var property in properties)
                {
                    if (property.GetIndexParameters().Length == 0)
                    {
                        var logAttribute = property.GetCustomAttribute<LogAttribute>();
                        // 默认记录属性，除非明确设置为 false
                        if (logAttribute is null || logAttribute.Enabled)
                        {
                            logProperties.Add(property.Name);
                        }
                    }
                }

                return logProperties.ToArray();
            });
        }

        /// <summary>
        /// 生成对象的日志字符串（对比模式）
        /// </summary>
        /// <param name="target">对比对象，如果提供则只记录变化的属性</param>
        /// <returns>日志字符串</returns>
        public virtual string ToLog(object target)
        {
            var properties = ToLogProperties();
            if (properties is null || properties.Length == 0)
                return string.Empty;

            var sb = new StringBuilder();
            var type = this.GetType();

            if (target is not null && target.GetType() == type)
            {
                // 对比模式：只记录变化的属性
                foreach (var propertyName in properties)
                {
                    var property = type.GetProperty(propertyName);
                    if (property is null) continue;

                    var thisValue = property.GetValueFast(this);
                    var targetValue = property.GetValueFast(target);

                    if (!Equals(thisValue, targetValue))
                    {
                        AppendProperty(sb, propertyName, thisValue);
                    }
                }
            }
            else
            {
                // 完整记录模式
                foreach (var propertyName in properties)
                {
                    var property = type.GetProperty(propertyName);
                    if (property is null) continue;

                    var value = property.GetValueFast(this);
                    AppendProperty(sb, propertyName, value);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 生成对象的完整日志字符串
        /// </summary>
        /// <returns>日志字符串</returns>
        public virtual string ToLog()
        {
            return ToLog(null);
        }

        #region Helper Methods

        private static PropertyInfo[] GetProperties(Type type)
        {
            return _propertiesCache.GetOrAdd(type, t =>
                t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 .Where(p => p.GetIndexParameters().Length == 0)
                 .ToArray());
        }

        private static void AppendProperty(StringBuilder sb, string propertyName, object value)
        {
            if (value is null || (value is string str && string.IsNullOrEmpty(str)))
                return;

            if (sb.Length > 0)
                sb.Append(", ");

            sb.Append(propertyName);
            sb.Append(':');
            sb.Append(FormatValue(value));
        }

        private static string FormatValue(object value)
        {
            if (value is null) return "null";

            // 处理特殊类型的格式化
            if (value is DateTime dateTime)
                return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
            if (value is bool boolean)
                return boolean ? "true" : "false";
            if (value is IFormattable formattable)
                return formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture);

            return value.ToString();
        }

        #endregion
    }

    /// <summary>
    /// 可复制接口，定义对象复制功能
    /// </summary>
    /// <typeparam name="T">源对象类型</typeparam>
    public interface ICopyable<in T>
    {
        /// <summary>
        /// 从源对象复制数据
        /// </summary>
        /// <param name="source">源对象</param>
        void CopyFrom(T source);
    }
}
