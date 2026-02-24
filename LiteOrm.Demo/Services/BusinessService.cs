using Autofac.Extras.DynamicProxy;
using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.Demo.Services
{
    /// <summary>
    /// 综合业务服务实现，演示跨服务事务
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Scoped), Intercept(typeof(ServiceInvokeInterceptor))]
    public class BusinessService : IBusinessService
    {
        private readonly IUserService _userService;
        private readonly ISalesService _salesService;

        /// <summary>
        /// 初始化业务服务
        /// </summary>
        public BusinessService(IUserService userService, ISalesService salesService)
        {
            _userService = userService;
            _salesService = salesService;
        }

        /// <summary>
        /// 注册用户并记录首笔销售（演示事务回滚）
        /// </summary>
        /// <param name="user">用户信息</param>
        /// <param name="firstSale">首笔销售记录</param>
        /// <returns>返回是否成功</returns>
        public async Task<bool> RegisterUserWithInitialSaleAsync(User user, SalesRecord firstSale)
        {
            // 1. 插入用户记录，会自动使用同一个事务上下文环境
            user.CreateTime = DateTime.Now;
            await _userService.InsertAsync(user);

            // 2. 补全销售记录的用户 ID (Insert 后生成的 ID 会自动回填到实体的 Id 属性)
            firstSale.SalesUserId = user.Id;
            firstSale.SaleTime = DateTime.Now;

            // 3. 插入销售记录，同样会自动使用同一个事务上下文环境
            await _salesService.InsertAsync(firstSale);

            throw new Exception("模拟异常，触发事务回滚");

            // 如果中间有任何一个环节失败，向上抛出异常，均会自动被 ServiceInvokeInterceptor 回滚
            return true;
        }
    }
}
