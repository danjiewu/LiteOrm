using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.Demo.Services
{
    /// <summary>
    /// 用户服务实现
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Scoped)]
    public class UserService : EntityService<User, UserView>, IUserService
    {
        /// <summary>
        /// 根据用户名异步获取用户信息
        /// </summary>
        /// <param name="userName">用户名</param>
        /// <returns>返回用户信息视图，如果不存在则返回 null</returns>
        public async Task<UserView?> GetByUserNameAsync(string userName)
        {
            return await SearchOneAsync(Expr.Property(nameof(User.UserName)) == userName);
        }
    }
}
