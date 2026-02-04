using LiteOrm.Common;
using LiteOrm.Demo.Models;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示高级表达式功能：外键查询、Lambda 转换、序列化、SQL 生成
    /// </summary>
    public static class ExprAdvancedDemo
    {
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static void ShowForeignExpr()
        {
            Console.WriteLine("\n[ForeignExpr] 外键表达式 (EXISTS 关联查询):");

            // 示例 1: 查询所属部门名称包含 "销售" 的用户
            Expr f1 = Expr.Foreign(nameof(User.DeptId), Expr.Property(nameof(Department.Name)).Contains("销售"));
            Console.WriteLine($"  用户所属部门名称包含 '销售': {f1}");

            // 示例 2: 查询销售记录，其中关联的用户年龄大于 30
            Expr f2 = Expr.Foreign(nameof(SalesRecord.SalesUserId), Expr.Property(nameof(User.Age)) > 30);
            Console.WriteLine($"  销售记录其关联用户年龄 > 30: {f2}");
        }

        public static void ShowLambdaExpr()
        {
            Console.WriteLine("\n[LambdaExpr] Lambda 表达式转换演示:");

            var lambdaExpr = Expr.Exp<SalesRecordView>(s => (s.ShipTime ?? DateTime.Now.AddDays(-1)) > s.SaleTime + TimeSpan.FromDays(3) && s.ProductName.Contains("机"));

            Console.WriteLine("  C# Lambda: s => (s.ShipTime ?? DateTime.Now) > s.SaleTime + TimeSpan.FromDays(3) && s.ProductName.Contains(\"机\")");
            Console.WriteLine($"  转换结果 Expr: {lambdaExpr}");

            string json = JsonSerializer.Serialize((Expr)lambdaExpr, jsonOptions);
            Console.WriteLine($"  序列化结果: {json}");

            var deserializedExpr = JsonSerializer.Deserialize<Expr>(json, jsonOptions);
            Console.WriteLine($"  反序列化后 Expr 类型: {deserializedExpr?.GetType().Name}");
            Console.WriteLine($"  反序列化后 Expr 内容: {deserializedExpr}");
        }

        public static void ShowExprConvert()
        {
            Console.WriteLine("\n[ExprConvert] 结构化查询转换演示：");

            var ageProps = Util.GetFilterProperties(typeof(User));
            var ageProp = ageProps.Find(nameof(User.Age), true);

            string queryValue = ">20";
            var parsedExpr = ExprConvert.Parse(ageProp, queryValue);
            Console.WriteLine($"  解析字符串 '{queryValue}' 为 Expr: {parsedExpr}");

            string inValue = "25,30,35";
            var inExpr = ExprConvert.Parse(ageProp, inValue);
            Console.WriteLine($"  解析字符串 '{inValue}' 为 Expr: {inExpr}");

            if (parsedExpr is LogicBinaryExpr be)
            {
                string backToText = ExprConvert.ToText(be.Operator, (be.Right as ValueExpr)?.Value);
                Console.WriteLine($"  将 Expr '{be}' 转换为字符串: {backToText}");
            }

            if (inExpr is LogicBinaryExpr beIn)
            {
                string inToText = ExprConvert.ToText(beIn.Operator, (beIn.Right as ValueExpr)?.Value);
                Console.WriteLine($"  将 IN 表达式 '{beIn}' 转换为字符串: {inToText}");
            }

            var queryString = new Dictionary<string, string>
            {
                { "UserName", "%Admin" },
                { "Age", ">20" }
            };
            var conditions = Util.ParseQueryCondition(queryString, typeof(User));
            Console.WriteLine($"\n  解析 QueryString (UserName=%Admin, Age=>20) 为 Expr 列表:");
            foreach (var cond in conditions)
            {
                Console.WriteLine($"    - {cond}");
            }
        }

        public static void ShowSqlGeneration()
        {
            Console.WriteLine("\n[SqlGen] 表达式生成 SQL 展示:");
            var expr = (Expr.Property(nameof(User.Age)) > 18) & (Expr.Property(nameof(User.UserName)).Contains("admin_"));
            var sqlGen = new SqlGen(typeof(User));

            // 1. 基础生成
            var result = sqlGen.ToSql(expr);
            Console.WriteLine("\n  (1) 基础 ToSql (仅生成条件片段):");
            Console.WriteLine($"      Expr: {expr}");
            Console.WriteLine($"      结果: {result}");

            // 2. 生成完整 SELECT 语句
            var selectResult = sqlGen.ToSql(new SelectExpr { Source = new WhereExpr(new TableExpr(sqlGen.Table), (LogicExpr)expr) });
            Console.WriteLine("\n  (2) ToSelectSql (生成完整查询):");
            Console.WriteLine($"      结果: {selectResult}");

            // 3. 生成 COUNT 语句
            var countResult = sqlGen.ToSql(new SelectExpr
            {
                Source = new WhereExpr(new TableExpr(sqlGen.Table), (LogicExpr)expr),
                Selects = new List<ValueTypeExpr> { new AggregateFunctionExpr("COUNT", new ValueExpr(1) { IsConst = true }) }
            });
            Console.WriteLine("\n  (3) ToCountSql (生成统计查询):");
            Console.WriteLine($"      结果: {countResult}");

            // 4. 生成 UPDATE 语句
            var updateExpr = new UpdateExpr
            {
                Source = new TableExpr(sqlGen.Table),
                Sets = new List<(string, ValueTypeExpr)>
                {
                    (nameof(User.UserName), Expr.Value("NewName")),
                    (nameof(User.Age), Expr.Value(30))
                },
                Where = Expr.Property(nameof(User.Id)) == 1
            };
            var updateResult = sqlGen.ToSql(updateExpr);
            Console.WriteLine("\n  (4) ToUpdateSql (生成更新语句):");
            Console.WriteLine($"      结果: {updateResult}");

            // 5. 生成 DELETE 语句
            var deleteExpr = new DeleteExpr
            {
                Source = new TableExpr(sqlGen.Table),
                Where = Expr.Property(nameof(User.Age)) < 18
            };
            var deleteResult = sqlGen.ToSql(deleteExpr);
            Console.WriteLine("\n  (5) ToDeleteSql (生成删除语句):");
            Console.WriteLine($"      结果: {deleteResult}");

            // 7. OrderBy 演示
            var orderByExpr = new TableExpr(sqlGen.Table)
                .OrderBy((Expr.Property(nameof(User.Age)), false)); // Age DESC
            var orderByResult = sqlGen.ToSql(new SelectExpr { Source = orderByExpr });
            Console.WriteLine("\n  (7) OrderByExpr (生成带排序查询):");
            Console.WriteLine($"      结果: {orderByResult}");

            // 8. GroupBy 演示
            var groupByQuery = new TableExpr(sqlGen.Table)
                .GroupBy(Expr.Property(nameof(User.DeptId)))
                .Select(Expr.Property(nameof(User.DeptId)), AggregateFunctionExpr.Count);
            
            var groupByResult = sqlGen.ToSql(new SelectExpr { Source = groupByQuery });
            Console.WriteLine("\n  (8) GroupByExpr (生成带分组聚合查询):");
            Console.WriteLine($"      结果: {groupByResult}");

            // 9. Section (分页) 演示
            var sectionExpr = new TableExpr(sqlGen.Table)
                .Section(10, 20); // Skip 10, Take 20
            
            var sectionResult = sqlGen.ToSql(new SelectExpr { Source = sectionExpr });
            Console.WriteLine("\n  (9) SectionExpr (生成分页查询):");
            Console.WriteLine($"      结果: {sectionResult}");

            // 10. 综合查询 (Where + OrderBy + Section)
            var complexQuery = new TableExpr(sqlGen.Table)
                .Where(Expr.Property(nameof(User.Age)) > 20)
                .OrderBy((Expr.Property(nameof(User.CreateTime)), true))
                .Section(0, 10);

            var complexResult = sqlGen.ToSql(new SelectExpr { Source = complexQuery });
            Console.WriteLine("\n  (10) 综合查询 (Where + OrderBy + Section):");
            Console.WriteLine($"       结果: {complexResult}");
        }

        public static void ShowQueryExpr()
        {
            Console.WriteLine("\n[QueryExpr] 结构化查询表达式演示:");

            // 使用 Expr 扩展方法链式构造查询
            var table = new TableExpr(TableInfoProvider.Default.GetTableView(typeof(User)));
            var query = table
                .Where(Expr.Property("Age") > 18)
                .GroupBy(Expr.Property("DeptId"))
                .Having(AggregateFunctionExpr.Count > 1)
                .Select(Expr.Property("DeptId"), AggregateFunctionExpr.Count)
                .OrderBy((Expr.Property("DeptId").Asc()))
                .Section(10, 20);

            Console.WriteLine($"  Table: {table}");
            Console.WriteLine($"  Result: {query}");

            // 更简洁的一行构建
            var fluentQuery = Expr.Table<SalesRecord>()
                .Where(Expr.Property("Status") == 1)
                .GroupBy(Expr.Property("CustomerId"))
                .Having(AggregateFunctionExpr.Count > 5)
                .Select(Expr.Property("CustomerId"), AggregateFunctionExpr.Count)
                .OrderBy((Expr.Property("CustomerId").Asc()))
                .Section(0, 10);

            Console.WriteLine($"\n  链式构建结果: {fluentQuery}");
        }
    }
}
