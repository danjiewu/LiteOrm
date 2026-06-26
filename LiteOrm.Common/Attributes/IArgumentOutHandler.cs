using System;
using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// 参数输出回写处理器接口。在远程调用的服务端和客户端分别执行，实现自定义的参数回写逻辑。
    /// </summary>
    /// <remarks>
    /// 通过 <see cref="ArgumentOutAttribute.HandlerType"/> 指定实现类型。
    /// 处理流程分为两阶段：
    /// 1. 服务端调用服务方法后，调用 <see cref="GenerateReturnValue"/> 从（可能已被修改的）参数对象
    ///    生成需要回传给客户端的返回值，服务端按 <see cref="ReturnType"/> 序列化后放入响应。
    /// 2. 客户端反序列化返回值后，调用 <see cref="WriteBack"/> 将其应用到原始参数对象（保持引用不变）。
    ///
    /// 处理器优先从 DI 容器解析，无法解析时通过带 <c>Type</c> 参数的构造函数创建，
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

    /// <summary>
    /// Identity 回写处理器。仅用于插入实体后回写自增主键（Identity 列）的值。
    /// </summary>
    /// <remarks>
    /// 服务端仅返回参数对象中 Identity 列的当前值；若参数类型没有 Identity 列，则返回 null（跳过回写）。
    /// 客户端将返回的 Identity 值写回原始参数对象的对应属性。
    ///
    /// Identity 列通过 <see cref="TableInfoProvider.Default"/> 元数据识别，
    /// 客户端与服务端均需注册 <see cref="TableInfoProvider.Default"/>。
    /// </remarks>
    public class IdentityArgumentOutHandler : IArgumentOutHandler
    {
        /// <inheritdoc/>
        public Type ReturnType { get; }

        /// <summary>
        /// 初始化 <see cref="IdentityArgumentOutHandler"/> 类的新实例，指定返回值类型。
        /// </summary>
        /// <param name="returnType">返回值的类型，通常为 Identity 列的属性类型（如 <c>typeof(int)</c>）。</param>
        public IdentityArgumentOutHandler(Type returnType)
        {
            ReturnType = returnType;
        }

        /// <inheritdoc/>
        public object GenerateReturnValue(object argument)
        {
            if (argument is null) return null;
            var prop = ResolveIdentityProperty(argument.GetType());
            return prop?.GetValue(argument);
        }

        /// <inheritdoc/>
        public void WriteBack(object originalArg, object returnValue)
        {
            if (originalArg is null || returnValue is null) return;
            var prop = ResolveIdentityProperty(originalArg.GetType());
            if (prop is null || !prop.CanWrite) return;
            prop.SetValue(originalArg, returnValue);
        }

        /// <summary>
        /// 通过 <see cref="TableInfoProvider.Default"/> 元数据解析类型上的 Identity 属性。
        /// 若未注册 <see cref="TableInfoProvider.Default"/> 或该类型无 Identity 列，则返回 null。
        /// </summary>
        private static PropertyInfo ResolveIdentityProperty(Type type)
        {
            var provider = TableInfoProvider.Default;
            if (provider is null) return null;
            try
            {
                var tableDef = provider.GetTableDefinition(type);
                return tableDef?.IdentityColumn?.Property;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// 通用回写处理器，适用于实现了 <see cref="ICopyable"/> 接口的参数类型。
    /// </summary>
    /// <remarks>
    /// 服务端直接返回参数对象本身，客户端通过 <see cref="ICopyable.CopyFrom"/> 将返回值整体复制到原始对象。
    /// </remarks>
    public class CopyableArgumentOutHandler : IArgumentOutHandler
    {
        /// <inheritdoc/>
        public Type ReturnType { get; }

        /// <summary>
        /// 初始化 <see cref="CopyableArgumentOutHandler"/> 类的新实例，指定返回值类型。
        /// </summary>
        /// <param name="returnType">返回值的类型，通常与参数类型一致。</param>
        public CopyableArgumentOutHandler(Type returnType)
        {
            ReturnType = returnType;
        }

        /// <inheritdoc/>
        public object GenerateReturnValue(object argument) => argument;

        /// <inheritdoc/>
        public void WriteBack(object originalArg, object returnValue)
        {
            if (originalArg is ICopyable target)
                target.CopyFrom(returnValue);
        }
    }
}
