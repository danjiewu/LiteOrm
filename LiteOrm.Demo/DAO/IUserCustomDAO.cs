using LiteOrm.Common;
using LiteOrm.Demo.Models;

namespace LiteOrm.Demo.DAO
{
    /// <summary>
    /// 自定义用户数据访问接口 - 展示如何扩展标准 DAO
    /// </summary>
    public interface IUserCustomDAO : IObjectViewDAO<UserView>
    {
        /// <summary>
        /// 自定义查询：按部门名称获取活跃用户
        /// </summary>
        Task<List<UserView>> GetActiveUsersByDeptAsync(string deptName, CancellationToken cancellationToken = default);
    }
}
