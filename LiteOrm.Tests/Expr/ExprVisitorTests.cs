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

        [Fact]
        public void Visit_Func_ShortCircuitsOnFalse()
        {
            var visited = new List<string>();

            ExprVisitor.Visit(node =>
            {
                visited.Add($"{node.ExprType}");
                return false;
            }, CreateSimpleExpr());

            Assert.Single(visited);
            Assert.Equal("LogicBinary", visited[0]);
        }

        [Fact]
        public void Visit_Action_PreOrder_VisitsAllNodes()
        {
            var visited = new List<string>();

            ExprVisitor.Visit(node =>
            {
                visited.Add($"{node.ExprType}");
            }, CreateSimpleExpr(), ExprVisitOrder.PreOrder);

            Assert.Equal(3, visited.Count);
            Assert.Equal("LogicBinary", visited[0]);
            Assert.Equal("Property", visited[1]);
            Assert.Equal("Value", visited[2]);
        }

        [Fact]
        public void Visit_Action_PostOrder_VisitsAllNodes()
        {
            var visited = new List<string>();

            ExprVisitor.Visit(node =>
            {
                visited.Add($"{node.ExprType}");
            }, CreateSimpleExpr(), ExprVisitOrder.PostOrder);

            Assert.Equal(3, visited.Count);
            Assert.Equal("Property", visited[0]);
            Assert.Equal("Value", visited[1]);
            Assert.Equal("LogicBinary", visited[2]);
        }

        [Fact]
        public void Visit_Action_DefaultOrder_IsPreOrder()
        {
            var visited = new List<string>();

            ExprVisitor.Visit(node =>
            {
                visited.Add($"{node.ExprType}");
            }, CreateSimpleExpr());

            Assert.Equal(3, visited.Count);
            Assert.Equal("LogicBinary", visited[0]);
        }

        [Fact]
        public void Visit_Action_NullRoot_DoesNotThrow()
        {
            var visited = new List<string>();
            var ex = Record.Exception(() => ExprVisitor.Visit(node => visited.Add("x"), null));
            Assert.Null(ex);
            Assert.Empty(visited);
        }

        [Fact]
        public void Visit_Action_NullVisitor_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ExprVisitor.Visit((Action<Expr>)null, CreateSimpleExpr()));
        }

        [Fact]
        public void VisitAll_IExprNodeVisitor_CallsBeginVisit()
        {
            var beginVisited = new List<string>();
            var endVisited = new List<string>();
            var visitor = new TestVisitor(beginVisited.Add, endVisited.Add);

            ExprVisitor.VisitAll(visitor, CreateSimpleExpr());

            Assert.Equal(3, beginVisited.Count);
            Assert.Equal(3, endVisited.Count);
            Assert.Equal("LogicBinary", beginVisited[0]);
            Assert.Equal("Property", beginVisited[1]);
            Assert.Equal("Value", beginVisited[2]);
            Assert.Equal("Property", endVisited[0]);
            Assert.Equal("Value", endVisited[1]);
            Assert.Equal("LogicBinary", endVisited[2]);
        }

        [Fact]
        public void VisitAll_IExprNodeVisitor_NullRoot_DoesNotThrow()
        {
            var visitor = new TestVisitor(_ => { }, _ => { });
            var ex = Record.Exception(() => ExprVisitor.VisitAll(visitor, null));
            Assert.Null(ex);
        }

        [Fact]
        public void VisitAll_IExprNodeVisitor_NullVisitor_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ExprVisitor.VisitAll((IExprNodeVisitor)null, CreateSimpleExpr()));
        }

        [Fact]
        public void VisitAll_ExprValidator_PreOrder_VisitsAll()
        {
            var visited = new List<string>();

            ExprVisitor.VisitAll(new TestValidator(node =>
            {
                visited.Add($"{node.ExprType}");
                return true;
            }), CreateSimpleExpr());

            Assert.Equal(3, visited.Count);
            Assert.Equal("LogicBinary", visited[0]);
            Assert.Equal("Property", visited[1]);
            Assert.Equal("Value", visited[2]);
        }

        [Fact]
        public void VisitAll_ExprValidator_ShortCircuitsOnFalse()
        {
            var visited = new List<string>();

            ExprVisitor.VisitAll(new TestValidator(node =>
            {
                visited.Add($"{node.ExprType}");
                return false;
            }), CreateSimpleExpr());

            Assert.Single(visited);
            Assert.Equal("LogicBinary", visited[0]);
        }

        [Fact]
        public void VisitAll_ExprValidator_ReturnsTrueOnSuccess()
        {
            bool result = ExprVisitor.VisitAll(
                new TestValidator(_ => true),
                CreateSimpleExpr());

            Assert.True(result);
        }

        [Fact]
        public void VisitAll_ExprValidator_ReturnsFalseOnFailure()
        {
            bool result = ExprVisitor.VisitAll(
                new TestValidator(_ => false),
                CreateSimpleExpr());

            Assert.False(result);
        }

        [Fact]
        public void VisitAll_ExprValidator_NullRoot_ReturnsTrue()
        {
            bool result = ExprVisitor.VisitAll(new TestValidator(_ => false), null);
            Assert.True(result);
        }

        [Fact]
        public void VisitAll_ExprValidator_NullValidator_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ExprVisitor.VisitAll((ExprValidator)null, CreateSimpleExpr()));
        }

        private static LogicExpr CreateSimpleExpr()
        {
            return Expr.Prop("Age") > 18;
        }

        private class TestVisitor : IExprNodeVisitor
        {
            private readonly Action<string> _onBegin;
            private readonly Action<string> _onEnd;

            public TestVisitor(Action<string> onBegin, Action<string> onEnd)
            {
                _onBegin = onBegin;
                _onEnd = onEnd;
            }

            public void BeginVisit(Expr node) => _onBegin($"{node.ExprType}");
            public void EndVisit(Expr node) => _onEnd($"{node.ExprType}");
        }

        private class TestValidator : ExprValidator
        {
            private readonly Func<Expr, bool> _validate;

            public TestValidator(Func<Expr, bool> validate)
            {
                _validate = validate;
            }

            public override bool Validate(Expr node) => _validate(node);
        }
    }
}
