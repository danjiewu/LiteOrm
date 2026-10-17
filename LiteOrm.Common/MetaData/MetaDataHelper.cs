using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表元数据构建的公共辅助方法。供 <see cref="LiteOrm.AttributeTableInfoProvider"/>（运行期读特性）
    /// 与 <see cref="CommonTableInfoProvider"/>（消费编译期元数据）共用，保证两条元数据线路产出一致的结果。
    /// </summary>
    internal static class MetaDataHelper
    {
        /// <summary>
        /// 按列的固定筛选值（<see cref="ColumnDefinition.Constant"/>）拼出表的固定筛选条件。
        /// </summary>
        /// <param name="columns">表定义中的列集合。</param>
        /// <returns>各列固定筛选条件以 AND 连接的表达式；没有列声明固定值时返回 null。</returns>
        internal static LogicExpr? BuildConstFilter(IEnumerable<ColumnDefinition> columns)
        {
            LogicExpr? constFilter = null;
            foreach (ColumnDefinition column in columns)
            {
                if (column.Constant is null) continue;
                constFilter &= Expr.Prop(column.PropertyName) == Expr.Const(column.Constant);
            }
            return constFilter;
        }

        /// <summary>
        /// 把固定筛选条件中的属性引用统一补上表别名，供关联表（<see cref="JoinedTable.ConstFilter"/>）复用。
        /// </summary>
        /// <param name="constFilter">原始固定筛选条件。</param>
        /// <param name="tableAlias">要补的表别名。</param>
        /// <returns>补上别名后的副本；<paramref name="constFilter"/> 为 null 时返回 null。</returns>
        internal static LogicExpr? AliasConstFilter(LogicExpr? constFilter, string tableAlias)
        {
            if (constFilter is null) return null;
            LogicExpr aliasedFilter = (LogicExpr)constFilter.Clone();
            ExprVisitor.Visit(node =>
            {
                if (node is PropertyExpr propertyExpr && String.IsNullOrEmpty(propertyExpr.TableAlias))
                {
                    propertyExpr.TableAlias = tableAlias;
                }
                return true;
            }, aliasedFilter);
            return aliasedFilter;
        }

        /// <summary>
        /// 提取关联表参与关联的键列，未声明时使用目标表主键。
        /// </summary>
        /// <param name="targetTableDef">关联目标表的表定义。</param>
        /// <param name="primeKeys">逗号分隔的键列名；为空时取目标表主键。</param>
        /// <returns>参与关联的键列集合。</returns>
        internal static ColumnDefinition[] GetJoinPrimeKeys(TableDefinition targetTableDef, string? primeKeys)
        {
            if (String.IsNullOrWhiteSpace(primeKeys))
            {
                return targetTableDef.Keys.ToArray();
            }

            string[] primeKeyNames = primeKeys!.Split(',');
            ColumnDefinition[] keys = new ColumnDefinition[primeKeyNames.Length];
            for (int i = 0; i < primeKeyNames.Length; i++)
            {
                string primeKeyName = primeKeyNames[i].Trim();
                ColumnDefinition? primeKey = targetTableDef.GetColumn(primeKeyName);
                if (primeKey == null)
                    throw new ArgumentException($"Prime key column \"{primeKeyName}\" does not exist in target table \"{targetTableDef.ObjectType.Name}\".");
                keys[i] = primeKey;
            }
            return keys;
        }

        /// <summary>
        /// 把声明在特性上的常量对齐到属性类型（字符串枚举名、整数转枚举、其余按 <see cref="Convert.ChangeType(object, Type)"/>）。
        /// </summary>
        /// <param name="property">常量所属的属性。</param>
        /// <param name="constant">特性上声明的原始常量值。</param>
        /// <returns>已对齐到属性类型的常量；<paramref name="constant"/> 为 null 时返回 null。</returns>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "PropertyInfo.PropertyType is a runtime-known Type whose properties are naturally available; under AOT, properties are preserved via [Table] + source generator.")]
#endif
        internal static object? ParseConstant(PropertyInfo property, object? constant)
        {
            if (constant is null) return null;

            Type propType = property.PropertyType.GetUnderlyingType();
            Type constantType = constant.GetType();
            if (constantType == propType || propType.IsAssignableFrom(constantType)) return constant;

            if (propType.IsEnum)
            {
                if (constant is string text)
                {
                    text = text.Trim();
                    if (text.Length == 0)
                        throw new ArgumentException($"Column.Constant cannot be empty for enum property \"{property.DeclaringType?.FullName}.{property.Name}\".");
                    if (Int32.TryParse(text, out int intValue))
                        return Enum.ToObject(propType, intValue);
                    return EnumUtil.Parse(propType, text) ?? Enum.Parse(propType, text);
                }
                else if (IsIntegerType(constantType))
                {
                    object numericValue = Convert.ChangeType(constant, Enum.GetUnderlyingType(propType));
                    return Enum.ToObject(propType, numericValue);
                }
            }
            try
            {
                return Convert.ChangeType(constant, propType);
            }
            catch { }
            return constant;
        }

        /// <summary>
        /// 判断类型是否为整数类型（含其可空形式）。
        /// </summary>
        /// <param name="type">待判断的类型。</param>
        /// <returns>是整数类型时返回 true。</returns>
        private static bool IsIntegerType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type == typeof(byte)
                || type == typeof(sbyte)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(int)
                || type == typeof(uint)
                || type == typeof(long)
                || type == typeof(ulong);
        }
    }
}
