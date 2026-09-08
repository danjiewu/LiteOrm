using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 程序集级设置：指定本程序集要启用哪些 LiteOrm 源生成内容（AOT/裁剪模式下的 TableInfo
    /// 注册器、DataReader 映射器、属性访问器、AOT 类型注册器、AutoRegister 注册器等）。
    /// <para>
    /// 若声明了本特性，以该定义为唯一依据（<b>定义优先</b>），仅生成其 <c>Kinds</c> 属性
    /// 指定的内容，不再依赖 AOT 构建属性自动判定；若未声明，则由源生成器根据构建属性
    /// （PublishAot / IsAotCompatible / PublishTrimmed / IsTrimmable 等）自动判定——AOT 开启时
    /// 自动全量生成，非 AOT 时回退到运行时反射路径（不生成）。
    /// </para>
    /// </summary>
    /// <example>
    /// 声明并限定仅生成 TableInfo 与 DataReaderMappers（表元数据 + 映射，含继承转换器复用）：
    /// <code>[assembly: LiteOrm.Common.LiteOrmCodeGen(
    ///     LiteOrm.Common.LiteOrmCodeGenKind.TableInfo |
    ///     LiteOrm.Common.LiteOrmCodeGenKind.DataReaderMappers)]</code>
    /// </example>
    [Flags]
    public enum LiteOrmCodeGenKind
    {
        /// <summary>不生成任何内容。</summary>
        None = 0,
        /// <summary>TableInfo 注册器（实体/视图表元数据及类型名、枚举映射）。</summary>
        TableInfo = 1 << 0,
        /// <summary>DataReader 映射器（实体映射委托与转换器字段）。</summary>
        DataReaderMappers = 1 << 1,
        /// <summary>属性访问器（PropertyAccessor 委托）。</summary>
        PropertyAccessors = 1 << 2,
        /// <summary>AOT 类型注册器（SqlBuilder / DbConnection）。</summary>
        AotTypeRegistration = 1 << 3,
        /// <summary>AutoRegister 注册器（自定义服务 / DAO）。</summary>
        AutoRegister = 1 << 4,
        /// <summary>生成全部内容。</summary>
        All = TableInfo | DataReaderMappers | PropertyAccessors | AotTypeRegistration | AutoRegister,
    }

    /// <summary>
    /// 程序集级控制 LiteOrm 源生成的内容类别。声明了本特性时以 <see cref="Kinds"/> 为准
    /// （定义优先），未声明时由源生成器按 AOT 构建属性自动判定。
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class LiteOrmCodeGenAttribute : Attribute
    {
        /// <summary>本次要生成的代码内容（Flag 组合）。</summary>
        public LiteOrmCodeGenKind Kinds { get; }

        /// <summary>
        /// 初始化要生成的代码内容。未传参时默认生成全部（<see cref="LiteOrmCodeGenKind.All"/>）。
        /// </summary>
        /// <param name="kinds">要生成的代码类别组合。</param>
        public LiteOrmCodeGenAttribute(LiteOrmCodeGenKind kinds = LiteOrmCodeGenKind.All)
        {
            Kinds = kinds;
        }
    }
}