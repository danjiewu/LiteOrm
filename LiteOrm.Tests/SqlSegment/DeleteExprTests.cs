using System;

using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the DeleteExpr class
    /// </summary>
    public partial class DeleteExprTests
    {
        /// <summary>
        /// Tests that the Source property getter returns the Table property value when Table is set to a valid TableExpr instance.
        /// Input: A DeleteExpr instance with Table set to a valid TableExpr.
        /// Expected: Source getter returns the same TableExpr instance.
        /// </summary>
        [Fact]
        public void Source_GetWhenTableIsSet_ReturnsTable()
        {
            // Arrange
            TableExpr tableExpr = new TableExpr();
            DeleteExpr deleteExpr = new DeleteExpr { Table = tableExpr };

            // Act
            SqlSegment result = deleteExpr.Source;

            // Assert
            Assert.Same(tableExpr, result);
        }

        /// <summary>
        /// Tests that the Source property getter returns null when Table is null.
        /// Input: A DeleteExpr instance with Table set to null.
        /// Expected: Source getter returns null.
        /// </summary>
        [Fact]
        public void Source_GetWhenTableIsNull_ReturnsNull()
        {
            // Arrange
            DeleteExpr deleteExpr = new DeleteExpr { Table = null };

            // Act
            SqlSegment result = deleteExpr.Source;

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that the Source property setter updates the Table property when set with a valid TableExpr.
        /// Input: Setting Source to a valid TableExpr instance.
        /// Expected: Table property is updated to the same TableExpr instance.
        /// </summary>
        [Fact]
        public void Source_SetWithValidTableExpr_UpdatesTable()
        {
            // Arrange
            DeleteExpr deleteExpr = new DeleteExpr();
            TableExpr tableExpr = new TableExpr();

            // Act
            deleteExpr.Source = tableExpr;

            // Assert
            Assert.Same(tableExpr, deleteExpr.Table);
        }

        /// <summary>
        /// Tests that the Source property setter sets Table to null when Source is set to null.
        /// Input: Setting Source to null.
        /// Expected: Table property is set to null.
        /// </summary>
        [Fact]
        public void Source_SetWithNull_SetsTableToNull()
        {
            // Arrange
            DeleteExpr deleteExpr = new DeleteExpr { Table = new TableExpr() };

            // Act
            deleteExpr.Source = null;

            // Assert
            Assert.Null(deleteExpr.Table);
        }

        /// <summary>
        /// Tests that the Source property setter throws InvalidCastException when set with an incompatible SqlSegment type.
        /// Input: Setting Source to a SqlSegment instance that is not a TableExpr.
        /// Expected: InvalidCastException is thrown.
        /// </summary>
        [Fact]
        public void Source_SetWithIncompatibleType_ThrowsInvalidCastException()
        {
            // Arrange
            DeleteExpr deleteExpr = new DeleteExpr();
            Mock<SqlSegment> incompatibleSegment = new Mock<SqlSegment>();

            // Act & Assert
            Assert.Throws<InvalidCastException>(() => deleteExpr.Source = incompatibleSegment.Object);
        }

        /// <summary>
        /// Tests that the Source property setter correctly casts and updates Table when given a TableExpr with initialized properties.
        /// Input: Setting Source to a TableExpr with Type property set.
        /// Expected: Table property is updated and retains the Type property value.
        /// </summary>
        [Fact]
        public void Source_SetWithTableExprWithProperties_PreservesProperties()
        {
            // Arrange
            DeleteExpr deleteExpr = new DeleteExpr();
            TableExpr tableExpr = new TableExpr(typeof(string));

            // Act
            deleteExpr.Source = tableExpr;

            // Assert
            Assert.Same(tableExpr, deleteExpr.Table);
            Assert.Equal(typeof(string), deleteExpr.Table.Type);
        }

        /// <summary>
        /// Tests the round-trip behavior of setting Table and reading Source, then setting Source and reading Table.
        /// Input: Multiple assignments to both Table and Source properties.
        /// Expected: Both properties remain synchronized.
        /// </summary>
        [Fact]
        public void Source_RoundTripSetAndGet_MaintainsConsistency()
        {
            // Arrange
            DeleteExpr deleteExpr = new DeleteExpr();
            TableExpr firstTable = new TableExpr();
            TableExpr secondTable = new TableExpr(typeof(int));

            // Act & Assert - Set Table, read Source
            deleteExpr.Table = firstTable;
            Assert.Same(firstTable, deleteExpr.Source);

            // Act & Assert - Set Source, read Table
            deleteExpr.Source = secondTable;
            Assert.Same(secondTable, deleteExpr.Table);

            // Act & Assert - Verify final state
            Assert.Same(secondTable, deleteExpr.Source);
            Assert.Same(secondTable, deleteExpr.Table);
        }

        /// <summary>
        /// Helper test entity class for creating TableExpr instances.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
            public string? Name { get; set; }
        }

        /// <summary>
        /// Tests that the constructor correctly sets Table and Where properties when both valid arguments are provided.
        /// </summary>
        [Fact]
        public void Constructor_WithValidTableAndWhere_SetsPropertiesCorrectly()
        {
            // Arrange
            var table = new TableExpr(typeof(TestEntity));
            var where = new Mock<LogicExpr>().Object;

            // Act
            var deleteExpr = new DeleteExpr(table, where);

            // Assert
            Assert.Same(table, deleteExpr.Table);
            Assert.Same(where, deleteExpr.Where);
        }

        /// <summary>
        /// Tests that the constructor correctly sets Table property and leaves Where as null when where parameter is omitted (using default value).
        /// </summary>
        [Fact]
        public void Constructor_WithValidTableAndDefaultWhere_SetsTableAndLeavesWhereNull()
        {
            // Arrange
            var table = new TableExpr(typeof(TestEntity));

            // Act
            var deleteExpr = new DeleteExpr(table);

            // Assert
            Assert.Same(table, deleteExpr.Table);
            Assert.Null(deleteExpr.Where);
        }

        /// <summary>
        /// Tests that the constructor correctly sets Table property and Where to null when null is explicitly passed for where parameter.
        /// </summary>
        [Fact]
        public void Constructor_WithValidTableAndExplicitNullWhere_SetsTableAndWhereToNull()
        {
            // Arrange
            var table = new TableExpr(typeof(TestEntity));

            // Act
            var deleteExpr = new DeleteExpr(table, null);

            // Assert
            Assert.Same(table, deleteExpr.Table);
            Assert.Null(deleteExpr.Where);
        }

        /// <summary>
        /// Tests that the constructor accepts and sets a null table parameter without throwing an exception.
        /// Edge case: No validation prevents null table.
        /// </summary>
        [Fact]
        public void Constructor_WithNullTable_SetsTableToNull()
        {
            // Arrange
            TableExpr? table = null;
            var where = new Mock<LogicExpr>().Object;

            // Act
            var deleteExpr = new DeleteExpr(table!, where);

            // Assert
            Assert.Null(deleteExpr.Table);
            Assert.Same(where, deleteExpr.Where);
        }

        /// <summary>
        /// Tests that the constructor handles both null table and null where parameters without throwing an exception.
        /// Edge case: No validation prevents null parameters.
        /// </summary>
        [Fact]
        public void Constructor_WithNullTableAndNullWhere_SetsBothPropertiesToNull()
        {
            // Arrange
            TableExpr? table = null;
            LogicExpr? where = null;

            // Act
            var deleteExpr = new DeleteExpr(table!, where);

            // Assert
            Assert.Null(deleteExpr.Table);
            Assert.Null(deleteExpr.Where);
        }

        /// <summary>
        /// Tests that the constructor works with different types used to create TableExpr.
        /// </summary>
        /// <param name="type">The type to use for creating the TableExpr.</param>
        [Theory]
        [InlineData(typeof(object))]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        [InlineData(typeof(TestEntity))]
        public void Constructor_WithDifferentTableTypes_SetsPropertiesCorrectly(Type type)
        {
            // Arrange
            var table = new TableExpr(type);
            var where = new Mock<LogicExpr>().Object;

            // Act
            var deleteExpr = new DeleteExpr(table, where);

            // Assert
            Assert.Same(table, deleteExpr.Table);
            Assert.Same(where, deleteExpr.Where);
        }

        /// <summary>
        /// Tests that Clone creates a new instance when both Table and Where are null.
        /// </summary>
        [Fact]
        public void Clone_BothTableAndWhereNull_ReturnsNewInstanceWithNullProperties()
        {
            // Arrange
            var deleteExpr = new DeleteExpr();

            // Act
            var cloned = deleteExpr.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.IsType<DeleteExpr>(cloned);
            Assert.NotSame(deleteExpr, cloned);
            var clonedDelete = (DeleteExpr)cloned;
            Assert.Null(clonedDelete.Table);
            Assert.Null(clonedDelete.Where);
        }

        /// <summary>
        /// Tests that Clone creates a deep copy when Table is not null and Where is null.
        /// </summary>
        [Fact]
        public void Clone_TableNotNullWhereNull_ReturnsDeepCopyWithClonedTable()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var deleteExpr = new DeleteExpr(table, null);

            // Act
            var cloned = deleteExpr.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.IsType<DeleteExpr>(cloned);
            Assert.NotSame(deleteExpr, cloned);
            var clonedDelete = (DeleteExpr)cloned;
            Assert.NotNull(clonedDelete.Table);
            Assert.NotSame(deleteExpr.Table, clonedDelete.Table);
            Assert.Null(clonedDelete.Where);
        }

        /// <summary>
        /// Tests that Clone creates a deep copy when Table is null and Where is not null.
        /// </summary>
        [Fact]
        public void Clone_TableNullWhereNotNull_ReturnsDeepCopyWithClonedWhere()
        {
            // Arrange
            var where = Expr.Prop("Age") > 100;
            var deleteExpr = new DeleteExpr { Where = where };

            // Act
            var cloned = deleteExpr.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.IsType<DeleteExpr>(cloned);
            Assert.NotSame(deleteExpr, cloned);
            var clonedDelete = (DeleteExpr)cloned;
            Assert.Null(clonedDelete.Table);
            Assert.NotNull(clonedDelete.Where);
            Assert.NotSame(deleteExpr.Where, clonedDelete.Where);
        }

        /// <summary>
        /// Tests that Clone creates a deep copy when both Table and Where are not null.
        /// </summary>
        [Fact]
        public void Clone_BothTableAndWhereNotNull_ReturnsDeepCopyWithBothCloned()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var where = Expr.Prop("Age") > 100;
            var deleteExpr = new DeleteExpr(table, where);

            // Act
            var cloned = deleteExpr.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.IsType<DeleteExpr>(cloned);
            Assert.NotSame(deleteExpr, cloned);
            var clonedDelete = (DeleteExpr)cloned;
            Assert.NotNull(clonedDelete.Table);
            Assert.NotSame(deleteExpr.Table, clonedDelete.Table);
            Assert.NotNull(clonedDelete.Where);
            Assert.NotSame(deleteExpr.Where, clonedDelete.Where);
        }

        /// <summary>
        /// Tests that Clone creates an independent copy and modifications to the clone do not affect the original.
        /// </summary>
        [Fact]
        public void Clone_ModifyClonedInstance_OriginalRemainsUnchanged()
        {
            // Arrange
            var originalTable = new TableExpr(typeof(string));
            var originalWhere = Expr.Prop("Age") > 100;
            var deleteExpr = new DeleteExpr(originalTable, originalWhere);

            // Act
            var cloned = (DeleteExpr)deleteExpr.Clone();
            cloned.Table = new TableExpr(typeof(int));
            cloned.Where = Expr.Prop("Name") == "Test";

            // Assert
            Assert.NotSame(deleteExpr.Table, cloned.Table);
            Assert.NotSame(deleteExpr.Where, cloned.Where);
            Assert.Same(originalTable, deleteExpr.Table);
            Assert.Same(originalWhere, deleteExpr.Where);
        }

        /// <summary>
        /// Tests that Clone creates an equal instance based on the Equals implementation.
        /// </summary>
        [Fact]
        public void Clone_WithTableAndWhere_ClonedInstanceEqualsOriginal()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var where = Expr.Prop("Age") > 100;
            var deleteExpr = new DeleteExpr(table, where);

            // Act
            var cloned = deleteExpr.Clone();

            // Assert
            Assert.True(deleteExpr.Equals(cloned));
        }

        /// <summary>
        /// Tests that Clone returns the correct type as defined in the base class.
        /// </summary>
        [Fact]
        public void Clone_ReturnsExprType_CanBeCastToDeleteExpr()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var deleteExpr = new DeleteExpr(table);

            // Act
            Expr cloned = deleteExpr.Clone();

            // Assert
            Assert.IsType<DeleteExpr>(cloned);
            Assert.Equal(ExprType.Delete, ((DeleteExpr)cloned).ExprType);
        }

        /// <summary>
        /// Tests that ExprType property returns Delete when using default constructor
        /// </summary>
        [Fact]
        public void ExprType_DefaultConstructor_ReturnsDelete()
        {
            // Arrange
            var deleteExpr = new DeleteExpr();

            // Act
            var result = deleteExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Delete, result);
        }

        /// <summary>
        /// Tests that ExprType property returns Delete when using parameterized constructor with valid table
        /// </summary>
        [Fact]
        public void ExprType_ParameterizedConstructorWithTable_ReturnsDelete()
        {
            // Arrange
            var table = new TableExpr("TestTable");
            var deleteExpr = new DeleteExpr(table);

            // Act
            var result = deleteExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Delete, result);
        }

        /// <summary>
        /// Tests that ExprType property returns Delete when using parameterized constructor with table and where clause
        /// </summary>
        [Fact]
        public void ExprType_ParameterizedConstructorWithTableAndWhere_ReturnsDelete()
        {
            // Arrange
            var table = new TableExpr("TestTable");
            var where = new LogicBinaryExpr(
                new PropertyExpr("Id"),
                LogicOp.Equal,
                new ValueExpr(1)
            );
            var deleteExpr = new DeleteExpr(table, where);

            // Act
            var result = deleteExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Delete, result);
        }

        /// <summary>
        /// Tests that ExprType property returns Delete when using parameterized constructor with null where clause
        /// </summary>
        [Fact]
        public void ExprType_ParameterizedConstructorWithNullWhere_ReturnsDelete()
        {
            // Arrange
            var table = new TableExpr("TestTable");
            var deleteExpr = new DeleteExpr(table, null);

            // Act
            var result = deleteExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Delete, result);
        }

        /// <summary>
        /// Tests that ToString returns a DELETE statement with WHERE clause
        /// when both Table and Where properties are set.
        /// </summary>
        [Fact]
        public void ToString_WithTableAndWhere_ReturnsDeleteStatementWithWhereClause()
        {
            // Arrange
            var table = new TableExpr(typeof(TestUser));
            var where = Expr.Prop("Age") > 100;
            var deleteExpr = new DeleteExpr(table, where);

            // Act
            var result = deleteExpr.ToString();

            // Assert
            Assert.StartsWith("DELETE FROM ", result);
            Assert.Contains(" WHERE ", result);
            Assert.Contains(table.ToString(), result);
        }

        /// <summary>
        /// Tests that ToString returns a DELETE statement without WHERE clause
        /// when Table is set but Where is null.
        /// </summary>
        [Fact]
        public void ToString_WithTableAndNullWhere_ReturnsDeleteStatementWithoutWhereClause()
        {
            // Arrange
            var table = new TableExpr(typeof(TestUser));
            var deleteExpr = new DeleteExpr(table, null);

            // Act
            var result = deleteExpr.ToString();

            // Assert
            Assert.StartsWith("DELETE FROM ", result);
            Assert.DoesNotContain("WHERE", result);
            Assert.Contains(table.ToString(), result);
        }

        /// <summary>
        /// Tests that ToString returns a DELETE statement when Table is null
        /// and Where is null (default state).
        /// </summary>
        [Fact]
        public void ToString_WithNullTableAndNullWhere_ReturnsDeleteStatementWithEmptyTable()
        {
            // Arrange
            var deleteExpr = new DeleteExpr();

            // Act
            var result = deleteExpr.ToString();

            // Assert
            Assert.Equal("DELETE FROM ", result);
            Assert.DoesNotContain("WHERE", result);
        }

        /// <summary>
        /// Tests that ToString returns a DELETE statement with WHERE clause
        /// when Table is null but Where is set.
        /// </summary>
        [Fact]
        public void ToString_WithNullTableAndWhere_ReturnsDeleteStatementWithWhereClause()
        {
            // Arrange
            var where = Expr.Prop("Age") > 100;
            var deleteExpr = new DeleteExpr { Where = where };

            // Act
            var result = deleteExpr.ToString();

            // Assert
            Assert.StartsWith("DELETE FROM ", result);
            Assert.Contains(" WHERE ", result);
            Assert.Contains(where.ToString(), result);
        }

        /// <summary>
        /// Tests that ToString correctly formats the complete DELETE statement
        /// with both table and where condition visible in output.
        /// </summary>
        [Fact]
        public void ToString_WithComplexWhereCondition_ReturnsFormattedDeleteStatement()
        {
            // Arrange
            var table = new TableExpr(typeof(TestUser));
            var where = Expr.Prop("Id") == 1;
            var deleteExpr = new DeleteExpr(table, where);

            // Act
            var result = deleteExpr.ToString();

            // Assert
            Assert.StartsWith("DELETE FROM ", result);
            Assert.Contains(table.ToString(), result);
            Assert.Contains(" WHERE ", result);
            Assert.EndsWith(where.ToString(), result);
        }

        /// <summary>
        /// Tests that the parameterless constructor creates a valid DeleteExpr instance
        /// with all properties initialized to their default values (null for reference types).
        /// </summary>
        [Fact]
        public void DeleteExpr_ParameterlessConstructor_CreatesInstanceWithDefaultValues()
        {
            // Arrange & Act
            var deleteExpr = new DeleteExpr();

            // Assert
            Assert.NotNull(deleteExpr);
            Assert.Null(deleteExpr.Table);
            Assert.Null(deleteExpr.Where);
            Assert.Null(deleteExpr.Source);
            Assert.Equal(ExprType.Delete, deleteExpr.ExprType);
        }

        /// <summary>
        /// Tests that the parameterless constructor creates an instance that can safely
        /// call ToString() method without throwing exceptions, even with null properties.
        /// </summary>
        [Fact]
        public void DeleteExpr_ParameterlessConstructor_ToStringWorksWithNullProperties()
        {
            // Arrange & Act
            var deleteExpr = new DeleteExpr();
            var result = deleteExpr.ToString();

            // Assert
            Assert.NotNull(result);
            Assert.Contains("DELETE FROM", result);
        }

        /// <summary>
        /// Tests that the parameterless constructor creates an instance that can safely
        /// call GetHashCode() method without throwing exceptions, even with null properties.
        /// </summary>
        [Fact]
        public void DeleteExpr_ParameterlessConstructor_GetHashCodeWorksWithNullProperties()
        {
            // Arrange & Act
            var deleteExpr = new DeleteExpr();
            var hashCode = deleteExpr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests that two instances created with the parameterless constructor are equal
        /// since they both have null properties.
        /// </summary>
        [Fact]
        public void DeleteExpr_ParameterlessConstructor_TwoInstancesAreEqual()
        {
            // Arrange & Act
            var deleteExpr1 = new DeleteExpr();
            var deleteExpr2 = new DeleteExpr();

            // Assert
            Assert.True(deleteExpr1.Equals(deleteExpr2));
        }

        /// <summary>
        /// Tests that Equals returns true when comparing the same instance.
        /// </summary>
        [Fact]
        public void Equals_SameInstance_ReturnsTrue()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var expr = new DeleteExpr(table, Expr.Prop("Age") > 100);

            // Act
            var result = expr.Equals(expr);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing two DeleteExpr instances with equal Table and Where properties.
        /// </summary>
        [Fact]
        public void Equals_EqualTableAndWhere_ReturnsTrue()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var where = Expr.Prop("Age") > 100;
            var expr1 = new DeleteExpr(table, where);
            var expr2 = new DeleteExpr(table, where);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both DeleteExpr instances have null Table and null Where properties.
        /// </summary>
        [Fact]
        public void Equals_BothNullTableAndWhere_ReturnsTrue()
        {
            // Arrange
            var expr1 = new DeleteExpr();
            var expr2 = new DeleteExpr();

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both DeleteExpr instances have the same Table and null Where properties.
        /// </summary>
        [Fact]
        public void Equals_SameTableNullWhere_ReturnsTrue()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var expr1 = new DeleteExpr(table, null);
            var expr2 = new DeleteExpr(table, null);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with null.
        /// </summary>
        [Fact]
        public void Equals_NullObject_ReturnsFalse()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var expr = new DeleteExpr(table, Expr.Prop("Age") > 100);

            // Act
            var result = expr.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with a different type.
        /// </summary>
        /// <param name="obj">The object to compare with.</param>
        [Theory]
        [InlineData("string")]
        [InlineData(123)]
        [InlineData(true)]
        public void Equals_DifferentType_ReturnsFalse(object obj)
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var expr = new DeleteExpr(table, Expr.Prop("Age") > 100);

            // Act
            var result = expr.Equals(obj);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing DeleteExpr instances with different Table properties.
        /// </summary>
        [Fact]
        public void Equals_DifferentTable_ReturnsFalse()
        {
            // Arrange
            var table1 = new TableExpr(typeof(string));
            var table2 = new TableExpr(typeof(int));
            var where = Expr.Prop("Age") > 100;
            var expr1 = new DeleteExpr(table1, where);
            var expr2 = new DeleteExpr(table2, where);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing DeleteExpr instances with different Where properties.
        /// </summary>
        [Fact]
        public void Equals_DifferentWhere_ReturnsFalse()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var where1 = Expr.Prop("Age") > 100;
            var where2 = Expr.Prop("Age") > 50;
            var expr1 = new DeleteExpr(table, where1);
            var expr2 = new DeleteExpr(table, where2);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing DeleteExpr instances with both different Table and Where properties.
        /// </summary>
        [Fact]
        public void Equals_DifferentTableAndWhere_ReturnsFalse()
        {
            // Arrange
            var table1 = new TableExpr(typeof(string));
            var table2 = new TableExpr(typeof(int));
            var where1 = Expr.Prop("Age") > 100;
            var where2 = Expr.Prop("Age") > 50;
            var expr1 = new DeleteExpr(table1, where1);
            var expr2 = new DeleteExpr(table2, where2);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one DeleteExpr has null Table and the other does not.
        /// </summary>
        [Fact]
        public void Equals_OneNullTable_ReturnsFalse()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var where = Expr.Prop("Age") > 100;
            var expr1 = new DeleteExpr(table, where);
            var expr2 = new DeleteExpr(null, where);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one DeleteExpr has null Where and the other does not.
        /// </summary>
        [Fact]
        public void Equals_OneNullWhere_ReturnsFalse()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var where = Expr.Prop("Age") > 100;
            var expr1 = new DeleteExpr(table, where);
            var expr2 = new DeleteExpr(table, null);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing DeleteExpr with a different Expr type.
        /// </summary>
        [Fact]
        public void Equals_DifferentExprType_ReturnsFalse()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var deleteExpr = new DeleteExpr(table, Expr.Prop("Age") > 100);
            var otherExpr = new TableExpr(typeof(string));

            // Act
            var result = deleteExpr.Equals(otherExpr);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that GetHashCode returns consistent values when called multiple times on the same instance.
        /// </summary>
        [Fact]
        public void GetHashCode_CalledMultipleTimes_ReturnsSameValue()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var where = Expr.Prop("Age") > 100;
            var deleteExpr = new DeleteExpr(table, where);

            // Act
            var hashCode1 = deleteExpr.GetHashCode();
            var hashCode2 = deleteExpr.GetHashCode();
            var hashCode3 = deleteExpr.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
            Assert.Equal(hashCode2, hashCode3);
        }

        /// <summary>
        /// Tests that GetHashCode returns the same value for two DeleteExpr instances with equal Table and Where properties.
        /// </summary>
        [Fact]
        public void GetHashCode_EqualInstances_ReturnsSameHashCode()
        {
            // Arrange
            var table1 = new TableExpr(typeof(string));
            var table2 = new TableExpr(typeof(string));
            var where1 = Expr.Prop("Age") > 100;
            var where2 = Expr.Prop("Age") > 100;
            var deleteExpr1 = new DeleteExpr(table1, where1);
            var deleteExpr2 = new DeleteExpr(table2, where2);

            // Act
            var hashCode1 = deleteExpr1.GetHashCode();
            var hashCode2 = deleteExpr2.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests GetHashCode when both Table and Where properties are null.
        /// </summary>
        [Fact]
        public void GetHashCode_BothTableAndWhereNull_ReturnsValidHashCode()
        {
            // Arrange
            var deleteExpr = new DeleteExpr();

            // Act
            var hashCode = deleteExpr.GetHashCode();

            // Assert
            Assert.IsType<int>(hashCode);
        }

        /// <summary>
        /// Tests GetHashCode when Table is set but Where is null.
        /// </summary>
        [Fact]
        public void GetHashCode_TableSetWhereNull_ReturnsValidHashCode()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var deleteExpr = new DeleteExpr(table, null);

            // Act
            var hashCode = deleteExpr.GetHashCode();

            // Assert
            Assert.IsType<int>(hashCode);
        }

        /// <summary>
        /// Tests GetHashCode when Where is set but Table is null.
        /// </summary>
        [Fact]
        public void GetHashCode_WhereSetTableNull_ReturnsValidHashCode()
        {
            // Arrange
            var where = Expr.Prop("Age") > 100;
            var deleteExpr = new DeleteExpr { Where = where };

            // Act
            var hashCode = deleteExpr.GetHashCode();

            // Assert
            Assert.IsType<int>(hashCode);
        }

        /// <summary>
        /// Tests GetHashCode when both Table and Where properties are set.
        /// </summary>
        [Fact]
        public void GetHashCode_BothTableAndWhereSet_ReturnsValidHashCode()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var where = Expr.Prop("Age") > 100;
            var deleteExpr = new DeleteExpr(table, where);

            // Act
            var hashCode = deleteExpr.GetHashCode();

            // Assert
            Assert.IsType<int>(hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns different values for DeleteExpr instances with different Table properties.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentTables_ReturnsDifferentHashCodes()
        {
            // Arrange
            var table1 = new TableExpr(typeof(string));
            var table2 = new TableExpr(typeof(int));
            var where = Expr.Prop("Age") > 100;
            var deleteExpr1 = new DeleteExpr(table1, where);
            var deleteExpr2 = new DeleteExpr(table2, where);

            // Act
            var hashCode1 = deleteExpr1.GetHashCode();
            var hashCode2 = deleteExpr2.GetHashCode();

            // Assert
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different values for DeleteExpr instances with different Where properties.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentWhereConditions_ReturnsDifferentHashCodes()
        {
            // Arrange
            var table = new TableExpr(typeof(string));
            var where1 = Expr.Prop("Age") > 100;
            var where2 = Expr.Prop("Age") > 50;
            var deleteExpr1 = new DeleteExpr(table, where1);
            var deleteExpr2 = new DeleteExpr(table, where2);

            // Act
            var hashCode1 = deleteExpr1.GetHashCode();
            var hashCode2 = deleteExpr2.GetHashCode();

            // Assert
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that two instances with null Table and Where properties have the same hash code.
        /// </summary>
        [Fact]
        public void GetHashCode_BothInstancesWithNullProperties_ReturnsSameHashCode()
        {
            // Arrange
            var deleteExpr1 = new DeleteExpr();
            var deleteExpr2 = new DeleteExpr();

            // Act
            var hashCode1 = deleteExpr1.GetHashCode();
            var hashCode2 = deleteExpr2.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }
    }
}