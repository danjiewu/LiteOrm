using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MyOrm.Common
{
    [Serializable]
    [Table]
    public abstract class ObjectBase : ICopyable, ICloneable, ILogable
    {
        public virtual void CopyFrom(object target)
        {
            if (!(target is ObjectBase)) return;
            Type type = this.GetType();
            if (type.IsSubclassOf(target.GetType())) type = target.GetType();
            foreach (PropertyInfo property in type.GetProperties())
            {
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    property.SetVal(this, property.GetVal(target));
                }
            }
        }

        [Column(false)]
        public virtual object this[string propertyName]
        {
            get
            {
                PropertyInfo property = this.GetType().GetProperty(propertyName);
                if (propertyName == null) throw new ArgumentOutOfRangeException(propertyName);
                return property.GetVal(this);
            }
            set
            {
                PropertyInfo property = this.GetType().GetProperty(propertyName);
                if (propertyName == null) throw new ArgumentOutOfRangeException(propertyName);
                property.SetVal(this, value);
            }
        }

        public object Clone()
        {
            return MemberwiseClone();
        }

        private static Dictionary<Type, string[]> toLogProperties = new Dictionary<Type, string[]>();

        protected virtual string[] ToLogProperties()
        {
            if (!toLogProperties.ContainsKey(this.GetType()))
            {
                lock (toLogProperties)
                {
                    if (!toLogProperties.ContainsKey(this.GetType()))
                    {
                        List<string> logProperties = new List<string>();
                        foreach (PropertyInfo property in this.GetType().GetProperties())
                        {
                            if (property.GetIndexParameters().Length == 0)
                            {
                                LogAttribute att = property.GetAttribute<LogAttribute>();
                                if (att == null || att.Enabled == true) { logProperties.Add(property.Name); }
                            }
                        }
                        toLogProperties[this.GetType()] = logProperties.ToArray();
                    }
                }
            }
            return toLogProperties[this.GetType()];
        }

        public virtual string ToLog(object target)
        {
            string[] properties = ToLogProperties();
            StringBuilder sb = new StringBuilder();
            if (properties != null)
            {
                if (target != null && target.GetType() == this.GetType())
                    foreach (string propertyName in properties)
                    {
                        object o = this[propertyName];
                        if (!Equals(o, ((ObjectBase)target)[propertyName]))
                        {
                            if (sb.Length > 0) sb.Append(",");
                            sb.AppendFormat("{0}:{1}", propertyName, o);
                        }
                    }
                else
                    foreach (string propertyName in properties)
                    {
                        string strProperty = Convert.ToString(this[propertyName]);
                        if (!String.IsNullOrEmpty(strProperty))
                        {
                            if (sb.Length > 0) sb.Append(",");
                            sb.AppendFormat("{0}:{1}", propertyName, strProperty);
                        }
                    }
            }
            return sb.ToString();
        }

        public virtual string ToLog()
        {
            return ToLog(null);
        }
    }
    public interface ICopyable
    {
        void CopyFrom(object target);
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
