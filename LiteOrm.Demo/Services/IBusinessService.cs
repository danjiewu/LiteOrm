using LiteOrm.Demo.Models;
using LiteOrm.Service;

namespace LiteOrm.Demo.Services
{
    /// <summary>
    /// 综合业务服务接口
    /// </summary>
    public interface IBusinessService
    {
        /// <summary>
        /// 注册用户并初始化一笔销售记录（演示事务控制）
        /// </summary>
        /// <param name="user">用户信息</param>
        /// <param name="firstSale">首笔销售记录</param>
        /// <returns>返回是否成功</returns>
        [Transaction]
        Task<bool> RegisterUserWithInitialSaleAsync(User user, SalesRecord firstSale);
    }
}
