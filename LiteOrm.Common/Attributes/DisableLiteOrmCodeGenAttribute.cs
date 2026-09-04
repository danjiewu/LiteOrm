using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 手动关闭本程序集的 LiteOrm 源生成代码（AOT/裁剪模式下的 TableInfo 注册器、
    /// DataReader 映射器、属性访问器、AOT 类型注册器、AutoRegister 注册器等）。
    /// <para>
    /// LiteOrm 源生成器会根据构建属性（PublishAot / IsAotCompatible / PublishTrimmed /
    /// IsTrimmable 等）自动判定是否生成 AOT 代码。当自动判定误判（例如非 AOT 工程因引用了
    /// 裁剪兼容的包而误触发），或你希望回退到运行时反射路径时，可在 AssemblyInfo 声明本特性
    /// 强制关闭代码生成。
    /// </para>
    /// </summary>
    /// <example>
    /// 完全关闭（与旧版本一致，亦可通过无参构造实现）：
    /// <code>[assembly: LiteOrm.Common.DisableLiteOrmCodeGen]</code>
    /// <code>[assembly: LiteOrm.Common.DisableLiteOrmCodeGen(LiteOrm.Common.LiteOrmCodeGenKind.All)]</code>
    /// 仅关闭 TableInfo 与 DataReaderMappers，保留属性访问器与 AOT 类型注册：
    /// <code>[assembly: LiteOrm.Common.DisableLiteOrmCodeGen(
    ///     LiteOrm.Common.LiteOrmCodeGenKind.TableInfo |
    ///     LiteOrm.Common.LiteOrmCodeGenKind.DataReaderMappers)]</code>
    /// </example>
    [Flags]
    public enum LiteOrmCodeGenKind
    {
        /// <summary>不关闭任何内容。</summary>
        None = 0,
        /// <summary>关闭 TableInfo 注册器（实体/视图表元数据及类型名、枚举映射）生成。</summary>
        TableInfo = 1 << 0,
        /// <summary>关闭 DataReader 映射器（实体映射委托与转换器字段）生成。</summary>
        DataReaderMappers = 1 << 1,
        /// <summary>关闭属性访问器（PropertyAccessor 委托）生成。</summary>
        PropertyAccessors = 1 << 2,
        /// <summary>关闭 AOT 类型注册器（SqlBuilder / DbConnection）生成。</summary>
        AotTypeRegistration = 1 << 3,
        /// <summary>关闭 AutoRegister 注册器（自定义服务 / DAO）生成。</summary>
        AutoRegister = 1 << 4,
        /// <summary>关闭全部代码生成。</summary>
        All = TableInfo | DataReaderMappers | PropertyAccessors | AotTypeRegistration | AutoRegister,
    }

    /// <summary>
    /// 用于按内容类别细粒度关闭 LiteOrm 源生成代码。无参声明时等价于
    /// <see cref="LiteOrmCodeGenKind.All"/>（完全关闭，保持旧行为）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class DisableLiteOrmCodeGenAttribute : Attribute
    {
        /// <summary>本次要关闭的代码生成内容（Flag 组合）。</summary>
        public LiteOrmCodeGenKind Kinds { get; }

        /// <summary>
        /// 初始化要关闭的代码生成内容。未传参时默认关闭全部（<see cref="LiteOrmCodeGenKind.All"/>）。
        /// </summary>
        /// <param name="kinds">要关闭的代码生成类别组合。</param>
        public DisableLiteOrmCodeGenAttribute(LiteOrmCodeGenKind kinds = LiteOrmCodeGenKind.All)
        {
            Kinds = kinds;
        }
    }
}