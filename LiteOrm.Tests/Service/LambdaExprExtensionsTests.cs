using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using LiteOrm.Common;
using LiteOrm.Service;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class LambdaExprExtensionsTests
    {
        [Fact]
        public void Search_WithPredicate_ForwardsToUnderlyingService()
        {
            var expected = new List<TestEntity> { new TestEntity { Id = 1, Name = "A" } };
            var service = new Mock<IEntityViewService<TestEntity>>();
            service.Setup(s => s.Search(It.IsAny<Expr>(), It.IsAny<string[]>())).Returns(expected);

            var result = service.Object.Search(x => x.Id > 0);

            Assert.Same(expected, result);
            service.Verify(s => s.Search(It.IsAny<Expr>(), It.IsAny<string[]>()), Times.Once);
        }

        [Fact]
        public void Search_WithQueryableExpression_ForwardsTableArgs()
        {
            var expected = new List<TestEntity>();
            var tableArgs = new[] { "T1", "T2" };
            var service = new Mock<IEntityViewService<TestEntity>>();
            service.Setup(s => s.Search(It.IsAny<Expr>(), tableArgs)).Returns(expected);

            var result = service.Object.Search(q => q.Where(x => x.Id > 0), tableArgs);

            Assert.Same(expected, result);
            service.Verify(s => s.Search(It.IsAny<Expr>(), tableArgs), Times.Once);
        }

        [Fact]
        public void SearchOne_WithPredicate_ForwardsToUnderlyingService()
        {
            var expected = new TestEntity { Id = 2, Name = "B" };
            var service = new Mock<IEntityViewService<TestEntity>>();
            service.Setup(s => s.SearchOne(It.IsAny<Expr>(), It.IsAny<string[]>())).Returns(expected);

            var result = service.Object.SearchOne(x => x.Id == 2);

            Assert.Same(expected, result);
        }

        [Fact]
        public void Exists_WithPredicate_ReturnsUnderlyingResult()
        {
            var service = new Mock<IEntityViewService<TestEntity>>();
            service.Setup(s => s.Exists(It.IsAny<Expr>(), It.IsAny<string[]>())).Returns(true);

            var result = service.Object.Exists(x => x.Name == "A", "Users");

            Assert.True(result);
            service.Verify(s => s.Exists(It.IsAny<Expr>(), It.Is<string[]>(x => x.Length == 1 && x[0] == "Users")), Times.Once);
        }

        [Fact]
        public void Count_WithPredicate_ReturnsUnderlyingResult()
        {
            var service = new Mock<IEntityViewService<TestEntity>>();
            service.Setup(s => s.Count(It.IsAny<Expr>(), It.IsAny<string[]>())).Returns(3);

            var result = service.Object.Count(x => x.Id > 0);

            Assert.Equal(3, result);
        }

        [Fact]
        public async Task SearchAsync_WithPredicate_ForwardsCancellationToken()
        {
            var expected = new List<TestEntity> { new TestEntity { Id = 1 } };
            var token = new CancellationTokenSource().Token;
            var service = new Mock<IEntityViewServiceAsync<TestEntity>>();
            service.Setup(s => s.SearchAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), token)).ReturnsAsync(expected);

            var result = await service.Object.SearchAsync(x => x.Id > 0, null, token);

            Assert.Same(expected, result);
            service.Verify(s => s.SearchAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), token), Times.Once);
        }

        [Fact]
        public async Task SearchOneAsync_WithQueryableExpression_ForwardsTableArgs()
        {
            var expected = new TestEntity { Id = 7 };
            var tableArgs = new[] { "Archive" };
            var service = new Mock<IEntityViewServiceAsync<TestEntity>>();
            service.Setup(s => s.SearchOneAsync(It.IsAny<Expr>(), tableArgs, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

            var result = await service.Object.SearchOneAsync(q => q.Take(1), tableArgs, CancellationToken.None);

            Assert.Same(expected, result);
            service.Verify(s => s.SearchOneAsync(It.IsAny<Expr>(), tableArgs, CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task ExistsAsync_WithPredicate_ReturnsUnderlyingResult()
        {
            var service = new Mock<IEntityViewServiceAsync<TestEntity>>();
            service.Setup(s => s.ExistsAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var result = await service.Object.ExistsAsync(x => x.Id > 0);

            Assert.True(result);
        }

        [Fact]
        public async Task CountAsync_WithPredicate_ReturnsUnderlyingResult()
        {
            var service = new Mock<IEntityViewServiceAsync<TestEntity>>();
            service.Setup(s => s.CountAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(8);

            var result = await service.Object.CountAsync(x => x.Id > 0);

            Assert.Equal(8, result);
        }

        [Fact]
        public async Task DeleteIDAsync_ForwardsAllParameters()
        {
            var service = new Mock<IEntityServiceAsync<TestEntity>>();
            var tableArgs = new[] { "Orders_2024" };
            var token = new CancellationTokenSource().Token;
            service.Setup(s => s.DeleteIDAsync(5, tableArgs, token)).ReturnsAsync(true);

            var result = await service.Object.DeleteIDAsync(5, tableArgs, token);

            Assert.True(result);
            service.Verify(s => s.DeleteIDAsync(5, tableArgs, token), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_WithPredicate_ForwardsConvertedExpression()
        {
            var service = new Mock<IEntityServiceAsync<TestEntity>>();
            service.Setup(s => s.DeleteAsync(It.IsAny<LogicExpr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(2);

            var result = await service.Object.DeleteAsync(x => x.Id > 10, new[] { "Users" }, CancellationToken.None);

            Assert.Equal(2, result);
            service.Verify(s => s.DeleteAsync(It.IsAny<LogicExpr>(), It.Is<string[]>(x => x.Length == 1 && x[0] == "Users"), CancellationToken.None), Times.Once);
        }

        [Fact]
        public void Search_WithNullExpression_ThrowsArgumentNullException()
        {
            var service = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = null!;

            Assert.Throws<ArgumentNullException>(() => service.Object.Search(expression));
        }

        [Fact]
        public void Search_WithNullService_ThrowsNullReferenceException()
        {
            IEntityViewService<TestEntity> service = null!;

            Assert.Throws<NullReferenceException>(() => service.Search(x => x.Id > 0));
        }

        public class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}
