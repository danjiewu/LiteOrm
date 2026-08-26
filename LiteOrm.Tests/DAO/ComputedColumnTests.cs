using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    [Collection("Database")]
    public class ComputedColumnTests : TestBase
    {
        public ComputedColumnTests(DatabaseFixture fixture) : base(fixture)
        {
            // FullName 计算列采用动态注册：声明时仅标记 ColumnMode.Computed。
            // 运行时通过全局单例 TableInfoProvider.Instance 设置 ExpressionExpr。
            // 使用 .Concat() 链（ValueSet，由 BuildConcatSql 按方言生成 ||），
            // 空格用 Char(32) 表达（LiteOrmSqlFunctionInitializer 注册各数据库方言），避免字符串常量参数化。
            var table = TableInfoProvider.Instance.GetTableDefinition(typeof(ComputedUserModel))!;
            table.Columns.First(c => c.Name == "FullName").ExpressionExpr = Expr.Prop("FirstName")
                .Concat(Expr.Func("Char", Expr.Const(32)))
                .Concat(Expr.Prop("LastName"));
        }

        [Fact]
        public void BuildCreateTableSql_ExcludesComputedColumn()
        {
            var table = new AttributeTableInfoProvider().GetTableDefinition(typeof(ComputedUserModel))!;
            var sql = SQLiteBuilder.Instance.BuildCreateTableSql(table.Name!, table.Columns);

            Assert.Contains("FirstName", sql);
            Assert.DoesNotContain("FullName", sql);
        }

        [Fact]
        public async Task ComputedColumn_InsertAndRead_ReturnsExpressionResult()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<ComputedUserModel>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<ComputedUserModel>>();

            var model = new ComputedUserModel { FirstName = "John", LastName = "Smith" };
            await service.InsertAsync(model, TestContext.Current.CancellationToken);

            var retrieved = await viewService.GetObjectAsync(model.Id, cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(retrieved);
            Assert.Equal("John Smith", retrieved.FullName);
        }

        [Fact]
        public async Task ComputedColumn_QueryCondition_UsesExpression()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<ComputedUserModel>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<ComputedUserModel>>();

            await service.InsertAsync(new ComputedUserModel { FirstName = "Alice", LastName = "Brown" }, TestContext.Current.CancellationToken);
            await service.InsertAsync(new ComputedUserModel { FirstName = "Bob", LastName = "White" }, TestContext.Current.CancellationToken);

            var result = await viewService.SearchAsync(u => u.FullName == "Alice Brown", cancellationToken: TestContext.Current.CancellationToken);

            Assert.Single(result);
            Assert.Equal("Alice", result[0].FirstName);
        }

        [Fact]
        public async Task ComputedColumn_Update_DoesNotWriteExpressionColumn()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<ComputedUserModel>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<ComputedUserModel>>();

            var model = new ComputedUserModel { FirstName = "A", LastName = "B" };
            await service.InsertAsync(model, TestContext.Current.CancellationToken);

            model.LastName = "C";
            await service.UpdateAsync(model, TestContext.Current.CancellationToken);

            var retrieved = await viewService.GetObjectAsync(model.Id, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("A C", retrieved.FullName);
        }

        [Table("ComputedUserModels")]
        public class ComputedUserModel
        {
            [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
            public int Id { get; set; }

            [Column("FirstName", AllowNull = true)]
            public string? FirstName { get; set; }

            [Column("LastName", AllowNull = true)]
            public string? LastName { get; set; }

            [Column("FullName", ColumnMode = ColumnMode.Computed)]
            public string? FullName { get; set; }
        }

        /// <summary>
        /// 用于测试字符串形式 Expression 的计算列。Total 列通过 ColumnMode.Computed 标记为计算列。
        /// 并在特性中直接设置 Expression = "{Price} * {Quantity}"，{属性名} 占位符在渲染时替换为限定列名。
        /// </summary>
        [Table("ComputedExprModels")]
        public class ComputedExprModel
        {
            [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
            public int Id { get; set; }

            [Column("Price")]
            public decimal Price { get; set; }

            [Column("Quantity")]
            public int Quantity { get; set; }

            [Column("Total", Expression = "{Price} * {Quantity}", ColumnMode = ColumnMode.Computed)]
            public decimal Total { get; set; }
        }

        /// <summary>
        /// 字符串形式 Expression 的计算列，渲染时 {Price}、{Quantity} 占位符替换为限定列名，整体以括号包裹。
        /// </summary>
        [Fact]
        public void ComputedColumn_Expression_RenderProducesSql()
        {
            var table = new AttributeTableInfoProvider().GetTableDefinition(typeof(ComputedExprModel))!;
            var totalCol = table.Columns.First(c => c.Name == "Total");

            var context = new SqlBuildContext(table, "T0", null) { SingleTable = false };
            var sb = new ValueStringBuilder();
            totalCol.RenderComputedExpression(ref sb, context, SQLiteBuilder.Instance);
            string sql = sb.ToString();
            sb.Dispose();

            // {Price} * {Quantity} 应渲染为 (...Price... * ...Quantity...) 形式
            Assert.Contains("Price", sql);
            Assert.Contains("Quantity", sql);
            Assert.Contains("*", sql);
            Assert.StartsWith("(", sql);
            Assert.EndsWith(")", sql);
        }

        /// <summary>
        /// 字符串形式 Expression 的计算列，在 SELECT 字段列表中以表达式渲染而非物理列名。
        /// </summary>
        [Fact]
        public void ComputedColumn_Expression_SelectFieldsSql()
        {
            var table = new AttributeTableInfoProvider().GetTableDefinition(typeof(ComputedExprModel))!;

            // 模拟 DAOBase.GetSelectFieldsSql 的行为
            var context = new SqlBuildContext(table, "T0", null);
            var sb = new ValueStringBuilder();
            foreach (var col in table.Columns)
            {
                if (sb.Length > 0) sb.Append(",");
                ((SqlObject)col).ToSql(ref sb, context, SQLiteBuilder.Instance);
            }
            string sql = sb.ToString();
            sb.Dispose();

            // Total 列应渲染为表达式而非物理列名
            Assert.Contains("*", sql);
            Assert.DoesNotContain("\"Total\"", sql);
        }

        /// <summary>
        /// 字符串形式 Expression 的计算列，通过 ExprSqlConverter 渲染 PropertyExpr 时同样展开为表达式。
        /// </summary>
        [Fact]
        public void ComputedColumn_Expression_ExprSqlConverterUsesExpr()
        {
            var table = new AttributeTableInfoProvider().GetTableDefinition(typeof(ComputedExprModel))!;

            // 模拟 ExprSqlConverter.ToSql(PropertyExpr) 的行�?
            var context = new SqlBuildContext(table, "T0", null);
            context.AddTableAlias("T0", table);
            var outputParams = new List<Param>();
            string sql = Expr.Prop("Total").ToSql(context, SQLiteBuilder.Instance, outputParams);

            // Total 列应渲染�?(...Price... * ...Quantity...) 而非 T0."Total"
            Assert.Contains("Price", sql);
            Assert.Contains("Quantity", sql);
            Assert.Contains("*", sql);
            Assert.DoesNotContain("\"Total\"", sql);
        }

        /// <summary>
        /// ExpressionExpr 优先级高于字符串 Expression：即将 Total 声明为 Expression = "{Price} * {Quantity}"。
        /// 动态设置 ExpressionExpr 后按 Expr 树渲染。此 ExpressionExpr 不允许使用会生成参数的 Expr（如非常量ValueExpr）。
        /// </summary>
        [Fact]
        public void ComputedColumn_ExpressionExpr_NonConstValueExprThrows()
        {
            var table = new AttributeTableInfoProvider().GetTableDefinition(typeof(ComputedExprModel))!;
            var totalCol = table.Columns.First(c => c.Name == "Total");
            // 非常量字符串值会产生参数化，不允许（同时覆盖了字符串 Expression）
            totalCol.ExpressionExpr = Expr.Prop("Price") + Expr.Value("surcharge");

            var context = new SqlBuildContext(table, "T0", null);
            Assert.Throws<NotSupportedException>(() =>
            {
                var sb = new ValueStringBuilder();
                try { totalCol.RenderComputedExpression(ref sb, context, SQLiteBuilder.Instance); }
                finally { sb.Dispose(); }
            });
        }

        /// <summary>
        /// ExpressionExpr 优先级高于字符串 Expression：动态设置ExpressionExpr 后按 Expr 树渲染。
        /// ExpressionExpr 允许常量值（Expr.Const），不生成参数。
        /// </summary>
        [Fact]
        public void ComputedColumn_ExpressionExpr_ConstValueExprOk()
        {
            var table = new AttributeTableInfoProvider().GetTableDefinition(typeof(ComputedExprModel))!;
            var totalCol = table.Columns.First(c => c.Name == "Total");
            // 常量值不产生参数化，允许（同时覆盖了字符形式 Expression）
            totalCol.ExpressionExpr = Expr.Prop("Price") + Expr.Const(100);

            var context = new SqlBuildContext(table, "T0", null);
            var sb = new ValueStringBuilder();
            totalCol.RenderComputedExpression(ref sb, context, SQLiteBuilder.Instance);
            string sql = sb.ToString();
            sb.Dispose();

            Assert.Contains("Price", sql);
            Assert.Contains("100", sql);
            Assert.Contains("+", sql);
        }
    }
}
