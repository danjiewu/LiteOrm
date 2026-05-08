using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表表达式
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class TableExpr : SourceExpr, IArged
    {
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public TableExpr() { }

        /// <summary>
        /// 根据对象类型初始化
        /// </summary>
        /// <param name="objectType">对象类型</param>
        public TableExpr(Type objectType)
        {
            Type = objectType;
        }

        /// <summary>
        /// 表表达式没有来源，因此重写 Source 属性，始终返回 null，并且设置时不执行任何操作
        /// </summary>
        [JsonIgnore]
        public override SqlSegment Source { get => null; set => _ = value; }

        /// <summary>
        /// 对象类型
        /// </summary>
        public Type Type { get; set; }

        private string[] _tableArgs;
        /// <summary>
        /// 表参数
        /// </summary>
        public string[] TableArgs
        {
            get => _tableArgs;
            set
            {
                if (value != null)
                {
                    foreach (var arg in value)
                    {
                        ThrowIfInvalidSqlName(nameof(TableArgs), arg);
                    }
                }
                _tableArgs = value;
            }
        }

        /// <summary>
        /// 表达式类型
        /// </summary>
        public override ExprType ExprType => ExprType.Table;

        /// <summary>
        /// 判断两个 TableExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj)
        {
            if (obj is TableExpr other)
            {
                if (!Equals(Type, other.Type)) return false;
                if (Alias != other.Alias) return false;
                if (!ArrayEquals(TableArgs, other.TableArgs)) return false;
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
                typeof(TableExpr).GetHashCode(),
                Type?.GetHashCode() ?? 0,
                Alias?.GetHashCode() ?? 0,
                (TableArgs == null || TableArgs.Length == 0) ? 0 : SequenceHash(TableArgs));
        }

        /// <summary>
        /// 返回 TableExpr 的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return Type?.Name ?? string.Empty;
        }

        /// <summary>
        /// 克隆 TableExpr
        /// </summary>
        public override Expr Clone()
        {
            var t = new TableExpr();
            t.Type = this.Type;
            t.Alias = this.Alias;
            t.TableArgs = this.TableArgs == null ? null : (string[])this.TableArgs.Clone();
            return t;
        }
    }
}