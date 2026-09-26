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
    /// 验证 <see cref="ForeignColumnAttribute"/> 引用**外部表的计算列**（<see cref="ColumnAttribute.Expression"/>）时的行为：
    /// 元数据解析出的目标列仍是计算列、SQL 渲染展开为表达式并以外部表别名限定、计算列不生成物理列，
    /// 以及真实数据库（SQLite）上的关联查询与条件过滤。
    /// <para>
    /// 视图 <see cref="ForeignComputedUserView"/> 通过 <c>Dept</c> 关联 <see cref="ForeignComputedDept"/>，
    /// 其 <see cref="ForeignComputedDept.DisplayName"/> 是不落库的计算列，视图属性 <see cref="ForeignComputedUserView.DeptDisplayName"/>
    /// 引用的正是这一列。
    /// </para>
    /// </summary>
    [Collection("Database")]
    public class ForeignColumnComputedTests : TestBase
    {
        public ForeignColumnComputedTests(DatabaseFixture fixture) : base(fixture) { }

        private IEntityServiceAsync<ForeignComputedDept> DeptService => ServiceProvider.GetRequiredService<IEntityServiceAsync<ForeignComputedDept>>();
        private IEntityServiceAsync<ForeignComputedUser> UserService => ServiceProvider.GetRequiredService<IEntityServiceAsync<ForeignComputedUser>>();
        private ObjectViewDAO<ForeignComputedUserView> ViewDao => ServiceProvider.GetRequiredService<ObjectViewDAO<ForeignComputedUserView>>();
        private IEntityViewServiceAsync<ForeignComputedUserView> ViewService => ServiceProvider.GetRequiredService<IEntityViewServiceAsync<ForeignComputedUserView>>();

        #region 元数据与 SQL 渲染

        /// <summary>
        /// 元数据解析：视图的 ForeignColumn 目标列落在外部表别名上，且目标列本身是带表达式的计算列。
        /// </summary>
        [Fact]
        public void Metadata_TargetColumnIsForeignComputedColumn()
        {
            var view = new AttributeTableInfoProvider().GetTableView(typeof(ForeignComputedUserView))!;
            var foreignColumn = Assert.IsType<ForeignColumn>(
                view.Columns.First(c => c.PropertyName == nameof(ForeignComputedUserView.DeptDisplayName)));

            Assert.NotNull(foreignColumn.TargetColumn);
            Assert.Equal("Dept", foreignColumn.TargetColumn!.Table?.Name);
            Assert.IsType<ColumnDefinition>(foreignColumn.TargetColumn.Column);

            // Definition 透传目标列定义，因此 ForeignColumn 自身也表现为计算列
            Assert.True(foreignColumn.Definition.IsComputed);
            Assert.True(foreignColumn.Definition.HasExpression);
            Assert.Equal(nameof(ForeignComputedDept.DisplayName), foreignColumn.Name);
        }

        /// <summary>
        /// SQL 渲染：引用外部计算列时展开为表达式，并以外部表别名限定表达式中的列引用，且不出现计算列自身的物理列名。
        /// </summary>
        [Fact]
        public void Sql_ForeignComputedColumnRendersExpressionWithForeignAlias()
        {
            var view = new AttributeTableInfoProvider().GetTableView(typeof(ForeignComputedUserView))!;
            var foreignColumn = view.Columns.First(c => c.PropertyName == nameof(ForeignComputedUserView.DeptDisplayName));

            var context = new SqlBuildContext(SQLiteBuilder.Instance, view, Constants.DefaultTableAlias, null);
            string sql = ((SqlObject)foreignColumn).ToSql(context);

            Assert.StartsWith("(", sql);
            Assert.EndsWith(")", sql);
            Assert.Contains("\"Dept\".\"Name\"", sql);
            Assert.Contains("\"Dept\".\"Code\"", sql);
            Assert.Contains("||", sql);
            // 计算列没有物理列，表达式中不应出现其列名
            Assert.DoesNotContain("\"DisplayName\"", sql);
        }

        /// <summary>
        /// Select 列表渲染：外部计算列按表达式输出，并用视图属性名作为别名回填。
        /// </summary>
        [Fact]
        public void Sql_ForeignComputedColumnInSelectListKeepsPropertyAlias()
        {
            var view = new AttributeTableInfoProvider().GetTableView(typeof(ForeignComputedUserView))!;
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

            Assert.Contains("(\"Dept\".\"Name\" || '-' || \"Dept\".\"Code\") AS \"DeptDisplayName\"", sql);
        }

        /// <summary>
        /// 真实 SQL 生成链路：以视图类型生成 SELECT 时同时带上关联表与外部计算列表达式，
        /// 且条件中引用该属性同样展开为表达式（而非物理列名）。
        /// </summary>
        [Fact]
        public void SqlGen_SelectAndConditionExpandForeignComputedColumn()
        {
            var sqlGen = new SqlGen(typeof(ForeignComputedUserView));
            var select = new SelectExpr
            {
                Source = new WhereExpr
                {
                    Source = new FromExpr(typeof(ForeignComputedUserView)),
                    Where = Expr.Prop(nameof(ForeignComputedUserView.DeptDisplayName)) == Expr.Value("Engineering-D001")
                },
                Selects = new List<SelectItemExpr>
                {
                    new SelectItemExpr(Expr.Prop(nameof(ForeignComputedUserView.DeptDisplayName)))
                }
            };

            var result = sqlGen.ToSql(select);

            Assert.Contains("LEFT JOIN", result.Sql);
            Assert.Contains("\"Dept\".\"Name\"", result.Sql);
            Assert.Contains("\"Dept\".\"Code\"", result.Sql);
            Assert.DoesNotContain("\"DisplayName\"", result.Sql);
            Assert.Single(result.Params);
        }

        /// <summary>
        /// 建表 DDL：外部表的计算列不生成物理列。
        /// </summary>
        [Fact]
        public void Ddl_ExcludesForeignComputedColumn()
        {
            var table = new AttributeTableInfoProvider().GetTableDefinition(typeof(ForeignComputedDept))!;
            string sql = SQLiteBuilder.Instance.BuildCreateTableSql(table.Name!, table.Columns);

            Assert.Contains("Code", sql);
            Assert.DoesNotContain("DisplayName", sql);
        }

        #endregion

        #region 真实数据库查询

        /// <summary>
        /// 实际数据库查询：按主键读取视图时，外部计算列返回表达式求值结果。
        /// </summary>
        [Fact]
        public async Task Database_GetObject_ReturnsForeignComputedValue()
        {
            var ct = TestContext.Current.CancellationToken;
            var (dept, user) = await InsertDeptAndUserAsync("Engineering", "D001", "FcUser", ct);

            var loaded = await ViewDao.GetObject(user.Id).FirstOrDefaultAsync(ct);

            Assert.NotNull(loaded);
            Assert.Equal("FcUser", loaded!.Name);
            Assert.Equal(dept.Id, loaded.DeptId);
            Assert.Equal("Engineering-D001", loaded.DeptDisplayName);
        }

        /// <summary>
        /// 实际数据库查询：条件中引用外部计算列，按表达式过滤。
        /// </summary>
        [Fact]
        public async Task Database_FilterByForeignComputedColumn()
        {
            var ct = TestContext.Current.CancellationToken;
            await InsertDeptAndUserAsync("Engineering", "D001", "FcUserA", ct);
            await InsertDeptAndUserAsync("Sales", "D002", "FcUserB", ct);

            var matched = await ViewDao.Search(
                Expr.Prop(nameof(ForeignComputedUserView.DeptDisplayName)) == "Engineering-D001").ToListAsync(ct);

            var row = Assert.Single(matched);
            Assert.Equal("FcUserA", row.Name);
            Assert.Equal("Engineering-D001", row.DeptDisplayName);

            Assert.Empty(await ViewDao.Search(
                Expr.Prop(nameof(ForeignComputedUserView.DeptDisplayName)) == "Engineering-D002").ToListAsync(ct));
        }

        /// <summary>
        /// 实际数据库查询：服务层 lambda 条件与读取路径（先由外部表列算出计算值，再回填视图属性）。
        /// </summary>
        [Fact]
        public async Task Service_QueryAndRetrieveForeignComputedValue()
        {
            var ct = TestContext.Current.CancellationToken;
            var (_, user) = await InsertDeptAndUserAsync("Finance", "D003", "FcUserService", ct);

            var matched = await ViewService.SearchAsync(
                v => v.DeptDisplayName == "Finance-D003", cancellationToken: ct);

            Assert.Single(matched);
            Assert.Equal(user.Id, matched[0].Id);

            var loaded = await ViewService.GetObjectAsync(user.Id, cancellationToken: ct);
            Assert.NotNull(loaded);
            Assert.Equal("Finance-D003", loaded!.DeptDisplayName);
        }

        /// <summary>
        /// 实际数据库查询：左联接未命中时，外部计算列取不到值，视图属性为 null。
        /// </summary>
        [Fact]
        public async Task Database_LeftJoinWithoutMatch_ForeignComputedValueIsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var dept = await InsertDeptAsync("Support", "D004", ct);
            var matchedUser = await InsertUserAsync("FcMatchedUser", dept.Id, ct);
            var unmatchedUser = await InsertUserAsync("FcUnmatchedUser", dept.Id + 1000, ct);

            var rows = await ViewDao.Search().ToListAsync(ct);

            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, r => r.Id == matchedUser.Id && r.DeptDisplayName == "Support-D004");
            Assert.Contains(rows, r => r.Id == unmatchedUser.Id && r.DeptDisplayName == null);
        }

        #endregion

        private async Task<(ForeignComputedDept Dept, ForeignComputedUser User)> InsertDeptAndUserAsync(
            string deptName, string deptCode, string userName, System.Threading.CancellationToken ct)
        {
            var dept = await InsertDeptAsync(deptName, deptCode, ct);
            var user = await InsertUserAsync(userName, dept.Id, ct);
            return (dept, user);
        }

        private async Task<ForeignComputedDept> InsertDeptAsync(string name, string code, System.Threading.CancellationToken ct)
        {
            var dept = new ForeignComputedDept { Name = name, Code = code };
            await DeptService.InsertAsync(dept, ct);
            Assert.True(dept.Id > 0);
            return dept;
        }

        private async Task<ForeignComputedUser> InsertUserAsync(string name, int deptId, System.Threading.CancellationToken ct)
        {
            var user = new ForeignComputedUser { Name = name, DeptId = deptId };
            await UserService.InsertAsync(user, ct);
            Assert.True(user.Id > 0);
            return user;
        }
    }

    /// <summary>
    /// 带计算列的外部表：<see cref="DisplayName"/> 不落库，查询时按 <c>Name || '-' || Code</c> 求值。
    /// </summary>
    [Table("FcDepts")]
    public class ForeignComputedDept
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Name", AllowNull = true)]
        public string? Name { get; set; }

        [Column("Code", AllowNull = true)]
        public string? Code { get; set; }

        [Column("DisplayName", Expression = "{Name} || '-' || {Code}", ColumnMode = ColumnMode.Computed)]
        public string? DisplayName { get; set; }
    }

    /// <summary>
    /// 关联 <see cref="ForeignComputedDept"/> 的主表。
    /// </summary>
    [Table("FcUsers")]
    public class ForeignComputedUser
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Name", AllowNull = true)]
        public string? Name { get; set; }

        [Column("DeptId")]
        [ForeignType(typeof(ForeignComputedDept), Alias = "Dept")]
        public int DeptId { get; set; }
    }

    /// <summary>
    /// 视图：<see cref="DeptDisplayName"/> 引用外部表 <see cref="ForeignComputedDept"/> 的计算列。
    /// </summary>
    [TableJoin(typeof(ForeignComputedDept), "DeptId", Alias = "Dept", JoinType = TableJoinType.Left)]
    public class ForeignComputedUserView : ForeignComputedUser
    {
        [ForeignColumn("Dept", Property = nameof(ForeignComputedDept.DisplayName))]
        public string? DeptDisplayName { get; set; }
    }
}
