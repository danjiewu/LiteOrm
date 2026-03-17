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

            /// <summary>
            /// ExprString 演示：将 Expr 条件对象嵌入插值字符串，按年龄范围查询
            /// </summary>
            Task<List<UserView>> SearchByAgeRangeAsync(int minAge, int maxAge, CancellationToken cancellationToken = default);

            /// <summary>
            /// ExprString 演示：普通值嵌入插值字符串（自动转为命名参数），按姓名关键字和最小年龄查询
            /// </summary>
            Task<List<UserView>> SearchByNamePatternAsync(string namePattern, int minAge, CancellationToken cancellationToken = default);
        }
}
