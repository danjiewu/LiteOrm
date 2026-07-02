namespace LiteOrm.Common
{
    /// <summary>
    /// 可复制接口，定义对象复制功能
    /// </summary>
    public interface ICopyable
    {
        /// <summary>
        /// 从源对象复制数据
        /// </summary>
        /// <param name="source">源对象</param>
        void CopyFrom(object source);
    }

    /// <summary>
    /// 可记录日志接口，定义对象日志记录功能
    /// </summary>
    public interface ILogable
    {
        /// <summary>
        /// 生成对象的日志字符串
        /// </summary>
        /// <returns>日志字符串</returns>
        string ToLog();
    }

    /// <summary>
    /// 带参数接口，定义表参数功能
    /// </summary>
    public interface IArged
    {
        /// <summary>
        /// 获取表参数数组
        /// </summary>
        string[] TableArgs { get; }
    }

    /// <summary>
    /// 根据属性名访问属性值
    /// </summary>
    public interface IIndexedProperty
    {
        /// <summary>
        /// 根据属性名设置和获取属性值
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <returns>属性的值</returns>
        object this[string propertyName] { get; set; }
    }
}
