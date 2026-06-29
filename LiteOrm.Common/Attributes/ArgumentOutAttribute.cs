using System;
using System.Reflection;

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
    /// // 自定义处理器，返回值类型为 UserDelta
    /// Task Insert([ArgumentOut(typeof(DeltaHandler), typeof(UserDelta))] User user);
    /// </code>
    ///
    /// 对于内置的常见回写场景，优先使用直接实现 <see cref="IArgumentOutHandler"/> 的派生特性，
    /// 无需指定 HandlerType 和 ReturnType：
    /// <list type="bullet">
    /// <item><see cref="IdentityOutAttribute"/>：插入实体后回写自增主键（Identity 列）。</item>
    /// <item><see cref="CopyableOutAttribute"/>：整体回写实现了 <see cref="ICopyable"/> 接口的参数。</item>
    /// </list>
    /// <code>
    /// // 插入实体后回写自增主键（单对象）
    /// Task Insert([IdentityOut] User user);
    /// // 插入实体集合后逐项回写自增主键
    /// Task BatchInsert([IdentityOut(Mode = ArgumentMode.Collection)] List&lt;User&gt; users);
    /// // 整体回写（参数实现 ICopyable）
    /// Task Insert([CopyableOut(typeof(CopyableUser))] CopyableUser user);
    /// </code>
    ///
    /// 通过 <see cref="HandlerType"/> 指定的处理器优先从 DI 容器解析，
    /// 无法解析时通过带 <c>Type</c> 参数的构造函数创建，
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
        /// 派生特性（如 <see cref="IdentityOutAttribute"/>）直接实现该接口时，此属性返回派生类型本身。
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

    /// <summary>
    /// Identity 回写特性。插入实体后回写自增主键（Identity 列）的值。
    /// </summary>
    /// <remarks>
    /// 该特性直接实现 <see cref="IArgumentOutHandler"/>，特性实例本身即为处理器，
    /// 无需通过 <see cref="ArgumentOutAttribute.HandlerType"/> 指定额外处理器类型。
    /// <para>
    /// 服务端仅返回参数对象中 Identity 列的当前值；若参数类型没有 Identity 列，则返回 null（跳过回写）。
    /// 客户端将返回的 Identity 值写回原始参数对象的对应属性。
    /// </para>
    /// <para>
    /// <see cref="IArgumentOutHandler.ReturnType"/> 固定为 <c>typeof(long)</c>。
    /// Identity 列通过 <see cref="TableInfoProvider.Default"/> 元数据识别，
    /// 客户端与服务端均需注册 <see cref="TableInfoProvider.Default"/>。
    /// </para>
    /// <code>
    /// // 单对象回写
    /// Task Insert([IdentityOut] User user);
    /// // 集合模式逐项回写
    /// Task BatchInsert([IdentityOut(Mode = ArgumentMode.Collection)] List&lt;User&gt; users);
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public class IdentityOutAttribute : ArgumentOutAttribute, IArgumentOutHandler
    {
        /// <summary>
        /// 初始化 <see cref="IdentityOutAttribute"/> 类的新实例。
        /// 返回值类型固定为 <c>typeof(long)</c>。
        /// </summary>
        public IdentityOutAttribute()
            : base(typeof(IdentityOutAttribute), typeof(long))
        {
        }

        /// <summary>
        /// 初始化 <see cref="IdentityOutAttribute"/> 类的新实例，指定返回值类型。
        /// </summary>
        /// <param name="returnType">返回值类型。</param>
        public IdentityOutAttribute(Type returnType)
            : base(typeof(IdentityOutAttribute), returnType)
        {
        }

        /// <inheritdoc/>
        public object GenerateReturnValue(object argument)
        {
            if (argument is null) return null;
            var prop = ResolveIdentityProperty(argument.GetType());
            return Convert.ChangeType(prop?.GetValueFast(argument), ReturnType);
        }

        /// <inheritdoc/>
        public void WriteBack(object originalArg, object returnValue)
        {
            if (originalArg is null || returnValue is null) return;
            var prop = ResolveIdentityProperty(originalArg.GetType());
            if (prop is null || !prop.CanWrite) return;
            returnValue = Convert.ChangeType(returnValue, prop.PropertyType);
            prop.SetValueFast(originalArg, returnValue);
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
    /// 通用整体回写特性，适用于实现了 <see cref="ICopyable"/> 接口的参数类型。
    /// </summary>
    /// <remarks>
    /// 该特性直接实现 <see cref="IArgumentOutHandler"/>，特性实例本身即为处理器，
    /// 无需通过 <see cref="ArgumentOutAttribute.HandlerType"/> 指定额外处理器类型。
    /// <para>
    /// 服务端直接返回参数对象本身，客户端通过 <see cref="ICopyable.CopyFrom"/> 将返回值整体复制到原始对象。
    /// </para>
    /// <para>
    /// <see cref="ArgumentOutAttribute.ReturnType"/> 由构造参数指定，通常与参数类型一致。
    /// </para>
    /// <code>
    /// Task Insert([CopyableOut(typeof(CopyableUser))] CopyableUser user);
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public class CopyableOutAttribute : ArgumentOutAttribute, IArgumentOutHandler
    {
        /// <summary>
        /// 初始化 <see cref="CopyableOutAttribute"/> 类的新实例，指定返回值类型。
        /// </summary>
        /// <param name="returnType">返回值的类型，通常与参数类型一致。</param>
        public CopyableOutAttribute(Type returnType)
            : base(typeof(CopyableOutAttribute), returnType)
        {
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
