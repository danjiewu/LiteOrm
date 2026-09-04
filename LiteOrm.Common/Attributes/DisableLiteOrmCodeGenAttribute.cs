using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 手动关闭本程序集的 LiteOrm 源生成代码（AOT/裁剪模式下的 TableInfo 注册器、
    /// AutoRegister 注册器、AOT 类型注册器等）。
    /// <para>
    /// LiteOrm 源生成器会根据构建属性（PublishAot / IsAotCompatible / PublishTrimmed /
    /// IsTrimmable 等）自动判定是否生成 AOT 代码。当自动判定误判（例如非 AOT 工程因引用了
    /// 裁剪兼容的包而误触发），或你希望回退到运行时反射路径时，可在 AssemblyInfo 声明本特性
    /// 强制关闭代码生成：
    /// </para>
    /// <code>[assembly: LiteOrm.Common.DisableLiteOrmCodeGen]</code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class DisableLiteOrmCodeGenAttribute : Attribute
    {
    }
}