using LiteOrm.Common;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// 完整的 Expr 类型测试类，覆盖所有 ExprType 枚举值。
    /// 测试各种表达式类型的创建、相等性、序列化等核心功能。
    /// </summary>
    [Collection("Database")]
    public class ExprTypeCompleteTests : TestBase
    {
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public ExprTypeCompleteTests(DatabaseFixture fixture) : base(fixture) { }

        private void TestSerialization<T>(T expr) where T : Expr
        {
            string json = JsonSerializer.Serialize<Expr>(expr, _jsonOptions);
            Expr deserialized = JsonSerializer.Deserialize<Expr>(json, _jsonOptions)!;
            Assert.True(expr.Equals(deserialized), $"Deserialized expr should be equal to original. JSON: {json}");
            Assert.Equal(expr.GetHashCode(), deserialized.GetHashCode());
        }

        #region ExprType.Table - TableExpr

        [Fact]
        public void TableExpr_Tests()
        {
            var t1 = new TableExpr(typeof(TestUser));
            var t2 = new TableExpr(typeof(TestUser));
            var t3 = new TableExpr(typeof(TestDepartment));
            var t4 = new TableExpr(typeof(TestUser)) { Alias = "u" };

            Assert.True(t1.Equals(t2));
            Assert.False(t1.Equals(t3));
            Assert.False(t1.Equals(t4));

            Assert.Equal("TestUser", t1.Type.Name);
            Assert.Null(t1.Alias);
            Assert.Empty(t1.TableArgs ?? Array.Empty<string>());

            TestSerialization(t1);
            TestSerialization(t4);
        }

        [Fact]
        public void TableExpr_WithAlias_Tests()
        {
            var t1 = new TableExpr(typeof(TestUser)) { Alias = "u" };
            var t2 = new TableExpr(typeof(TestUser)) { Alias = "u" };
            var t3 = new TableExpr(typeof(TestUser)) { Alias = "t" };

            Assert.True(t1.Equals(t2));
            Assert.False(t1.Equals(t3));
            Assert.Equal("u", t1.Alias);
            TestSerialization(t1);
        }

        [Fact]
        public void TableExpr_WithTableArgs_Tests()
        {
            var t1 = new TableExpr(typeof(TestUser)) { TableArgs = new[] { "2024", "01" } };
            var t2 = new TableExpr(typeof(TestUser)) { TableArgs = new[] { "2024", "01" } };
            var t3 = new TableExpr(typeof(TestUser)) { TableArgs = new[] { "2024", "02" } };

            Assert.True(t1.Equals(t2));
            Assert.False(t1.Equals(t3));
            TestSerialization(t1);
        }

        #endregion

        #region ExprType.TableJoin - TableJoinExpr

        [Fact]
        public void TableJoinExpr_Tests()
        {
            var table = new TableExpr(typeof(TestDepartment)) { Alias = "d" };
            var on = Expr.Prop("u", "DeptId") == Expr.Prop("d", "Id");
            var join1 = new TableJoinExpr(table, on) { JoinType = TableJoinType.Left };
            var join2 = new TableJoinExpr(table, on) { JoinType = TableJoinType.Left };
            var join3 = new TableJoinExpr(table, on) { JoinType = TableJoinType.Inner };

            Assert.True(join1.Equals(join2));
            Assert.False(join1.Equals(join3));
            Assert.Equal(TableJoinType.Left, join1.JoinType);
            TestSerialization(join1);
        }

        [Fact]
        public void TableJoinExpr_WithVariousJoinTypes_Tests()
        {
            var table = new TableExpr(typeof(TestDepartment));
            var on = Expr.Prop("DeptId") == Expr.Prop("Id");

            var leftJoin = new TableJoinExpr(table, on) { JoinType = TableJoinType.Left };
            var rightJoin = new TableJoinExpr(table, on) { JoinType = TableJoinType.Right };
            var innerJoin = new TableJoinExpr(table, on) { JoinType = TableJoinType.Inner };
            var crossJoin = new TableJoinExpr(table, null) { JoinType = TableJoinType.Cross };

            Assert.Equal(TableJoinType.Left, leftJoin.JoinType);
            Assert.Equal(TableJoinType.Right, rightJoin.JoinType);
            Assert.Equal(TableJoinType.Inner, innerJoin.JoinType);
            Assert.Equal(TableJoinType.Cross, crossJoin.JoinType);

            TestSerialization(leftJoin);
            TestSerialization(rightJoin);
            TestSerialization(innerJoin);
            TestSerialization(crossJoin);
        }

        #endregion

        #region ExprType.From - FromExpr

        [Fact]
        public void FromExpr_Tests()
        {
            var f1 = new FromExpr(typeof(TestUser));
            var f2 = new FromExpr(typeof(TestUser));
            var f3 = new FromExpr(typeof(TestDepartment));

            Assert.True(f1.Equals(f2));
            Assert.False(f1.Equals(f3));
            TestSerialization(f1);
        }

        [Fact]
        public void FromExpr_WithAlias_Tests()
        {
            var f1 = new FromExpr(typeof(TestUser)) { Source = new TableExpr(typeof(TestUser)) { Alias = "u" } };
            var f2 = new FromExpr(typeof(TestUser)) { Source = new TableExpr(typeof(TestUser)) { Alias = "u" } };
            var f3 = new FromExpr(typeof(TestUser)) { Source = new TableExpr(typeof(TestUser)) { Alias = "t" } };

            Assert.True(f1.Equals(f2));
            Assert.False(f1.Equals(f3));
            TestSerialization(f1);
        }

        [Fact]
        public void FromExpr_WithTableArgs_Tests()
        {
            var f1 = new FromExpr(new TableExpr(typeof(TestUser)) { TableArgs = new[] { "arg1" } });
            var f2 = new FromExpr(new TableExpr(typeof(TestUser)) { TableArgs = new[] { "arg1" } });
            var f3 = new FromExpr(new TableExpr(typeof(TestUser)) { TableArgs = new[] { "arg2" } });

            Assert.True(f1.Equals(f2));
            Assert.False(f1.Equals(f3));
            TestSerialization(f1);
        }

        #endregion

        #region ExprType.Select - SelectExpr

        [Fact]
        public void SelectExpr_Tests()
        {
            var from = new FromExpr(typeof(TestUser));
            var s1 = new SelectExpr(from, Expr.Prop("Id"), Expr.Prop("Name"));
            var s2 = new SelectExpr(from, Expr.Prop("Id"), Expr.Prop("Name"));
            var s3 = new SelectExpr(from, Expr.Prop("Id"), Expr.Prop("Age"));

            Assert.True(s1.Equals(s2));
            Assert.False(s1.Equals(s3));
            Assert.Equal(2, s1.Selects.Count);
            TestSerialization(s1);
        }

        [Fact]
        public void SelectExpr_WithAlias_Tests()
        {
            var from = new FromExpr(typeof(TestUser));
            var s1 = new SelectExpr(from, Expr.Prop("Id").As("UserId"));
            var s2 = new SelectExpr(from, Expr.Prop("Id").As("UserId"));
            var s3 = new SelectExpr(from, Expr.Prop("Id").As("Id"));

            Assert.True(s1.Equals(s2));
            Assert.False(s1.Equals(s3));
            TestSerialization(s1);
        }

        #endregion

        #region ExprType.SelectItem - SelectItemExpr

        [Fact]
        public void SelectItemExpr_Tests()
        {
            var item1 = new SelectItemExpr(Expr.Prop("Id")) { Alias = "UserId" };
            var item2 = new SelectItemExpr(Expr.Prop("Id")) { Alias = "UserId" };
            var item3 = new SelectItemExpr(Expr.Prop("Id")) { Alias = "Id" };
            var item4 = new SelectItemExpr(Expr.Prop("Name")) { Alias = "UserId" };

            Assert.True(item1.Equals(item2));
            Assert.False(item1.Equals(item3));
            Assert.False(item1.Equals(item4));
            Assert.Equal("UserId", item1.Alias);
            TestSerialization(item1);
        }

        [Fact]
        public void SelectItemExpr_WithFunction_Tests()
        {
            var funcExpr = new FunctionExpr("COUNT", Expr.Prop("Id"));
            var item1 = new SelectItemExpr(funcExpr) { Alias = "UserCount" };
            var item2 = new SelectItemExpr(funcExpr) { Alias = "UserCount" };

            Assert.True(item1.Equals(item2));
            TestSerialization(item1);
        }

        #endregion

        #region ExprType.OrderByItem - OrderByItemExpr

        [Fact]
        public void OrderByItemExpr_Tests()
        {
            var item1 = new OrderByItemExpr(Expr.Prop("Age"), true);
            var item2 = new OrderByItemExpr(Expr.Prop("Age"), true);
            var item3 = new OrderByItemExpr(Expr.Prop("Age"), false);
            var item4 = new OrderByItemExpr(Expr.Prop("Name"), true);

            Assert.True(item1.Equals(item2));
            Assert.False(item1.Equals(item3));
            Assert.False(item1.Equals(item4));
            Assert.True(item1.Ascending);
            TestSerialization(item1);
        }

        [Fact]
        public void OrderByItemExpr_Descending_Tests()
        {
            var item = new OrderByItemExpr(Expr.Prop("CreateTime"), false);
            Assert.False(item.Ascending);
            Assert.Equal("CreateTime", (item.Field as PropertyExpr).PropertyName);
            TestSerialization(item);
        }

        #endregion

        #region ExprType.Function - FunctionExpr

        [Fact]
        public void FunctionExpr_Tests()
        {
            var f1 = new FunctionExpr("SUM", Expr.Prop("Amount"));
            var f2 = new FunctionExpr("SUM", Expr.Prop("Amount"));
            var f3 = new FunctionExpr("AVG", Expr.Prop("Amount"));

            Assert.True(f1.Equals(f2));
            Assert.False(f1.Equals(f3));
            Assert.Equal("SUM", f1.FunctionName);
            Assert.Single(f1.Args);
            TestSerialization(f1);
        }

        [Fact]
        public void FunctionExpr_WithMultipleArgs_Tests()
        {
            var f1 = new FunctionExpr("CONCAT", Expr.Prop("FirstName"), Expr.Prop("LastName"));
            var f2 = new FunctionExpr("CONCAT", Expr.Prop("FirstName"), Expr.Prop("LastName"));
            var f3 = new FunctionExpr("CONCAT", Expr.Prop("FirstName"), Expr.Const(" "), Expr.Prop("LastName"));

            Assert.True(f1.Equals(f2));
            Assert.False(f1.Equals(f3));
            Assert.Equal(2, f1.Args.Count);
            TestSerialization(f1);
        }

        [Fact]
        public void FunctionExpr_IsAggregate_Tests()
        {
            var count = new FunctionExpr("COUNT", Expr.Prop("Id")) { IsAggregate = true };
            var sum = new FunctionExpr("SUM", Expr.Prop("Amount")) { IsAggregate = true };

            Assert.True(count.IsAggregate);
            Assert.True(sum.IsAggregate);
            TestSerialization(count);
        }

        #endregion

        #region ExprType.Foreign - ForeignExpr

        [Fact]
        public void ForeignExpr_Tests()
        {
            var f1 = new ForeignExpr(typeof(TestDepartment), Expr.Prop("Name") == "IT");
            var f2 = new ForeignExpr(typeof(TestDepartment), Expr.Prop("Name") == "IT");
            var f3 = new ForeignExpr(typeof(TestDepartment), Expr.Prop("Name") == "HR");
            var f4 = new ForeignExpr(typeof(TestUser), Expr.Prop("Name") == "IT");

            Assert.True(f1.Equals(f2));
            Assert.False(f1.Equals(f3));
            Assert.False(f1.Equals(f4));
            Assert.False(f1.AutoRelated);
            TestSerialization(f1);
        }

        [Fact]
        public void ForeignExpr_AutoRelated_Tests()
        {
            var f1 = new ForeignExpr(typeof(TestDepartment), Expr.Prop("Name") == "IT") { AutoRelated = true };
            var f2 = new ForeignExpr(typeof(TestDepartment), Expr.Prop("Name") == "IT") { AutoRelated = true };
            var f3 = new ForeignExpr(typeof(TestDepartment), Expr.Prop("Name") == "IT") { AutoRelated = false };

            Assert.True(f1.Equals(f2));
            Assert.False(f1.Equals(f3));
            Assert.True(f1.AutoRelated);
            TestSerialization(f1);
        }

        [Fact]
        public void ForeignExpr_WithTableArgs_Tests()
        {
            var f1 = new ForeignExpr(typeof(TestDepartment), Expr.Prop("Name") == "IT", "2024");
            var f2 = new ForeignExpr(typeof(TestDepartment), Expr.Prop("Name") == "IT", "2024");
            var f3 = new ForeignExpr(typeof(TestDepartment), Expr.Prop("Name") == "IT", "2025");

            Assert.True(f1.Equals(f2));
            Assert.False(f1.Equals(f3));
            TestSerialization(f1);
        }

        #endregion

        #region ExprType.Lambda - LambdaExpr

        [Fact]
        public void LambdaExpr_Tests()
        {
            var l1 = Expr.Lambda<TestUser>(u => u.Age > 18);
            var l2 = Expr.Lambda<TestUser>(u => u.Age > 18);
            var l3 = Expr.Lambda<TestUser>(u => u.Age > 20);

            Assert.True(l1.Equals(l2));
            Assert.False(l1.Equals(l3));
            Assert.IsType<LogicBinaryExpr>(l1);
            TestSerialization(l1);
        }

        #endregion

        #region ExprType.LogicBinary - LogicBinaryExpr

        [Fact]
        public void LogicBinaryExpr_AllOperators_Tests()
        {
            var equal = Expr.Prop("Age") == 18;
            var notEqual = Expr.Prop("Age") != 18;
            var greaterThan = Expr.Prop("Age") > 18;
            var lessThan = Expr.Prop("Age") < 18;
            var greaterOrEqual = Expr.Prop("Age") >= 18;
            var lessOrEqual = Expr.Prop("Age") <= 18;

            Assert.IsType<LogicBinaryExpr>(equal);
            Assert.IsType<LogicBinaryExpr>(notEqual);
            Assert.IsType<LogicBinaryExpr>(greaterThan);
            Assert.IsType<LogicBinaryExpr>(lessThan);
            Assert.IsType<LogicBinaryExpr>(greaterOrEqual);
            Assert.IsType<LogicBinaryExpr>(lessOrEqual);

            TestSerialization(equal);
            TestSerialization(notEqual);
            TestSerialization(greaterThan);
            TestSerialization(lessThan);
            TestSerialization(greaterOrEqual);
            TestSerialization(lessOrEqual);
        }

        [Fact]
        public void LogicBinaryExpr_Reverse_Tests()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Age"), LogicOperator.GreaterThan, Expr.Const(18));
            var reversed = expr.Reverse(true);

            Assert.Equal(LogicOperator.LessThan, reversed.Operator);
            Assert.Equal(18, (reversed.Left as ValueExpr).Value);
            Assert.Equal("Age", (reversed.Right as PropertyExpr).PropertyName);
        }

        #endregion

        #region ExprType.And - AndExpr

        [Fact]
        public void AndExpr_Tests()
        {
            var e1 = Expr.Prop("Age") > 18;
            var e2 = Expr.Prop("Name") == "John";
            var and1 = new AndExpr(e1, e2);
            var and2 = new AndExpr(e1, e2);
            var and3 = new AndExpr(e2, e1);

            Assert.True(and1.Equals(and2));
            Assert.True(and1.Equals(and3));
            Assert.Equal(2, and1.Count);
            TestSerialization(and1);
        }

        [Fact]
        public void AndExpr_MultipleItems_Tests()
        {
            var e1 = Expr.Prop("Age") > 18;
            var e2 = Expr.Prop("Name") == "John";
            var e3 = Expr.Prop("DeptId") == 1;
            var and = new AndExpr(e1, e2, e3);

            Assert.Equal(3, and.Count);
            TestSerialization(and);
        }

        #endregion

        #region ExprType.Or - OrExpr

        [Fact]
        public void OrExpr_Tests()
        {
            var e1 = Expr.Prop("Age") > 18;
            var e2 = Expr.Prop("Name") == "John";
            var or1 = new OrExpr(e1, e2);
            var or2 = new OrExpr(e1, e2);
            var or3 = new OrExpr(e2, e1);

            Assert.True(or1.Equals(or2));
            Assert.True(or1.Equals(or3));
            Assert.Equal(2, or1.Count);
            TestSerialization(or1);
        }

        [Fact]
        public void OrExpr_MultipleItems_Tests()
        {
            var e1 = Expr.Prop("Age") > 18;
            var e2 = Expr.Prop("Age") < 65;
            var e3 = Expr.Prop("DeptId") == 1;
            var or = new OrExpr(e1, e2, e3);

            Assert.Equal(3, or.Count);
            TestSerialization(or);
        }

        #endregion

        #region ExprType.Not - NotExpr

        [Fact]
        public void NotExpr_Tests()
        {
            var inner = Expr.Prop("Age") > 18;
            var n1 = new NotExpr(inner);
            var n2 = new NotExpr(inner);
            var n3 = new NotExpr(Expr.Prop("Age") < 18);

            Assert.True(n1.Equals(n2));
            Assert.False(n1.Equals(n3));
            TestSerialization(n1);
        }

        [Fact]
        public void NotExpr_DoubleNegate_Tests()
        {
            var inner = Expr.Prop("Age") > 18;
            var not1 = new NotExpr(inner);
            var not2 = new NotExpr(not1);

            Assert.IsType<NotExpr>(not1);
            Assert.IsType<NotExpr>(not2);
            TestSerialization(not2);
        }

        #endregion

        #region ExprType.ValueBinary - ValueBinaryExpr

        [Fact]
        public void ValueBinaryExpr_AllOperators_Tests()
        {
            var add = Expr.Prop("Age") + 1;
            var subtract = Expr.Prop("Age") - 1;
            var multiply = Expr.Prop("Age") * 2;
            var divide = Expr.Prop("Age") / 2;
            var concat = Expr.Prop("FirstName").Concat(Expr.Prop("LastName"));

            Assert.IsType<ValueBinaryExpr>(add);
            Assert.IsType<ValueBinaryExpr>(subtract);
            Assert.IsType<ValueBinaryExpr>(multiply);
            Assert.IsType<ValueBinaryExpr>(divide);
            Assert.IsType<ValueSet>(concat);

            TestSerialization(add);
            TestSerialization(subtract);
            TestSerialization(multiply);
            TestSerialization(divide);
            TestSerialization(concat);
        }

        [Fact]
        public void ValueBinaryExpr_Chained_Tests()
        {
            var expr = (Expr.Prop("Price") * 1.1) + 10;
            Assert.IsType<ValueBinaryExpr>(expr);
            TestSerialization(expr);
        }

        #endregion

        #region ExprType.ValueSet - ValueSet

        [Fact]
        public void ValueSet_ListJoinType_Tests()
        {
            var vs1 = new ValueSet(ValueJoinType.List, Expr.Const(1), Expr.Const(2), Expr.Const(3));
            var vs2 = new ValueSet(ValueJoinType.List, Expr.Const(1), Expr.Const(2), Expr.Const(3));
            var vs3 = new ValueSet(ValueJoinType.List, Expr.Const(3), Expr.Const(2), Expr.Const(1));

            Assert.True(vs1.Equals(vs2));
            Assert.False(vs1.Equals(vs3));
            Assert.Equal(ValueJoinType.List, vs1.JoinType);
            Assert.Equal(3, vs1.Count);
            TestSerialization(vs1);
        }

        [Fact]
        public void ValueSet_ConcatJoinType_Tests()
        {
            var vs1 = new ValueSet(ValueJoinType.Concat, Expr.Prop("FirstName"), Expr.Const(" "), Expr.Prop("LastName"));
            var vs2 = new ValueSet(ValueJoinType.Concat, Expr.Prop("FirstName"), Expr.Const(" "), Expr.Prop("LastName"));
            var vs3 = new ValueSet(ValueJoinType.Concat, Expr.Prop("FirstName"), Expr.Prop("LastName"));

            Assert.True(vs1.Equals(vs2));
            Assert.False(vs1.Equals(vs3));
            Assert.Equal(ValueJoinType.Concat, vs1.JoinType);
            TestSerialization(vs1);
        }

        [Fact]
        public void ValueSet_Empty_Tests()
        {
            var vs = new ValueSet();
            Assert.Empty(vs);
            Assert.Equal(ValueJoinType.List, vs.JoinType);
            TestSerialization(vs);
        }

        #endregion

        #region ExprType.Unary - UnaryExpr

        [Fact]
        public void UnaryExpr_Negate_Tests()
        {
            var expr = -Expr.Prop("Score");
            Assert.IsType<UnaryExpr>(expr);
            Assert.Equal(UnaryOperator.Nagive, (expr as UnaryExpr).Operator);
            TestSerialization(expr);
        }

        [Fact]
        public void UnaryExpr_BitwiseNot_Tests()
        {
            var expr = ~Expr.Prop("Flag");
            Assert.IsType<UnaryExpr>(expr);
            Assert.Equal(UnaryOperator.BitwiseNot, (expr as UnaryExpr).Operator);
            TestSerialization(expr);
        }

        [Fact]
        public void UnaryExpr_Distinct_Tests()
        {
            var expr = Expr.Prop("Name").Distinct();
            Assert.IsType<UnaryExpr>(expr);
            Assert.Equal(UnaryOperator.Distinct, (expr as UnaryExpr).Operator);
            TestSerialization(expr);
        }

        #endregion

        #region ExprType.Property - PropertyExpr

        [Fact]
        public void PropertyExpr_Tests()
        {
            var p1 = Expr.Prop("Name");
            var p2 = Expr.Prop("Name");
            var p3 = Expr.Prop("Age");

            Assert.True(p1.Equals(p2));
            Assert.False(p1.Equals(p3));
            Assert.Equal("Name", (p1 as PropertyExpr).PropertyName);
            Assert.Null((p1 as PropertyExpr).TableAlias);
            TestSerialization(p1);
        }

        [Fact]
        public void PropertyExpr_WithTableAlias_Tests()
        {
            var p1 = Expr.Prop("u", "Name");
            var p2 = Expr.Prop("u", "Name");
            var p3 = Expr.Prop("d", "Name");

            Assert.True(p1.Equals(p2));
            Assert.False(p1.Equals(p3));
            Assert.Equal("Name", p1.PropertyName);
            Assert.Equal("u", p1.TableAlias);
            TestSerialization(p1);
        }

        #endregion

        #region ExprType.Value - ValueExpr

        [Fact]
        public void ValueExpr_Null_Tests()
        {
            Assert.True(Expr.Null.Equals(new ValueExpr(null)));
            TestSerialization(Expr.Null);
        }

        [Fact]
        public void ValueExpr_Primitives_Tests()
        {
            var v1 = new ValueExpr(42);
            var v2 = new ValueExpr(42);
            var v3 = new ValueExpr("hello");
            var v4 = new ValueExpr(3.14);

            Assert.True(v1.Equals(v2));
            Assert.False(v1.Equals(v3));
            Assert.False(v1.Equals(v4));
            TestSerialization(v1);
            TestSerialization(v3);
            TestSerialization(v4);
        }

        [Fact]
        public void ValueExpr_Collections_Tests()
        {
            var arr = new int[] { 1, 2, 3 };
            var list = new System.Collections.Generic.List<int> { 1, 2, 3 };
            var v1 = new ValueExpr(arr);
            var v2 = new ValueExpr(list);

            Assert.True(v1.Equals(v2));
            TestSerialization(v1);
        }

        [Fact]
        public void ValueExpr_IsConst_Tests()
        {
            var constExpr = new ValueExpr(42) { IsConst = true };
            var varExpr = new ValueExpr(42) { IsConst = false };

            Assert.True(constExpr.IsConst);
            Assert.False(varExpr.IsConst);
            TestSerialization(constExpr);
            TestSerialization(varExpr);
        }

        #endregion

        #region ExprType.GenericSql - GenericSqlExpr

        [Fact]
        public void GenericSqlExpr_Tests()
        {
            GenericSqlExpr.Register("TestKey", (ctx, builder, pms, arg) => "TEST SQL");
            var g1 = GenericSqlExpr.Get("TestKey", 123);
            var g2 = GenericSqlExpr.Get("TestKey", 123);
            var g3 = GenericSqlExpr.Get("TestKey", 456);
            var g4 = GenericSqlExpr.Get("TestKey");

            Assert.True(g1.Equals(g2));
            Assert.False(g1.Equals(g3));
            Assert.False(g1.Equals(g4));
            TestSerialization(g1);
            TestSerialization(g4);
        }

        #endregion

        #region ExprType.Update - UpdateExpr

        [Fact]
        public void UpdateExpr_Tests()
        {
            var table = new TableExpr(typeof(TestUser));
            var u1 = new UpdateExpr(table, Expr.Prop("Id") == 1);
            u1.Set(("Name", Expr.Const("NewName")), ("Age", Expr.Const(30)));

            var u2 = new UpdateExpr(table, Expr.Prop("Id") == 1);
            u2.Set(("Name", Expr.Const("NewName")), ("Age", Expr.Const(30)));

            Assert.True(u1.Equals(u2));
            Assert.Equal(2, u1.Sets.Count);
            TestSerialization(u1);
        }

        [Fact]
        public void UpdateExpr_WithWhere_Tests()
        {
            var table = new TableExpr(typeof(TestUser));
            var update = new UpdateExpr(table);
            update.Where = Expr.Prop("Age") > 18;
            update.Set(("Name", Expr.Const("Updated")));

            Assert.NotNull(update.Where);
            TestSerialization(update);
        }

        #endregion

        #region ExprType.Delete - DeleteExpr

        [Fact]
        public void DeleteExpr_Tests()
        {
            var table = new TableExpr(typeof(TestUser));
            var d1 = new DeleteExpr(table, Expr.Prop("Age") > 100);
            var d2 = new DeleteExpr(table, Expr.Prop("Age") > 100);
            var d3 = new DeleteExpr(table, Expr.Prop("Age") > 50);

            Assert.True(d1.Equals(d2));
            Assert.False(d1.Equals(d3));
            Assert.NotNull(d1.Where);
            TestSerialization(d1);
        }

        [Fact]
        public void DeleteExpr_WithTableExpr_Tests()
        {
            var table = new TableExpr(typeof(TestUser)) { Alias = "u" };
            var delete = new DeleteExpr(table, Expr.Prop("u", "Age") > 100);

            Assert.NotNull(delete.Table);
            Assert.Equal("u", delete.Table.Alias);
            TestSerialization(delete);
        }

        #endregion

        #region ExprType.Where - WhereExpr

        [Fact]
        public void WhereExpr_Tests()
        {
            var from = new FromExpr(typeof(TestUser));
            var w1 = new WhereExpr(from, Expr.Prop("Age") > 18);
            var w2 = new WhereExpr(from, Expr.Prop("Age") > 18);
            var w3 = new WhereExpr(from, Expr.Prop("Age") > 20);

            Assert.True(w1.Equals(w2));
            Assert.False(w1.Equals(w3));
            TestSerialization(w1);
        }

        [Fact]
        public void WhereExpr_Chained_Tests()
        {
            var from = new FromExpr(typeof(TestUser));
            var where = new WhereExpr(from, Expr.Prop("Age") > 18);
            where.Where = where.Where.And(Expr.Prop("Name") == "John");

            Assert.IsType<AndExpr>(where.Where);
            TestSerialization(where);
        }

        #endregion

        #region ExprType.GroupBy - GroupByExpr

        [Fact]
        public void GroupByExpr_Tests()
        {
            var from = new FromExpr(typeof(TestUser));
            var g1 = new GroupByExpr(from, Expr.Prop("DeptId"), Expr.Prop("Sex"));
            var g2 = new GroupByExpr(from, Expr.Prop("DeptId"), Expr.Prop("Sex"));
            var g3 = new GroupByExpr(from, Expr.Prop("DeptId"));

            Assert.True(g1.Equals(g2));
            Assert.False(g1.Equals(g3));
            Assert.Equal(2, g1.GroupBys.Count);
            TestSerialization(g1);
        }

        [Fact]
        public void GroupByExpr_SingleColumn_Tests()
        {
            var from = new FromExpr(typeof(TestUser));
            var groupBy = new GroupByExpr(from, Expr.Prop("DeptId"));

            Assert.Single(groupBy.GroupBys);
            TestSerialization(groupBy);
        }

        #endregion

        #region ExprType.OrderBy - OrderByExpr

        [Fact]
        public void OrderByExpr_Tests()
        {
            var from = new FromExpr(typeof(TestUser));
            var o1 = new OrderByExpr(from, Expr.Prop("Age").Asc(), Expr.Prop("Name").Desc());
            var o2 = new OrderByExpr(from, Expr.Prop("Age").Asc(), Expr.Prop("Name").Desc());
            var o3 = new OrderByExpr(from, Expr.Prop("Age").Asc());

            Assert.True(o1.Equals(o2));
            Assert.False(o1.Equals(o3));
            Assert.Equal(2, o1.OrderBys.Count);
            TestSerialization(o1);
        }

        [Fact]
        public void OrderByExpr_TupleConstructor_Tests()
        {
            var from = new FromExpr(typeof(TestUser));
            var orderBy = new OrderByExpr(from, ("Age", true), ("Name", false));

            Assert.Equal(2, orderBy.OrderBys.Count);
            Assert.True(orderBy.OrderBys[0].Ascending);
            Assert.False(orderBy.OrderBys[1].Ascending);
            TestSerialization(orderBy);
        }

        #endregion

        #region ExprType.Having - HavingExpr

        [Fact]
        public void HavingExpr_Tests()
        {
            var from = new FromExpr(typeof(TestUser));
            var groupBy = new GroupByExpr(from, Expr.Prop("DeptId"));
            var h1 = new HavingExpr(groupBy, Expr.Prop("Id").Count() > 5);
            var h2 = new HavingExpr(groupBy, Expr.Prop("Id").Count() > 5);
            var h3 = new HavingExpr(groupBy, Expr.Prop("Id").Count() > 10);

            Assert.True(h1.Equals(h2));
            Assert.False(h1.Equals(h3));
            Assert.NotNull(h1.Having);
            TestSerialization(h1);
        }

        #endregion

        #region ExprType.Section - SectionExpr

        [Fact]
        public void SectionExpr_Tests()
        {
            var from = new FromExpr(typeof(TestUser));
            var s1 = new SectionExpr(from, 10, 20);
            var s2 = new SectionExpr(from, 10, 20);
            var s3 = new SectionExpr(from, 5, 20);

            Assert.True(s1.Equals(s2));
            Assert.False(s1.Equals(s3));
            Assert.Equal(10, s1.Skip);
            Assert.Equal(20, s1.Take);
            TestSerialization(s1);
        }

        [Fact]
        public void SectionExpr_Chained_Tests()
        {
            var from = new FromExpr(typeof(TestUser));
            var where = new WhereExpr(from, Expr.Prop("Age") > 18);
            var orderBy = new OrderByExpr(where, Expr.Prop("Age").Asc());
            var section = new SectionExpr(orderBy, 0, 10);

            Assert.Equal(0, section.Skip);
            Assert.Equal(10, section.Take);
            TestSerialization(section);
        }

        #endregion

        #region Complex Expr Combinations

        [Fact]
        public void ComplexExpr_UpdateWithSubquery_Tests()
        {
            var update = Expr.Update<TestUser>()
                .Where(Expr.Prop("Id").In(Expr.From<TestDepartment>().Where(Expr.Prop("Name") == "IT").Select(Expr.Prop("Id"))))
                .Set(("Name", Expr.Const("Updated")));

            Assert.NotNull(update.Where);
            TestSerialization(update);
        }

        [Fact]
        public void ComplexExpr_SelectWithAllClauses_Tests()
        {
            var query = Expr.From<TestUser>()
                .Where(Expr.Prop("Age") > 18)
                .GroupBy(Expr.Prop("DeptId"))
                .Having(Expr.Prop("Id").Count() > 1)
                .Select(Expr.Prop("DeptId"), Expr.Prop("Id").Count().As("UserCount"))
                .OrderBy(Expr.Prop("DeptId").Asc())
                .Section(0, 10);

            Assert.NotNull(query);
            TestSerialization(query);
        }

        [Fact]
        public void ComplexExpr_NestedExists_Tests()
        {
            var expr = Expr.ExistsRelated<TestDepartment>(
                Expr.Prop("Name") == "IT" &
                Expr.Exists<TestUser>(Expr.Prop("DeptId") == Expr.Prop("TestDepartment", "Id"))
            );

            Assert.NotNull(expr);
            Assert.True(expr.AutoRelated);
            TestSerialization(expr);
        }

        #endregion

        #region Equality and HashCode

        [Fact]
        public void AllExprTypes_HashCodeConsistency_Tests()
        {
            var exprs = new Expr[]
            {
                Expr.Prop("Age") == 18,
                Expr.Prop("Age") > 18,
                new AndExpr(Expr.Prop("Age") > 18, Expr.Prop("Name") == "John"),
                new OrExpr(Expr.Prop("Age") > 18, Expr.Prop("Name") == "John"),
                new NotExpr(Expr.Prop("Age") > 18),
                new ValueBinaryExpr(Expr.Prop("Age"), ValueOperator.Add, Expr.Const(1)),
                new FunctionExpr("COUNT", Expr.Prop("Id")),
                new ValueSet(ValueJoinType.List, Expr.Const(1), Expr.Const(2)),
                new UnaryExpr(UnaryOperator.Nagive, Expr.Prop("Score")),
                Expr.Const(42),
                new GenericSqlExpr("TestKey"),
                Expr.From<TestUser>(),
                new SelectExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Id")),
                new WhereExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Age") > 18),
                new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("DeptId")),
                new OrderByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Age").Asc()),
                new HavingExpr(new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("DeptId")), Expr.Prop("Id").Count() > 1),
                new SectionExpr(new FromExpr(typeof(TestUser)), 0, 10),
                new UpdateExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1).Set(("Name", Expr.Const("Test"))),
                new DeleteExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Age") > 100),
                new TableJoinExpr(new TableExpr(typeof(TestDepartment)), Expr.Prop("u", "DeptId") == Expr.Prop("d", "Id")) { JoinType = TableJoinType.Left }
            };

            foreach (var expr in exprs)
            {
                var hash1 = expr.GetHashCode();
                var json = JsonSerializer.Serialize<Expr>(expr, _jsonOptions);
                var deserialized = JsonSerializer.Deserialize<Expr>(json, _jsonOptions);
                var hash2 = deserialized.GetHashCode();
                Assert.Equal(hash1, hash2);
            }
        }

        #endregion
    }
}