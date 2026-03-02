using LiteOrm.Common;
using LiteOrm.Demo.Models;

namespace LiteOrm.Demo
{
    /// <summary>
    /// 自定义用户数据访问实现
    /// </summary>
    /// <remarks>
    /// 继承自 ObjectViewDAO<UserView> 以获得基本的查询能力。
    /// 通过标记 [AutoRegister] 自动注册到 DI 容器。
    /// </remarks>
    public class UserCustomDAO : ObjectViewDAO<UserView>, DAO.IUserCustomDAO
    {
        public async Task<List<UserView>> GetActiveUsersByDeptAsync(string deptName, CancellationToken cancellationToken = default)
        {
            // 使用 ExprString 方式构建查询
            var result = Search($"WHERE {Expr.Prop("DeptName") == deptName} AND {Expr.Prop("Age") > 18}");
            return await result.ToListAsync(cancellationToken);
        }
    }
}
