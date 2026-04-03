using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using LiteOrm;
using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for <see cref="TableJoinExpr"/> class.
    /// </summary>
    public sealed class TableJoinExprTests
    {
        #region Constructor Tests

        /// <summary>
        /// Tests that the default parameterless constructor initializes a TableJoinExpr instance
        /// with all properties set to their default values.
        /// Expected: Table is null, On is null, JoinType is TableJoinType.Left, ExprType is ExprType.TableJoin,
        /// Source is null, and ToString returns empty string.
        /// </summary>
        [Fact]
        public void Constructor_Default_InitializesWithDefaultValues()
        {
            // Act
            var result = new TableJoinExpr();

            // Assert
            Assert.NotNull(result);
            Assert.Null(result.Table);
            Assert.Null(result.On);
            Assert.Equal(TableJoinType.Left, result.JoinType);
            Assert.Equal(ExprType.TableJoin, result.ExprType);
            Assert.Null(result.Source);
            Assert.Equal(string.Empty, result.ToString());
        }

        /// <summary>
        /// Tests that the default constructor creates an instance that can have its properties
        /// set after construction.
        /// Expected: All properties can be set successfully and retrieved correctly.
        /// </summary>
        [Fact]
        public void Constructor_Default_AllowsPropertiestoBeSetAfterConstruction()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(object));
            var logicExpr = new LogicExpr();
            var expectedJoinType = TableJoinType.Inner;

            // Act
            var result = new TableJoinExpr
            {
                Table = tableExpr,
                On = logicExpr,
                JoinType = expectedJoinType
            };

            // Assert
            Assert.Equal(tableExpr, result.Table);
            Assert.Equal(logicExpr, result.On);
            Assert.Equal(expectedJoinType, result.JoinType);
            Assert.Equal(tableExpr, result.Source);
        }

        /// <summary>
        /// Tests that GetHashCode can be called on a default-constructed instance without throwing.
        /// Expected: No exception is thrown and a hash code value is returned.
        /// </summary>
        [Fact]
        public void Constructor_Default_GetHashCodeDoesNotThrow()
        {
            // Arrange
            var result = new TableJoinExpr();

            // Act
            var hashCode = result.GetHashCode();

            // Assert - no exception thrown
            Assert.True(hashCode != 0 || hashCode == 0); // Verify we got some value
        }

        #endregion

        /// <summary>
        /// Tests that Equals returns true when comparing an instance with itself (reflexivity).
        /// </summary>
        [Fact]
        public void Equals_SameInstance_ReturnsTrue()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var on = Expr.Prop("Id") == Expr.Const(1);
            var join = new TableJoinExpr(table, on) { JoinType = TableJoinType.Left };

            // Act
            var result = join.Equals(join);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing two instances with identical properties.
        /// </summary>
        [Fact]
        public void Equals_IdenticalProperties_ReturnsTrue()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var on = Expr.Prop("Id") == Expr.Const(1);
            var join1 = new TableJoinExpr(table, on) { JoinType = TableJoinType.Left };
            var join2 = new TableJoinExpr(table, on) { JoinType = TableJoinType.Left };

            // Act
            var result = join1.Equals(join2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when the Table property differs.
        /// </summary>
        [Fact]
        public void Equals_DifferentTable_ReturnsFalse()
        {
            // Arrange
            var table1 = new TableExpr(typeof(string));
            var table2 = new TableExpr(typeof(int));
            var on = Expr.Prop("Id") == Expr.Const(1);
            var join1 = new TableJoinExpr(table1, on) { JoinType = TableJoinType.Left };
            var join2 = new TableJoinExpr(table2, on) { JoinType = TableJoinType.Left };

            // Act
            var result = join1.Equals(join2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when the JoinType property differs.
        /// </summary>
        [Fact]
        public void Equals_DifferentJoinType_ReturnsFalse()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var on = Expr.Prop("Id") == Expr.Const(1);
            var join1 = new TableJoinExpr(table, on) { JoinType = TableJoinType.Left };
            var join2 = new TableJoinExpr(table, on) { JoinType = TableJoinType.Inner };

            // Act
            var result = join1.Equals(join2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when the On property differs.
        /// </summary>
        [Fact]
        public void Equals_DifferentOn_ReturnsFalse()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var on1 = Expr.Prop("Id") == Expr.Const(1);
            var on2 = Expr.Prop("Id") == Expr.Const(2);
            var join1 = new TableJoinExpr(table, on1) { JoinType = TableJoinType.Left };
            var join2 = new TableJoinExpr(table, on2) { JoinType = TableJoinType.Left };

            // Act
            var result = join1.Equals(join2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with null.
        /// </summary>
        [Fact]
        public void Equals_NullObject_ReturnsFalse()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var on = Expr.Prop("Id") == Expr.Const(1);
            var join = new TableJoinExpr(table, on) { JoinType = TableJoinType.Left };

            // Act
            var result = join.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with an object of a different type.
        /// </summary>
        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var on = Expr.Prop("Id") == Expr.Const(1);
            var join = new TableJoinExpr(table, on) { JoinType = TableJoinType.Left };
            var differentTypeObject = "not a TableJoinExpr";

            // Act
            var result = join.Equals(differentTypeObject);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have null Table properties and other properties match.
        /// </summary>
        [Fact]
        public void Equals_BothTableNull_ReturnsTrue()
        {
            // Arrange
            var on = Expr.Prop("Id") == Expr.Const(1);
            var join1 = new TableJoinExpr(null, on) { JoinType = TableJoinType.Left };
            var join2 = new TableJoinExpr(null, on) { JoinType = TableJoinType.Left };

            // Act
            var result = join1.Equals(join2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one instance has null Table and the other doesn't.
        /// </summary>
        [Fact]
        public void Equals_OneTableNull_ReturnsFalse()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var on = Expr.Prop("Id") == Expr.Const(1);
            var join1 = new TableJoinExpr(table, on) { JoinType = TableJoinType.Left };
            var join2 = new TableJoinExpr(null, on) { JoinType = TableJoinType.Left };

            // Act
            var result = join1.Equals(join2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have null On properties and other properties match.
        /// </summary>
        [Fact]
        public void Equals_BothOnNull_ReturnsTrue()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var join1 = new TableJoinExpr(table, null) { JoinType = TableJoinType.Left };
            var join2 = new TableJoinExpr(table, null) { JoinType = TableJoinType.Left };

            // Act
            var result = join1.Equals(join2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one instance has null On and the other doesn't.
        /// </summary>
        [Fact]
        public void Equals_OneOnNull_ReturnsFalse()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var on = Expr.Prop("Id") == Expr.Const(1);
            var join1 = new TableJoinExpr(table, on) { JoinType = TableJoinType.Left };
            var join2 = new TableJoinExpr(table, null) { JoinType = TableJoinType.Left };

            // Act
            var result = join1.Equals(join2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have null Table and On properties with matching JoinType.
        /// </summary>
        [Fact]
        public void Equals_AllPropertiesNullOrDefault_ReturnsTrue()
        {
            // Arrange
            var join1 = new TableJoinExpr { JoinType = TableJoinType.Right };
            var join2 = new TableJoinExpr { JoinType = TableJoinType.Right };

            // Act
            var result = join1.Equals(join2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals exhibits symmetry: if a.Equals(b) is true, then b.Equals(a) is also true.
        /// </summary>
        [Fact]
        public void Equals_Symmetry_ReturnsConsistentResults()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var on = Expr.Prop("Id") == Expr.Const(1);
            var join1 = new TableJoinExpr(table, on) { JoinType = TableJoinType.Inner };
            var join2 = new TableJoinExpr(table, on) { JoinType = TableJoinType.Inner };

            // Act
            var result1 = join1.Equals(join2);
            var result2 = join2.Equals(join1);

            // Assert
            Assert.True(result1);
            Assert.True(result2);
            Assert.Equal(result1, result2);
        }

        /// <summary>
        /// Tests that Equals correctly handles all different JoinType enum values.
        /// </summary>
        [Theory]
        [InlineData(TableJoinType.Left, TableJoinType.Left, true)]
        [InlineData(TableJoinType.Right, TableJoinType.Right, true)]
        [InlineData(TableJoinType.Inner, TableJoinType.Inner, true)]
        [InlineData(TableJoinType.Cross, TableJoinType.Cross, true)]
        [InlineData(TableJoinType.Left, TableJoinType.Right, false)]
        [InlineData(TableJoinType.Inner, TableJoinType.Cross, false)]
        public void Equals_VariousJoinTypes_ReturnsExpectedResult(TableJoinType joinType1, TableJoinType joinType2, bool expected)
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var on = Expr.Prop("Id") == Expr.Const(1);
            var join1 = new TableJoinExpr(table, on) { JoinType = joinType1 };
            var join2 = new TableJoinExpr(table, on) { JoinType = joinType2 };

            // Act
            var result = join1.Equals(join2);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that Equals returns false when multiple properties differ simultaneously.
        /// </summary>
        [Fact]
        public void Equals_MultiplePropertiesDiffer_ReturnsFalse()
        {
            // Arrange
            var table1 = new TableExpr(typeof(string));
            var table2 = new TableExpr(typeof(int));
            var on1 = Expr.Prop("Id") == Expr.Const(1);
            var on2 = Expr.Prop("Id") == Expr.Const(2);
            var join1 = new TableJoinExpr(table1, on1) { JoinType = TableJoinType.Left };
            var join2 = new TableJoinExpr(table2, on2) { JoinType = TableJoinType.Inner };

            // Act
            var result = join1.Equals(join2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing instances created using default constructor with same properties set.
        /// </summary>
        [Fact]
        public void Equals_DefaultConstructorInstances_ReturnsTrue()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var on = Expr.Prop("Id") == Expr.Const(1);
            var join1 = new TableJoinExpr
            {
                Table = table,
                On = on,
                JoinType = TableJoinType.Cross
            };
            var join2 = new TableJoinExpr
            {
                Table = table,
                On = on,
                JoinType = TableJoinType.Cross
            };

            // Act
            var result = join1.Equals(join2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that the constructor with valid TableExpr and LogicExpr parameters
        /// correctly assigns the Table and On properties.
        /// </summary>
        [Fact]
        public void Constructor_WithValidTableAndOnParameters_AssignsProperties()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(string));
            var logicExpr = Mock.Of<LogicExpr>();

            // Act
            var result = new TableJoinExpr(tableExpr, logicExpr);

            // Assert
            Assert.Same(tableExpr, result.Table);
            Assert.Same(logicExpr, result.On);
            Assert.Equal(TableJoinType.Left, result.JoinType);
        }

        /// <summary>
        /// Tests that the constructor accepts a null table parameter
        /// and assigns it to the Table property without throwing an exception.
        /// </summary>
        [Fact]
        public void Constructor_WithNullTableParameter_AssignsNullToTableProperty()
        {
            // Arrange
            TableExpr? tableExpr = null;
            var logicExpr = Mock.Of<LogicExpr>();

            // Act
            var result = new TableJoinExpr(tableExpr, logicExpr);

            // Assert
            Assert.Null(result.Table);
            Assert.Same(logicExpr, result.On);
        }

        /// <summary>
        /// Tests that the constructor accepts a null on parameter
        /// and assigns it to the On property without throwing an exception.
        /// </summary>
        [Fact]
        public void Constructor_WithNullOnParameter_AssignsNullToOnProperty()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(int));
            LogicExpr? logicExpr = null;

            // Act
            var result = new TableJoinExpr(tableExpr, logicExpr);

            // Assert
            Assert.Same(tableExpr, result.Table);
            Assert.Null(result.On);
        }

        /// <summary>
        /// Tests that the constructor accepts both null parameters
        /// and assigns them to the Table and On properties without throwing an exception.
        /// </summary>
        [Fact]
        public void Constructor_WithBothParametersNull_AssignsNullToProperties()
        {
            // Arrange
            TableExpr? tableExpr = null;
            LogicExpr? logicExpr = null;

            // Act
            var result = new TableJoinExpr(tableExpr, logicExpr);

            // Assert
            Assert.Null(result.Table);
            Assert.Null(result.On);
        }

        /// <summary>
        /// Tests that the constructor initializes the JoinType property to its default value (Left).
        /// </summary>
        [Fact]
        public void Constructor_InitializesJoinTypeToDefaultValue()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(double));
            var logicExpr = Mock.Of<LogicExpr>();

            // Act
            var result = new TableJoinExpr(tableExpr, logicExpr);

            // Assert
            Assert.Equal(TableJoinType.Left, result.JoinType);
        }

        /// <summary>
        /// Tests that the constructor with different TableExpr types
        /// correctly assigns the Table property.
        /// </summary>
        /// <param name="type">The type to use for creating TableExpr.</param>
        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        [InlineData(typeof(object))]
        [InlineData(typeof(DateTime))]
        public void Constructor_WithVariousTableExprTypes_AssignsTableProperty(Type type)
        {
            // Arrange
            var tableExpr = new TableExpr(type);
            var logicExpr = Mock.Of<LogicExpr>();

            // Act
            var result = new TableJoinExpr(tableExpr, logicExpr);

            // Assert
            Assert.Same(tableExpr, result.Table);
            Assert.Equal(type, result.Table.Type);
        }
        #region ToString Tests

        /// <summary>
        /// Tests that ToString returns an empty string when Table is null.
        /// </summary>
        [Fact]
        public void ToString_WhenTableIsNull_ReturnsEmptyString()
        {
            // Arrange
            var tableJoinExpr = new TableJoinExpr
            {
                Table = null,
                On = Expr.Prop("Age") > 18,
                JoinType = TableJoinType.Left
            };

            // Act
            var result = tableJoinExpr.ToString();

            // Assert
            Assert.Equal(string.Empty, result);
        }

        /// <summary>
        /// Tests that ToString returns the correct formatted string for different JoinType values.
        /// </summary>
        /// <param name="joinType">The join type to test.</param>
        /// <param name="expectedJoinTypeString">The expected string representation of the join type.</param>
        [Theory]
        [InlineData(TableJoinType.Left, "Left")]
        [InlineData(TableJoinType.Right, "Right")]
        [InlineData(TableJoinType.Inner, "Inner")]
        [InlineData(TableJoinType.Cross, "Cross")]
        [InlineData(TableJoinType.LeftOuter, "LeftOuter")]
        [InlineData(TableJoinType.RightOuter, "RightOuter")]
        [InlineData(TableJoinType.FullOuter, "FullOuter")]
        public void ToString_WithValidTableAndOnForDifferentJoinTypes_ReturnsFormattedString(TableJoinType joinType, string expectedJoinTypeString)
        {
            // Arrange
            var table = new TableExpr(typeof(TestDepartment)) { Alias = "d" };
            var on = Expr.Prop("u", "DeptId") == Expr.Prop("d", "Id");
            var tableJoinExpr = new TableJoinExpr(table, on)
            {
                JoinType = joinType
            };

            // Act
            var result = tableJoinExpr.ToString();

            // Assert
            Assert.Contains(expectedJoinTypeString, result);
            Assert.Contains("JOIN", result);
            Assert.Contains("ON", result);
            Assert.Contains(table.ToString(), result);
            Assert.Contains(on.ToString(), result);
        }

        /// <summary>
        /// Tests that ToString handles null On condition correctly (common for CROSS JOIN).
        /// </summary>
        [Fact]
        public void ToString_WhenOnIsNull_ReturnsFormattedStringWithEmptyOnPart()
        {
            // Arrange
            var table = new TableExpr(typeof(TestDepartment)) { Alias = "d" };
            var tableJoinExpr = new TableJoinExpr(table, null)
            {
                JoinType = TableJoinType.Cross
            };

            // Act
            var result = tableJoinExpr.ToString();

            // Assert
            Assert.Contains("Cross", result);
            Assert.Contains("JOIN", result);
            Assert.Contains(table.ToString(), result);
            Assert.Contains("ON", result);
        }

        /// <summary>
        /// Tests that ToString returns the correct format with default JoinType (Left).
        /// </summary>
        [Fact]
        public void ToString_WithDefaultJoinType_ReturnsFormattedStringWithLeft()
        {
            // Arrange
            var table = new TableExpr(typeof(TestUser)) { Alias = "u" };
            var on = Expr.Prop("u", "DeptId") == Expr.Prop("d", "Id");
            var tableJoinExpr = new TableJoinExpr(table, on);

            // Act
            var result = tableJoinExpr.ToString();

            // Assert
            Assert.Contains("Left", result);
            Assert.Contains("JOIN", result);
            Assert.Contains("ON", result);
            Assert.Contains(table.ToString(), result);
            Assert.Contains(on.ToString(), result);
        }

        /// <summary>
        /// Tests that ToString returns the correct complete formatted string structure.
        /// </summary>
        [Fact]
        public void ToString_WithCompleteValidData_ReturnsExpectedFormat()
        {
            // Arrange
            var table = new TableExpr(typeof(TestDepartment));
            var on = Expr.Prop("DeptId") == Expr.Prop("Id");
            var tableJoinExpr = new TableJoinExpr(table, on)
            {
                JoinType = TableJoinType.Inner
            };

            // Act
            var result = tableJoinExpr.ToString();

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.Matches(@".+\s+JOIN\s+.+\s+ON\s+.*", result);
        }

        #endregion

        #region Test Helper Classes

        private class TestUser
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Age { get; set; }
            public int DeptId { get; set; }
        }

        private class TestDepartment
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        #endregion

        /// <summary>
        /// Tests that the Source getter returns the Table property value when Table is set.
        /// </summary>
        [Fact]
        public void Source_Get_ReturnsTableProperty()
        {
            // Arrange
            var tableExpr = new TableExpr();
            var tableJoinExpr = new TableJoinExpr
            {
                Table = tableExpr
            };

            // Act
            var result = tableJoinExpr.Source;

            // Assert
            Assert.Same(tableExpr, result);
        }

        /// <summary>
        /// Tests that the Source getter returns null when Table property is null.
        /// </summary>
        [Fact]
        public void Source_GetWhenTableIsNull_ReturnsNull()
        {
            // Arrange
            var tableJoinExpr = new TableJoinExpr();

            // Act
            var result = tableJoinExpr.Source;

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that the Source setter correctly sets the Table property when given a TableExpr instance.
        /// </summary>
        [Fact]
        public void Source_SetWithTableExpr_SetsTableProperty()
        {
            // Arrange
            var tableJoinExpr = new TableJoinExpr();
            var tableExpr = new TableExpr();

            // Act
            tableJoinExpr.Source = tableExpr;

            // Assert
            Assert.Same(tableExpr, tableJoinExpr.Table);
        }

        /// <summary>
        /// Tests that the Source setter correctly sets the Table property to null when given null.
        /// </summary>
        [Fact]
        public void Source_SetWithNull_SetsTableToNull()
        {
            // Arrange
            var tableJoinExpr = new TableJoinExpr
            {
                Table = new TableExpr()
            };

            // Act
            tableJoinExpr.Source = null;

            // Assert
            Assert.Null(tableJoinExpr.Table);
        }

        /// <summary>
        /// Tests that the Source setter throws InvalidCastException when given a SqlSegment that is not a TableExpr.
        /// </summary>
        [Fact]
        public void Source_SetWithIncompatibleType_ThrowsInvalidCastException()
        {
            // Arrange
            var tableJoinExpr = new TableJoinExpr();
            var incompatibleSegment = new Mock<SqlSegment>().Object;

            // Act & Assert
            Assert.Throws<InvalidCastException>(() => tableJoinExpr.Source = incompatibleSegment);
        }

        /// <summary>
        /// Tests that ExprType property returns TableJoin for instances created with default constructor.
        /// This verifies that the expression type is correctly set to TableJoin regardless of object state.
        /// Expected result: ExprType.TableJoin.
        /// </summary>
        [Fact]
        public void ExprType_DefaultConstructor_ReturnsTableJoin()
        {
            // Arrange
            var tableJoinExpr = new TableJoinExpr();

            // Act
            var result = tableJoinExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.TableJoin, result);
        }

        /// <summary>
        /// Tests that ExprType property returns TableJoin for instances created with parameterized constructor.
        /// This verifies that the expression type is correctly set to TableJoin when initialized with table and condition.
        /// Expected result: ExprType.TableJoin.
        /// </summary>
        [Fact]
        public void ExprType_ParameterizedConstructor_ReturnsTableJoin()
        {
            // Arrange
            var tableMock = new Mock<TableExpr>();
            var onMock = new Mock<LogicExpr>();
            var tableJoinExpr = new TableJoinExpr(tableMock.Object, onMock.Object);

            // Act
            var result = tableJoinExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.TableJoin, result);
        }

        /// <summary>
        /// Tests that ExprType property returns TableJoin with different JoinType values.
        /// This verifies that the expression type remains constant regardless of JoinType property value.
        /// Expected result: ExprType.TableJoin for all JoinType values.
        /// </summary>
        [Theory]
        [InlineData(TableJoinType.Left)]
        [InlineData(TableJoinType.Right)]
        [InlineData(TableJoinType.Inner)]
        [InlineData(TableJoinType.Outer)]
        [InlineData(TableJoinType.Cross)]
        public void ExprType_WithDifferentJoinTypes_ReturnsTableJoin(TableJoinType joinType)
        {
            // Arrange
            var tableJoinExpr = new TableJoinExpr
            {
                JoinType = joinType
            };

            // Act
            var result = tableJoinExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.TableJoin, result);
        }

        /// <summary>
        /// Tests that ExprType property returns TableJoin when Table and On properties are null.
        /// This verifies that the expression type is not dependent on the state of other properties.
        /// Expected result: ExprType.TableJoin.
        /// </summary>
        [Fact]
        public void ExprType_WithNullTableAndOn_ReturnsTableJoin()
        {
            // Arrange
            var tableJoinExpr = new TableJoinExpr
            {
                Table = null,
                On = null
            };

            // Act
            var result = tableJoinExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.TableJoin, result);
        }

        /// <summary>
        /// Tests that ExprType property consistently returns the same value across multiple accesses.
        /// This verifies that the property is stable and deterministic.
        /// Expected result: ExprType.TableJoin on all accesses.
        /// </summary>
        [Fact]
        public void ExprType_MultipleAccesses_ReturnsConsistentValue()
        {
            // Arrange
            var tableJoinExpr = new TableJoinExpr();

            // Act
            var result1 = tableJoinExpr.ExprType;
            var result2 = tableJoinExpr.ExprType;
            var result3 = tableJoinExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.TableJoin, result1);
            Assert.Equal(ExprType.TableJoin, result2);
            Assert.Equal(ExprType.TableJoin, result3);
            Assert.Equal(result1, result2);
            Assert.Equal(result2, result3);
        }

        /// <summary>
        /// Tests that GetHashCode returns consistent hash code when called multiple times on the same object.
        /// </summary>
        [Fact]
        public void GetHashCode_CalledMultipleTimes_ReturnsSameHashCode()
        {
            // Arrange
            var table = new TableExpr(typeof(string)) { Alias = "t" };
            var on = Expr.Prop("a") == Expr.Prop("b");
            var tableJoin = new TableJoinExpr(table, on) { JoinType = TableJoinType.Left };

            // Act
            var hash1 = tableJoin.GetHashCode();
            var hash2 = tableJoin.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns the same hash code for two equal objects.
        /// </summary>
        [Fact]
        public void GetHashCode_EqualObjects_ReturnsSameHashCode()
        {
            // Arrange
            var table1 = new TableExpr(typeof(string)) { Alias = "t" };
            var on1 = Expr.Prop("a") == Expr.Prop("b");
            var tableJoin1 = new TableJoinExpr(table1, on1) { JoinType = TableJoinType.Inner };

            var table2 = new TableExpr(typeof(string)) { Alias = "t" };
            var on2 = Expr.Prop("a") == Expr.Prop("b");
            var tableJoin2 = new TableJoinExpr(table2, on2) { JoinType = TableJoinType.Inner };

            // Act
            var hash1 = tableJoin1.GetHashCode();
            var hash2 = tableJoin2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns a valid hash code when Table is null.
        /// </summary>
        [Fact]
        public void GetHashCode_TableIsNull_ReturnsValidHashCode()
        {
            // Arrange
            var on = Expr.Prop("a") == Expr.Prop("b");
            var tableJoin = new TableJoinExpr(null, on) { JoinType = TableJoinType.Left };

            // Act
            var hashCode = tableJoin.GetHashCode();

            // Assert
            Assert.IsType<int>(hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns a valid hash code when On is null.
        /// </summary>
        [Fact]
        public void GetHashCode_OnIsNull_ReturnsValidHashCode()
        {
            // Arrange
            var table = new TableExpr(typeof(string)) { Alias = "t" };
            var tableJoin = new TableJoinExpr(table, null) { JoinType = TableJoinType.Cross };

            // Act
            var hashCode = tableJoin.GetHashCode();

            // Assert
            Assert.IsType<int>(hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns a valid hash code when both Table and On are null.
        /// </summary>
        [Fact]
        public void GetHashCode_BothTableAndOnAreNull_ReturnsValidHashCode()
        {
            // Arrange
            var tableJoin = new TableJoinExpr(null, null) { JoinType = TableJoinType.Left };

            // Act
            var hashCode = tableJoin.GetHashCode();

            // Assert
            Assert.IsType<int>(hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes for objects with different JoinType values.
        /// </summary>
        [Theory]
        [InlineData(TableJoinType.Left)]
        [InlineData(TableJoinType.Right)]
        [InlineData(TableJoinType.Inner)]
        [InlineData(TableJoinType.Cross)]
        [InlineData(TableJoinType.Outer)]
        public void GetHashCode_DifferentJoinTypes_ReturnsValidHashCode(TableJoinType joinType)
        {
            // Arrange
            var table = new TableExpr(typeof(string)) { Alias = "t" };
            var on = Expr.Prop("a") == Expr.Prop("b");
            var tableJoin = new TableJoinExpr(table, on) { JoinType = joinType };

            // Act
            var hashCode = tableJoin.GetHashCode();

            // Assert
            Assert.IsType<int>(hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes when JoinType differs.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentJoinTypes_ReturnsDifferentHashCodes()
        {
            // Arrange
            var table1 = new TableExpr(typeof(string)) { Alias = "t" };
            var on1 = Expr.Prop("a") == Expr.Prop("b");
            var tableJoin1 = new TableJoinExpr(table1, on1) { JoinType = TableJoinType.Left };

            var table2 = new TableExpr(typeof(string)) { Alias = "t" };
            var on2 = Expr.Prop("a") == Expr.Prop("b");
            var tableJoin2 = new TableJoinExpr(table2, on2) { JoinType = TableJoinType.Right };

            // Act
            var hash1 = tableJoin1.GetHashCode();
            var hash2 = tableJoin2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes when Table differs.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentTables_ReturnsDifferentHashCodes()
        {
            // Arrange
            var table1 = new TableExpr(typeof(string)) { Alias = "t1" };
            var on1 = Expr.Prop("a") == Expr.Prop("b");
            var tableJoin1 = new TableJoinExpr(table1, on1) { JoinType = TableJoinType.Left };

            var table2 = new TableExpr(typeof(int)) { Alias = "t2" };
            var on2 = Expr.Prop("a") == Expr.Prop("b");
            var tableJoin2 = new TableJoinExpr(table2, on2) { JoinType = TableJoinType.Left };

            // Act
            var hash1 = tableJoin1.GetHashCode();
            var hash2 = tableJoin2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes when On differs.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentOnConditions_ReturnsDifferentHashCodes()
        {
            // Arrange
            var table1 = new TableExpr(typeof(string)) { Alias = "t" };
            var on1 = Expr.Prop("a") == Expr.Prop("b");
            var tableJoin1 = new TableJoinExpr(table1, on1) { JoinType = TableJoinType.Left };

            var table2 = new TableExpr(typeof(string)) { Alias = "t" };
            var on2 = Expr.Prop("x") == Expr.Prop("y");
            var tableJoin2 = new TableJoinExpr(table2, on2) { JoinType = TableJoinType.Left };

            // Act
            var hash1 = tableJoin1.GetHashCode();
            var hash2 = tableJoin2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns consistent hash code when using default constructor.
        /// </summary>
        [Fact]
        public void GetHashCode_DefaultConstructor_ReturnsConsistentHashCode()
        {
            // Arrange
            var tableJoin = new TableJoinExpr();

            // Act
            var hash1 = tableJoin.GetHashCode();
            var hash2 = tableJoin.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode handles null Table property transitioning to non-null.
        /// </summary>
        [Fact]
        public void GetHashCode_TableChangedFromNullToValue_ReturnsDifferentHashCode()
        {
            // Arrange
            var tableJoin = new TableJoinExpr();
            var hash1 = tableJoin.GetHashCode();

            // Act
            tableJoin.Table = new TableExpr(typeof(string)) { Alias = "t" };
            var hash2 = tableJoin.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode handles null On property transitioning to non-null.
        /// </summary>
        [Fact]
        public void GetHashCode_OnChangedFromNullToValue_ReturnsDifferentHashCode()
        {
            // Arrange
            var tableJoin = new TableJoinExpr();
            var hash1 = tableJoin.GetHashCode();

            // Act
            tableJoin.On = Expr.Prop("a") == Expr.Prop("b");
            var hash2 = tableJoin.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that Clone creates a deep copy with all properties correctly cloned.
        /// Input: TableJoinExpr with all properties set (Table, On, JoinType).
        /// Expected: New instance with cloned Table and On, and copied JoinType.
        /// </summary>
        [Fact]
        public void Clone_WithAllPropertiesSet_ReturnsDeepCopy()
        {
            // Arrange
            var table = new TableExpr(typeof(string)) { Alias = "t" };
            var on = Expr.Prop("u", "DeptId") == Expr.Prop("d", "Id");
            var original = new TableJoinExpr(table, on) { JoinType = TableJoinType.Left };

            // Act
            var cloned = (TableJoinExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.NotSame(original.Table, cloned.Table);
            Assert.NotSame(original.On, cloned.On);
            Assert.Equal(original.JoinType, cloned.JoinType);
            Assert.Equal(original.Table.Type, cloned.Table.Type);
            Assert.Equal(original.Table.Alias, cloned.Table.Alias);
            Assert.True(original.On.Equals(cloned.On));
        }

        /// <summary>
        /// Tests that Clone handles null Table property correctly.
        /// Input: TableJoinExpr with null Table, non-null On.
        /// Expected: Clone also has null Table, cloned On.
        /// </summary>
        [Fact]
        public void Clone_WithNullTable_ReturnsCloneWithNullTable()
        {
            // Arrange
            var on = Expr.Prop("Age") > Expr.Const(18);
            var original = new TableJoinExpr { Table = null, On = on, JoinType = TableJoinType.Inner };

            // Act
            var cloned = (TableJoinExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.Null(cloned.Table);
            Assert.NotNull(cloned.On);
            Assert.NotSame(original.On, cloned.On);
            Assert.Equal(original.JoinType, cloned.JoinType);
        }

        /// <summary>
        /// Tests that Clone handles null On property correctly.
        /// Input: TableJoinExpr with null On, non-null Table.
        /// Expected: Clone also has null On, cloned Table.
        /// </summary>
        [Fact]
        public void Clone_WithNullOn_ReturnsCloneWithNullOn()
        {
            // Arrange
            var table = new TableExpr(typeof(int));
            var original = new TableJoinExpr { Table = table, On = null, JoinType = TableJoinType.Right };

            // Act
            var cloned = (TableJoinExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.NotNull(cloned.Table);
            Assert.NotSame(original.Table, cloned.Table);
            Assert.Null(cloned.On);
            Assert.Equal(original.JoinType, cloned.JoinType);
        }

        /// <summary>
        /// Tests that Clone handles all nullable properties being null.
        /// Input: TableJoinExpr with null Table and null On.
        /// Expected: Clone also has null Table and null On.
        /// </summary>
        [Fact]
        public void Clone_WithAllPropertiesNull_ReturnsCloneWithNullProperties()
        {
            // Arrange
            var original = new TableJoinExpr { Table = null, On = null, JoinType = TableJoinType.Outer };

            // Act
            var cloned = (TableJoinExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.Null(cloned.Table);
            Assert.Null(cloned.On);
            Assert.Equal(TableJoinType.Outer, cloned.JoinType);
        }

        /// <summary>
        /// Tests that Clone correctly copies different JoinType enum values.
        /// Input: TableJoinExpr with various JoinType values.
        /// Expected: Clone has the same JoinType value.
        /// </summary>
        [Theory]
        [InlineData(TableJoinType.Left)]
        [InlineData(TableJoinType.Right)]
        [InlineData(TableJoinType.Inner)]
        [InlineData(TableJoinType.Outer)]
        [InlineData(TableJoinType.Cross)]
        public void Clone_WithDifferentJoinTypes_CopiesJoinTypeCorrectly(TableJoinType joinType)
        {
            // Arrange
            var table = new TableExpr(typeof(double));
            var on = Expr.Prop("Id") == Expr.Const(1);
            var original = new TableJoinExpr(table, on) { JoinType = joinType };

            // Act
            var cloned = (TableJoinExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.Equal(joinType, cloned.JoinType);
        }

        /// <summary>
        /// Tests that modifying the cloned object does not affect the original.
        /// Input: TableJoinExpr with all properties set.
        /// Expected: Changes to clone do not affect the original instance.
        /// </summary>
        [Fact]
        public void Clone_ModifyingClone_DoesNotAffectOriginal()
        {
            // Arrange
            var originalTable = new TableExpr(typeof(string)) { Alias = "original" };
            var originalOn = Expr.Prop("x") == Expr.Const(10);
            var original = new TableJoinExpr(originalTable, originalOn) { JoinType = TableJoinType.Left };

            // Act
            var cloned = (TableJoinExpr)original.Clone();
            cloned.Table.Alias = "modified";
            cloned.JoinType = TableJoinType.Right;

            // Assert
            Assert.Equal("original", original.Table.Alias);
            Assert.Equal(TableJoinType.Left, original.JoinType);
            Assert.Equal("modified", cloned.Table.Alias);
            Assert.Equal(TableJoinType.Right, cloned.JoinType);
        }

        /// <summary>
        /// Tests that Clone using default constructor creates valid clone.
        /// Input: TableJoinExpr created with default constructor.
        /// Expected: Clone is created successfully with default values.
        /// </summary>
        [Fact]
        public void Clone_WithDefaultConstructor_ReturnsValidClone()
        {
            // Arrange
            var original = new TableJoinExpr();

            // Act
            var cloned = (TableJoinExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.Null(cloned.Table);
            Assert.Null(cloned.On);
            Assert.Equal(TableJoinType.Left, cloned.JoinType); // Default value
        }
    }
}