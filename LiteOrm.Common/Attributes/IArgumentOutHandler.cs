using System;
using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// 参数输出回写处理器接口。在远程调用的服务端和客户端分别执行，实现自定义的参数回写逻辑。
    /// </summary>
    /// <remarks>
    /// 处理器可由 <see cref="ArgumentOutAttribute"/> 通过 <see cref="ArgumentOutAttribute.HandlerType"/> 指定实现类型；
    /// 也可由直接实现 <see cref="IArgumentOutHandler"/> 的特性（如 <see cref="IdentityOutAttribute"/>、<see cref="CopyableOutAttribute"/>）承担，
    /// 此时特性实例本身即为处理器，无需额外指定 HandlerType。
    ///
    /// 处理流程分为两阶段：
    /// 1. 服务端调用服务方法后，调用 <see cref="GenerateReturnValue"/> 从（可能已被修改的）参数对象
    ///    生成需要回传给客户端的返回值，服务端按 <see cref="ReturnType"/> 序列化后放入响应。
    /// 2. 客户端反序列化返回值后，调用 <see cref="WriteBack"/> 将其应用到原始参数对象（保持引用不变）。
    ///
    /// 通过 <see cref="ArgumentOutAttribute"/> 指定 HandlerType 时，处理器优先从 DI 容器解析，
    /// 无法解析时通过带 <c>Type</c> 参数的构造函数创建，
    /// 将 <see cref="ArgumentOutAttribute.ReturnType"/> 作为构造参数传入。
    /// </remarks>
    public interface IArgumentOutHandler
    {
        /// <summary>
        /// 返回值的类型。用于服务端序列化和客户端反序列化，确保两端使用一致的类型。
        /// </summary>
        Type ReturnType { get; }

        /// <summary>
        /// 服务端：从参数对象生成需要回传给客户端的返回值。
        /// </summary>
        /// <param name="argument">参数对象（可能已被服务方法修改）。</param>
        /// <returns>需要回传的返回值。返回 null 表示跳过该参数的回写。</returns>
        object GenerateReturnValue(object argument);

        /// <summary>
        /// 客户端：从服务端返回值回写到原始参数对象。
        /// </summary>
        /// <param name="originalArg">客户端原始参数对象（保持引用不变，仅更新其状态）。</param>
        /// <param name="returnValue">服务端生成的返回值（已按 <see cref="ReturnType"/> 反序列化）。</param>
        void WriteBack(object originalArg, object returnValue);
    }
}
