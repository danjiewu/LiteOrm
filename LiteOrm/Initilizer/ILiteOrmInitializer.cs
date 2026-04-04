namespace LiteOrm
{
    /// <summary>
    /// LiteOrm 启动初始化器。
    /// </summary>
    public interface ILiteOrmInitializer
    {
        /// <summary>
        /// 执行初始化逻辑。
        /// </summary>
        void Start();
    }
}
