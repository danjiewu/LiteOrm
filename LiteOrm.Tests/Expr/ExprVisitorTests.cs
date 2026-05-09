using System;
using System.Collections.Generic;
using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class ExprVisitorTests
    {
        [Fact]
        public void Visit_PreOrder_ShouldVisitParentBeforeChildren()
        {
            var visited = new List<string>();

            ExprVisitor.Visit(node =>
            {
                visited.Add($"{node.ExprType}");
                return true;
            }, CreateSimpleExpr(), ExprVisitOrder.PreOrder);

            Assert.Equal(
            [
                "LogicBinary",
                "Property",
                "Value"
            ], visited);
        }

        [Fact]
        public void Visit_PostOrder_ShouldVisitChildrenBeforeParent()
        {
            var visited = new List<string>();

            ExprVisitor.Visit(node =>
            {
                visited.Add($"{node.ExprType}");
                return true;
            }, CreateSimpleExpr(), ExprVisitOrder.PostOrder);

            Assert.Equal(
            [
                "Property",
                "Value",
                "LogicBinary"
            ], visited);
        }

        [Fact]
        public void Visit_Both_ShouldVisitEachNodeOnBothSides()
        {
            var visited = new List<string>();

            ExprVisitor.Visit(node =>
            {
                visited.Add($"{node.ExprType}");
                return true;
            }, CreateSimpleExpr());

            Assert.Equal(
            [
                "LogicBinary",
                "Property",
                "Value"
            ], visited);
        }

        private static LogicExpr CreateSimpleExpr()
        {
            return Expr.Prop("Age") > 18;
        }
    }
}
