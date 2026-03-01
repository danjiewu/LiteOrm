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
            // 使用基类提供的 MakeConditionCommand 构建复杂查询
            // 这里演示使用 Join 的手动 SQL 或者是组合 Expr
            var expr = Expr.Prop("DeptName") == deptName & Expr.Prop("Age") > 18;

            // 也可以直接写原生 SQL 演示
            string sql = $"SELECT {AllFieldsSql} FROM {ParamFromTable} WHERE [Dept].[Name] = @DeptName AND Age > 18";
            using var command = MakeNamedParamCommand(sql, new[] { new KeyValuePair<string, object>("DeptName", deptName) });
            return await GetAllAsync(command, cancellationToken);
        }
    }
}
