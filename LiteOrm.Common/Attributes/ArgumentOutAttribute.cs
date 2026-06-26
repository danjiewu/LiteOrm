using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 参数输出的处理模式。
    /// </summary>
    public enum ArgumentMode
    {
        /// <summary>
        /// 单对象模式。参数为单个实体，处理器直接处理该对象。
        /// </summary>
        Single = 0,

        /// <summary>
        /// 集合模式。参数为实体集合（实现 <see cref="System.Collections.IEnumerable"/>），
        /// 基础设施逐项调用处理器的 <see cref="IArgumentOutHandler.GenerateReturnValue"/> 和 <see cref="IArgumentOutHandler.WriteBack"/>。
        /// </summary>
        Collection = 1,
    }

    /// <summary>
    /// 参数输出特性，用于标记远程调用中需要回写的参数。
    /// </summary>
    /// <remarks>
    /// 标记此特性的参数，在服务端调用完成后，通过 <see cref="HandlerType"/> 指定的
    /// <see cref="IArgumentOutHandler"/> 处理器将服务端修改的参数状态同步回客户端原始对象
    /// （例如插入实体后回写自增主键、回填创建时间等）。
    ///
    /// <see cref="HandlerType"/> 和 <see cref="ReturnType"/> 均为必填项，常见用法：
    /// <code>
    /// // 插入实体后回写自增主键（单对象，返回值类型为 int）
    /// Task Insert([ArgumentOut(typeof(IdentityArgumentOutHandler), typeof(int))] User user);
    /// // 插入实体集合后逐项回写自增主键
    /// Task BatchInsert([ArgumentOut(typeof(IdentityArgumentOutHandler), typeof(int), Mode = ArgumentMode.Collection)] List&lt;User&gt; users);
    /// // 整体回写（参数实现 ICopyable，返回值类型为 CopyableUser）
    /// Task Insert([ArgumentOut(typeof(CopyableArgumentOutHandler), typeof(CopyableUser))] CopyableUser user);
    /// // 自定义处理器，返回值类型为 UserDelta
    /// Task Insert([ArgumentOut(typeof(DeltaHandler), typeof(UserDelta))] User user);
    /// </code>
    ///
    /// 处理器优先从 DI 容器解析，无法解析时通过带 <c>Type</c> 参数的构造函数创建，
    /// 将 <see cref="ReturnType"/> 作为构造参数传入。
    ///
    /// 集合模式下，<see cref="ReturnType"/> 表示单个元素的返回值类型（如 <c>typeof(int)</c>），
    /// 基础设施会将其包装为 <c>List&lt;ReturnType&gt;</c> 进行序列化和反序列化。
    /// 注意：仅支持标记在方法参数上。值类型参数由于按值传递，回写无法反映到调用方的局部变量，应避免使用。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public class ArgumentOutAttribute : Attribute
    {
        /// <summary>
        /// 初始化 <see cref="ArgumentOutAttribute"/> 类的新实例，指定回写处理器类型和返回值类型。
        /// </summary>
        /// <param name="handlerType">回写处理器类型，需实现 <see cref="IArgumentOutHandler"/> 接口。</param>
        /// <param name="returnType">处理器返回值的类型，用于服务端序列化和客户端反序列化。</param>
        public ArgumentOutAttribute(Type handlerType, Type returnType)
        {
            HandlerType = handlerType;
            ReturnType = returnType;
        }

        /// <summary>
        /// 回写处理器类型，需实现 <see cref="IArgumentOutHandler"/> 接口。
        /// </summary>
        public Type HandlerType { get; }

        /// <summary>
        /// 处理器返回值的类型。
        /// 用于服务端序列化和客户端反序列化；同时作为构造参数传入处理器。
        /// 集合模式下表示单个元素的返回值类型。
        /// </summary>
        public Type ReturnType { get; }

        /// <summary>
        /// 参数处理模式。默认为 <see cref="ArgumentMode.Single"/>（单对象）。
        /// 设为 <see cref="ArgumentMode.Collection"/> 时，参数作为集合逐项处理。
        /// </summary>
        public ArgumentMode Mode { get; set; } = ArgumentMode.Single;
    }
}
