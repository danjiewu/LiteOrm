using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// From 片段，表示查询的数据源（支持单表或多表视图）
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class FromExpr : Expr, ISourceAnchor, ISqlSegment
    {
        /// <summary>
        /// 初始化 FromExpr 类的新实例
        /// </summary>
        public FromExpr() { }

        /// <summary>
        /// 使用指定的实体类型初始化 FromExpr 类的新实例
        /// </summary>
        /// <param name="objectType">实体类型</param>
        public FromExpr(Type objectType)
        {
            ObjectType = objectType;
        }

        /// <summary>
        /// 获取或设置别名
        /// </summary>
        private string _alias;
        public string Alias 
        { 
            get => _alias;
            set
            {
                if (value != null && !LiteOrm.Common.Const.ValidNameRegex.IsMatch(value))
                {
                    throw new ArgumentException("Alias contains illegal characters. Only letters, numbers, and underscores are allowed.", nameof(Alias));
                }
                _alias = value;
            }
        }

        /// <summary>
        /// 获取或设置实体类型（用于获取表或视图定义）
        /// </summary>
        public Type ObjectType { get; set; }

        /// <summary>
        /// 获取表名参数数组
        /// </summary>
        private string[] _tableArgs;
        public string[] TableArgs 
        { 
            get => _tableArgs;
            set
            {
                if (value != null)
                {
                    foreach (var arg in value)
                    {
                        if (arg != null && !LiteOrm.Common.Const.ValidNameRegex.IsMatch(arg))
                        {
                            throw new ArgumentException("Table argument contains illegal characters. Only letters, numbers, and underscores are allowed.", nameof(TableArgs));
                        }
                    }
                }
                _tableArgs = value;
            }
        }

        /// <summary>
        /// 获取片段类型，返回 From 类型标识
        /// </summary>
        public SqlSegmentType SegmentType => SqlSegmentType.From;

        /// <summary>
        /// 获取或设置源片段
        /// </summary>
        public ISqlSegment Source { get; set; }

        /// <summary>
        /// 判断两个 FromExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj)
        {
            if (obj is FromExpr other)
            {
                if (!Equals(ObjectType, other.ObjectType)) return false;
                if (Alias != other.Alias) return false;
                if (!ArrayEquals(TableArgs, other.TableArgs)) return false;
                if (!Equals(Source, other.Source)) return false;
                return true;
            }
            return false;
        }

        private static bool ArrayEquals(string[] a, string[] b)
        {
            if (a == null || a.Length == 0) return b == null || b.Length == 0;
            if (b == null || b.Length == 0) return false;
            return a.SequenceEqual(b);
        }

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode()
        {
            return OrderedHashCodes(
                typeof(FromExpr).GetHashCode(),
                ObjectType?.GetHashCode() ?? 0,
                Alias?.GetHashCode() ?? 0,
                Source?.GetHashCode() ?? 0,
                (TableArgs == null || TableArgs.Length == 0) ? 0 : SequenceHash(TableArgs));
        }

        /// <summary>
        /// 返回表或视图的名称字符串
        /// </summary>
        /// <returns>名称</returns>
        public override string ToString()
        {
            return ObjectType?.Name ?? string.Empty;
        }
    }
}
