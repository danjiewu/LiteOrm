using System;

namespace LiteOrm.Service
{
    /// <summary>
    /// 服务异常类，用于表示服务层发生的异常
    /// </summary>
    [Serializable]
    public class ServiceException : Exception
    {
        /// <summary>
        /// 初始化 <see cref="ServiceException"/> 类的新实例
        /// </summary>
        public ServiceException() : base() { }

        /// <summary>
        /// 使用指定的错误消息初始化 <see cref="ServiceException"/> 类的新实例
        /// </summary>
        /// <param name="message">描述错误的消息</param>
        public ServiceException(string message) : base(message) { }

        /// <summary>
        /// 使用指定的错误消息和对导致此异常的内部异常的引用来初始化 <see cref="ServiceException"/> 类的新实例
        /// </summary>
        /// <param name="message">解释异常原因的错误消息</param>
        /// <param name="inner">导致当前异常的异常，如果未指定内部异常，则为 null</param>
        public ServiceException(string message, Exception inner) : base(message, inner) { }
    }
}
