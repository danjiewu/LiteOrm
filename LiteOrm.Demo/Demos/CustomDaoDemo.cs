using LiteOrm.Demo.DAO;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示如何使用自定义 DAO 扩展原始 CRUD 功能
    /// </summary>
    public static class CustomDaoDemo
    {
        public static async Task ShowCustomDaoDemoAsync(IUserCustomDAO userCustomDao)
        {
            Console.WriteLine("\n--- 自定义 DAO (UserCustomDAO) 展示 ---");
            string deptName = "销售部";
            var users = await userCustomDao.GetActiveUsersByDeptAsync(deptName);
            Console.WriteLine($" {deptName} 部门中年龄 > 18 的活跃用户数量: {users.Count}");
            foreach (var user in users)
            {
                Console.WriteLine($"    - 用户名:{user.UserName}, 年龄:{user.Age}, 部门:{user.DeptName}");
            }
        }
    }
}
