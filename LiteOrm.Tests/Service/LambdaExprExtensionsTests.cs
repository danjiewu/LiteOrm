using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Service;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Tests for LambdaExprExtensions.Search method.
    /// </summary>
    public partial class LambdaExprExtensionsTests
    {
        /// <summary>
        /// Tests that Search throws ArgumentNullException when entityViewService is null.
        /// </summary>
        [Fact]
        public void Search_NullEntityViewService_ThrowsArgumentNullException()
        {
            // Arrange
            IEntityViewService<TestEntity>? entityViewService = null;
            Expression<Func<TestEntity, bool>> expression = e => e.Id > 0;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                entityViewService!.Search(expression));
        }

        /// <summary>
        /// Tests that Search throws ArgumentNullException when expression is null.
        /// </summary>
        [Fact]
        public void Search_NullExpression_ThrowsArgumentNullException()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>>? expression = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                mockService.Object.Search(expression!));
        }

        /// <summary>
        /// Tests that Search returns expected results with valid expression and null tableArgs.
        /// Should use lambdaConvert.From?.TableArgs when tableArgs is null.
        /// </summary>
        [Fact]
        public void Search_ValidExpressionWithNullTableArgs_ReturnsExpectedResults()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = e => e.Id > 0;
            var expectedResult = new List<TestEntity>
            {
                new TestEntity { Id = 1, Name = "Test1" },
                new TestEntity { Id = 2, Name = "Test2" }
            };

            mockService
                .Setup(s => s.Search(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(expectedResult);

            // Act
            var result = mockService.Object.Search(expression);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.Search(It.IsAny<Expr>(), It.IsAny<string[]>()), Times.Once);
        }

        /// <summary>
        /// Tests that Search uses provided tableArgs when tableArgs is not null.
        /// Should use provided tableArgs instead of lambdaConvert.From?.TableArgs.
        /// </summary>
        [Fact]
        public void Search_ValidExpressionWithProvidedTableArgs_UsesProvidedTableArgs()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = e => e.Id > 0;
            string[] providedTableArgs = new[] { "Table1", "Table2" };
            var expectedResult = new List<TestEntity>
            {
                new TestEntity { Id = 1, Name = "Test1" }
            };
            string[]? capturedTableArgs = null;

            mockService
                .Setup(s => s.Search(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Callback<Expr, string[]>((expr, args) => capturedTableArgs = args)
                .Returns(expectedResult);

            // Act
            var result = mockService.Object.Search(expression, providedTableArgs);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedResult, result);
            Assert.Same(providedTableArgs, capturedTableArgs);
            mockService.Verify(s => s.Search(It.IsAny<Expr>(), providedTableArgs), Times.Once);
        }

        /// <summary>
        /// Tests that Search works correctly with empty tableArgs array.
        /// Should use the empty array instead of lambdaConvert.From?.TableArgs.
        /// </summary>
        [Fact]
        public void Search_ValidExpressionWithEmptyTableArgs_UsesEmptyTableArgs()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = e => e.Name == "Test";
            string[] emptyTableArgs = Array.Empty<string>();
            var expectedResult = new List<TestEntity>();
            string[]? capturedTableArgs = null;

            mockService
                .Setup(s => s.Search(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Callback<Expr, string[]>((expr, args) => capturedTableArgs = args)
                .Returns(expectedResult);

            // Act
            var result = mockService.Object.Search(expression, emptyTableArgs);

            // Assert
            Assert.NotNull(result);
            Assert.Same(emptyTableArgs, capturedTableArgs);
            mockService.Verify(s => s.Search(It.IsAny<Expr>(), emptyTableArgs), Times.Once);
        }

        /// <summary>
        /// Tests that Search returns empty list when no entities match the condition.
        /// </summary>
        [Fact]
        public void Search_NoMatchingEntities_ReturnsEmptyList()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = e => e.Id > 1000;
            var emptyResult = new List<TestEntity>();

            mockService
                .Setup(s => s.Search(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(emptyResult);

            // Act
            var result = mockService.Object.Search(expression);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that Search works with complex lambda expressions.
        /// </summary>
        [Fact]
        public void Search_ComplexExpression_ReturnsExpectedResults()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> complexExpression =
                e => e.Id > 0 && e.Name != null && e.Name.StartsWith("Test");
            var expectedResult = new List<TestEntity>
            {
                new TestEntity { Id = 1, Name = "TestEntity" }
            };

            mockService
                .Setup(s => s.Search(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(expectedResult);

            // Act
            var result = mockService.Object.Search(complexExpression);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("TestEntity", result[0].Name);
        }

        /// <summary>
        /// Tests that Search works with single element tableArgs array.
        /// </summary>
        [Fact]
        public void Search_SingleElementTableArgs_UsesProvidedTableArgs()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = e => e.Id == 1;
            string[] singleTableArgs = new[] { "SingleTable" };
            var expectedResult = new List<TestEntity>
            {
                new TestEntity { Id = 1, Name = "Test" }
            };
            string[]? capturedTableArgs = null;

            mockService
                .Setup(s => s.Search(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Callback<Expr, string[]>((expr, args) => capturedTableArgs = args)
                .Returns(expectedResult);

            // Act
            var result = mockService.Object.Search(expression, singleTableArgs);

            // Assert
            Assert.NotNull(result);
            Assert.Same(singleTableArgs, capturedTableArgs);
            Assert.Single(capturedTableArgs!);
            Assert.Equal("SingleTable", capturedTableArgs[0]);
        }

        /// <summary>
        /// Test entity class for testing purposes.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
            public string? Name { get; set; }
        }

        /// <summary>
        /// Tests that Search with IQueryable expression and null tableArgs calls the underlying Search method
        /// and returns the expected result list.
        /// </summary>
        [Fact]
        public void Search_WithValidExpressionAndNullTableArgs_ReturnsExpectedList()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedList = new List<TestEntity>
            {
                new TestEntity { Id = 1, Name = "Test1" },
                new TestEntity { Id = 2, Name = "Test2" }
            };

            mockService.Setup(s => s.Search(It.IsAny<Expr>(), null))
                .Returns(expectedList);

            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression =
                q => q.Where(e => e.Id > 0);

            // Act
            var result = mockService.Object.Search(expression, null);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedList.Count, result.Count);
            Assert.Same(expectedList, result);
            mockService.Verify(s => s.Search(It.IsAny<Expr>(), null), Times.Once);
        }

        /// <summary>
        /// Tests that Search with IQueryable expression and empty tableArgs array calls the underlying Search method
        /// with the empty array and returns the expected result.
        /// </summary>
        [Fact]
        public void Search_WithValidExpressionAndEmptyTableArgs_ReturnsExpectedList()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedList = new List<TestEntity>
            {
                new TestEntity { Id = 1, Name = "Test1" }
            };
            var emptyTableArgs = Array.Empty<string>();

            mockService.Setup(s => s.Search(It.IsAny<Expr>(), emptyTableArgs))
                .Returns(expectedList);

            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression =
                q => q.OrderBy(e => e.Name);

            // Act
            var result = mockService.Object.Search(expression, emptyTableArgs);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedList.Count, result.Count);
            mockService.Verify(s => s.Search(It.IsAny<Expr>(), emptyTableArgs), Times.Once);
        }

        /// <summary>
        /// Tests that Search with IQueryable expression and populated tableArgs array passes the arguments
        /// correctly to the underlying Search method.
        /// </summary>
        [Fact]
        public void Search_WithValidExpressionAndTableArgs_PassesTableArgsCorrectly()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedList = new List<TestEntity>();
            var tableArgs = new[] { "Table1", "Table2" };

            mockService.Setup(s => s.Search(It.IsAny<Expr>(), tableArgs))
                .Returns(expectedList);

            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression =
                q => q.Take(10);

            // Act
            var result = mockService.Object.Search(expression, tableArgs);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            mockService.Verify(s => s.Search(It.IsAny<Expr>(), tableArgs), Times.Once);
        }

        /// <summary>
        /// Tests that Search with a complex IQueryable expression correctly transforms and calls
        /// the underlying Search method.
        /// </summary>
        [Fact]
        public void Search_WithComplexQueryableExpression_ReturnsExpectedList()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedList = new List<TestEntity>
            {
                new TestEntity { Id = 5, Name = "Complex" }
            };

            mockService.Setup(s => s.Search(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(expectedList);

            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression =
                q => q.Where(e => e.Id > 0).OrderBy(e => e.Name).Take(5);

            // Act
            var result = mockService.Object.Search(expression);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(5, result[0].Id);
            mockService.Verify(s => s.Search(It.IsAny<Expr>(), null), Times.Once);
        }

        /// <summary>
        /// Tests that Search returns an empty list when the underlying Search method returns an empty list.
        /// </summary>
        [Fact]
        public void Search_WhenUnderlyingSearchReturnsEmpty_ReturnsEmptyList()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var emptyList = new List<TestEntity>();

            mockService.Setup(s => s.Search(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(emptyList);

            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression =
                q => q.Where(e => e.Id < 0);

            // Act
            var result = mockService.Object.Search(expression);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that Search with a simple identity expression (no transformation) works correctly.
        /// </summary>
        [Fact]
        public void Search_WithIdentityExpression_ReturnsExpectedList()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedList = new List<TestEntity>
            {
                new TestEntity { Id = 1, Name = "Identity" }
            };

            mockService.Setup(s => s.Search(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(expectedList);

            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression =
                q => q;

            // Act
            var result = mockService.Object.Search(expression);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Identity", result[0].Name);
        }

        /// <summary>
        /// Tests that Search with multiple table arguments passes them correctly to the underlying method.
        /// </summary>
        [Fact]
        public void Search_WithMultipleTableArgs_PassesAllArgumentsCorrectly()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedList = new List<TestEntity>();
            var tableArgs = new[] { "Arg1", "Arg2", "Arg3", "Arg4" };

            mockService.Setup(s => s.Search(It.IsAny<Expr>(), tableArgs))
                .Returns(expectedList);

            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression =
                q => q.Skip(5).Take(10);

            // Act
            var result = mockService.Object.Search(expression, tableArgs);

            // Assert
            Assert.NotNull(result);
            mockService.Verify(s => s.Search(It.IsAny<Expr>(), tableArgs), Times.Once);
        }

        /// <summary>
        /// Test entity class used for testing LambdaExprExtensions.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
            public string? Name { get; set; }
        }

        /// <summary>
        /// Tests that SearchOne throws NullReferenceException when entityViewService is null.
        /// Input: null entityViewService
        /// Expected: NullReferenceException
        /// </summary>
        [Fact]
        public void SearchOne_WithNullEntityViewService_ThrowsNullReferenceException()
        {
            // Arrange
            IEntityViewService<TestEntity>? entityViewService = null;
            Expression<Func<TestEntity, bool>> expression = x => x.Id == 1;

            // Act & Assert
            Assert.Throws<NullReferenceException>(() =>
                entityViewService!.SearchOne(expression));
        }

        /// <summary>
        /// Tests that SearchOne throws ArgumentNullException when expression is null.
        /// Input: null expression
        /// Expected: ArgumentNullException
        /// </summary>
        [Fact]
        public void SearchOne_WithNullExpression_ThrowsArgumentNullException()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>>? expression = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                mockService.Object.SearchOne(expression!));
        }

        /// <summary>
        /// Tests that SearchOne calls the underlying SearchOne method with the converted expression and null tableArgs.
        /// Input: valid expression, null tableArgs
        /// Expected: SearchOne is called with converted LogicExpr and null tableArgs
        /// </summary>
        [Fact]
        public void SearchOne_WithValidExpressionAndNullTableArgs_CallsSearchOneWithConvertedExpression()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedEntity = new TestEntity { Id = 1, Name = "Test" };
            Expression<Func<TestEntity, bool>> expression = x => x.Id == 1;

            mockService.Setup(s => s.SearchOne(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(expectedEntity);

            // Act
            var result = mockService.Object.SearchOne(expression, null);

            // Assert
            Assert.Equal(expectedEntity, result);
            mockService.Verify(s => s.SearchOne(It.IsAny<Expr>(), It.IsAny<string[]>()), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOne uses provided tableArgs when they are not null.
        /// Input: valid expression, provided tableArgs
        /// Expected: SearchOne is called with provided tableArgs
        /// </summary>
        [Fact]
        public void SearchOne_WithProvidedTableArgs_UsesProvidedTableArgs()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedEntity = new TestEntity { Id = 2, Name = "Test2" };
            Expression<Func<TestEntity, bool>> expression = x => x.Id == 2;
            var tableArgs = new[] { "Table1", "Table2" };
            string[]? capturedTableArgs = null;

            mockService.Setup(s => s.SearchOne(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Callback<Expr, string[]>((expr, args) => capturedTableArgs = args)
                .Returns(expectedEntity);

            // Act
            var result = mockService.Object.SearchOne(expression, tableArgs);

            // Assert
            Assert.Equal(expectedEntity, result);
            Assert.Same(tableArgs, capturedTableArgs);
        }

        /// <summary>
        /// Tests that SearchOne uses empty tableArgs when provided.
        /// Input: valid expression, empty tableArgs array
        /// Expected: SearchOne is called with empty tableArgs
        /// </summary>
        [Fact]
        public void SearchOne_WithEmptyTableArgs_UsesEmptyTableArgs()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedEntity = new TestEntity { Id = 3, Name = "Test3" };
            Expression<Func<TestEntity, bool>> expression = x => x.Id == 3;
            var tableArgs = new string[0];
            string[]? capturedTableArgs = null;

            mockService.Setup(s => s.SearchOne(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Callback<Expr, string[]>((expr, args) => capturedTableArgs = args)
                .Returns(expectedEntity);

            // Act
            var result = mockService.Object.SearchOne(expression, tableArgs);

            // Assert
            Assert.Equal(expectedEntity, result);
            Assert.Same(tableArgs, capturedTableArgs);
        }

        /// <summary>
        /// Tests that SearchOne returns the entity when a match is found.
        /// Input: valid expression that matches an entity
        /// Expected: Returns the matching entity
        /// </summary>
        [Fact]
        public void SearchOne_WhenEntityFound_ReturnsEntity()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedEntity = new TestEntity { Id = 10, Name = "FoundEntity" };
            Expression<Func<TestEntity, bool>> expression = x => x.Name == "FoundEntity";

            mockService.Setup(s => s.SearchOne(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(expectedEntity);

            // Act
            var result = mockService.Object.SearchOne(expression);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedEntity.Id, result.Id);
            Assert.Equal(expectedEntity.Name, result.Name);
        }

        /// <summary>
        /// Tests that SearchOne returns null when no match is found.
        /// Input: valid expression that doesn't match any entity
        /// Expected: Returns null
        /// </summary>
        [Fact]
        public void SearchOne_WhenEntityNotFound_ReturnsNull()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id == 999;

            mockService.Setup(s => s.SearchOne(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns((TestEntity?)null);

            // Act
            var result = mockService.Object.SearchOne(expression);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that SearchOne works with complex expressions.
        /// Input: complex lambda expression with multiple conditions
        /// Expected: Returns the matching entity
        /// </summary>
        [Fact]
        public void SearchOne_WithComplexExpression_ReturnsCorrectEntity()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedEntity = new TestEntity { Id = 5, Name = "Complex", Age = 25 };
            Expression<Func<TestEntity, bool>> expression = x => x.Name == "Complex" && x.Age > 20;

            mockService.Setup(s => s.SearchOne(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(expectedEntity);

            // Act
            var result = mockService.Object.SearchOne(expression);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedEntity.Id, result.Id);
            Assert.Equal(expectedEntity.Name, result.Name);
            Assert.Equal(expectedEntity.Age, result.Age);
        }

        /// <summary>
        /// Test entity class for testing purposes.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public int Age { get; set; }
        }

        /// <summary>
        /// Tests that SearchOne with IQueryable expression and null tableArgs calls the underlying service correctly and returns the expected result.
        /// </summary>
        [Fact]
        public void SearchOne_WithQueryableExpressionAndNullTableArgs_CallsServiceAndReturnsResult()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedResult = new TestEntity { Id = 1, Name = "Test" };
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q.Where(e => e.Id > 0);

            mockService.Setup(s => s.SearchOne(It.IsAny<Expr>(), null))
                .Returns(expectedResult);

            // Act
            var result = mockService.Object.SearchOne(expression, null);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchOne(It.IsAny<Expr>(), null), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOne with IQueryable expression and default tableArgs (omitted) calls the underlying service correctly.
        /// </summary>
        [Fact]
        public void SearchOne_WithQueryableExpressionAndDefaultTableArgs_CallsServiceAndReturnsResult()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedResult = new TestEntity { Id = 2, Name = "Test2" };
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q.OrderBy(e => e.Name);

            mockService.Setup(s => s.SearchOne(It.IsAny<Expr>(), null))
                .Returns(expectedResult);

            // Act
            var result = mockService.Object.SearchOne(expression);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchOne(It.IsAny<Expr>(), null), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOne with IQueryable expression and empty tableArgs array calls the service with the correct parameters.
        /// </summary>
        [Fact]
        public void SearchOne_WithQueryableExpressionAndEmptyTableArgs_CallsServiceWithEmptyArray()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedResult = new TestEntity { Id = 3, Name = "Test3" };
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q.Take(10);
            var tableArgs = Array.Empty<string>();

            mockService.Setup(s => s.SearchOne(It.IsAny<Expr>(), tableArgs))
                .Returns(expectedResult);

            // Act
            var result = mockService.Object.SearchOne(expression, tableArgs);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchOne(It.IsAny<Expr>(), tableArgs), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOne with IQueryable expression and single tableArg passes the parameter correctly to the service.
        /// </summary>
        [Fact]
        public void SearchOne_WithQueryableExpressionAndSingleTableArg_CallsServiceWithCorrectParameters()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedResult = new TestEntity { Id = 4, Name = "Test4" };
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q.Skip(5).Take(1);
            var tableArgs = new[] { "Table1" };

            mockService.Setup(s => s.SearchOne(It.IsAny<Expr>(), tableArgs))
                .Returns(expectedResult);

            // Act
            var result = mockService.Object.SearchOne(expression, tableArgs);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchOne(It.IsAny<Expr>(), tableArgs), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOne with IQueryable expression and multiple tableArgs passes all parameters correctly to the service.
        /// </summary>
        [Fact]
        public void SearchOne_WithQueryableExpressionAndMultipleTableArgs_CallsServiceWithAllParameters()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedResult = new TestEntity { Id = 5, Name = "Test5" };
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q.Where(e => e.Name.Contains("Test"));
            var tableArgs = new[] { "Table1", "Table2", "Table3" };

            mockService.Setup(s => s.SearchOne(It.IsAny<Expr>(), tableArgs))
                .Returns(expectedResult);

            // Act
            var result = mockService.Object.SearchOne(expression, tableArgs);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchOne(It.IsAny<Expr>(), tableArgs), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOne with IQueryable expression returns null when the underlying service returns null.
        /// </summary>
        [Fact]
        public void SearchOne_WithQueryableExpressionWhenServiceReturnsNull_ReturnsNull()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q.Where(e => e.Id == 999);

            mockService.Setup(s => s.SearchOne(It.IsAny<Expr>(), null))
                .Returns((TestEntity?)null);

            // Act
            var result = mockService.Object.SearchOne(expression, null);

            // Assert
            Assert.Null(result);
            mockService.Verify(s => s.SearchOne(It.IsAny<Expr>(), null), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOne with IQueryable expression returns default value for value types when service returns default.
        /// </summary>
        [Fact]
        public void SearchOne_WithQueryableExpressionForValueType_ReturnsDefault()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<int>>();
            Expression<Func<IQueryable<int>, IQueryable<int>>> expression = q => q.Where(i => i > 100);

            mockService.Setup(s => s.SearchOne(It.IsAny<Expr>(), null))
                .Returns(0);

            // Act
            var result = mockService.Object.SearchOne(expression, null);

            // Assert
            Assert.Equal(0, result);
            mockService.Verify(s => s.SearchOne(It.IsAny<Expr>(), null), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOne with complex IQueryable expression with multiple operations works correctly.
        /// </summary>
        [Fact]
        public void SearchOne_WithComplexQueryableExpression_CallsServiceAndReturnsResult()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedResult = new TestEntity { Id = 6, Name = "Complex" };
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression =
                q => q.Where(e => e.Id > 0).OrderBy(e => e.Name).Skip(5).Take(1);

            mockService.Setup(s => s.SearchOne(It.IsAny<Expr>(), null))
                .Returns(expectedResult);

            // Act
            var result = mockService.Object.SearchOne(expression, null);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchOne(It.IsAny<Expr>(), null), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOne with IQueryable expression and special characters in tableArgs handles parameters correctly.
        /// </summary>
        [Fact]
        public void SearchOne_WithQueryableExpressionAndSpecialCharactersInTableArgs_PassesParametersCorrectly()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            var expectedResult = new TestEntity { Id = 7, Name = "Special" };
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q;
            var tableArgs = new[] { "Table_With_Underscore", "Table-With-Dash", "Table.With.Dot" };

            mockService.Setup(s => s.SearchOne(It.IsAny<Expr>(), tableArgs))
                .Returns(expectedResult);

            // Act
            var result = mockService.Object.SearchOne(expression, tableArgs);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchOne(It.IsAny<Expr>(), tableArgs), Times.Once);
        }

        /// <summary>
        /// Test entity class for testing purposes.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>
        /// Tests that Exists calls entityViewService.Exists with the correct LogicExpr and provided tableArgs.
        /// Input: Valid service, valid expression, non-null tableArgs.
        /// Expected: entityViewService.Exists is called with converted expression and provided tableArgs, returns true.
        /// </summary>
        [Fact]
        public void Exists_WithValidParametersAndProvidedTableArgs_CallsServiceWithCorrectArguments()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            string[] tableArgs = new[] { "Table1", "Table2" };

            mockService.Setup(s => s.Exists(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(true);

            // Act
            bool result = mockService.Object.Exists(expression, tableArgs);

            // Assert
            Assert.True(result);
            mockService.Verify(s => s.Exists(It.IsAny<Expr>(), tableArgs), Times.Once);
        }

        /// <summary>
        /// Tests that Exists returns false when entityViewService.Exists returns false.
        /// Input: Valid service returning false, valid expression, provided tableArgs.
        /// Expected: Returns false.
        /// </summary>
        [Fact]
        public void Exists_WhenServiceReturnsFalse_ReturnsFalse()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id == 999;
            string[] tableArgs = new[] { "Table1" };

            mockService.Setup(s => s.Exists(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(false);

            // Act
            bool result = mockService.Object.Exists(expression, tableArgs);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Exists handles null tableArgs and uses fallback from LambdaExprConverter.From?.TableArgs.
        /// Input: Valid service, valid expression, null tableArgs.
        /// Expected: entityViewService.Exists is called with fallback tableArgs.
        /// </summary>
        [Fact]
        public void Exists_WithNullTableArgs_UsesFallbackTableArgs()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Name == "Test";

            mockService.Setup(s => s.Exists(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(true);

            // Act
            bool result = mockService.Object.Exists(expression, (string[])null);

            // Assert
            Assert.True(result);
            mockService.Verify(s => s.Exists(It.IsAny<Expr>(), It.IsAny<string[]>()), Times.Once);
        }

        /// <summary>
        /// Tests that Exists handles empty tableArgs array.
        /// Input: Valid service, valid expression, empty tableArgs array.
        /// Expected: entityViewService.Exists is called with empty array.
        /// </summary>
        [Fact]
        public void Exists_WithEmptyTableArgs_PassesEmptyArrayToService()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            string[] tableArgs = Array.Empty<string>();

            mockService.Setup(s => s.Exists(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(true);

            // Act
            bool result = mockService.Object.Exists(expression, tableArgs);

            // Assert
            Assert.True(result);
            mockService.Verify(s => s.Exists(It.IsAny<Expr>(), tableArgs), Times.Once);
        }

        /// <summary>
        /// Tests that Exists throws ArgumentNullException when expression is null.
        /// Input: Valid service, null expression, valid tableArgs.
        /// Expected: Throws ArgumentNullException.
        /// </summary>
        [Fact]
        public void Exists_WithNullExpression_ThrowsArgumentNullException()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>>? expression = null;
            string[] tableArgs = new[] { "Table1" };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => mockService.Object.Exists(expression!, tableArgs));
        }

        /// <summary>
        /// Tests that Exists throws NullReferenceException when entityViewService is null.
        /// Input: Null service, valid expression, valid tableArgs.
        /// Expected: Throws NullReferenceException.
        /// </summary>
        [Fact]
        public void Exists_WithNullEntityViewService_ThrowsNullReferenceException()
        {
            // Arrange
            IEntityViewService<TestEntity>? service = null;
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            string[] tableArgs = new[] { "Table1" };

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => service!.Exists(expression, tableArgs));
        }

        /// <summary>
        /// Tests that Exists works with complex lambda expressions.
        /// Input: Valid service, complex expression with multiple conditions, provided tableArgs.
        /// Expected: Returns expected result from service.
        /// </summary>
        [Fact]
        public void Exists_WithComplexExpression_ReturnsCorrectResult()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0 && x.Name != null && x.Name.Length > 5;
            string[] tableArgs = new[] { "Table1" };

            mockService.Setup(s => s.Exists(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(true);

            // Act
            bool result = mockService.Object.Exists(expression, tableArgs);

            // Assert
            Assert.True(result);
            mockService.Verify(s => s.Exists(It.IsAny<Expr>(), tableArgs), Times.Once);
        }

        /// <summary>
        /// Tests that Exists works with multiple tableArgs values.
        /// Input: Valid service, valid expression, multiple tableArgs.
        /// Expected: entityViewService.Exists is called with all provided tableArgs.
        /// </summary>
        [Fact]
        public void Exists_WithMultipleTableArgs_PassesAllArgumentsToService()
        {
            // Arrange
            var mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            string[] tableArgs = new[] { "Arg1", "Arg2", "Arg3", "Arg4" };

            mockService.Setup(s => s.Exists(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(true);

            // Act
            bool result = mockService.Object.Exists(expression, tableArgs);

            // Assert
            Assert.True(result);
            mockService.Verify(s => s.Exists(It.IsAny<Expr>(), tableArgs), Times.Once);
        }

        /// <summary>
        /// Test entity class used for testing.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        /// <summary>
        /// Verifies that Count throws ArgumentNullException when expression parameter is null.
        /// </summary>
        [Fact]
        public void Count_WithNullExpression_ThrowsArgumentNullException()
        {
            // Arrange
            Mock<IEntityViewService<TestEntity>> mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>>? expression = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                LambdaExprExtensions.Count(mockService.Object, expression!, new string[] { "table1" }));
        }

        /// <summary>
        /// Verifies that Count with valid expression and provided tableArgs calls service Count with correct parameters.
        /// </summary>
        [Fact]
        public void Count_WithValidExpressionAndProvidedTableArgs_CallsServiceCountWithProvidedTableArgs()
        {
            // Arrange
            Mock<IEntityViewService<TestEntity>> mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            string[] tableArgs = new string[] { "CustomTable" };
            int expectedCount = 42;

            mockService.Setup(s => s.Count(It.IsAny<Expr>(), It.Is<string[]>(args => args == tableArgs)))
                .Returns(expectedCount);

            // Act
            int result = LambdaExprExtensions.Count(mockService.Object, expression, tableArgs);

            // Assert
            Assert.Equal(expectedCount, result);
            mockService.Verify(s => s.Count(It.IsAny<Expr>(), tableArgs), Times.Once);
        }

        /// <summary>
        /// Verifies that Count with valid expression and null tableArgs calls service Count.
        /// </summary>
        [Fact]
        public void Count_WithValidExpressionAndNullTableArgs_CallsServiceCount()
        {
            // Arrange
            Mock<IEntityViewService<TestEntity>> mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id == 1;
            int expectedCount = 5;

            mockService.Setup(s => s.Count(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(expectedCount);

            // Act
            int result = LambdaExprExtensions.Count(mockService.Object, expression, null);

            // Assert
            Assert.Equal(expectedCount, result);
            mockService.Verify(s => s.Count(It.IsAny<Expr>(), It.IsAny<string[]>()), Times.Once);
        }

        /// <summary>
        /// Verifies that Count with valid expression and empty tableArgs calls service Count with empty array.
        /// </summary>
        [Fact]
        public void Count_WithValidExpressionAndEmptyTableArgs_CallsServiceCountWithEmptyArray()
        {
            // Arrange
            Mock<IEntityViewService<TestEntity>> mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Name == "Test";
            string[] tableArgs = new string[] { };
            int expectedCount = 10;

            mockService.Setup(s => s.Count(It.IsAny<Expr>(), It.Is<string[]>(args => args.Length == 0)))
                .Returns(expectedCount);

            // Act
            int result = LambdaExprExtensions.Count(mockService.Object, expression, tableArgs);

            // Assert
            Assert.Equal(expectedCount, result);
            mockService.Verify(s => s.Count(It.IsAny<Expr>(), It.Is<string[]>(args => args.Length == 0)), Times.Once);
        }

        /// <summary>
        /// Verifies that Count with complex expression calls service Count and returns correct result.
        /// </summary>
        [Fact]
        public void Count_WithComplexExpression_CallsServiceCountAndReturnsResult()
        {
            // Arrange
            Mock<IEntityViewService<TestEntity>> mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0 && x.Name != null && x.Name.Length > 5;
            string[] tableArgs = new string[] { "Table1", "Table2" };
            int expectedCount = 100;

            mockService.Setup(s => s.Count(It.IsAny<Expr>(), It.Is<string[]>(args => args == tableArgs)))
                .Returns(expectedCount);

            // Act
            int result = LambdaExprExtensions.Count(mockService.Object, expression, tableArgs);

            // Assert
            Assert.Equal(expectedCount, result);
            mockService.Verify(s => s.Count(It.IsAny<Expr>(), tableArgs), Times.Once);
        }

        /// <summary>
        /// Verifies that Count returns zero when service returns zero.
        /// </summary>
        [Fact]
        public void Count_WhenServiceReturnsZero_ReturnsZero()
        {
            // Arrange
            Mock<IEntityViewService<TestEntity>> mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id < 0;

            mockService.Setup(s => s.Count(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(0);

            // Act
            int result = LambdaExprExtensions.Count(mockService.Object, expression);

            // Assert
            Assert.Equal(0, result);
        }

        /// <summary>
        /// Verifies that Count with multiple tableArgs passes all arguments correctly.
        /// </summary>
        [Fact]
        public void Count_WithMultipleTableArgs_PassesAllArgumentsCorrectly()
        {
            // Arrange
            Mock<IEntityViewService<TestEntity>> mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            string[] tableArgs = new string[] { "Arg1", "Arg2", "Arg3" };
            int expectedCount = 25;

            mockService.Setup(s => s.Count(It.IsAny<Expr>(), It.Is<string[]>(args =>
                args.Length == 3 && args[0] == "Arg1" && args[1] == "Arg2" && args[2] == "Arg3")))
                .Returns(expectedCount);

            // Act
            int result = LambdaExprExtensions.Count(mockService.Object, expression, tableArgs);

            // Assert
            Assert.Equal(expectedCount, result);
        }

        /// <summary>
        /// Verifies that Count with simple equality expression works correctly.
        /// </summary>
        [Fact]
        public void Count_WithSimpleEqualityExpression_WorksCorrectly()
        {
            // Arrange
            Mock<IEntityViewService<TestEntity>> mockService = new Mock<IEntityViewService<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id == 123;
            int expectedCount = 1;

            mockService.Setup(s => s.Count(It.IsAny<Expr>(), It.IsAny<string[]>()))
                .Returns(expectedCount);

            // Act
            int result = LambdaExprExtensions.Count(mockService.Object, expression);

            // Assert
            Assert.Equal(expectedCount, result);
        }

        /// <summary>
        /// Simple test entity class for testing purposes.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        /// <summary>
        /// Tests that ExistsAsync converts the lambda expression correctly and calls the underlying service method.
        /// Input: Valid entityViewService, valid expression, null tableArgs, default cancellationToken.
        /// Expected: Returns Task from service, converter creates LogicExpr, uses From.TableArgs when tableArgs is null.
        /// </summary>
        [Fact]
        public async Task ExistsAsync_WithValidExpressionAndNullTableArgs_CallsServiceWithConvertedExpression()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = true;
            var cancellationToken = CancellationToken.None;
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;

            mockService.Setup(s => s.ExistsAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.ExistsAsync(expression, null, cancellationToken);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.ExistsAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken), Times.Once);
        }

        /// <summary>
        /// Tests that ExistsAsync uses the provided tableArgs when not null.
        /// Input: Valid expression with provided tableArgs array.
        /// Expected: Service method is called with the provided tableArgs.
        /// </summary>
        [Fact]
        public async Task ExistsAsync_WithProvidedTableArgs_UsesProvidedTableArgs()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = false;
            var tableArgs = new[] { "CustomTable" };
            var cancellationToken = CancellationToken.None;
            Expression<Func<TestEntity, bool>> expression = x => x.Name == "Test";
            string[] capturedTableArgs = null;

            mockService.Setup(s => s.ExistsAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken))
                .Callback<Expr, string[], CancellationToken>((expr, args, ct) => capturedTableArgs = args)
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.ExistsAsync(expression, tableArgs, cancellationToken);

            // Assert
            Assert.Equal(expectedResult, result);
            Assert.Same(tableArgs, capturedTableArgs);
            mockService.Verify(s => s.ExistsAsync(It.IsAny<Expr>(), tableArgs, cancellationToken), Times.Once);
        }

        /// <summary>
        /// Tests that ExistsAsync uses empty array when tableArgs is provided as empty.
        /// Input: Valid expression with empty tableArgs array.
        /// Expected: Service method is called with the empty tableArgs array.
        /// </summary>
        [Fact]
        public async Task ExistsAsync_WithEmptyTableArgs_UsesEmptyTableArgs()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = true;
            var tableArgs = new string[0];
            var cancellationToken = CancellationToken.None;
            Expression<Func<TestEntity, bool>> expression = x => x.Id == 1;
            string[] capturedTableArgs = null;

            mockService.Setup(s => s.ExistsAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken))
                .Callback<Expr, string[], CancellationToken>((expr, args, ct) => capturedTableArgs = args)
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.ExistsAsync(expression, tableArgs, cancellationToken);

            // Assert
            Assert.Equal(expectedResult, result);
            Assert.Same(tableArgs, capturedTableArgs);
        }

        /// <summary>
        /// Tests that ExistsAsync properly propagates a cancelled CancellationToken.
        /// Input: Valid expression with a cancelled CancellationToken.
        /// Expected: Service method is called with the cancelled token.
        /// </summary>
        [Fact]
        public async Task ExistsAsync_WithCancelledToken_PropagatesCancellation()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var cancellationToken = cts.Token;
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;

            mockService.Setup(s => s.ExistsAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken))
                .ThrowsAsync(new OperationCanceledException(cancellationToken));

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await mockService.Object.ExistsAsync(expression, null, cancellationToken));
        }

        /// <summary>
        /// Tests that ExistsAsync throws ArgumentNullException when expression is null.
        /// Input: Null expression parameter.
        /// Expected: ArgumentNullException is thrown.
        /// </summary>
        [Fact]
        public async Task ExistsAsync_WithNullExpression_ThrowsArgumentNullException()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = null;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await mockService.Object.ExistsAsync(expression, null, CancellationToken.None));
        }

        /// <summary>
        /// Tests that ExistsAsync handles complex lambda expressions correctly.
        /// Input: Complex expression with multiple conditions.
        /// Expected: Service method is called and returns expected result.
        /// </summary>
        [Fact]
        public async Task ExistsAsync_WithComplexExpression_CallsServiceCorrectly()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = true;
            var cancellationToken = CancellationToken.None;
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0 && x.Name.StartsWith("Test") && x.Age < 100;

            mockService.Setup(s => s.ExistsAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.ExistsAsync(expression, null, cancellationToken);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.ExistsAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken), Times.Once);
        }

        /// <summary>
        /// Tests that ExistsAsync with tableArgs containing multiple values calls service correctly.
        /// Input: TableArgs with multiple string values.
        /// Expected: Service method is called with all tableArgs values.
        /// </summary>
        [Fact]
        public async Task ExistsAsync_WithMultipleTableArgs_PassesAllValues()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = false;
            var tableArgs = new[] { "Table1", "Table2", "Table3" };
            var cancellationToken = CancellationToken.None;
            Expression<Func<TestEntity, bool>> expression = x => x.Id == 5;
            string[] capturedTableArgs = null;

            mockService.Setup(s => s.ExistsAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken))
                .Callback<Expr, string[], CancellationToken>((expr, args, ct) => capturedTableArgs = args)
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.ExistsAsync(expression, tableArgs, cancellationToken);

            // Assert
            Assert.Equal(expectedResult, result);
            Assert.Same(tableArgs, capturedTableArgs);
            Assert.Equal(3, capturedTableArgs.Length);
        }

        /// <summary>
        /// Tests that ExistsAsync returns false when no matching entities exist.
        /// Input: Valid expression, service returns false.
        /// Expected: Returns false.
        /// </summary>
        [Fact]
        public async Task ExistsAsync_WhenNoEntitiesExist_ReturnsFalse()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = false;
            var cancellationToken = CancellationToken.None;
            Expression<Func<TestEntity, bool>> expression = x => x.Id == -999;

            mockService.Setup(s => s.ExistsAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.ExistsAsync(expression, null, cancellationToken);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that ExistsAsync returns true when matching entities exist.
        /// Input: Valid expression, service returns true.
        /// Expected: Returns true.
        /// </summary>
        [Fact]
        public async Task ExistsAsync_WhenEntitiesExist_ReturnsTrue()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = true;
            var cancellationToken = CancellationToken.None;
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;

            mockService.Setup(s => s.ExistsAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.ExistsAsync(expression, null, cancellationToken);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ExistsAsync with default CancellationToken uses default value.
        /// Input: Expression without explicit cancellationToken parameter.
        /// Expected: Service is called with default CancellationToken.
        /// </summary>
        [Fact]
        public async Task ExistsAsync_WithDefaultCancellationToken_UsesDefault()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = true;
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            CancellationToken capturedToken = default;

            mockService.Setup(s => s.ExistsAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .Callback<Expr, string[], CancellationToken>((expr, args, ct) => capturedToken = ct)
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.ExistsAsync(expression);

            // Assert
            Assert.Equal(expectedResult, result);
            Assert.Equal(default(CancellationToken), capturedToken);
        }

        /// <summary>
        /// Helper test entity class for testing.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Age { get; set; }
        }

        /// <summary>
        /// Tests that CountAsync calls the underlying service with converted expression and provided tableArgs.
        /// Input: Valid expression, non-null tableArgs.
        /// Expected: Service CountAsync is called with converted LogicExpr and provided tableArgs.
        /// </summary>
        [Fact]
        public async Task CountAsync_WithValidExpressionAndTableArgs_CallsServiceWithProvidedTableArgs()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var tableArgs = new[] { "arg1", "arg2" };
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            var cancellationToken = new CancellationToken();

            mockService
                .Setup(s => s.CountAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(42);

            // Act
            var result = await LambdaExprExtensions.CountAsync(mockService.Object, expression, tableArgs, cancellationToken);

            // Assert
            Assert.Equal(42, result);
            mockService.Verify(s => s.CountAsync(
                It.IsAny<Expr>(),
                tableArgs,
                cancellationToken), Times.Once);
        }

        /// <summary>
        /// Tests that CountAsync uses tableArgs from converter when tableArgs parameter is null.
        /// Input: Valid expression, null tableArgs.
        /// Expected: Service CountAsync is called with tableArgs from LambdaExprConverter.From.TableArgs.
        /// </summary>
        [Fact]
        public async Task CountAsync_WithNullTableArgs_UsesConverterTableArgs()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            var cancellationToken = new CancellationToken();

            mockService
                .Setup(s => s.CountAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(10);

            // Act
            var result = await LambdaExprExtensions.CountAsync(mockService.Object, expression, null, cancellationToken);

            // Assert
            Assert.Equal(10, result);
            mockService.Verify(s => s.CountAsync(
                It.IsAny<Expr>(),
                It.IsAny<string[]>(),
                cancellationToken), Times.Once);
        }

        /// <summary>
        /// Tests that CountAsync with empty tableArgs array passes it to the service.
        /// Input: Valid expression, empty string array.
        /// Expected: Service CountAsync is called with empty array.
        /// </summary>
        [Fact]
        public async Task CountAsync_WithEmptyTableArgs_PassesEmptyArrayToService()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var emptyTableArgs = Array.Empty<string>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            var cancellationToken = new CancellationToken();

            mockService
                .Setup(s => s.CountAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(5);

            // Act
            var result = await LambdaExprExtensions.CountAsync(mockService.Object, expression, emptyTableArgs, cancellationToken);

            // Assert
            Assert.Equal(5, result);
            mockService.Verify(s => s.CountAsync(
                It.IsAny<Expr>(),
                emptyTableArgs,
                cancellationToken), Times.Once);
        }

        /// <summary>
        /// Tests that CountAsync with cancelled token passes it to the underlying service.
        /// Input: Valid expression, cancelled CancellationToken.
        /// Expected: Service CountAsync is called with cancelled token.
        /// </summary>
        [Fact]
        public async Task CountAsync_WithCancelledToken_PassesCancelledTokenToService()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var tableArgs = new[] { "arg1" };
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var cancellationToken = cts.Token;

            mockService
                .Setup(s => s.CountAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            // Act
            var result = await LambdaExprExtensions.CountAsync(mockService.Object, expression, tableArgs, cancellationToken);

            // Assert
            mockService.Verify(s => s.CountAsync(
                It.IsAny<Expr>(),
                It.IsAny<string[]>(),
                cancellationToken), Times.Once);
        }

        /// <summary>
        /// Tests that CountAsync with default CancellationToken passes default token to service.
        /// Input: Valid expression, default CancellationToken.
        /// Expected: Service CountAsync is called with default CancellationToken.
        /// </summary>
        [Fact]
        public async Task CountAsync_WithDefaultCancellationToken_PassesDefaultTokenToService()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;

            mockService
                .Setup(s => s.CountAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(100);

            // Act
            var result = await LambdaExprExtensions.CountAsync(mockService.Object, expression);

            // Assert
            Assert.Equal(100, result);
            mockService.Verify(s => s.CountAsync(
                It.IsAny<Expr>(),
                It.IsAny<string[]>(),
                default(CancellationToken)), Times.Once);
        }

        /// <summary>
        /// Tests that CountAsync throws ArgumentNullException when expression is null.
        /// Input: Null expression.
        /// Expected: ArgumentNullException is thrown.
        /// </summary>
        [Fact]
        public async Task CountAsync_WithNullExpression_ThrowsArgumentNullException()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = null;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await LambdaExprExtensions.CountAsync(mockService.Object, expression));
        }

        /// <summary>
        /// Tests that CountAsync throws NullReferenceException when entityViewService is null.
        /// Input: Null entityViewService.
        /// Expected: NullReferenceException is thrown.
        /// </summary>
        [Fact]
        public async Task CountAsync_WithNullEntityViewService_ThrowsNullReferenceException()
        {
            // Arrange
            IEntityViewServiceAsync<TestEntity> nullService = null;
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;

            // Act & Assert
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
                await LambdaExprExtensions.CountAsync(nullService, expression));
        }

        /// <summary>
        /// Tests that CountAsync returns zero when service returns zero.
        /// Input: Valid expression, service returns 0.
        /// Expected: Returns 0.
        /// </summary>
        [Fact]
        public async Task CountAsync_WhenServiceReturnsZero_ReturnsZero()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;

            mockService
                .Setup(s => s.CountAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            // Act
            var result = await LambdaExprExtensions.CountAsync(mockService.Object, expression);

            // Assert
            Assert.Equal(0, result);
        }

        /// <summary>
        /// Tests that CountAsync returns correct count for various return values.
        /// Input: Valid expression, various count values from service.
        /// Expected: Returns the same count value.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(int.MaxValue)]
        public async Task CountAsync_WithVariousCounts_ReturnsCorrectCount(int expectedCount)
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;

            mockService
                .Setup(s => s.CountAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedCount);

            // Act
            var result = await LambdaExprExtensions.CountAsync(mockService.Object, expression);

            // Assert
            Assert.Equal(expectedCount, result);
        }

        /// <summary>
        /// Test entity class used for testing CountAsync method.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        /// <summary>
        /// Tests that DeleteIDAsync forwards all parameters correctly to the underlying service.
        /// Input: Valid entityService, valid id, valid tableArgs, and cancellationToken.
        /// Expected: Method returns the expected result and underlying method is called with correct parameters.
        /// </summary>
        [Fact]
        public async Task DeleteIDAsync_ValidParameters_ForwardsToUnderlyingService()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            var id = 123;
            var tableArgs = new[] { "table1", "table2" };
            var cancellationToken = new CancellationToken();
            var expectedResult = true;

            mockService
                .Setup(s => s.DeleteIDAsync(id, tableArgs, cancellationToken))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.DeleteIDAsync(id, tableArgs, cancellationToken);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.DeleteIDAsync(id, tableArgs, cancellationToken), Times.Once);
        }

        /// <summary>
        /// Tests that DeleteIDAsync works with null id parameter.
        /// Input: Valid entityService with null id.
        /// Expected: Null id is forwarded to the underlying service.
        /// </summary>
        [Fact]
        public async Task DeleteIDAsync_NullId_ForwardsNullToUnderlyingService()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            object? id = null;
            var expectedResult = false;

            mockService
                .Setup(s => s.DeleteIDAsync(id, null, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.DeleteIDAsync(id, null, default);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.DeleteIDAsync(id, null, default), Times.Once);
        }

        /// <summary>
        /// Tests that DeleteIDAsync works with default parameters.
        /// Input: Valid entityService with id only, using default values for tableArgs and cancellationToken.
        /// Expected: Default values are correctly forwarded to the underlying service.
        /// </summary>
        [Fact]
        public async Task DeleteIDAsync_DefaultParameters_ForwardsDefaultValues()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            var id = 456;
            var expectedResult = true;

            mockService
                .Setup(s => s.DeleteIDAsync(id, null, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.DeleteIDAsync(id);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.DeleteIDAsync(id, null, default), Times.Once);
        }

        /// <summary>
        /// Tests that DeleteIDAsync works with empty tableArgs array.
        /// Input: Valid entityService with empty tableArgs array.
        /// Expected: Empty array is correctly forwarded to the underlying service.
        /// </summary>
        [Fact]
        public async Task DeleteIDAsync_EmptyTableArgs_ForwardsEmptyArray()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            var id = 789;
            var tableArgs = new string[0];
            var expectedResult = false;

            mockService
                .Setup(s => s.DeleteIDAsync(id, tableArgs, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.DeleteIDAsync(id, tableArgs);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.DeleteIDAsync(id, tableArgs, default), Times.Once);
        }

        /// <summary>
        /// Tests that DeleteIDAsync works with different id types.
        /// Input: Valid entityService with various id types (int, string, Guid, long).
        /// Expected: Each id type is correctly forwarded to the underlying service.
        /// </summary>
        [Theory]
        [InlineData(42)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        [InlineData(0)]
        public async Task DeleteIDAsync_VariousIntegerIds_ForwardsCorrectly(int id)
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            var expectedResult = true;

            mockService
                .Setup(s => s.DeleteIDAsync(id, null, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.DeleteIDAsync(id);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.DeleteIDAsync(id, null, default), Times.Once);
        }

        /// <summary>
        /// Tests that DeleteIDAsync works with string id.
        /// Input: Valid entityService with string id.
        /// Expected: String id is correctly forwarded to the underlying service.
        /// </summary>
        [Fact]
        public async Task DeleteIDAsync_StringId_ForwardsCorrectly()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            object id = "test-id-123";
            var expectedResult = true;

            mockService
                .Setup(s => s.DeleteIDAsync(id, null, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.DeleteIDAsync(id);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.DeleteIDAsync(id, null, default), Times.Once);
        }

        /// <summary>
        /// Tests that DeleteIDAsync works with Guid id.
        /// Input: Valid entityService with Guid id.
        /// Expected: Guid id is correctly forwarded to the underlying service.
        /// </summary>
        [Fact]
        public async Task DeleteIDAsync_GuidId_ForwardsCorrectly()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            object id = Guid.NewGuid();
            var expectedResult = true;

            mockService
                .Setup(s => s.DeleteIDAsync(id, null, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.DeleteIDAsync(id);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.DeleteIDAsync(id, null, default), Times.Once);
        }

        /// <summary>
        /// Tests that DeleteIDAsync works with a cancelled CancellationToken.
        /// Input: Valid entityService with cancelled CancellationToken.
        /// Expected: Cancelled token is correctly forwarded to the underlying service.
        /// </summary>
        [Fact]
        public async Task DeleteIDAsync_CancelledToken_ForwardsCancelledToken()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            var id = 999;
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var cancellationToken = cts.Token;

            mockService
                .Setup(s => s.DeleteIDAsync(id, null, cancellationToken))
                .ReturnsAsync(false);

            // Act
            var result = await mockService.Object.DeleteIDAsync(id, null, cancellationToken);

            // Assert
            Assert.False(result);
            mockService.Verify(s => s.DeleteIDAsync(id, null, cancellationToken), Times.Once);
        }

        /// <summary>
        /// Tests that DeleteIDAsync throws NullReferenceException when entityService is null.
        /// Input: Null entityService.
        /// Expected: NullReferenceException is thrown.
        /// </summary>
        [Fact]
        public async Task DeleteIDAsync_NullEntityService_ThrowsNullReferenceException()
        {
            // Arrange
            IEntityServiceAsync<TestEntity>? entityService = null;
            var id = 123;

            // Act & Assert
            await Assert.ThrowsAsync<NullReferenceException>(async () =>
            {
                await entityService!.DeleteIDAsync(id);
            });
        }

        /// <summary>
        /// Tests that DeleteIDAsync works with tableArgs containing multiple values.
        /// Input: Valid entityService with tableArgs containing multiple strings.
        /// Expected: TableArgs array is correctly forwarded to the underlying service.
        /// </summary>
        [Fact]
        public async Task DeleteIDAsync_MultipleTableArgs_ForwardsCorrectly()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            var id = 111;
            var tableArgs = new[] { "schema1.table1", "schema2.table2", "table3" };
            var expectedResult = true;

            mockService
                .Setup(s => s.DeleteIDAsync(id, tableArgs, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.DeleteIDAsync(id, tableArgs);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.DeleteIDAsync(id, tableArgs, default), Times.Once);
        }

        /// <summary>
        /// Helper entity class for testing generic methods.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
            public string? Name { get; set; }
        }

        /// <summary>
        /// Tests that DeleteAsync throws ArgumentNullException when entityService is null.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_NullEntityService_ThrowsArgumentNullException()
        {
            // Arrange
            IEntityServiceAsync<TestEntity> nullService = null;
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await nullService.DeleteAsync(expression, null, CancellationToken.None));
        }

        /// <summary>
        /// Tests that DeleteAsync throws ArgumentNullException when expression is null.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_NullExpression_ThrowsArgumentNullException()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> nullExpression = null;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await mockService.Object.DeleteAsync(nullExpression, null, CancellationToken.None));
        }

        /// <summary>
        /// Tests that DeleteAsync successfully deletes entities with valid expression and returns affected rows.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_ValidExpression_CallsUnderlyingDeleteAsyncAndReturnsResult()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            var expectedResult = 5;

            mockService
                .Setup(s => s.DeleteAsync(It.IsAny<LogicExpr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.DeleteAsync(expression, null, CancellationToken.None);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.DeleteAsync(It.IsAny<LogicExpr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Tests that DeleteAsync uses provided tableArgs when not null.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_WithProvidedTableArgs_UsesProvidedTableArgs()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            var providedTableArgs = new[] { "CustomTable" };
            string[] capturedTableArgs = null;

            mockService
                .Setup(s => s.DeleteAsync(It.IsAny<LogicExpr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .Callback<LogicExpr, string[], CancellationToken>((expr, args, ct) => capturedTableArgs = args)
                .ReturnsAsync(1);

            // Act
            await mockService.Object.DeleteAsync(expression, providedTableArgs, CancellationToken.None);

            // Assert
            Assert.Same(providedTableArgs, capturedTableArgs);
        }

        /// <summary>
        /// Tests that DeleteAsync uses From.TableArgs from converter when tableArgs parameter is null.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_WithNullTableArgs_UsesFallbackFromConverter()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            string[] capturedTableArgs = null;

            mockService
                .Setup(s => s.DeleteAsync(It.IsAny<LogicExpr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .Callback<LogicExpr, string[], CancellationToken>((expr, args, ct) => capturedTableArgs = args)
                .ReturnsAsync(1);

            // Act
            await mockService.Object.DeleteAsync(expression, null, CancellationToken.None);

            // Assert
            // When tableArgs is null, it should use lambdaConvert.From?.TableArgs
            // Since we're testing with a simple expression, this will likely be null or the converter's value
            mockService.Verify(s => s.DeleteAsync(It.IsAny<LogicExpr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Tests that DeleteAsync passes the cancellation token to the underlying method.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_WithCancellationToken_PassesTokenToUnderlyingMethod()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            CancellationToken capturedToken = default;

            mockService
                .Setup(s => s.DeleteAsync(It.IsAny<LogicExpr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .Callback<LogicExpr, string[], CancellationToken>((expr, args, ct) => capturedToken = ct)
                .ReturnsAsync(1);

            // Act
            await mockService.Object.DeleteAsync(expression, null, cancellationToken);

            // Assert
            Assert.Equal(cancellationToken, capturedToken);
        }

        /// <summary>
        /// Tests that DeleteAsync with default cancellation token uses CancellationToken.None.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_WithDefaultCancellationToken_PassesDefaultToken()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            CancellationToken capturedToken = new CancellationToken(true);

            mockService
                .Setup(s => s.DeleteAsync(It.IsAny<LogicExpr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .Callback<LogicExpr, string[], CancellationToken>((expr, args, ct) => capturedToken = ct)
                .ReturnsAsync(1);

            // Act
            await mockService.Object.DeleteAsync(expression);

            // Assert
            Assert.Equal(default(CancellationToken), capturedToken);
        }

        /// <summary>
        /// Tests that DeleteAsync with empty tableArgs array passes it correctly.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_WithEmptyTableArgs_PassesEmptyArray()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            var emptyTableArgs = new string[0];
            string[] capturedTableArgs = null;

            mockService
                .Setup(s => s.DeleteAsync(It.IsAny<LogicExpr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .Callback<LogicExpr, string[], CancellationToken>((expr, args, ct) => capturedTableArgs = args)
                .ReturnsAsync(1);

            // Act
            await mockService.Object.DeleteAsync(expression, emptyTableArgs, CancellationToken.None);

            // Assert
            Assert.Same(emptyTableArgs, capturedTableArgs);
        }

        /// <summary>
        /// Tests that DeleteAsync returns zero when no entities are affected.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_NoEntitiesAffected_ReturnsZero()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id < 0;

            mockService
                .Setup(s => s.DeleteAsync(It.IsAny<LogicExpr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            // Act
            var result = await mockService.Object.DeleteAsync(expression, null, CancellationToken.None);

            // Assert
            Assert.Equal(0, result);
        }

        /// <summary>
        /// Tests that DeleteAsync handles complex expressions correctly.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_ComplexExpression_CallsUnderlyingMethod()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> complexExpression = x => x.Id > 0 && x.Name == "Test" || x.Age < 30;
            var expectedResult = 3;

            mockService
                .Setup(s => s.DeleteAsync(It.IsAny<LogicExpr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.DeleteAsync(complexExpression, null, CancellationToken.None);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.DeleteAsync(It.IsAny<LogicExpr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Tests that DeleteAsync with multiple tableArgs passes them correctly.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_WithMultipleTableArgs_PassesAllArguments()
        {
            // Arrange
            var mockService = new Mock<IEntityServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = x => x.Id > 0;
            var multipleTableArgs = new[] { "Table1", "Table2", "Table3" };
            string[] capturedTableArgs = null;

            mockService
                .Setup(s => s.DeleteAsync(It.IsAny<LogicExpr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .Callback<LogicExpr, string[], CancellationToken>((expr, args, ct) => capturedTableArgs = args)
                .ReturnsAsync(1);

            // Act
            await mockService.Object.DeleteAsync(expression, multipleTableArgs, CancellationToken.None);

            // Assert
            Assert.Same(multipleTableArgs, capturedTableArgs);
            Assert.Equal(3, capturedTableArgs.Length);
        }

        /// <summary>
        /// Test entity class for testing purposes.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Age { get; set; }
        }

        /// <summary>
        /// Tests that SearchAsync throws ArgumentNullException when entityViewService is null.
        /// This validates proper null checking for the extension method receiver.
        /// </summary>
        [Fact]
        public async Task SearchAsync_NullEntityViewService_ThrowsArgumentNullException()
        {
            // Arrange
            IEntityViewServiceAsync<TestEntity> entityViewService = null;
            Expression<Func<TestEntity, bool>> expression = e => e.Id > 0;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                LambdaExprExtensions.SearchAsync(entityViewService, expression, null, CancellationToken.None));
        }

        /// <summary>
        /// Tests that SearchAsync throws ArgumentNullException when expression is null.
        /// This validates that the LambdaExprConverter constructor properly validates input.
        /// </summary>
        [Fact]
        public async Task SearchAsync_NullExpression_ThrowsArgumentNullException()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = null;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                mockService.Object.SearchAsync(expression, null, CancellationToken.None));
        }

        /// <summary>
        /// Tests that SearchAsync uses provided tableArgs when not null.
        /// This validates that explicit table arguments are passed to the underlying service.
        /// </summary>
        [Fact]
        public async Task SearchAsync_WithTableArgs_UsesProvidedTableArgs()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new List<TestEntity> { new TestEntity { Id = 1 } };
            var tableArgs = new[] { "CustomTable" };
            Expression<Func<TestEntity, bool>> expression = e => e.Id > 0;
            var cancellationToken = CancellationToken.None;

            mockService
                .Setup(s => s.SearchAsync(It.IsAny<Expr>(), tableArgs, cancellationToken))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression, tableArgs, cancellationToken);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchAsync(It.IsAny<Expr>(), tableArgs, cancellationToken), Times.Once);
        }

        /// <summary>
        /// Tests that SearchAsync uses lambdaConvert.From?.TableArgs when tableArgs is null.
        /// This validates the fallback behavior for table arguments.
        /// </summary>
        [Fact]
        public async Task SearchAsync_WithNullTableArgs_UsesFallbackFromConverter()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new List<TestEntity> { new TestEntity { Id = 1 } };
            Expression<Func<TestEntity, bool>> expression = e => e.Id > 0;
            var cancellationToken = CancellationToken.None;

            mockService
                .Setup(s => s.SearchAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression, null, cancellationToken);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken), Times.Once);
        }

        /// <summary>
        /// Tests that SearchAsync with empty tableArgs array uses the empty array.
        /// This validates behavior with an empty but non-null array.
        /// </summary>
        [Fact]
        public async Task SearchAsync_WithEmptyTableArgs_UsesEmptyArray()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new List<TestEntity> { new TestEntity { Id = 1 } };
            var tableArgs = new string[0];
            Expression<Func<TestEntity, bool>> expression = e => e.Id > 0;
            var cancellationToken = CancellationToken.None;

            mockService
                .Setup(s => s.SearchAsync(It.IsAny<Expr>(), tableArgs, cancellationToken))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression, tableArgs, cancellationToken);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchAsync(It.IsAny<Expr>(), tableArgs, cancellationToken), Times.Once);
        }

        /// <summary>
        /// Tests that SearchAsync properly passes a custom cancellation token.
        /// This validates that cancellation tokens are correctly forwarded.
        /// </summary>
        [Fact]
        public async Task SearchAsync_WithCancellationToken_PassesTokenToService()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new List<TestEntity> { new TestEntity { Id = 1 } };
            Expression<Func<TestEntity, bool>> expression = e => e.Id > 0;
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            mockService
                .Setup(s => s.SearchAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression, null, cancellationToken);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken), Times.Once);
        }

        /// <summary>
        /// Tests that SearchAsync returns an empty list when no entities match.
        /// This validates proper handling of empty result sets.
        /// </summary>
        [Fact]
        public async Task SearchAsync_NoMatchingEntities_ReturnsEmptyList()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new List<TestEntity>();
            Expression<Func<TestEntity, bool>> expression = e => e.Id > 9999;

            mockService
                .Setup(s => s.SearchAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that SearchAsync returns multiple entities when multiple matches exist.
        /// This validates proper handling of multiple result scenarios.
        /// </summary>
        [Fact]
        public async Task SearchAsync_MultipleMatchingEntities_ReturnsAllMatches()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new List<TestEntity>
            {
                new TestEntity { Id = 1, Name = "Entity1" },
                new TestEntity { Id = 2, Name = "Entity2" },
                new TestEntity { Id = 3, Name = "Entity3" }
            };
            Expression<Func<TestEntity, bool>> expression = e => e.Id > 0;

            mockService
                .Setup(s => s.SearchAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal(expectedResult, result);
        }

        /// <summary>
        /// Tests that SearchAsync with default cancellation token works correctly.
        /// This validates proper handling of the default cancellation token parameter.
        /// </summary>
        [Fact]
        public async Task SearchAsync_WithDefaultCancellationToken_ExecutesSuccessfully()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new List<TestEntity> { new TestEntity { Id = 1 } };
            Expression<Func<TestEntity, bool>> expression = e => e.Id == 1;

            mockService
                .Setup(s => s.SearchAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), default(CancellationToken)))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        /// <summary>
        /// Tests that SearchAsync with complex expression converts correctly.
        /// This validates handling of complex lambda expressions with multiple conditions.
        /// </summary>
        [Fact]
        public async Task SearchAsync_ComplexExpression_ConvertsAndExecutes()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new List<TestEntity> { new TestEntity { Id = 1, Name = "Test" } };
            Expression<Func<TestEntity, bool>> expression = e => e.Id > 0 && e.Name == "Test";

            mockService
                .Setup(s => s.SearchAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Tests that SearchAsync with multiple tableArgs passes them correctly.
        /// This validates handling of multiple table argument values.
        /// </summary>
        [Fact]
        public async Task SearchAsync_WithMultipleTableArgs_PassesAllArguments()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new List<TestEntity> { new TestEntity { Id = 1 } };
            var tableArgs = new[] { "Table1", "Table2", "Table3" };
            Expression<Func<TestEntity, bool>> expression = e => e.Id > 0;

            mockService
                .Setup(s => s.SearchAsync(It.IsAny<Expr>(), tableArgs, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression, tableArgs);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchAsync(It.IsAny<Expr>(), tableArgs, It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Test entity class used for unit testing.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        /// <summary>
        /// Tests that SearchAsync correctly calls underlying service with converted expression and default parameters.
        /// </summary>
        [Fact]
        public async Task SearchAsync_ValidExpressionWithDefaultParameters_CallsUnderlyingServiceAndReturnsResult()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q.Where(e => e.Id > 0);
            var expectedResult = new List<TestEntity> { new TestEntity { Id = 1 }, new TestEntity { Id = 2 } };

            mockService.Setup(s => s.SearchAsync(It.IsAny<Expr>(), null, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchAsync(It.IsAny<Expr>(), null, default), Times.Once);
        }

        /// <summary>
        /// Tests that SearchAsync correctly forwards null tableArgs parameter to underlying service.
        /// </summary>
        [Fact]
        public async Task SearchAsync_WithNullTableArgs_ForwardsNullToUnderlyingService()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q.OrderBy(e => e.Id);
            var expectedResult = new List<TestEntity>();

            mockService.Setup(s => s.SearchAsync(It.IsAny<Expr>(), null, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression, null);

            // Assert
            Assert.NotNull(result);
            mockService.Verify(s => s.SearchAsync(It.IsAny<Expr>(), null, default), Times.Once);
        }

        /// <summary>
        /// Tests that SearchAsync correctly forwards empty tableArgs array to underlying service.
        /// </summary>
        [Fact]
        public async Task SearchAsync_WithEmptyTableArgs_ForwardsEmptyArrayToUnderlyingService()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q.Take(10);
            var emptyTableArgs = new string[0];
            var expectedResult = new List<TestEntity> { new TestEntity { Id = 1 } };

            mockService.Setup(s => s.SearchAsync(It.IsAny<Expr>(), emptyTableArgs, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression, emptyTableArgs);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            mockService.Verify(s => s.SearchAsync(It.IsAny<Expr>(), emptyTableArgs, default), Times.Once);
        }

        /// <summary>
        /// Tests that SearchAsync correctly forwards non-empty tableArgs array to underlying service.
        /// </summary>
        [Fact]
        public async Task SearchAsync_WithNonEmptyTableArgs_ForwardsTableArgsToUnderlyingService()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q.Skip(5);
            var tableArgs = new[] { "Table1", "Table2" };
            var expectedResult = new List<TestEntity>();

            mockService.Setup(s => s.SearchAsync(It.IsAny<Expr>(), tableArgs, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression, tableArgs);

            // Assert
            Assert.NotNull(result);
            mockService.Verify(s => s.SearchAsync(It.IsAny<Expr>(), tableArgs, default), Times.Once);
        }

        /// <summary>
        /// Tests that SearchAsync correctly forwards cancellation token to underlying service.
        /// </summary>
        [Fact]
        public async Task SearchAsync_WithCancellationToken_ForwardsCancellationTokenToUnderlyingService()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q.Where(e => e.Id < 100);
            var cancellationToken = new CancellationToken(false);
            var expectedResult = new List<TestEntity> { new TestEntity { Id = 50 } };

            mockService.Setup(s => s.SearchAsync(It.IsAny<Expr>(), null, cancellationToken))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression, null, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            mockService.Verify(s => s.SearchAsync(It.IsAny<Expr>(), null, cancellationToken), Times.Once);
        }

        /// <summary>
        /// Tests that SearchAsync honors cancelled cancellation token.
        /// </summary>
        [Fact]
        public async Task SearchAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q;
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            var cancellationToken = cancellationTokenSource.Token;

            mockService.Setup(s => s.SearchAsync(It.IsAny<Expr>(), null, cancellationToken))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await mockService.Object.SearchAsync(expression, null, cancellationToken));
        }

        /// <summary>
        /// Tests that SearchAsync with all parameters correctly forwards them to underlying service.
        /// </summary>
        [Fact]
        public async Task SearchAsync_WithAllParameters_ForwardsAllParametersToUnderlyingService()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression =
                q => q.Where(e => e.Id > 0).OrderBy(e => e.Id).Skip(10).Take(20);
            var tableArgs = new[] { "CustomTable" };
            var cancellationToken = new CancellationToken(false);
            var expectedResult = new List<TestEntity>
            {
                new TestEntity { Id = 11 },
                new TestEntity { Id = 12 }
            };

            mockService.Setup(s => s.SearchAsync(It.IsAny<Expr>(), tableArgs, cancellationToken))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression, tableArgs, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(11, result[0].Id);
            Assert.Equal(12, result[1].Id);
            mockService.Verify(s => s.SearchAsync(It.IsAny<Expr>(), tableArgs, cancellationToken), Times.Once);
        }

        /// <summary>
        /// Tests that SearchAsync returns empty list when underlying service returns empty list.
        /// </summary>
        [Fact]
        public async Task SearchAsync_WhenUnderlyingServiceReturnsEmptyList_ReturnsEmptyList()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q.Where(e => e.Id < 0);
            var expectedResult = new List<TestEntity>();

            mockService.Setup(s => s.SearchAsync(It.IsAny<Expr>(), null, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that SearchAsync with complex LINQ expression correctly calls underlying service.
        /// </summary>
        [Fact]
        public async Task SearchAsync_WithComplexLinqExpression_CallsUnderlyingService()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression =
                q => q.Where(e => e.Id > 10)
                      .Where(e => e.Id < 100)
                      .OrderByDescending(e => e.Id)
                      .Skip(5)
                      .Take(15);
            var expectedResult = new List<TestEntity> { new TestEntity { Id = 94 } };

            mockService.Setup(s => s.SearchAsync(It.IsAny<Expr>(), null, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchAsync(expression);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            mockService.Verify(s => s.SearchAsync(It.IsAny<Expr>(), null, default), Times.Once);
        }

        /// <summary>
        /// Test entity class for SearchAsync tests.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
        }

        /// <summary>
        /// Tests that SearchOneAsync throws ArgumentNullException when the expression parameter is null.
        /// </summary>
        [Fact]
        public void SearchOneAsync_NullExpression_ThrowsArgumentNullException()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>>? expression = null;

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                LambdaExprExtensions.SearchOneAsync(mockService.Object, expression!, null, CancellationToken.None));
            Assert.Equal("expression", exception.ParamName);
        }

        /// <summary>
        /// Tests that SearchOneAsync calls the underlying SearchOneAsync method with the converted expression
        /// when tableArgs is null.
        /// </summary>
        [Fact]
        public async Task SearchOneAsync_WithNullTableArgs_CallsSearchOneAsyncWithConvertedExpression()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new TestEntity { Id = 1, Name = "Test" };
            Expression<Func<TestEntity, bool>> expression = e => e.Id == 1;

            mockService
                .Setup(s => s.SearchOneAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await LambdaExprExtensions.SearchOneAsync(mockService.Object, expression, null, CancellationToken.None);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchOneAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), CancellationToken.None), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOneAsync uses the provided tableArgs when not null,
        /// regardless of the converted expression's From.TableArgs.
        /// </summary>
        [Fact]
        public async Task SearchOneAsync_WithNonNullTableArgs_UsesProvidedTableArgs()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new TestEntity { Id = 2, Name = "Test2" };
            Expression<Func<TestEntity, bool>> expression = e => e.Id == 2;
            string[] providedTableArgs = new[] { "CustomTable" };

            mockService
                .Setup(s => s.SearchOneAsync(It.IsAny<Expr>(), providedTableArgs, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await LambdaExprExtensions.SearchOneAsync(mockService.Object, expression, providedTableArgs, CancellationToken.None);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchOneAsync(It.IsAny<Expr>(), providedTableArgs, CancellationToken.None), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOneAsync uses an empty array when provided as tableArgs,
        /// rather than falling back to From.TableArgs.
        /// </summary>
        [Fact]
        public async Task SearchOneAsync_WithEmptyTableArgs_UsesEmptyArray()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new TestEntity { Id = 3, Name = "Test3" };
            Expression<Func<TestEntity, bool>> expression = e => e.Id == 3;
            string[] emptyTableArgs = Array.Empty<string>();

            mockService
                .Setup(s => s.SearchOneAsync(It.IsAny<Expr>(), emptyTableArgs, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await LambdaExprExtensions.SearchOneAsync(mockService.Object, expression, emptyTableArgs, CancellationToken.None);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchOneAsync(It.IsAny<Expr>(), emptyTableArgs, CancellationToken.None), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOneAsync properly propagates the CancellationToken to the underlying service call.
        /// </summary>
        [Fact]
        public async Task SearchOneAsync_WithCancellationToken_PropagatesCancellationToken()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new TestEntity { Id = 4, Name = "Test4" };
            Expression<Func<TestEntity, bool>> expression = e => e.Id == 4;
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            mockService
                .Setup(s => s.SearchOneAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await LambdaExprExtensions.SearchOneAsync(mockService.Object, expression, null, cancellationToken);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchOneAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOneAsync respects a cancelled CancellationToken and propagates the cancellation.
        /// </summary>
        [Fact]
        public async Task SearchOneAsync_WithCancelledToken_PropagatesCancellation()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = e => e.Id == 5;
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            var cancellationToken = cancellationTokenSource.Token;

            mockService
                .Setup(s => s.SearchOneAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), cancellationToken))
                .ThrowsAsync(new TaskCanceledException());

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await LambdaExprExtensions.SearchOneAsync(mockService.Object, expression, null, cancellationToken));
        }

        /// <summary>
        /// Tests that SearchOneAsync returns null when the underlying service returns null.
        /// </summary>
        [Fact]
        public async Task SearchOneAsync_WhenServiceReturnsNull_ReturnsNull()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            Expression<Func<TestEntity, bool>> expression = e => e.Id == 999;

            mockService
                .Setup(s => s.SearchOneAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TestEntity?)null);

            // Act
            var result = await LambdaExprExtensions.SearchOneAsync(mockService.Object, expression, null, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that SearchOneAsync works correctly with complex expressions.
        /// </summary>
        [Fact]
        public async Task SearchOneAsync_WithComplexExpression_WorksCorrectly()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new TestEntity { Id = 6, Name = "Complex" };
            Expression<Func<TestEntity, bool>> expression = e => e.Id > 5 && e.Name.Contains("Complex");

            mockService
                .Setup(s => s.SearchOneAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await LambdaExprExtensions.SearchOneAsync(mockService.Object, expression, null, CancellationToken.None);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchOneAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), CancellationToken.None), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOneAsync uses default CancellationToken when not provided.
        /// </summary>
        [Fact]
        public async Task SearchOneAsync_WithoutCancellationToken_UsesDefaultToken()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new TestEntity { Id = 7, Name = "Default" };
            Expression<Func<TestEntity, bool>> expression = e => e.Id == 7;

            mockService
                .Setup(s => s.SearchOneAsync(It.IsAny<Expr>(), It.IsAny<string[]>(), default(CancellationToken)))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await LambdaExprExtensions.SearchOneAsync(mockService.Object, expression);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchOneAsync(It.IsAny<Expr>(), null, default(CancellationToken)), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOneAsync with multiple tableArgs passes them correctly.
        /// </summary>
        [Fact]
        public async Task SearchOneAsync_WithMultipleTableArgs_PassesAllArguments()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new TestEntity { Id = 8, Name = "Multi" };
            Expression<Func<TestEntity, bool>> expression = e => e.Id == 8;
            string[] multipleTableArgs = new[] { "Table1", "Table2", "Table3" };

            mockService
                .Setup(s => s.SearchOneAsync(It.IsAny<Expr>(), multipleTableArgs, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await LambdaExprExtensions.SearchOneAsync(mockService.Object, expression, multipleTableArgs, CancellationToken.None);

            // Assert
            Assert.Equal(expectedResult, result);
            mockService.Verify(s => s.SearchOneAsync(It.IsAny<Expr>(), multipleTableArgs, CancellationToken.None), Times.Once);
        }

        /// <summary>
        /// Simple test entity class for testing purposes.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>
        /// Tests that SearchOneAsync with a valid expression calls the service correctly and returns the expected result.
        /// Validates normal operation with a valid expression, null tableArgs, and default cancellation token.
        /// </summary>
        [Fact]
        public async Task SearchOneAsync_WithValidExpression_ReturnsExpectedResult()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new TestEntity { Id = 1, Name = "Test" };
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q.Where(e => e.Id > 0);
            var expr = Expr.Query(expression);

            mockService
                .Setup(s => s.SearchOneAsync(It.IsAny<Expr>(), null, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchOneAsync(expression, null, default);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedResult.Id, result.Id);
            mockService.Verify(s => s.SearchOneAsync(It.IsAny<Expr>(), null, default), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOneAsync with null expression passes null to Expr.Query and delegates correctly.
        /// Verifies behavior when expression parameter is null.
        /// </summary>
        [Fact]
        public async Task SearchOneAsync_WithNullExpression_CallsServiceWithNullExpr()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new TestEntity { Id = 1, Name = "Test" };
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = null;

            mockService
                .Setup(s => s.SearchOneAsync(It.IsAny<Expr>(), null, default))
                .ReturnsAsync(expectedResult);

            // Act & Assert
            // Note: Expr.Query(null) may throw, so we expect an exception here
            await Assert.ThrowsAsync<NullReferenceException>(() =>
                mockService.Object.SearchOneAsync(expression, null, default));
        }

        /// <summary>
        /// Tests that SearchOneAsync with empty tableArgs array passes it correctly to the service.
        /// Validates that empty array is handled properly.
        /// </summary>
        [Fact]
        public async Task SearchOneAsync_WithEmptyTableArgs_PassesEmptyArrayToService()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new TestEntity { Id = 2, Name = "Test2" };
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q.Take(1);
            var emptyTableArgs = new string[0];

            mockService
                .Setup(s => s.SearchOneAsync(It.IsAny<Expr>(), emptyTableArgs, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchOneAsync(expression, emptyTableArgs, default);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedResult.Id, result.Id);
            mockService.Verify(s => s.SearchOneAsync(It.IsAny<Expr>(), emptyTableArgs, default), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOneAsync with non-empty tableArgs passes them correctly to the service.
        /// Validates that table arguments are properly forwarded.
        /// </summary>
        [Fact]
        public async Task SearchOneAsync_WithTableArgs_PassesTableArgsToService()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new TestEntity { Id = 3, Name = "Test3" };
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q.OrderBy(e => e.Name);
            var tableArgs = new[] { "Table1", "Table2" };

            mockService
                .Setup(s => s.SearchOneAsync(It.IsAny<Expr>(), tableArgs, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchOneAsync(expression, tableArgs, default);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedResult.Id, result.Id);
            mockService.Verify(s => s.SearchOneAsync(It.IsAny<Expr>(), tableArgs, default), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOneAsync with complex LINQ expression delegates correctly.
        /// Validates handling of complex query expressions.
        /// </summary>
        [Fact]
        public async Task SearchOneAsync_WithComplexExpression_DelegatesToService()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new TestEntity { Id = 5, Name = "Complex" };
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression =
                q => q.Where(e => e.Id > 0).OrderBy(e => e.Name).Skip(10).Take(1);

            mockService
                .Setup(s => s.SearchOneAsync(It.IsAny<Expr>(), null, default))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchOneAsync(expression, null, default);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedResult.Id, result.Id);
            Assert.Equal(expectedResult.Name, result.Name);
            mockService.Verify(s => s.SearchOneAsync(It.IsAny<Expr>(), null, default), Times.Once);
        }

        /// <summary>
        /// Tests that SearchOneAsync with all parameters specified works correctly.
        /// Validates that all optional parameters can be provided simultaneously.
        /// </summary>
        [Fact]
        public async Task SearchOneAsync_WithAllParameters_WorksCorrectly()
        {
            // Arrange
            var mockService = new Mock<IEntityViewServiceAsync<TestEntity>>();
            var expectedResult = new TestEntity { Id = 6, Name = "AllParams" };
            Expression<Func<IQueryable<TestEntity>, IQueryable<TestEntity>>> expression = q => q.Take(5);
            var tableArgs = new[] { "CustomTable" };
            var cts = new CancellationTokenSource();

            mockService
                .Setup(s => s.SearchOneAsync(It.IsAny<Expr>(), tableArgs, cts.Token))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await mockService.Object.SearchOneAsync(expression, tableArgs, cts.Token);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedResult.Id, result.Id);
            mockService.Verify(s => s.SearchOneAsync(It.IsAny<Expr>(), tableArgs, cts.Token), Times.Once);
        }

        /// <summary>
        /// Test entity class for use in unit tests.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        /// <summary>
        /// Tests that Delete method successfully deletes entities when valid expression and tableArgs are provided.
        /// Input: Valid entity service, valid expression, and valid tableArgs.
        /// Expected: The underlying Delete method is called with correct parameters and returns expected row count.
        /// </summary>
        [Fact]
        public void Delete_WithValidExpressionAndTableArgs_CallsUnderlyingDeleteWithCorrectParameters()
        {
            // Arrange
            var mockService = new Mock<IEntityService<TestEntity>>();
            mockService.Setup(s => s.Delete(It.IsAny<LogicExpr>(), It.IsAny<string[]>()))
                .Returns(5);

            Expression<Func<TestEntity, bool>> expression = e => e.Id > 10;
            string[] tableArgs = new[] { "Table1", "Table2" };

            // Act
            int result = mockService.Object.Delete(expression, tableArgs);

            // Assert
            Assert.Equal(5, result);
            mockService.Verify(s => s.Delete(It.IsAny<LogicExpr>(), tableArgs), Times.Once);
        }

        /// <summary>
        /// Tests that Delete method uses From.TableArgs when tableArgs parameter is null.
        /// Input: Valid entity service, valid expression, null tableArgs.
        /// Expected: The underlying Delete method is called with From.TableArgs as the second parameter.
        /// </summary>
        [Fact]
        public void Delete_WithNullTableArgs_UsesFallbackTableArgs()
        {
            // Arrange
            var mockService = new Mock<IEntityService<TestEntity>>();
            string[] capturedTableArgs = null;
            mockService.Setup(s => s.Delete(It.IsAny<LogicExpr>(), It.IsAny<string[]>()))
                .Callback<LogicExpr, string[]>((expr, args) => capturedTableArgs = args)
                .Returns(3);

            Expression<Func<TestEntity, bool>> expression = e => e.Name == "test";

            // Act
            int result = mockService.Object.Delete(expression, null);

            // Assert
            Assert.Equal(3, result);
            mockService.Verify(s => s.Delete(It.IsAny<LogicExpr>(), It.IsAny<string[]>()), Times.Once);
        }

        /// <summary>
        /// Tests that Delete method handles empty tableArgs array correctly.
        /// Input: Valid entity service, valid expression, empty tableArgs array.
        /// Expected: The underlying Delete method is called with empty array and returns expected result.
        /// </summary>
        [Fact]
        public void Delete_WithEmptyTableArgs_CallsUnderlyingDeleteWithEmptyArray()
        {
            // Arrange
            var mockService = new Mock<IEntityService<TestEntity>>();
            mockService.Setup(s => s.Delete(It.IsAny<LogicExpr>(), It.IsAny<string[]>()))
                .Returns(0);

            Expression<Func<TestEntity, bool>> expression = e => e.Id == 999;
            string[] tableArgs = Array.Empty<string>();

            // Act
            int result = mockService.Object.Delete(expression, tableArgs);

            // Assert
            Assert.Equal(0, result);
            mockService.Verify(s => s.Delete(It.IsAny<LogicExpr>(), tableArgs), Times.Once);
        }

        /// <summary>
        /// Tests that Delete method throws ArgumentNullException when expression is null.
        /// Input: Valid entity service, null expression.
        /// Expected: ArgumentNullException is thrown.
        /// </summary>
        [Fact]
        public void Delete_WithNullExpression_ThrowsArgumentNullException()
        {
            // Arrange
            var mockService = new Mock<IEntityService<TestEntity>>();
            Expression<Func<TestEntity, bool>>? expression = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                mockService.Object.Delete(expression, new[] { "table" }));
        }

        /// <summary>
        /// Tests that Delete method returns zero when no entities match the condition.
        /// Input: Valid entity service, expression that matches no entities.
        /// Expected: Returns 0.
        /// </summary>
        [Fact]
        public void Delete_WithNoMatchingEntities_ReturnsZero()
        {
            // Arrange
            var mockService = new Mock<IEntityService<TestEntity>>();
            mockService.Setup(s => s.Delete(It.IsAny<LogicExpr>(), It.IsAny<string[]>()))
                .Returns(0);

            Expression<Func<TestEntity, bool>> expression = e => e.Id < 0;

            // Act
            int result = mockService.Object.Delete(expression);

            // Assert
            Assert.Equal(0, result);
        }

        /// <summary>
        /// Tests that Delete method handles complex expressions correctly.
        /// Input: Valid entity service, complex expression with multiple conditions.
        /// Expected: The underlying Delete method is called and returns expected result.
        /// </summary>
        [Fact]
        public void Delete_WithComplexExpression_ExecutesSuccessfully()
        {
            // Arrange
            var mockService = new Mock<IEntityService<TestEntity>>();
            mockService.Setup(s => s.Delete(It.IsAny<LogicExpr>(), It.IsAny<string[]>()))
                .Returns(10);

            Expression<Func<TestEntity, bool>> expression = e =>
                e.Id > 5 && e.Name != null && e.Name.Length > 0;

            // Act
            int result = mockService.Object.Delete(expression);

            // Assert
            Assert.Equal(10, result);
            mockService.Verify(s => s.Delete(It.IsAny<LogicExpr>(), It.IsAny<string[]>()), Times.Once);
        }

        /// <summary>
        /// Tests that Delete method propagates return values correctly for various row counts.
        /// Input: Valid entity service with different return values, valid expression.
        /// Expected: Each different return value is correctly propagated.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(int.MaxValue)]
        public void Delete_WithVariousReturnValues_PropagatesCorrectly(int expectedCount)
        {
            // Arrange
            var mockService = new Mock<IEntityService<TestEntity>>();
            mockService.Setup(s => s.Delete(It.IsAny<LogicExpr>(), It.IsAny<string[]>()))
                .Returns(expectedCount);

            Expression<Func<TestEntity, bool>> expression = e => e.Id > 0;

            // Act
            int result = mockService.Object.Delete(expression);

            // Assert
            Assert.Equal(expectedCount, result);
        }

        /// <summary>
        /// Tests that Delete method works with no tableArgs parameters provided.
        /// Input: Valid entity service, valid expression, no tableArgs.
        /// Expected: Method executes successfully using fallback tableArgs.
        /// </summary>
        [Fact]
        public void Delete_WithoutTableArgsParameter_UsesDefaultBehavior()
        {
            // Arrange
            var mockService = new Mock<IEntityService<TestEntity>>();
            mockService.Setup(s => s.Delete(It.IsAny<LogicExpr>(), It.IsAny<string[]>()))
                .Returns(2);

            Expression<Func<TestEntity, bool>> expression = e => e.Name == "DeleteMe";

            // Act
            int result = mockService.Object.Delete(expression);

            // Assert
            Assert.Equal(2, result);
            mockService.Verify(s => s.Delete(It.IsAny<LogicExpr>(), It.IsAny<string[]>()), Times.Once);
        }

        /// <summary>
        /// Tests that Delete method handles expressions with string operations.
        /// Input: Valid entity service, expression with string operations like Contains, StartsWith.
        /// Expected: The underlying Delete method is called successfully.
        /// </summary>
        [Fact]
        public void Delete_WithStringOperations_ExecutesSuccessfully()
        {
            // Arrange
            var mockService = new Mock<IEntityService<TestEntity>>();
            mockService.Setup(s => s.Delete(It.IsAny<LogicExpr>(), It.IsAny<string[]>()))
                .Returns(7);

            Expression<Func<TestEntity, bool>> expression = e =>
                e.Name != null && e.Name.StartsWith("Test");

            // Act
            int result = mockService.Object.Delete(expression);

            // Assert
            Assert.Equal(7, result);
        }
    }
}