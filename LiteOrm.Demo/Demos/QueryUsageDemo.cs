using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// ��ʾ���ֲ�ѯ�÷�������������ѯ���ֱ���ѯ�Լ����ӱ���ʽ��ѯ
    /// </summary>
    public static class QueryUsageDemo
    {
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static async Task ShowJoinQueryAsync(IDepartmentService deptService)
        {
            Console.WriteLine("\n--- ������ѯչʾ (�Զ���ѯ������ͼ�ֶ�) ---");
            var depts = await deptService.SearchAsync(null);
            foreach (var d in depts)
            {
                Console.WriteLine($" ID: {d.Id}, ����: {d.Name}, ����Ա: {d.ManagerName ?? "δָ��"}, �ϼ�: {d.ParentName ?? "��"}");
            }
        }

        public static async Task ShowArgedQueryAsync(ISalesService salesService)
        {
            Console.WriteLine("\n--- �ֱ���ѯչʾ ---");
            string currentMonth = DateTime.Now.ToString("yyyyMM");
            var sales = await salesService.SearchAsync(null, [currentMonth]);
            Console.WriteLine($"{currentMonth} �·������ܼ�¼��: {sales.Count}");
            foreach (var sale in sales)
            {
                Console.WriteLine($"    - ID:{sale.Id}, ��Ʒ:{sale.ProductName}, ���:{sale.Amount}, ҵ��Ա:{sale.UserName}, ����ʱ��:{sale.SaleTime:yyyy-MM-dd HH:mm} ����ʱ��:{sale.ShipTime:yyyy-MM-dd HH:mm}");
            }
        }

        public static async Task ShowQueryResultsAsync(IUserService userService, ISalesService salesService)
        {
            Console.WriteLine("\n[QueryResults] ʹ�� Expr ����ʵ�ʲ�ѯչʾ:");
            string currentMonth = DateTime.Now.ToString("yyyyMM");

            // ʾ�� 1
            var expr1 = Expr.Exp<UserView>(u => u.Age > 25 && u.CreateTime.AddDays(10) > DateTime.Now && u.UserName.Substring(2, 2) == "����");
            var users1 = await userService.SearchAsync(expr1);
            Console.WriteLine($"\n[ʾ�� 1] ���� > 25 ���û������� '����' ���û�:");
            Console.WriteLine($"  Expr ���л����: {JsonSerializer.Serialize(expr1, jsonOptions)}");
            foreach (var user in users1)
            {
                Console.WriteLine($"    - ID:{user.Id}, �˺�:{user.UserName}, ����:{user.Age}, ����:{user.DeptName}");
            }

            // ʾ�� 2
            var threeDaysAgo = DateTime.Now.AddDays(-3);
            var expr2 = Expr.Exp<SalesRecordView>(s => s.SaleTime < threeDaysAgo && s.ShipTime == null);
            var sales2 = await salesService.SearchAsync(expr2, [currentMonth], new PageSection(0, 10).OrderByDesc(nameof(SalesRecord.Amount)));
            Console.WriteLine($"\n[ʾ�� 2] 3��ǰ�Ķ�������δ������������ȡǰ10��:");
            Console.WriteLine($"  Expr ���л����: {JsonSerializer.Serialize(expr2, jsonOptions)}");
            foreach (var sale in sales2)
            {
                Console.WriteLine($"    - ID:{sale.Id}, ��Ʒ:{sale.ProductName}, ���:{sale.Amount}, ҵ��Ա:{sale.UserName}");
            }

            // ʾ�� 3: GenericSqlExpr
            GenericSqlExpr.Register("DirectorDeptOrders", (ctx, builder, pms, arg) =>
            {
                string paramName = pms.Count.ToString();
                pms.Add(new KeyValuePair<string, object>(paramName, arg));
                return $@"SalesUserId IN (
                    SELECT u.Id FROM Users u 
                    WHERE u.DeptId IN (
                        WITH RECURSIVE SubDepts(Id) AS (
                            SELECT Id FROM Departments WHERE ManagerId = {builder.ToSqlParam(paramName)}
                            UNION ALL
                            SELECT d.Id FROM Departments d JOIN SubDepts s ON d.ParentId = s.Id
                        ) SELECT Id FROM SubDepts
                    )
                )";
            });

            var directorId = 6;
            var complexExpr = GenericSqlExpr.Get("DirectorDeptOrders", directorId) & Expr.Property(nameof(SalesRecord.SaleTime)) > threeDaysAgo;
            var directorOrders = await salesService.SearchAsync(complexExpr, [currentMonth]);
            Console.WriteLine($"\n[ʾ�� 3] ���۲����� (ID:{directorId}) ������� 3 ���ڵĶ��� ({currentMonth}):");
            Console.WriteLine($"  Expr ���л����: {JsonSerializer.Serialize(complexExpr, jsonOptions)}");
            Console.WriteLine($"  ��ѯ����¼��: {directorOrders.Count}");

            // ʾ�� 4: ForeignExpr
            var expr4 = Expr.Foreign("Dept", Expr.Property(nameof(Department.Name)) == "���۲�");
            var users4 = await userService.SearchAsync(expr4);
            Console.WriteLine($"\n[ʾ�� 4] ���� '���۲�' ���û� (ʹ�� ForeignExpr):");
            Console.WriteLine($"  ��ѯ����¼��: {users4.Count}");
        }
    }
}
