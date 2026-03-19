using LiteOrm.Common;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using System.Configuration;
using System.Linq.Expressions;
using System.Text.Encodings.Web;
using System.Text.Json;
using Xunit;
using static System.Collections.Specialized.BitVector32;

namespace LiteOrm.Tests
{
    [Collection("Database")]
    public class ExprTests : TestBase
    {
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public ExprTests(DatabaseFixture fixture) : base(fixture) { }

        private void TestSerialization<T>(T expr) where T : Expr
        {
            string json = JsonSerializer.Serialize<Expr>(expr, _jsonOptions);
            Expr deserialized = JsonSerializer.Deserialize<Expr>(json, _jsonOptions)!;

            Assert.True(expr.Equals(deserialized), $"Deserialized expr should be equal to original. JSON: {json}");

            Assert.Equal(expr.GetHashCode(), deserialized.GetHashCode());
        }

        [Fact]
        public void ValueExpr_Tests()
        {
            // 测试相等性
            ValueExpr e1 = 123;
            ValueExpr e2 = 123;
            ValueExpr e3 = 456;
            ValueExpr e4 = 123L;

            Assert.True(e1.Equals(e2));
            Assert.False(e1.Equals(e3));
            Assert.True(e1.Equals(e4));

            ValueExpr s1 = "test";
            ValueExpr s2 = "test";
            ValueExpr s3 = "other";
            Assert.True(s1.Equals(s2));
            Assert.False(s1.Equals(s3));

            Expr n1 = Expr.Null;
            Expr n2 = new ValueExpr(null);
            Assert.True(n1.Equals(n2));

            Expr list1 = new ValueExpr(new List<int> { 1, 2, 3 });
            Expr list2 = new ValueExpr(new int[] { 1, 2, 3 });
            Assert.True(list1.Equals(list2));

            // 测试序列化
            TestSerialization(e1); // 简化：不考虑类型变化
            TestSerialization(e4);
            TestSerialization(s1);
            TestSerialization(n1);
            TestSerialization(list1); // 简化：不考虑序列化后的类型变化


            // 非常量值序列化
            Expr nonConst = new ValueExpr("dynamic") { IsConst = false };
            TestSerialization(nonConst);
        }


        [Fact]
        public void PropertyExpr_Tests()
        {
            // 基本 PropertyExpr
            Expr p1 = Expr.Prop("Name");
            Expr p2 = Expr.Prop("Name");
            Expr p3 = Expr.Prop("Age");

            Assert.True(p1.Equals(p2));
            Assert.False(p1.Equals(p3));

            // 带表别名的 PropertyExpr
            Expr pa1 = Expr.Prop("u", "Name");
            Expr pa2 = Expr.Prop("u", "Name");
            Expr pa3 = Expr.Prop("Name");
            Expr pa4 = Expr.Prop("u", "Age");

            Assert.True(pa1.Equals(pa2));
            Assert.False(pa1.Equals(pa3)); // 不同的属性名
            Assert.False(pa1.Equals(pa4)); // 不同的属性
            Assert.Equal("Name", (pa1 as PropertyExpr).PropertyName);
            Assert.Equal("u", (pa1 as PropertyExpr).TableAlias);

            // 表别名验证 - 应该对无效字符抛出异常
            var propWithAlias = new PropertyExpr("Name");
            Assert.Throws<ArgumentException>(() => propWithAlias.TableAlias = "u@123");
            Assert.Throws<ArgumentException>(() => propWithAlias.TableAlias = "u-name");

            // 属性名验证 - 应该对无效字符抛出异常
            Assert.Throws<ArgumentException>(() => new PropertyExpr("Name@123"));
            Assert.Throws<ArgumentException>(() => new PropertyExpr("Name-Column"));

            // 序列化
            TestSerialization(p1);
            TestSerialization(p3);
            TestSerialization(pa1);
        }

        [Fact]
        public void BinaryExpr_Tests()
        {
            // 相等性测试
            Expr b1 = (Expr.Prop("Age") > 18);
            Expr b2 = (Expr.Prop("Age") > 18);
            Expr b3 = (Expr.Prop("Age") >= 18);
            Expr b4 = (Expr.Prop("Name") == "John");

            Assert.True(b1.Equals(b2));
            Assert.False(b1.Equals(b3));
            Assert.False(b1.Equals(b4));

            // 序列化
            TestSerialization(b1);
            TestSerialization(b4);

            // 复杂二元表达式
            Expr complex = (Expr.Prop("Price") * 1.1) > 100;
            TestSerialization(complex);
        }

        [Fact]
        public void UnaryExpr_Tests()
        {
            // 相等性测试
            Expr u1 = !(Expr.Prop("IsDeleted") != 0);
            Expr u2 = !(Expr.Prop("IsDeleted") != 0);
            Expr u3 = !(Expr.Prop("IsActive") != 0);

            Assert.True(u1.Equals(u2));
            Assert.False(u1.Equals(u3));
            Assert.IsType<NotExpr>(u1);

            Expr v1 = -Expr.Prop("Score");
            Expr v2 = -Expr.Prop("Score");
            Expr v3 = ~Expr.Prop("Flag");

            Assert.True(v1.Equals(v2));
            Assert.False(v1.Equals(v3));
            Assert.IsType<UnaryExpr>(v1);

            // 序列化
            TestSerialization(u1);
            TestSerialization(v1);
            TestSerialization(v3);
        }

        [Fact]
        public void ExprSet_Tests()
        {
            // 相等性测试
            Expr s1 = Expr.Prop("Age") > 18 & Expr.Prop("Name") == "John";
            Expr s2 = Expr.Prop("Name") == "John" & Expr.Prop("Age") > 18;
            Expr s3 = Expr.Prop("Age") > 18 | Expr.Prop("Name") == "John";

            Assert.True(s1.Equals(s2), "And 集合应该与顺序无关");
            Assert.False(s1.Equals(s3), "And 集合不应该等于 Or 集合");

            // 列表类型的 ExprSet (IN 子句风格)
            var inSet1 = new ValueSet(ValueJoinType.List, new ValueExpr(1), new ValueExpr(2), new ValueExpr(3));
            var inSet2 = new ValueSet(ValueJoinType.List, new ValueExpr(1), new ValueExpr(2), new ValueExpr(3));
            var inSet3 = new ValueSet(ValueJoinType.List, new ValueExpr(3), new ValueExpr(2), new ValueExpr(1));

            Assert.True(inSet1.Equals(inSet2));
            Assert.False(inSet1.Equals(inSet3), "对于列表 JoinType，顺序应该重要");

            // 序列化
            TestSerialization(s1);
            TestSerialization(s3);
            TestSerialization(inSet1);
        }

        [Fact]
        public void LambdaExpr_Tests()
        {
            // 相等性测试
            // LambdaExpr 使用 LambdaExprConverter.ToExpr() 进行相等性检查
            Expr l1 = Expr.Lambda<TestUser>(u => u.Age > 18);
            Expr l2 = Expr.Lambda<TestUser>(u => u.Age > 18);
            Expr l3 = Expr.Lambda<TestUser>(u => u.Age > 20);

            Assert.True(l1.Equals(l2));
            Assert.False(l1.Equals(l3));

            // 序列化
            // 注意：LambdaExpr 序列化为其 InnerExpr
            string json = JsonSerializer.Serialize<Expr>(l1, _jsonOptions);
            Expr deserialized = JsonSerializer.Deserialize<Expr>(json, _jsonOptions)!;

            // deserialized 不会是 LambdaExpr，而是转换后的 BinaryExpr
            Assert.IsType<LogicBinaryExpr>(deserialized);
            Assert.True(l1.Equals(deserialized));
        }

        [Fact]
        public void ForeignExpr_Tests()
        {
            // 相等性测试
            Expr f1 = Expr.Exists<object>(Expr.Prop("Name") == "IT");
            Expr f2 = Expr.Exists<object>(Expr.Prop("Name") == "IT");
            Expr f3 = Expr.Exists<object>(Expr.Prop("Name") == "HR");
            Expr f4 = Expr.Exists<string>(Expr.Prop("Name") == "IT");

            Assert.True(f1.Equals(f2));
            Assert.False(f1.Equals(f3));
            Assert.False(f1.Equals(f4));

            // 序列化
            TestSerialization(f1);
        }

        [Fact]
        public void LambdaExprConverter_ForeignExists_Tests()
        {
            var result = Expr.Lambda<TestDepartment>(d => d.Id > 0 && Expr.Exists<TestUser>(u => u.DeptId == d.Id));
            Assert.NotNull(result);
        }

        [Fact]
        public void GenericSqlExpr_Tests()
        {
            // 注册一个虚拟处理器
            GenericSqlExpr.Register("TestKey", (SqlBuildContext ctx, ISqlBuilder builder, ICollection<KeyValuePair<string, object>> pms, object arg) => "TEST SQL");

            // 相等性测试
            Expr g1 = GenericSqlExpr.Get("TestKey", 123);
            Expr g2 = GenericSqlExpr.Get("TestKey", 123);
            Expr g3 = GenericSqlExpr.Get("TestKey", 456);
            Expr g4 = GenericSqlExpr.Get("TestKey");

            Assert.True(g1.Equals(g2));
            Assert.False(g1.Equals(g3));
            Assert.False(g1.Equals(g4));

            // 序列化
            TestSerialization(g1);
            TestSerialization(g4);
        }

        [Fact]
        public void FunctionExpr_Tests()
        {
            // 相等性测试
            Expr f1 = new FunctionExpr("Now");
            Expr f2 = new FunctionExpr("Now");
            Expr f3 = new FunctionExpr("Today");
            Expr f4 = new FunctionExpr("ABS", (-10));
            Expr f5 = new FunctionExpr("ABS", (-10));

            Assert.True(f1.Equals(f2));
            Assert.False(f1.Equals(f3));
            Assert.True(f4.Equals(f5));

            // 序列化
            TestSerialization(f1);
            TestSerialization(f4);
        }

        [Fact]
        public void ExprFactory_Tests()
        {
            // 值表达式
            Assert.Equal(new ValueExpr(123), Expr.Const(123));

            // And / Or
            var e1 = Expr.Prop("A") == 1;
            var e2 = Expr.Prop("B") == 2;
            Assert.Equal(new LogicSet(LogicJoinType.And, e1, e2), Expr.And(e1, e2));
            Assert.Equal(new LogicSet(LogicJoinType.Or, e1, e2), Expr.Or(e1, e2));

            // Not
            Assert.Equal(new NotExpr(e1), Expr.Not(e1));

            // 函数
            Assert.Equal(new FunctionExpr("ABS", (ValueTypeExpr)10), Expr.Func("ABS", 10));

            // 连接 / 列表
            var v1 = (ValueTypeExpr)new ValueExpr("a");
            var v2 = (ValueTypeExpr)new ValueExpr("b");
            Assert.Equal(new ValueSet(ValueJoinType.Concat, v1, v2), Expr.Concat(v1, v2));
            Assert.Equal(new ValueSet(ValueJoinType.List, v1, v2), Expr.List(v1, v2));

            // Sql / StaticSql
            GenericSqlExpr.Register("TestKey2", (SqlBuildContext ctx, ISqlBuilder builder, ICollection<KeyValuePair<string, object>> pms, object arg) => "TEST");
            Assert.Equal(GenericSqlExpr.Get("TestKey2", 1), Expr.Sql("TestKey2", 1));
            Assert.Equal(GenericSqlExpr.Get("TestKey2"), Expr.StaticSql("TestKey2"));

            // Between
            Assert.Equal((Expr.Prop("Age") >= (ValueTypeExpr)10) & (Expr.Prop("Age") <= (ValueTypeExpr)20), Expr.Prop("Age").Between(10, 20));
        }

        [Fact]
        public void SelectItemExpr_Serialization_Tests()
        {
            // 带别名的选择项（基于属性的序列化）
            var sie1 = new SelectItemExpr(Expr.Prop("DeptId")) { Alias = "Department" };
            var sie2 = new SelectItemExpr(Expr.Const(1)) { Alias = "Count" };
            var selectExpr = new SelectExpr(new FromExpr(), sie1, sie2);

            string json = JsonSerializer.Serialize<Expr>(selectExpr, _jsonOptions);
            // 验证格式包含 Alias 属性
            Assert.Contains("\"Alias\"", json);
            Assert.Contains("\"Department\"", json);
            Assert.Contains("\"Count\"", json);

            var deserialized = JsonSerializer.Deserialize<Expr>(json, _jsonOptions) as SelectExpr;
            Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.Selects.Count);
            Assert.Equal("Department", deserialized.Selects[0].Alias);

            // 别名验证 - 应该对无效字符抛出异常
            Assert.Throws<ArgumentException>(() => sie1.Alias = "Dept@Id");
            Assert.Throws<ArgumentException>(() => sie2.Alias = "Count-1");

            Assert.Equal("Count", deserialized.Selects[1].Alias);
            Assert.True(selectExpr.Equals(deserialized));
        }

        [Fact]
        public void ExprExtensions_Tests()
        {
            var p = Expr.Prop("Age");

            // IsNull / IsNotNull
            Assert.Equal(p == Expr.Null, p.IsNull());
            Assert.Equal(p != Expr.Null, p.IsNotNull());

            // 聚合函数
            Assert.Equal(new FunctionExpr("COUNT", p.Distinct()) { IsAggregate = true }, p.Count(true));
            Assert.Equal(new FunctionExpr("SUM", p) { IsAggregate = true }, p.Sum());

            // 排序
            var asc = p.Asc();
            Assert.Equal(p, asc.Field);
            Assert.True(asc.Ascending);

            var desc = p.Desc();
            Assert.Equal(p, desc.Field);
            Assert.False(desc.Ascending);
        }

        [Fact]
        public void StructuredQuery_Tests()
        {
            var table = new FromExpr();

            var query = table
                .Where(Expr.Prop("Age") > 18)
                .Select(Expr.Prop("Id"), Expr.Prop("Name"))
                .OrderBy(Expr.Prop("CreateTime").Desc())
                .Section(10, 5);

            Assert.IsType<SectionExpr>(query);
            Assert.IsType<OrderByExpr>(query.Source);

            var orderBy = (OrderByExpr)query.Source;
            Assert.Single(orderBy.OrderBys);
            Assert.False(orderBy.OrderBys[0].Ascending); // Desc

            var select = (SelectExpr)orderBy.Source;
            Assert.Equal(2, select.Selects.Count);

            var where = (WhereExpr)select.Source;
            Assert.Equal(Expr.Prop("Age") > 18, where.Where);

            Assert.Equal(table, where.Source);
        }

        [Fact]
        public void QueryExpr_Constructor_Tests()
        {
            var table = Expr.From<TestUser>();
            var section = table
                .Where(Expr.Prop("Age") > 18)
                .GroupBy(Expr.Prop("DeptId"))
                .Having(Expr.Count() > 1)
                .Select(Expr.Prop("DeptId"), Expr.Count())
                .OrderBy(Expr.Prop("DeptId").Asc())
                .Section(10, 20);

            Assert.NotNull(section.Source);
            Assert.Equal(10, section.Skip);
            Assert.Equal(20, section.Take);

            // 序列化和相等性测试
            TestSerialization(section);
        }

        [Fact]
        public void GroupByExpr_Constructor_params_Test()
        {
            var table = new FromExpr(typeof(TestUser));
            var groupBy = new GroupByExpr(table, Expr.Prop("DeptId"), Expr.Prop("Sex"));
            Assert.Equal(2, groupBy.GroupBys.Count);
            Assert.Equal("DeptId", (groupBy.GroupBys[0] as PropertyExpr).PropertyName);
            // 序列化和相等性测试
            TestSerialization(groupBy);
        }

        [Fact]
        public void OrderByExpr_Constructor_params_Test()
        {
            var table = new FromExpr(typeof(TestUser));
            var orderBy = new OrderByExpr(table, (Expr.Prop("Age"), true), (Expr.Prop("Name"), false));
            Assert.Equal(2, orderBy.OrderBys.Count);
            Assert.True(orderBy.OrderBys[0].Ascending);
            Assert.False(orderBy.OrderBys[1].Ascending);
        }

        [Fact]
        public void DeleteExpr_Tests()
        {
            var table = new FromExpr(typeof(TestUser));
            var delete = new DeleteExpr(table, Expr.Prop("Age") > 100);

            TestSerialization(delete);
        }

        [Fact]
        public void UpdateExpr_Tests()
        {
            var table = new FromExpr(typeof(TestUser));
            var update = new UpdateExpr(table, Expr.Prop("Id") == 1);
            update.Set(("Name", "NewName"));
            update.Set(("Age", 30));

            TestSerialization(update);
        }

        [Fact]
        public void FromExpr_TableArgs_Alias_Tests()
        {
            var from1 = new FromExpr(typeof(TestUser)) { Alias = "u" };
            from1.TableArgs = new[] { "arg1", "arg2" };

            var from2 = new FromExpr(typeof(TestUser)) { Alias = "u" };
            from2.TableArgs = new[] { "arg1", "arg2" };

            var from3 = new FromExpr(typeof(TestUser)) { Alias = "u" };
            from3.TableArgs = new[] { "arg1", "arg3" };

            var from4 = new FromExpr(typeof(TestUser)) { Alias = "u2" };
            from4.TableArgs = new[] { "arg1", "arg2" };

            var from5 = new FromExpr(typeof(TestUser));

            Assert.True(from1.Equals(from2));
            Assert.Equal(from1.GetHashCode(), from2.GetHashCode());

            Assert.False(from1.Equals(from3));
            Assert.False(from1.Equals(from4));
            Assert.False(from1.Equals(from5));

            TestSerialization(from1);

            var fromWithAliasOnly = new FromExpr(typeof(TestUser)) { Alias = "t" };
            TestSerialization(fromWithAliasOnly);

            var fromWithTableArgsOnly = new FromExpr(typeof(TestUser));
            fromWithTableArgsOnly.TableArgs = new[] { "2024" };
            TestSerialization(fromWithTableArgsOnly);
        }
    }
}
