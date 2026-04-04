using LiteOrm.Common;
using LiteOrm.Demo.Models;
using static LiteOrm.Common.Expr;

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
        public UserCustomDAO(TableInfoProvider tableInfoProvider, BulkProviderFactory bulkFactory)
            : base(tableInfoProvider, bulkFactory)
        {
        }

        public async Task<List<UserView>> GetActiveUsersByDeptAsync(string deptName, CancellationToken cancellationToken = default)
        {
            // 使用 ExprString 方式构建查询
            var result = Search($"WHERE {Prop("DeptName") == deptName} AND {Prop("Age") > 18}");
            return await result.ToListAsync(cancellationToken);
        }

        public async Task<List<UserView>> SearchByAgeRangeAsync(int minAge, int maxAge, CancellationToken cancellationToken = default)
        {
            // Expr 对象嵌入 ExprString：自动展开为 SQL 片段（非参数化）
            var minExpr = Prop("Age") >= minAge;
            var maxExpr = Prop("Age") <= maxAge;
            return await Search($"WHERE {minExpr} AND {maxExpr} ORDER BY Age").ToListAsync(cancellationToken);
        }

        public async Task<List<UserView>> SearchByNamePatternAsync(string namePattern, int minAge, CancellationToken cancellationToken = default)
        {
            // 普通值嵌入 ExprString：int/string 自动转为命名参数（@p0, @p1...），防止 SQL 注入
            var nameExpr = Prop("UserName").Contains(namePattern);
            return await Search($"WHERE {nameExpr} AND {Prop("Age")} >= {minAge} ORDER BY [UserName] DESC").ToListAsync(cancellationToken);
        }
    }
}
