using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MyOrm.Common
{
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

        public virtual void CopyFrom(ObjectBase target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
  

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

        public virtual object this[string propertyName]
        {
            get
            {
                if (string.IsNullOrEmpty(propertyName))
                    throw new ArgumentException("Property name cannot be null or empty", nameof(propertyName));

                var property = this.GetType().GetProperty(propertyName);
                if (property == null)
                    throw new ArgumentOutOfRangeException(nameof(propertyName),
                        $"Property '{propertyName}' not found on type {this.GetType().Name}");

                return property.GetValueFast(this);
            }
            set
            {
                if (string.IsNullOrEmpty(propertyName))
                    throw new ArgumentException("Property name cannot be null or empty", nameof(propertyName));

                var property = this.GetType().GetProperty(propertyName);
                if (property == null)
                    throw new ArgumentOutOfRangeException(nameof(propertyName),
                        $"Property '{propertyName}' not found on type {this.GetType().Name}");

                property.SetValueFast(this, value);
            }
        }

        public object Clone()
        {
            return MemberwiseClone();
        }

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
                        if (logAttribute == null || logAttribute.Enabled)
                        {
                            logProperties.Add(property.Name);
                        }
                    }
                }

                return logProperties.ToArray();
            });
        }

        public virtual string ToLog(object target)
        {
            var properties = ToLogProperties();
            if (properties == null || properties.Length == 0)
                return string.Empty;

            var sb = new StringBuilder();
            var type = this.GetType();

            if (target != null && target.GetType() == type)
            {
                // 对比模式：只记录变化的属性
                foreach (var propertyName in properties)
                {
                    var property = type.GetProperty(propertyName);
                    if (property == null) continue;

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
                    if (property == null) continue;

                    var value = property.GetValueFast(this);
                    AppendProperty(sb, propertyName, value);
                }
            }

            return sb.ToString();
        }

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
            if (value == null || (value is string str && string.IsNullOrEmpty(str)))
                return;

            if (sb.Length > 0)
                sb.Append(", ");

            sb.Append(propertyName);
            sb.Append(':');
            sb.Append(FormatValue(value));
        }

        private static string FormatValue(object value)
        {
            if (value == null) return "null";

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

    public interface ICopyable<in T>
    {
        void CopyFrom(T source);
    }

    public interface ILogable
    {
        string ToLog();
    }

    public interface IArged
    {
        string[] TableArgs { get; }
    }
}