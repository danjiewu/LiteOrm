using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示外键关联查询表达式。
    /// 该表达式用于构建 EXISTS 子查询，支持完全通过 InnerExpr 控制关联条件。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class ForeignExpr : LogicExpr
    {
        /// <summary>
        /// 获取或设置针对关联表的内部过滤表达式。
        /// </summary>
        public LogicExpr InnerExpr { get; set; }

        private Type _foreign;
        /// <summary>
        /// 获取或设置外部实体类型。
        /// </summary>
        public new Type Foreign
        {
            get { return _foreign; }
            set { _foreign = value; }
        }

        private string _alias;
        /// <summary>
        /// 获取或设置外部表的别名。
        /// </summary>
        public string Alias
        {
            get { return _alias; }
            set
            {
                if (value != null && !LiteOrm.Common.Const.ValidNameRegex.IsMatch(value))
                {
                    throw new System.ArgumentException("Foreign table alias contains illegal characters. Only letters, numbers, and underscores are allowed.", nameof(Alias));
                }
                _alias = value;
            }
        }

        private string[] _tableArgs;
        /// <summary>
        /// 获取或设置用于动态表名的参数集合。
        /// </summary>
        public string[]
        TableArgs
        {
            get { return _tableArgs; }
            set
            {
                if (value != null)
                {
                    foreach (var arg in value)
                    {
                        if (arg != null && !LiteOrm.Common.Const.ValidNameRegex.IsMatch(arg))
                        {
                            throw new System.ArgumentException("Table name parameter contains illegal characters. Only letters, numbers, and underscores are allowed.", nameof(TableArgs));
                        }
                    }
                }
                _tableArgs = value;
            }
        }

        /// <summary>
        /// 初始化 <see cref="ForeignExpr"/> 类的新实例。
        /// </summary>
        public ForeignExpr() { }

        /// <summary>
        /// 使用指定的外部实体类型和内部表达式初始化 <see cref="ForeignExpr"/> 类的新实例。
        /// </summary>
        /// <param name="foreign">外部实体类型。</param>
        /// <param name="expr">内部过滤表达式。</param>
        public ForeignExpr(Type foreign, LogicExpr expr)
        {
            Foreign = foreign;
            InnerExpr = expr;
        }

        /// <summary>
        /// 使用指定的外部实体类型、内部表达式和表名参数初始化 <see cref="ForeignExpr"/> 类的新实例。
        /// </summary>
        /// <param name="foreign">外部实体类型。</param>
        /// <param name="expr">内部过滤表达式。</param>
        /// <param name="tableArgs">动态表名参数。</param>
        public ForeignExpr(Type foreign, LogicExpr expr, params string[] tableArgs)
        {
            Foreign = foreign;
            InnerExpr = expr;
            TableArgs = tableArgs;
        }

        /// <summary>
        /// 使用指定的外部实体类型、别名和内部表达式初始化 <see cref="ForeignExpr"/> 类的新实例。
        /// </summary>
        /// <param name="foreign">外部实体类型。</param>
        /// <param name="alias">外部表别名。</param>
        /// <param name="expr">内部过滤表达式。</param>
        public ForeignExpr(Type foreign, string alias, LogicExpr expr)
        {
            Foreign = foreign;
            Alias = alias;
            InnerExpr = expr;
        }

        /// <summary>
        /// 使用指定的外部实体类型、别名、内部表达式和表名参数初始化 <see cref="ForeignExpr"/> 类的新实例。
        /// </summary>
        /// <param name="foreign">外部实体类型。</param>
        /// <param name="alias">外部表别名。</param>
        /// <param name="expr">内部过滤表达式。</param>
        /// <param name="tableArgs">动态表名参数。</param>
        public ForeignExpr(Type foreign, string alias, LogicExpr expr, params string[] tableArgs)
        {
            Foreign = foreign;
            Alias = alias;
            InnerExpr = expr;
            TableArgs = tableArgs;
        }

        /// <summary>
        /// 比较两个 ForeignExpr 是否相等。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is ForeignExpr f && f.Foreign == Foreign && f.Alias == Alias && Equals(f.InnerExpr, InnerExpr) &&
                   ((f.TableArgs == null && TableArgs == null) || (f.TableArgs != null && TableArgs != null && f.TableArgs.SequenceEqual(TableArgs)));
        }

        /// <summary>
        /// 生成哈希值。
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode()
        {
            int hash = OrderedHashCodes(GetType().GetHashCode(), Foreign?.GetHashCode() ?? 0, Alias?.GetHashCode() ?? 0, InnerExpr?.GetHashCode() ?? 0);
            if (TableArgs != null)
            {
                foreach (var arg in TableArgs)
                    hash = OrderedHashCodes(hash, arg?.GetHashCode() ?? 0);
            }
            return hash;
        }
        /// <summary>
        /// 返回表达式的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return $"Foreign {Foreign?.Name ?? Alias}{{{InnerExpr}}}";
        }
    }
}
