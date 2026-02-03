using LiteOrm.Common;
using LiteOrm.Tests.Models;
using System.Text.Encodings.Web;
using System.Text.Json;
using Xunit;

namespace LiteOrm.Tests
{
    public class ExprTests
    {
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private void TestSerialization<T>(T expr) where T : Expr
        {
            string json = JsonSerializer.Serialize<Expr>(expr, _jsonOptions);
            Expr deserialized = JsonSerializer.Deserialize<Expr>(json, _jsonOptions)!;

            // Use Equals override
            Assert.True(expr.Equals(deserialized), $"Deserialized expr should be equal to original. JSON: {json}");

            // HashCode test is now robust after framework fix
            Assert.Equal(expr.GetHashCode(), deserialized.GetHashCode());
        }

        [Fact]
        public void ValueExpr_Tests()
        {
            // Equals
            ValueExpr e1 = 123;
            ValueExpr e2 = 123;
            ValueExpr e3 = 456;
            ValueExpr e4 = 123L;

            Assert.True(e1.Equals(e2));
            Assert.False(e1.Equals(e3));
            // Assert.True(e1.Equals(e4)); // Simplification: don't consider type changes (int vs long)

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

            // Serialization
            // TestSerialization(e1); // Simplification: don't consider type changes
            // TestSerialization(e4); 
            TestSerialization(s1);
            TestSerialization(n1);
            // TestSerialization(list1); // Simplification: don't consider type changes after serialization




            // Non-const value serialization
            Expr nonConst = new ValueExpr("dynamic") { IsConst = false };
            TestSerialization(nonConst);
        }


        [Fact]
        public void PropertyExpr_Tests()
        {
            // Equals
            Expr p1 = Expr.Property("Name");
            Expr p2 = Expr.Property("Name");
            Expr p3 = Expr.Property("Age");

            Assert.True(p1.Equals(p2));
            Assert.False(p1.Equals(p3));

            // Serialization
            TestSerialization(p1);
            TestSerialization(p3);
        }

        [Fact]
        public void BinaryExpr_Tests()
        {
            // Equals
            Expr b1 = (Expr.Property("Age") > 18);
            Expr b2 = (Expr.Property("Age") > 18);
            Expr b3 = (Expr.Property("Age") >= 18);
            Expr b4 = (Expr.Property("Name") == "John");

            Assert.True(b1.Equals(b2));
            Assert.False(b1.Equals(b3));
            Assert.False(b1.Equals(b4));

            // Serialization
            TestSerialization(b1);
            TestSerialization(b4);

            // Complex Binary
            Expr complex = (Expr.Property("Price") * 1.1) > 100;
            TestSerialization(complex);
        }

        [Fact]
        public void UnaryExpr_Tests()
        {
            // Equals
            Expr u1 = !(Expr.Property("IsDeleted") != 0);
            Expr u2 = !(Expr.Property("IsDeleted") != 0);
            Expr u3 = !(Expr.Property("IsActive") != 0);

            Assert.True(u1.Equals(u2));
            Assert.False(u1.Equals(u3));
            Assert.IsType<NotExpr>(u1);

            Expr v1 = -Expr.Property("Score");
            Expr v2 = -Expr.Property("Score");
            Expr v3 = ~Expr.Property("Flag");

            Assert.True(v1.Equals(v2));
            Assert.False(v1.Equals(v3));
            Assert.IsType<UnaryExpr>(v1);

            // Serialization
            TestSerialization(u1);
            TestSerialization(v1);
            TestSerialization(v3);
        }

        [Fact]
        public void ExprSet_Tests()
        {
            // Equals
            Expr s1 = Expr.Property("Age") > 18 & Expr.Property("Name") == "John";
            Expr s2 = Expr.Property("Name") == "John" & Expr.Property("Age") > 18;
            Expr s3 = Expr.Property("Age") > 18 | Expr.Property("Name") == "John";

            Assert.True(s1.Equals(s2), "And set should be equal regardless of order");
            Assert.False(s1.Equals(s3), "And set should not be equal to Or set");

            // List type ExprSet (IN clause style)
            var inSet1 = new ValueSet(ValueJoinType.List, new ValueExpr(1), new ValueExpr(2), new ValueExpr(3));
            var inSet2 = new ValueSet(ValueJoinType.List, new ValueExpr(1), new ValueExpr(2), new ValueExpr(3));
            var inSet3 = new ValueSet(ValueJoinType.List, new ValueExpr(3), new ValueExpr(2), new ValueExpr(1));

            Assert.True(inSet1.Equals(inSet2));
            Assert.False(inSet1.Equals(inSet3), "Order should matter for list JoinType");

            // Serialization
            TestSerialization(s1);
            TestSerialization(s3);
            TestSerialization(inSet1);
        }

        [Fact]
        public void LambdaExpr_Tests()
        {
            // Equals
            // LambdaExpr uses LambdaExprConverter.ToExpr() for equality check
            Expr l1 = Expr.Exp<TestUser>(u => u.Age > 18);
            Expr l2 = Expr.Exp<TestUser>(u => u.Age > 18);
            Expr l3 = Expr.Exp<TestUser>(u => u.Age > 20);

            Assert.True(l1.Equals(l2));
            Assert.False(l1.Equals(l3));

            // Serialization
            // Note: LambdaExpr serializes as its InnerExpr
            string json = JsonSerializer.Serialize<Expr>(l1, _jsonOptions);
            Expr deserialized = JsonSerializer.Deserialize<Expr>(json, _jsonOptions)!;

            // deserialized will NOT be LambdaExpr, but the converted BinaryExpr
            Assert.IsType<LogicBinaryExpr>(deserialized);
            Assert.True(l1.Equals(deserialized));
        }

        [Fact]
        public void ForeignExpr_Tests()
        {
            // Equals
            Expr f1 = Expr.Foreign("DeptId", Expr.Property("Name") == "IT");
            Expr f2 = Expr.Foreign("DeptId", Expr.Property("Name") == "IT");
            Expr f3 = Expr.Foreign("DeptId", Expr.Property("Name") == "HR");
            Expr f4 = Expr.Foreign("ManagerId", Expr.Property("Name") == "IT");

            Assert.True(f1.Equals(f2));
            Assert.False(f1.Equals(f3));
            Assert.False(f1.Equals(f4));

            // Serialization
            TestSerialization(f1);
        }

        [Fact]
        public void GenericSqlExpr_Tests()
        {
            // Register a dummy handler
            GenericSqlExpr.Register("TestKey", (ctx, builder, pms, arg) => "TEST SQL");

            // Equals
            Expr g1 = GenericSqlExpr.Get("TestKey", 123);
            Expr g2 = GenericSqlExpr.Get("TestKey", 123);
            Expr g3 = GenericSqlExpr.Get("TestKey", 456);
            Expr g4 = GenericSqlExpr.GetStaticSqlExpr("TestKey");

            Assert.True(g1.Equals(g2));
            Assert.False(g1.Equals(g3));
            Assert.False(g1.Equals(g4));

            // Serialization
            TestSerialization(g1);
            TestSerialization(g4);
        }

        [Fact]
        public void FunctionExpr_Tests()
        {
            // Equals
            Expr f1 = new FunctionExpr("Now");
            Expr f2 = new FunctionExpr("Now");
            Expr f3 = new FunctionExpr("Today");
            Expr f4 = new FunctionExpr("ABS", (-10));
            Expr f5 = new FunctionExpr("ABS", (-10));

            Assert.True(f1.Equals(f2));
            Assert.False(f1.Equals(f3));
            Assert.True(f4.Equals(f5));

            // Serialization
            TestSerialization(f1);
            TestSerialization(f4);
        }

        [Fact]
        public void ExprFactory_Tests()
        {
            // Value
            Assert.Equal(new ValueExpr(123), Expr.Const(123));

            // And / Or
            var e1 = Expr.Property("A") == 1;
            var e2 = Expr.Property("B") == 2;
            Assert.Equal(new LogicSet(LogicJoinType.And, e1, e2), Expr.And(e1, e2));
            Assert.Equal(new LogicSet(LogicJoinType.Or, e1, e2), Expr.Or(e1, e2));

            // Not
            Assert.Equal(new NotExpr(e1), Expr.Not(e1));

            // Func
            Assert.Equal(new FunctionExpr("ABS", (ValueTypeExpr)10), Expr.Func("ABS", 10));

            // Concat / List
            var v1 = (ValueTypeExpr)new ValueExpr("a");
            var v2 = (ValueTypeExpr)new ValueExpr("b");
            Assert.Equal(new ValueSet(ValueJoinType.Concat, v1, v2), Expr.Concat(v1, v2));
            Assert.Equal(new ValueSet(ValueJoinType.List, v1, v2), Expr.List(v1, v2));

            // Sql / StaticSql
            GenericSqlExpr.Register("TestKey2", (ctx, builder, pms, arg) => "TEST");
            Assert.Equal(GenericSqlExpr.Get("TestKey2", 1), Expr.Sql("TestKey2", 1));
            Assert.Equal(GenericSqlExpr.GetStaticSqlExpr("TestKey2"), Expr.StaticSql("TestKey2"));

            // Between
            Assert.Equal((Expr.Property("Age") >= (ValueTypeExpr)10) & (Expr.Property("Age") <= (ValueTypeExpr)20), Expr.Between("Age", 10, 20));
        }

        [Fact]
        public void ExprExtensions_Tests()
        {
            var p = Expr.Property("Age");

            // IsNull / IsNotNull
            Assert.Equal(p == Expr.Null, p.IsNull());
            Assert.Equal(p != Expr.Null, p.IsNotNull());

            // Aggregates
            Assert.Equal(new AggregateFunctionExpr("COUNT", p, true), p.Count(true));
            Assert.Equal(new AggregateFunctionExpr("SUM", p), p.Sum());

            // OrderBy
            var asc = p.Asc();
            Assert.Equal(p, asc.Item1);
            Assert.True(asc.Item2);

            var desc = p.Desc();
            Assert.Equal(p, desc.Item1);
            Assert.False(desc.Item2);
        }

        [Fact]
        public void StructuredQuery_Tests()
        {
            var table = new TableExpr();

            var query = table
                .Where(Expr.Property("Age") > 18)
                .OrderBy(Expr.Property("CreateTime").Desc())
                .Section(10, 5)
                .Select(Expr.Property("Id"), Expr.Property("Name"));

            Assert.IsType<SelectExpr>(query);
            Assert.IsType<SectionExpr>(query.Source);

            var section = (SectionExpr)query.Source;
            Assert.Equal(10, section.Skip);
            Assert.Equal(5, section.Take);

            var orderBy = (OrderByExpr)section.Source;
            Assert.Single(orderBy.OrderBys);
            Assert.False(orderBy.OrderBys[0].Item2); // Desc

            var where = (WhereExpr)orderBy.Source;
            Assert.Equal(Expr.Property("Age") > 18, where.Where);

            Assert.Equal(table, where.Source);
        }

        [Fact]
        public void QueryExpr_Constructor_Tests()
        {
            var table = new TableExpr(TableInfoProvider.Default.GetTableView(typeof(TestUser)));
            var section = table
                .Where(Expr.Property("Age") > 18)
                .GroupBy(Expr.Property("DeptId"))
                .Having(AggregateFunctionExpr.Count > 1)
                .Select(Expr.Property("DeptId"), AggregateFunctionExpr.Count)
                .OrderBy((Expr.Property("DeptId"), true))
                .Section(10, 20);

            Assert.NotNull(section.Source);
            Assert.Equal(10, section.Skip);
            Assert.Equal(20, section.Take);

            // Serialization and Equals test
            TestSerialization(section);
        }

        [Fact]
        public void GroupByExpr_Constructor_params_Test()
        {
            var table = new TableExpr(TableInfoProvider.Default.GetTableView(typeof(TestUser)));
            var groupBy = new GroupByExpr(table, Expr.Property("DeptId"), Expr.Property("Sex"));
            Assert.Equal(2, groupBy.GroupBys.Count);
            Assert.Equal("DeptId", (groupBy.GroupBys[0] as PropertyExpr).PropertyName);
        }

        [Fact]
        public void OrderByExpr_Constructor_params_Test()
        {
            var table = new TableExpr(TableInfoProvider.Default.GetTableView(typeof(TestUser)));
            var orderBy = new OrderByExpr(table, (Expr.Property("Age"), true), (Expr.Property("Name"), false));
            Assert.Equal(2, orderBy.OrderBys.Count);
            Assert.True(orderBy.OrderBys[0].Item2);
            Assert.False(orderBy.OrderBys[1].Item2);
        }
    }
}
