using System;
using System.Collections.Generic;
using System.Linq;
using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class ExprExtensionsTests
    {
        [Fact]
        public void Operator_Equals_CreatesLogicBinaryExpr()
        {
            var result = Expr.Prop("Age") == new ValueExpr(18);

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.Equal, bin.Operator);
        }

        [Fact]
        public void Operator_NotEquals_CreatesLogicBinaryExpr()
        {
            var result = Expr.Prop("Age") != new ValueExpr(18);

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.NotEqual, bin.Operator);
        }

        [Fact]
        public void Operator_GreaterThan_CreatesLogicBinaryExpr()
        {
            var result = Expr.Prop("Age") > new ValueExpr(18);

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.GreaterThan, bin.Operator);
        }

        [Fact]
        public void Operator_LessThan_CreatesLogicBinaryExpr()
        {
            var result = Expr.Prop("Age") < new ValueExpr(18);

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.LessThan, bin.Operator);
        }

        [Fact]
        public void Operator_GreaterThanOrEqual_CreatesLogicBinaryExpr()
        {
            var result = Expr.Prop("Age") >= new ValueExpr(18);

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.GreaterThanOrEqual, bin.Operator);
        }

        [Fact]
        public void Operator_LessThanOrEqual_CreatesLogicBinaryExpr()
        {
            var result = Expr.Prop("Age") <= new ValueExpr(18);

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.LessThanOrEqual, bin.Operator);
        }

        [Fact]
        public void Operator_Plus_CreatesValueBinaryExpr()
        {
            var result = Expr.Prop("Price") + new ValueExpr(10);

            var bin = Assert.IsType<ValueBinaryExpr>(result);
            Assert.Equal(ValueOperator.Add, bin.Operator);
        }

        [Fact]
        public void Operator_Minus_CreatesValueBinaryExpr()
        {
            var result = Expr.Prop("Price") - new ValueExpr(10);

            var bin = Assert.IsType<ValueBinaryExpr>(result);
            Assert.Equal(ValueOperator.Subtract, bin.Operator);
        }

        [Fact]
        public void Operator_Multiply_CreatesValueBinaryExpr()
        {
            var result = Expr.Prop("Price") * new ValueExpr(2);

            var bin = Assert.IsType<ValueBinaryExpr>(result);
            Assert.Equal(ValueOperator.Multiply, bin.Operator);
        }

        [Fact]
        public void Operator_Divide_CreatesValueBinaryExpr()
        {
            var result = Expr.Prop("Price") / new ValueExpr(2);

            var bin = Assert.IsType<ValueBinaryExpr>(result);
            Assert.Equal(ValueOperator.Divide, bin.Operator);
        }

        [Fact]
        public void Operator_Modulo_CreatesValueBinaryExpr()
        {
            var result = Expr.Prop("Value") % new ValueExpr(3);

            var bin = Assert.IsType<ValueBinaryExpr>(result);
            Assert.Equal(ValueOperator.Modulo, bin.Operator);
        }

        [Fact]
        public void Operator_UnaryMinus_CreatesUnaryExpr()
        {
            var result = -Expr.Prop("Value");

            var ue = Assert.IsType<UnaryExpr>(result);
            Assert.Equal(UnaryOperator.Nagive, ue.Operator);
        }

        [Fact]
        public void Operator_BitwiseNot_CreatesUnaryExpr()
        {
            var result = ~Expr.Prop("Value");

            var ue = Assert.IsType<UnaryExpr>(result);
            Assert.Equal(UnaryOperator.BitwiseNot, ue.Operator);
        }

        [Fact]
        public void ImplicitConversion_String_ToValueTypeExpr()
        {
            ValueTypeExpr expr = "hello";

            var ve = Assert.IsType<ValueExpr>(expr);
            Assert.Equal("hello", ve.Value);
            Assert.False(ve.IsConst);
        }

        [Fact]
        public void ImplicitConversion_Int_ToValueTypeExpr()
        {
            ValueTypeExpr expr = 42;

            var ve = Assert.IsType<ValueExpr>(expr);
            Assert.Equal(42, ve.Value);
        }

        [Fact]
        public void ImplicitConversion_Long_ToValueTypeExpr()
        {
            ValueTypeExpr expr = 42L;

            var ve = Assert.IsType<ValueExpr>(expr);
            Assert.Equal(42L, ve.Value);
        }

        [Fact]
        public void ImplicitConversion_Bool_ToValueTypeExpr()
        {
            ValueTypeExpr expr = true;

            var ve = Assert.IsType<ValueExpr>(expr);
            Assert.Equal(true, ve.Value);
        }

        [Fact]
        public void ImplicitConversion_Double_ToValueTypeExpr()
        {
            ValueTypeExpr expr = 3.14;

            var ve = Assert.IsType<ValueExpr>(expr);
            Assert.Equal(3.14, ve.Value);
        }

        [Fact]
        public void Operator_And_CombinesIntoAndExpr()
        {
            var left = Expr.Prop("A") == new ValueExpr(1);
            var right = Expr.Prop("B") == new ValueExpr(2);

            var result = left & right;

            var and = Assert.IsType<AndExpr>(result);
            Assert.Equal(2, and.Count);
        }

        [Fact]
        public void Operator_Or_CombinesIntoOrExpr()
        {
            var left = Expr.Prop("A") == new ValueExpr(1);
            var right = Expr.Prop("B") == new ValueExpr(2);

            var result = left | right;

            var or = Assert.IsType<OrExpr>(result);
            Assert.Equal(2, or.Count);
        }

        [Fact]
        public void Operator_Not_WrapsInNotExpr()
        {
            var operand = Expr.Prop("A") == new ValueExpr(1);

            var result = !operand;

            var not = Assert.IsType<NotExpr>(result);
            Assert.Same(operand, not.Operand);
        }

        [Fact]
        public void Operator_And_LeftNull_ReturnsRight()
        {
            LogicExpr left = null;
            var right = Expr.Prop("B") == new ValueExpr(2);

            var result = left & right;

            Assert.Same(right, result);
        }

        [Fact]
        public void Operator_And_RightNull_ReturnsLeft()
        {
            var left = Expr.Prop("A") == new ValueExpr(1);
            LogicExpr right = null;

            var result = left & right;

            Assert.Same(left, result);
        }

        [Fact]
        public void Operator_Or_LeftNull_ReturnsRight()
        {
            LogicExpr left = null;
            var right = Expr.Prop("B") == new ValueExpr(2);

            var result = left | right;

            Assert.Same(right, result);
        }

        [Fact]
        public void Operator_Or_RightNull_ReturnsLeft()
        {
            var left = Expr.Prop("A") == new ValueExpr(1);
            LogicExpr right = null;

            var result = left | right;

            Assert.Same(left, result);
        }

        [Fact]
        public void And_CombinesTwoLogicExpr()
        {
            var left = Expr.Prop("A") == 1;
            var right = Expr.Prop("B") == 2;

            var result = left.And(right);

            var and = Assert.IsType<AndExpr>(result);
            Assert.Equal(2, and.Count);
        }

        [Fact]
        public void And_LeftNull_ReturnsRight()
        {
            LogicExpr left = null;
            var right = Expr.Prop("B") == 2;

            var result = left.And(right);

            Assert.Same(right, result);
        }

        [Fact]
        public void Or_CombinesTwoLogicExpr()
        {
            var left = Expr.Prop("A") == 1;
            var right = Expr.Prop("B") == 2;

            var result = left.Or(right);

            var or = Assert.IsType<OrExpr>(result);
            Assert.Equal(2, or.Count);
        }

        [Fact]
        public void Or_LeftNull_ReturnsRight()
        {
            LogicExpr left = null;
            var right = Expr.Prop("B") == 2;

            var result = left.Or(right);

            Assert.Same(right, result);
        }

        [Fact]
        public void Not_WrapsInNotExpr()
        {
            var operand = Expr.Prop("A") == 1;

            var result = operand.Not();

            Assert.IsType<NotExpr>(result);
        }

        [Fact]
        public void Not_DoubleNot_ReturnsOriginal()
        {
            var operand = Expr.Prop("A") == 1;

            var result = operand.Not().Not();

            Assert.Same(operand, result);
        }

        [Fact]
        public void Equal_CreatesLogicBinaryExpr()
        {
            var result = Expr.Prop("Id").Equal(new ValueExpr(1));

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.Equal, bin.Operator);
        }

        [Fact]
        public void NotEqual_CreatesLogicBinaryExpr()
        {
            var result = Expr.Prop("Id").NotEqual(new ValueExpr(1));

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.NotEqual, bin.Operator);
        }

        [Fact]
        public void In_IEnumerable_OperatorIsIn()
        {
            var result = Expr.Prop("Id").In(new List<int> { 1, 2, 3 });

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.In, bin.Operator);
        }

        [Fact]
        public void In_Params_OperatorIsIn()
        {
            var result = Expr.Prop("Id").In(1, 2, 3);

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.In, bin.Operator);
        }

        [Fact]
        public void Between_CombinesGteAndLte()
        {
            var result = Expr.Prop("Age").Between(new ValueExpr(18), new ValueExpr(65));

            var and = Assert.IsType<AndExpr>(result);
            Assert.Equal(2, and.Count);
        }

        [Fact]
        public void Like_CreatesLogicBinaryExpr()
        {
            var result = Expr.Prop("Name").Like("%test%");

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.Like, bin.Operator);
        }

        [Fact]
        public void Contains_CreatesLogicBinaryExpr()
        {
            var result = Expr.Prop("Name").Contains("test");

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.Contains, bin.Operator);
        }

        [Fact]
        public void StartsWith_CreatesLogicBinaryExpr()
        {
            var result = Expr.Prop("Name").StartsWith("abc");

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.StartsWith, bin.Operator);
        }

        [Fact]
        public void EndsWith_CreatesLogicBinaryExpr()
        {
            var result = Expr.Prop("Name").EndsWith("xyz");

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.EndsWith, bin.Operator);
        }

        [Fact]
        public void RegexpLike_CreatesLogicBinaryExpr()
        {
            var result = Expr.Prop("Name").RegexpLike(@"\d+");

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.RegexpLike, bin.Operator);
        }

        [Fact]
        public void IsNull_CreatesEqualsNullExpr()
        {
            var result = Expr.Prop("Name").IsNull();

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.Equal, bin.Operator);
            Assert.Equal(Expr.Null, bin.Right);
        }

        [Fact]
        public void IsNotNull_CreatesNotEqualsNullExpr()
        {
            var result = Expr.Prop("Name").IsNotNull();

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.NotEqual, bin.Operator);
            Assert.Equal(Expr.Null, bin.Right);
        }

        [Fact]
        public void IfNull_CreatesFunctionExpr()
        {
            var result = Expr.Prop("Name").IfNull(new ValueExpr("default"));

            var func = Assert.IsType<FunctionExpr>(result);
            Assert.Equal("IfNull", func.FunctionName);
        }

        [Fact]
        public void Asc_CreatesOrderByItemExpr()
        {
            var result = Expr.Prop("Name").Asc();

            var item = Assert.IsType<OrderByItemExpr>(result);
            Assert.True(item.Ascending);
        }

        [Fact]
        public void Desc_CreatesOrderByItemExpr()
        {
            var result = Expr.Prop("Name").Desc();

            var item = Assert.IsType<OrderByItemExpr>(result);
            Assert.False(item.Ascending);
        }

        [Fact]
        public void Count_CreatesFunctionExpr()
        {
            var result = Expr.Prop("Id").Count();

            var func = Assert.IsType<FunctionExpr>(result);
            Assert.Equal("COUNT", func.FunctionName);
            Assert.True(func.IsAggregate);
        }

        [Fact]
        public void Count_WithDistinct_CreatesDistinctFunctionExpr()
        {
            var result = Expr.Prop("Id").Count(true);

            var func = Assert.IsType<FunctionExpr>(result);
            Assert.Equal("COUNT", func.FunctionName);
            Assert.Single(func.Args);
            var distinct = Assert.IsType<UnaryExpr>(func.Args[0]);
            Assert.Equal(UnaryOperator.Distinct, distinct.Operator);
        }

        [Fact]
        public void Sum_CreatesFunctionExpr()
        {
            var result = Expr.Prop("Price").Sum();

            var func = Assert.IsType<FunctionExpr>(result);
            Assert.Equal("SUM", func.FunctionName);
            Assert.True(func.IsAggregate);
        }

        [Fact]
        public void Avg_CreatesFunctionExpr()
        {
            var result = Expr.Prop("Price").Avg();

            var func = Assert.IsType<FunctionExpr>(result);
            Assert.Equal("AVG", func.FunctionName);
            Assert.True(func.IsAggregate);
        }

        [Fact]
        public void Max_CreatesFunctionExpr()
        {
            var result = Expr.Prop("Price").Max();

            var func = Assert.IsType<FunctionExpr>(result);
            Assert.Equal("MAX", func.FunctionName);
            Assert.True(func.IsAggregate);
        }

        [Fact]
        public void Min_CreatesFunctionExpr()
        {
            var result = Expr.Prop("Price").Min();

            var func = Assert.IsType<FunctionExpr>(result);
            Assert.Equal("MIN", func.FunctionName);
            Assert.True(func.IsAggregate);
        }

        [Fact]
        public void Concat_CreatesValueSet()
        {
            var result = Expr.Prop("A").Concat(Expr.Prop("B"));

            var set = Assert.IsType<ValueSet>(result);
            Assert.Equal(ValueJoinType.Concat, set.JoinType);
        }

        [Fact]
        public void As_OnValueTypeExpr_CreatesSelectItemExpr()
        {
            var result = Expr.Prop("Name").As("UserName");

            var item = Assert.IsType<SelectItemExpr>(result);
            Assert.Equal("UserName", item.Alias);
        }

        [Fact]
        public void AndIf_ConditionTrue_AddsCondition()
        {
            var expr = Expr.Prop("A") == 1;
            var right = Expr.Prop("B") == 2;

            var result = expr.AndIf(true, right);

            var and = Assert.IsType<AndExpr>(result);
            Assert.Equal(2, and.Count);
        }

        [Fact]
        public void AndIf_ConditionFalse_ReturnsOriginal()
        {
            var expr = Expr.Prop("A") == 1;
            var right = Expr.Prop("B") == 2;

            var result = expr.AndIf(false, right);

            Assert.Same(expr, result);
        }

        [Fact]
        public void OrIf_ConditionTrue_AddsCondition()
        {
            var expr = Expr.Prop("A") == 1;
            var right = Expr.Prop("B") == 2;

            var result = expr.OrIf(true, right);

            var or = Assert.IsType<OrExpr>(result);
            Assert.Equal(2, or.Count);
        }

        [Fact]
        public void OrIf_ConditionFalse_ReturnsOriginal()
        {
            var expr = Expr.Prop("A") == 1;
            var right = Expr.Prop("B") == 2;

            var result = expr.OrIf(false, right);

            Assert.Same(expr, result);
        }

        [Fact]
        public void Distinct_CreatesUnaryExpr()
        {
            var result = Expr.Prop("Id").Distinct();

            var ue = Assert.IsType<UnaryExpr>(result);
            Assert.Equal(UnaryOperator.Distinct, ue.Operator);
        }

        [Fact]
        public void Lower_CreatesFunctionExpr()
        {
            var vbe = new ValueBinaryExpr(Expr.Prop("A"), ValueOperator.Concat, new ValueExpr("B"));
            var result = vbe.Lower();

            var func = Assert.IsType<FunctionExpr>(result);
            Assert.Equal("LOWER", func.FunctionName);
        }

        [Fact]
        public void Upper_CreatesFunctionExpr()
        {
            var vbe = new ValueBinaryExpr(Expr.Prop("A"), ValueOperator.Concat, new ValueExpr("B"));
            var result = vbe.Upper();

            var func = Assert.IsType<FunctionExpr>(result);
            Assert.Equal("UPPER", func.FunctionName);
        }

        [Fact]
        public void AsValue_WithValueTypeExpr_ReturnsSame()
        {
            var expr = Expr.Prop("Name");

            var result = expr.AsValue();

            Assert.Same(expr, result);
        }

        [Fact]
        public void AsLogic_WithLogicExpr_ReturnsSame()
        {
            var expr = Expr.Prop("A") == 1;

            var result = expr.AsLogic();

            Assert.Same(expr, result);
        }

        [Fact]
        public void AsLogic_WithValueTypeExpr_ConvertsToNotZero()
        {
            var result = Expr.Prop("Id").AsLogic();

            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.NotEqual, bin.Operator);
        }

        [Fact]
        public void AsLogic_WithUnsupportedType_Throws()
        {
            var expr = new UpdateExpr();

            Assert.Throws<NotSupportedException>(() => expr.AsLogic());
        }

        [Fact]
        public void Union_CombinesSelectExpr()
        {
            var s1 = new SelectExpr(null, Expr.Prop("Id").As("Id"));
            var s2 = new SelectExpr(null, Expr.Prop("Name").As("Name"));

            var result = s1.Union(s2);

            Assert.Single(result.NextSelects);
            Assert.Equal(SelectSetType.Union, result.NextSelects[0].SetType);
        }

        [Fact]
        public void UnionAll_CombinesSelectExpr()
        {
            var s1 = new SelectExpr(null, Expr.Prop("Id").As("Id"));
            var s2 = new SelectExpr(null, Expr.Prop("Name").As("Name"));

            var result = s1.UnionAll(s2);

            Assert.Single(result.NextSelects);
            Assert.Equal(SelectSetType.UnionAll, result.NextSelects[0].SetType);
        }

        [Fact]
        public void Intersect_CombinesSelectExpr()
        {
            var s1 = new SelectExpr(null, Expr.Prop("Id").As("Id"));
            var s2 = new SelectExpr(null, Expr.Prop("Name").As("Name"));

            var result = s1.Intersect(s2);

            Assert.Single(result.NextSelects);
            Assert.Equal(SelectSetType.Intersect, result.NextSelects[0].SetType);
        }

        [Fact]
        public void Intersect_NullSource_Throws()
        {
            var s2 = new SelectExpr(null, Expr.Prop("Name").As("Name"));

            Assert.Throws<ArgumentNullException>(() => ((SelectExpr)null).Intersect(s2));
        }

        [Fact]
        public void Except_CombinesSelectExpr()
        {
            var s1 = new SelectExpr(null, Expr.Prop("Id").As("Id"));
            var s2 = new SelectExpr(null, Expr.Prop("Name").As("Name"));

            var result = s1.Except(s2);

            Assert.Single(result.NextSelects);
            Assert.Equal(SelectSetType.Except, result.NextSelects[0].SetType);
        }

        [Fact]
        public void Except_NullSource_Throws()
        {
            var s2 = new SelectExpr(null, Expr.Prop("Name").As("Name"));

            Assert.Throws<ArgumentNullException>(() => ((SelectExpr)null).Except(s2));
        }

        [Fact]
        public void Where_OnSourceAnchor_CreatesWhereExpr()
        {
            var source = new FromExpr(typeof(TestEntity));
            var condition = Expr.Prop("Id") == 1;

            var result = source.Where(condition);

            Assert.IsType<WhereExpr>(result);
            Assert.Same(source, result.Source);
        }

        [Fact]
        public void GroupBy_OnGroupByAnchor_CreatesGroupByExpr()
        {
            var source = new FromExpr(typeof(TestEntity));
            var result = source.GroupBy(Expr.Prop("DeptId"));

            Assert.IsType<GroupByExpr>(result);
        }

        [Fact]
        public void OrderBy_OnOrderByAnchor_CreatesOrderByExpr()
        {
            var source = new FromExpr(typeof(TestEntity));
            var result = source.OrderBy(Expr.Prop("Name").Asc());

            Assert.IsType<OrderByExpr>(result);
        }

        [Fact]
        public void Section_OnSectionAnchor_CreatesSectionExpr()
        {
            var source = new FromExpr(typeof(TestEntity));
            var result = source.Section(0, 10);

            var sec = Assert.IsType<SectionExpr>(result);
            Assert.Equal(0, sec.Skip);
            Assert.Equal(10, sec.Take);
        }

        [Fact]
        public void Select_OnSelectAnchor_CreatesSelectExpr()
        {
            var source = new FromExpr(typeof(TestEntity));
            var result = source.Select(Expr.Prop("Name").As("Name"));

            Assert.IsType<SelectExpr>(result);
        }

        [Fact]
        public void Having_OnHavingAnchor_CreatesHavingExpr()
        {
            var source = new GroupByExpr(new FromExpr(typeof(TestEntity)));
            var result = source.Having(Expr.Prop("Count") > 1);

            Assert.IsType<HavingExpr>(result);
        }

        [Fact]
        public void Where_OnUpdateExpr_ReturnsUpdateExprWithWhere()
        {
            var update = Expr.Update(typeof(TestEntity));
            var result = update.Where(Expr.Prop("Id") == 1);

            Assert.IsType<UpdateExpr>(result);
            Assert.NotNull(((UpdateExpr)result).Where);
        }

        [Fact]
        public void Where_OnDeleteExpr_ReturnsDeleteExprWithWhere()
        {
            var delete = Expr.Delete(typeof(TestEntity));
            var result = delete.Where(Expr.Prop("Id") == 1);

            Assert.IsType<DeleteExpr>(result);
            Assert.NotNull(((DeleteExpr)result).Where);
        }

        [Fact]
        public void Over_WithPartitionBy_SetsFunctionName()
        {
            var func = Expr.Prop("Price").Sum();

            var result = func.Over(Expr.Prop("DeptId"));

            Assert.Equal("Over", result.FunctionName);
        }

        private class TestEntity { }
    }
}
