using LiteOrm.CodeGen;
using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// 验证 <see cref="ForeignColumnAttribute"/> **再引用 ForeignColumn** 时的行为：视图上的外键列指向外部表的
    /// 外键列（该属性自身也是 <see cref="ForeignColumn"/>），链路终点落在第三张表的计算列上。
    /// <para>
    /// 链路：<see cref="ChainedForeignUserView.DeptCityLabel"/> → <see cref="ChainedForeignDept.CityLabel"/>（本身是外键列）
    /// → <see cref="ForeignComputedCity.Label"/>（计算列，不落库）。
    /// </para>
    /// 覆盖元数据解析、SQL 渲染（别名应取终点表别名）、SELECT 列表、SqlGen 全链路、建表 DDL 与真实 SQLite 查询。
    /// </summary>
    [Collection("Database")]
    public class ForeignColumnChainedComputedTests : TestBase
    {
        public ForeignColumnChainedComputedTests(DatabaseFixture fixture) : base(fixture) { }

        private IEntityServiceAsync<ForeignComputedCity> CityService => ServiceProvider.GetRequiredService<IEntityServiceAsync<ForeignComputedCity>>();
        private IEntityServiceAsync<ChainedForeignDept> DeptService => ServiceProvider.GetRequiredService<IEntityServiceAsync<ChainedForeignDept>>();
        private IEntityServiceAsync<ChainedForeignUser> UserService => ServiceProvider.GetRequiredService<IEntityServiceAsync<ChainedForeignUser>>();
        private ObjectViewDAO<ChainedForeignDept> DeptViewDao => ServiceProvider.GetRequiredService<ObjectViewDAO<ChainedForeignDept>>();
        private ObjectViewDAO<ChainedForeignUserView> ViewDao => ServiceProvider.GetRequiredService<ObjectViewDAO<ChainedForeignUserView>>();
        private IEntityViewServiceAsync<ChainedForeignUserView> ViewService => ServiceProvider.GetRequiredService<IEntityViewServiceAsync<ChainedForeignUserView>>();

        #region 元数据与 SQL 渲染

        /// <summary>
        /// 元数据解析：链路每个环节都能定位到下一级，最终落在终点表的计算列上，且 <see cref="ForeignColumn.Definition"/>
        /// 沿链路递归透传为同一份计算列定义。
        /// </summary>
        [Fact]
        public void Metadata_ChainedForeignColumnResolvesToUltimateComputedColumn()
        {
            var provider = new AttributeTableInfoProvider();

            // 中间表自身的外键列：指向 City 表的计算列
            var midView = provider.GetTableView(typeof(ChainedForeignDept))!;
            var midColumn = Assert.IsType<ForeignColumn>(
                midView.Columns.First(c => c.PropertyName == nameof(ChainedForeignDept.CityLabel)));
            Assert.Equal("City", midColumn.TargetColumn!.Table?.Name);
            Assert.Equal(nameof(ForeignComputedCity.Label), midColumn.Name);
            Assert.True(midColumn.Definition.IsComputed);
            Assert.True(midColumn.Definition.HasExpression);

            // 视图的外键列：目标是中间表上的外键列属性，需递归解析到终点表
            var view = provider.GetTableView(typeof(ChainedForeignUserView))!;
            var viewColumn = Assert.IsType<ForeignColumn>(
                view.Columns.First(c => c.PropertyName == nameof(ChainedForeignUserView.DeptCityLabel)));

            Assert.NotNull(viewColumn.TargetColumn);
            Assert.Equal("City", viewColumn.TargetColumn!.Table?.Name);
            Assert.IsType<ColumnDefinition>(viewColumn.TargetColumn.Column);
            Assert.Equal(nameof(ForeignComputedCity.Label), viewColumn.Name);

            // Definition 递归透传，视图与中间表指向同一份计算列定义
            Assert.True(viewColumn.Definition.IsComputed);
            Assert.True(viewColumn.Definition.HasExpression);
            Assert.Same(midColumn.Definition, viewColumn.Definition);
        }

        /// <summary>
        /// SQL 渲染：链式引用同样展开为终点表的计算列表达式，表达式内的列引用以**终点表别名**限定，
        /// 中间表别名与被引用计算列的列名都不应出现。
        /// </summary>
        [Fact]
        public void Sql_ChainedForeignColumnExpandsToUltimateComputedExpression()
        {
            var view = new AttributeTableInfoProvider().GetTableView(typeof(ChainedForeignUserView))!;
            var foreignColumn = view.Columns.First(c => c.PropertyName == nameof(ChainedForeignUserView.DeptCityLabel));

            var context = new SqlBuildContext(SQLiteBuilder.Instance, view, Constants.DefaultTableAlias, null);
            string sql = ((SqlObject)foreignColumn).ToSql(context);

            Assert.StartsWith("(", sql);
            Assert.EndsWith(")", sql);
            Assert.Contains("\"City\".\"Name\"", sql);
            Assert.Contains("\"City\".\"Code\"", sql);
            Assert.Contains("-", sql);
            // 中间表的别名与外键列名、计算列自身列名都不应出现
            Assert.DoesNotContain("\"Dept\".", sql);
            Assert.DoesNotContain("CityLabel", sql);
            Assert.DoesNotContain("\"Label\"", sql);
        }

        /// <summary>
        /// Select 列表渲染：链式引用按表达式输出，并用视图属性名作为别名回填。
        /// </summary>
        [Fact]
        public void Sql_ChainedForeignColumnInSelectListKeepsPropertyAlias()
        {
            var provider = new AttributeTableInfoProvider();

            // 中间表视图：别名回填为中间表的属性名
            var midView = provider.GetTableView(typeof(ChainedForeignDept))!;
            Assert.Contains("(\"City\".\"Name\" || '-' || \"City\".\"Code\") AS \"CityLabel\"", RenderSelectList(midView));

            // 上层视图：别名回填为视图自己的属性名
            var view = provider.GetTableView(typeof(ChainedForeignUserView))!;
            Assert.Contains("(\"City\".\"Name\" || '-' || \"City\".\"Code\") AS \"DeptCityLabel\"", RenderSelectList(view));
        }

        /// <summary>
        /// 真实 SQL 生成链路：以视图类型生成 SELECT 时同时带上两级关联表，条件中引用链式外键列同样展开为表达式。
        /// </summary>
        [Fact]
        public void SqlGen_SelectAndConditionExpandChainedForeignColumn()
        {
            var sqlGen = new SqlGen(typeof(ChainedForeignUserView));
            var select = new SelectExpr
            {
                Source = new WhereExpr
                {
                    Source = new FromExpr(typeof(ChainedForeignUserView)),
                    Where = Expr.Prop(nameof(ChainedForeignUserView.DeptCityLabel)) == Expr.Value("Suzhou-C001")
                },
                Selects = new List<SelectItemExpr>
                {
                    new SelectItemExpr(Expr.Prop(nameof(ChainedForeignUserView.DeptCityLabel)))
                }
            };

            var result = sqlGen.ToSql(select);

            // 两级关联表都要出现在 FROM 中
            Assert.Contains("LEFT JOIN", result.Sql);
            Assert.Contains("\"Dept\"", result.Sql);
            Assert.Contains("\"City\"", result.Sql);
            Assert.Contains("\"Dept\".\"CityId\" = \"City\".\"Id\"", result.Sql);
            Assert.Contains("\"City\".\"Name\"", result.Sql);
            Assert.Contains("\"City\".\"Code\"", result.Sql);
            Assert.DoesNotContain("\"Dept\".\"CityLabel\"", result.Sql);
            Assert.DoesNotContain("\"City\".\"Label\"", result.Sql);
            Assert.Single(result.Params);
        }

        /// <summary>
        /// 建表 DDL：终点表的计算列与中间表的外键列都不生成物理列。
        /// </summary>
        [Fact]
        public void Ddl_ExcludesChainedComputedAndForeignColumns()
        {
            var provider = new AttributeTableInfoProvider();

            string citySql = SQLiteBuilder.Instance.BuildCreateTableSql(
                nameof(ForeignComputedCity), provider.GetTableDefinition(typeof(ForeignComputedCity))!.Columns);
            Assert.Contains("Code", citySql);
            Assert.DoesNotContain("Label", citySql);

            string deptSql = SQLiteBuilder.Instance.BuildCreateTableSql(
                nameof(ChainedForeignDept), provider.GetTableDefinition(typeof(ChainedForeignDept))!.Columns);
            Assert.Contains("CityId", deptSql);
            Assert.DoesNotContain("CityLabel", deptSql);
        }

        #endregion

        #region 真实数据库查询

        /// <summary>
        /// 实际数据库查询：按主键读取视图，链式外键列返回终点表计算列的求值结果。
        /// </summary>
        [Fact]
        public async Task Database_GetObject_ReturnsChainedComputedValue()
        {
            var ct = TestContext.Current.CancellationToken;
            var (city, dept, user) = await InsertChainAsync("Suzhou", "C001", "ChainDept", "ChainUser", ct);

            var loaded = await ViewDao.GetObject(user.Id).FirstOrDefaultAsync(ct);

            Assert.NotNull(loaded);
            Assert.Equal(city.Id, dept.CityId);
            Assert.Equal("ChainUser", loaded!.Name);
            Assert.Equal(dept.Id, loaded.DeptId);
            Assert.Equal("Suzhou-C001", loaded.DeptCityLabel);
        }

        /// <summary>
        /// 实际数据库查询：中间表自身的视图查询同样支持链式外键列。
        /// </summary>
        [Fact]
        public async Task Database_IntermediateViewQueryReturnsChainedValue()
        {
            var ct = TestContext.Current.CancellationToken;
            var (_, dept, _) = await InsertChainAsync("Nanjing", "C002", "MidDept", "MidUser", ct);

            var loaded = await DeptViewDao.GetObject(dept.Id).FirstOrDefaultAsync(ct);

            Assert.NotNull(loaded);
            Assert.Equal("MidDept", loaded!.Name);
            Assert.Equal("Nanjing-C002", loaded.CityLabel);
        }

        /// <summary>
        /// 实际数据库查询：条件中引用链式外键列，按终点表表达式过滤。
        /// </summary>
        [Fact]
        public async Task Database_FilterByChainedForeignColumn()
        {
            var ct = TestContext.Current.CancellationToken;
            await InsertChainAsync("Suzhou", "C001", "DeptA", "UserA", ct);
            await InsertChainAsync("Wuxi", "C002", "DeptB", "UserB", ct);

            var matched = await ViewDao.Search(
                Expr.Prop(nameof(ChainedForeignUserView.DeptCityLabel)) == "Wuxi-C002").ToListAsync(ct);

            var row = Assert.Single(matched);
            Assert.Equal("UserB", row.Name);
            Assert.Equal("Wuxi-C002", row.DeptCityLabel);

            Assert.Empty(await ViewDao.Search(
                Expr.Prop(nameof(ChainedForeignUserView.DeptCityLabel)) == "Wuxi-C001").ToListAsync(ct));
        }

        /// <summary>
        /// 实际数据库查询：服务层 lambda 条件与读取路径。
        /// </summary>
        [Fact]
        public async Task Service_QueryAndRetrieveChainedComputedValue()
        {
            var ct = TestContext.Current.CancellationToken;
            var (_, _, user) = await InsertChainAsync("Changzhou", "C003", "DeptService", "UserService", ct);

            var matched = await ViewService.SearchAsync(
                v => v.DeptCityLabel == "Changzhou-C003", cancellationToken: ct);

            Assert.Single(matched);
            Assert.Equal(user.Id, matched[0].Id);

            var loaded = await ViewService.GetObjectAsync(user.Id, cancellationToken: ct);
            Assert.NotNull(loaded);
            Assert.Equal("Changzhou-C003", loaded!.DeptCityLabel);
        }

        /// <summary>
        /// 实际数据库查询：链尾左联接未命中时，链式外键列的取不到值，视图属性为 null。
        /// </summary>
        [Fact]
        public async Task Database_ChainedLeftJoinWithoutMatch_ValueIsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var city = await InsertCityAsync("Suzhou", "C001", ct);
            var matchedDept = await InsertDeptAsync("MatchedDept", city.Id, ct);
            var unmatchedDept = await InsertDeptAsync("UnmatchedDept", city.Id + 1000, ct);
            var matchedUser = await InsertUserAsync("ChainMatchedUser", matchedDept.Id, ct);
            var unmatchedUser = await InsertUserAsync("ChainUnmatchedUser", unmatchedDept.Id, ct);

            var rows = await ViewDao.Search().ToListAsync(ct);

            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, r => r.Id == matchedUser.Id && r.DeptCityLabel == "Suzhou-C001");
            Assert.Contains(rows, r => r.Id == unmatchedUser.Id && r.DeptCityLabel == null);
        }

        #endregion

        #region 计算列表达式中引用 ForeignColumn

        /// <summary>
        /// 元数据：中间表的计算列 <see cref="ChainedForeignDept.DeptCityLabel"/> 表达式引用同表的外键列
        /// <see cref="ChainedForeignDept.CityLabel"/>；上层视图引用的正是这个计算列。
        /// </summary>
        [Fact]
        public void Metadata_ComputedColumnReferencingForeignColumn()
        {
            var provider = new AttributeTableInfoProvider();

            var midView = provider.GetTableView(typeof(ChainedForeignDept))!;
            var midComputed = Assert.IsType<ColumnDefinition>(
                midView.Columns.First(c => c.PropertyName == nameof(ChainedForeignDept.DeptCityLabel)));
            Assert.True(midComputed.IsComputed);
            Assert.True(midComputed.HasExpression);
            Assert.Contains("{CityLabel}", midComputed.Expression!);

            var view = provider.GetTableView(typeof(ChainedForeignUserView))!;
            var foreignColumn = Assert.IsType<ForeignColumn>(
                view.Columns.First(c => c.PropertyName == nameof(ChainedForeignUserView.DeptFullLabel)));
            Assert.Equal("Dept", foreignColumn.TargetColumn!.Table?.Name);
            Assert.Same(midComputed, foreignColumn.TargetColumn.Column);
            Assert.True(foreignColumn.Definition.IsComputed);
            Assert.True(foreignColumn.Definition.HasExpression);
        }

        /// <summary>
        /// SQL 渲染：表达式里的外键列引用展开为链尾计算列表达式（以链尾表别名限定），
        /// 同表的物理列引用以中间表别名限定。
        /// </summary>
        [Fact]
        public void Sql_ComputedColumnReferencingForeignColumnExpandsNestedChain()
        {
            var view = new AttributeTableInfoProvider().GetTableView(typeof(ChainedForeignUserView))!;
            var foreignColumn = view.Columns.First(c => c.PropertyName == nameof(ChainedForeignUserView.DeptFullLabel));

            var context = new SqlBuildContext(SQLiteBuilder.Instance, view, Constants.DefaultTableAlias, null);
            string sql = ((SqlObject)foreignColumn).ToSql(context);

            Assert.Equal("((\"City\".\"Name\" || '-' || \"City\".\"Code\") || '-' || \"Dept\".\"Name\")", sql);
        }

        /// <summary>
        /// SELECT 列表渲染：中间表视图与上层视图各自以属性名回填别名。
        /// </summary>
        [Fact]
        public void Sql_ComputedColumnReferencingForeignColumnInSelectListKeepsPropertyAlias()
        {
            var provider = new AttributeTableInfoProvider();

            // 中间表视图：本表列以主表别名限定，计算列名即属性名，无需 AS
            string midSelect = RenderSelectList(provider.GetTableView(typeof(ChainedForeignDept))!);
            Assert.Contains("(\"City\".\"Name\" || '-' || \"City\".\"Code\") AS \"CityLabel\"", midSelect);
            Assert.Contains($"((\"City\".\"Name\" || '-' || \"City\".\"Code\") || '-' || \"{Constants.DefaultTableAlias}\".\"Name\")", midSelect);

            // 上层视图：本表列以中间表别名限定，别名回填为视图属性名
            string viewSelect = RenderSelectList(provider.GetTableView(typeof(ChainedForeignUserView))!);
            Assert.Contains("((\"City\".\"Name\" || '-' || \"City\".\"Code\") || '-' || \"Dept\".\"Name\") AS \"DeptFullLabel\"", viewSelect);
        }

        /// <summary>
        /// 真实 SQL 生成链路：条件中引用该属性同样展开为嵌套表达式。
        /// </summary>
        [Fact]
        public void SqlGen_ConditionOnComputedColumnReferencingForeignColumn()
        {
            var sqlGen = new SqlGen(typeof(ChainedForeignUserView));
            var select = new SelectExpr
            {
                Source = new WhereExpr
                {
                    Source = new FromExpr(typeof(ChainedForeignUserView)),
                    Where = Expr.Prop(nameof(ChainedForeignUserView.DeptFullLabel)) == Expr.Value("Suzhou-C001-ChainDept")
                },
                Selects = new List<SelectItemExpr>
                {
                    new SelectItemExpr(Expr.Prop(nameof(ChainedForeignUserView.DeptFullLabel)))
                }
            };

            var result = sqlGen.ToSql(select);

            Assert.Contains("LEFT JOIN", result.Sql);
            Assert.Contains("\"City\".\"Name\"", result.Sql);
            Assert.Contains("\"Dept\".\"Name\"", result.Sql);
            Assert.DoesNotContain("\"CityLabel\"", result.Sql);
            Assert.Single(result.Params);
        }

        /// <summary>
        /// 建表 DDL：引用外键列的计算列同样不生成物理列。
        /// </summary>
        [Fact]
        public void Ddl_ExcludesComputedColumnReferencingForeignColumn()
        {
            var table = new AttributeTableInfoProvider().GetTableDefinition(typeof(ChainedForeignDept))!;
            string sql = SQLiteBuilder.Instance.BuildCreateTableSql(nameof(ChainedForeignDept), table.Columns);

            Assert.Contains("CityId", sql);
            Assert.DoesNotContain("DeptCityLabel", sql);
        }

        /// <summary>
        /// 实际数据库查询：视图读取嵌套计算列，取值为「链尾计算值 + 中间表列值」。
        /// </summary>
        [Fact]
        public async Task Database_GetObject_ReturnsNestedComputedValue()
        {
            var ct = TestContext.Current.CancellationToken;
            var (_, _, user) = await InsertChainAsync("Suzhou", "C001", "ChainDept", "ChainUser", ct);

            var loaded = await ViewDao.GetObject(user.Id).FirstOrDefaultAsync(ct);

            Assert.NotNull(loaded);
            Assert.Equal("Suzhou-C001-ChainDept", loaded!.DeptFullLabel);
        }

        /// <summary>
        /// 实际数据库查询：中间表自身的视图查询同样能得到该计算列的值。
        /// </summary>
        [Fact]
        public async Task Database_IntermediateViewReturnsComputedValueReferencingForeignColumn()
        {
            var ct = TestContext.Current.CancellationToken;
            var (_, dept, _) = await InsertChainAsync("Nanjing", "C002", "MidDept", "MidUser", ct);

            var loaded = await DeptViewDao.GetObject(dept.Id).FirstOrDefaultAsync(ct);

            Assert.NotNull(loaded);
            Assert.Equal("Nanjing-C002-MidDept", loaded!.DeptCityLabel);
        }

        /// <summary>
        /// 实际数据库查询：条件中引用嵌套计算列，按表达式过滤。
        /// </summary>
        [Fact]
        public async Task Database_FilterByNestedComputedForeignColumn()
        {
            var ct = TestContext.Current.CancellationToken;
            await InsertChainAsync("Suzhou", "C001", "DeptA", "UserA", ct);
            await InsertChainAsync("Wuxi", "C002", "DeptB", "UserB", ct);

            var matched = await ViewDao.Search(
                Expr.Prop(nameof(ChainedForeignUserView.DeptFullLabel)) == "Wuxi-C002-DeptB").ToListAsync(ct);

            var row = Assert.Single(matched);
            Assert.Equal("UserB", row.Name);
            Assert.Equal("Wuxi-C002-DeptB", row.DeptFullLabel);

            Assert.Empty(await ViewDao.Search(
                Expr.Prop(nameof(ChainedForeignUserView.DeptFullLabel)) == "Wuxi-C002-DeptA").ToListAsync(ct));
        }

        /// <summary>
        /// 实际数据库查询：服务层 lambda 条件与读取路径。
        /// </summary>
        [Fact]
        public async Task Service_QueryNestedComputedForeignColumn()
        {
            var ct = TestContext.Current.CancellationToken;
            var (_, _, user) = await InsertChainAsync("Changzhou", "C003", "DeptService", "UserService", ct);

            var matched = await ViewService.SearchAsync(
                v => v.DeptFullLabel == "Changzhou-C003-DeptService", cancellationToken: ct);

            Assert.Single(matched);
            Assert.Equal(user.Id, matched[0].Id);

            var loaded = await ViewService.GetObjectAsync(user.Id, cancellationToken: ct);
            Assert.NotNull(loaded);
            Assert.Equal("Changzhou-C003-DeptService", loaded!.DeptFullLabel);
        }

        /// <summary>
        /// 实际数据库查询：链尾左联接未命中时，嵌套计算列取不到值，视图属性为 null。
        /// </summary>
        [Fact]
        public async Task Database_ChainedComputedLeftJoinWithoutMatch_ValueIsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var city = await InsertCityAsync("Suzhou", "C001", ct);
            var matchedDept = await InsertDeptAsync("MatchedDept", city.Id, ct);
            var unmatchedDept = await InsertDeptAsync("UnmatchedDept", city.Id + 1000, ct);
            var matchedUser = await InsertUserAsync("NestedMatchedUser", matchedDept.Id, ct);
            var unmatchedUser = await InsertUserAsync("NestedUnmatchedUser", unmatchedDept.Id, ct);

            var rows = await ViewDao.Search().ToListAsync(ct);

            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, r => r.Id == matchedUser.Id && r.DeptFullLabel == "Suzhou-C001-MatchedDept");
            Assert.Contains(rows, r => r.Id == unmatchedUser.Id && r.DeptFullLabel == null);
        }

        #endregion

        private static string RenderSelectList(TableView view)
        {
            var context = new SqlBuildContext(SQLiteBuilder.Instance, view, Constants.DefaultTableAlias, null);
            var sb = new ValueStringBuilder();
            foreach (var column in view.SelectColumns)
            {
                if (sb.Length > 0) sb.Append(",");
                ((SqlObject)column).ToSql(ref sb, context);
                if (!string.Equals(column.Name, column.PropertyName, System.StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append(" AS ");
                    sb.Append(SQLiteBuilder.Instance.ToSqlName(column.PropertyName));
                }
            }
            string sql = sb.ToString();
            sb.Dispose();
            return sql;
        }

        private async Task<(ForeignComputedCity City, ChainedForeignDept Dept, ChainedForeignUser User)> InsertChainAsync(
            string cityName, string cityCode, string deptName, string userName, System.Threading.CancellationToken ct)
        {
            var city = await InsertCityAsync(cityName, cityCode, ct);
            var dept = await InsertDeptAsync(deptName, city.Id, ct);
            var user = await InsertUserAsync(userName, dept.Id, ct);
            return (city, dept, user);
        }

        private async Task<ForeignComputedCity> InsertCityAsync(string name, string code, System.Threading.CancellationToken ct)
        {
            var city = new ForeignComputedCity { Name = name, Code = code };
            await CityService.InsertAsync(city, ct);
            Assert.True(city.Id > 0);
            return city;
        }

        private async Task<ChainedForeignDept> InsertDeptAsync(string name, int cityId, System.Threading.CancellationToken ct)
        {
            var dept = new ChainedForeignDept { Name = name, CityId = cityId };
            await DeptService.InsertAsync(dept, ct);
            Assert.True(dept.Id > 0);
            return dept;
        }

        private async Task<ChainedForeignUser> InsertUserAsync(string name, int deptId, System.Threading.CancellationToken ct)
        {
            var user = new ChainedForeignUser { Name = name, DeptId = deptId };
            await UserService.InsertAsync(user, ct);
            Assert.True(user.Id > 0);
            return user;
        }
    }

    /// <summary>
    /// 链路终点的外部表：<see cref="Label"/> 是不落库的计算列。
    /// </summary>
    [Table("FcCities")]
    public class ForeignComputedCity
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Name", AllowNull = true)]
        public string? Name { get; set; }

        [Column("Code", AllowNull = true)]
        public string? Code { get; set; }

        [Column("Label", Expression = "{Name} || '-' || {Code}", ColumnMode = ColumnMode.Computed)]
        public string? Label { get; set; }
    }

    /// <summary>
    /// 链路的中间表：<see cref="CityLabel"/> 自身就是外键列，指向 <see cref="ForeignComputedCity"/> 的计算列。
    /// </summary>
    [Table("FcChainDepts")]
    public class ChainedForeignDept
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Name", AllowNull = true)]
        public string? Name { get; set; }

        [Column("CityId")]
        [ForeignType(typeof(ForeignComputedCity), Alias = "City")]
        public int CityId { get; set; }

        [ForeignColumn("City", Property = nameof(ForeignComputedCity.Label))]
        public string? CityLabel { get; set; }

        [Column(Expression = "{CityLabel} || '-' || {Name}", ColumnMode = ColumnMode.Computed)]
        public string? DeptCityLabel => CityLabel + "-" + Name;
    }

    /// <summary>
    /// 主表：与 <see cref="ChainedForeignDept"/> 关联。
    /// </summary>
    [Table("FcChainUsers")]
    public class ChainedForeignUser
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Name", AllowNull = true)]
        public string? Name { get; set; }

        [Column("DeptId")]
        [ForeignType(typeof(ChainedForeignDept), Alias = "Dept")]
        public int DeptId { get; set; }
    }

    /// <summary>
    /// 视图：<see cref="DeptCityLabel"/> 引用中间表的 <see cref="ChainedForeignDept.CityLabel"/>（其本身是外键列），
    /// 因此需要同时关联 <see cref="ChainedForeignDept"/> 与 <see cref="ForeignComputedCity"/> 两张表。
    /// </summary>
    [TableJoin(typeof(ChainedForeignDept), "DeptId", Alias = "Dept", JoinType = TableJoinType.Left)]
    [TableJoin("Dept", typeof(ForeignComputedCity), "CityId", Alias = "City", JoinType = TableJoinType.Left)]
    public class ChainedForeignUserView : ChainedForeignUser
    {
        [ForeignColumn("Dept", Property = nameof(ChainedForeignDept.CityLabel))]
        public string? DeptCityLabel { get; set; }

        /// <summary>
        /// 引用中间表的**计算列**，而该计算列的表达式又引用了中间表自身的外键列（<see cref="ChainedForeignDept.CityLabel"/>）。
        /// </summary>
        [ForeignColumn("Dept", Property = nameof(ChainedForeignDept.DeptCityLabel))]
        public string? DeptFullLabel { get; set; }
    }
}
