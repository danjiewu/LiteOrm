using LiteOrm.Demo.Models;
using LiteOrm.Service;

namespace LiteOrm.Demo.Services
{
    /// <summary>
    /// 用户业务逻辑接口
    /// </summary>
    public interface IUserService : 
        IEntityService<User>, IEntityServiceAsync<User>, 
        IEntityViewService<UserView>, IEntityViewServiceAsync<UserView>
    {
        /// <summary>
        /// 根据用户名异步获取用户信息
        /// </summary>
        /// <param name="userName">用户名</param>
        /// <returns>返回用户信息视图</returns>
        Task<UserView?> GetByUserNameAsync(string userName);
    }
}
