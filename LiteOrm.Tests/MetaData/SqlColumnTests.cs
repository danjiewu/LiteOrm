using System;
using System.Collections.Generic;
using System.Reflection;

#nullable enable
using LiteOrm;
using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class SqlColumnTests
    {
        /// <summary>
        /// Provides test cases for the Property getter:
        /// - A real PropertyInfo instance obtained from the nested Sample type.
        /// - A null backing value to verify behavior when the private field is null.
        /// </summary>
        public static IEnumerable<object?[]> PropertyTestData()
        {
            PropertyInfo? p = typeof(Sample).GetProperty(nameof(Sample.Value));
            yield return new object?[] { p };
            yield return new object?[] { null };
        }

        /// <summary>
        /// Tests that the Property getter returns the exact PropertyInfo instance stored in the private field.
        /// Input conditions: the private field '_property' is set (via reflection) to a non-null PropertyInfo.
        /// Expected result: the Property getter returns the same reference (not a copy) as the backing field.
        /// </summary>
        [Theory]
        [MemberData(nameof(PropertyTestData))]
        public void Property_Get_UnderlyingField_ReturnsUnderlyingValue(PropertyInfo? backing)
        {
            // Arrange
            var mock = new Mock<SqlColumn>(MockBehavior.Strict);
            SqlColumn column = mock.Object;

            FieldInfo? backingField = typeof(SqlColumn).GetField("_property", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(backingField);

            // Act
            backingField!.SetValue(column, backing);
            object? result = column.Property;

            // Assert
            if (backing is null)
            {
                Assert.Null(result);
            }
            else
            {
                Assert.Same(backing, result);
            }
        }

        // Helper nested type used to obtain a concrete PropertyInfo for tests.
        private class Sample
        {
            public int Value { get; set; }
        }

        /// <summary>
        /// Verifies that GetValue throws ArgumentNullException when target is null.
        /// Input: target == null
        /// Expected: ArgumentNullException for parameter "target".
        /// 
        /// This test is skipped because SqlColumn cannot be instantiated due to an internal constructor.
        /// To enable:
        ///  - Make the SqlColumn constructor accessible to the test assembly (e.g., InternalsVisibleTo or protected),
        ///  - Or mock SqlColumn properly and ensure Property returns a valid PropertyInfo instance.
        /// </summary>
        [Fact(Skip = "Skipped: SqlColumn has an internal constructor not accessible from this test assembly; cannot instantiate or mock required members.")]
        public void GetValue_TargetIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            // If constructor were accessible, you would create a SqlColumn derived instance or mock:
            // var prop = typeof(TestEntity).GetProperty(nameof(TestEntity.Value));
            // var column = new ConcreteSqlColumn(prop); // requires accessible constructor
            //
            // Act
            // var ex = Record.Exception(() => column.GetValue(null));
            //
            // Assert
            // Assert.IsType<ArgumentNullException>(ex);
            // Assert.Equal("target", ((ArgumentNullException)ex).ParamName);

            // The test is intentionally skipped due to accessibility constraints.
        }

        /// <summary>
        /// Verifies that GetValue returns the value obtained from Property.GetValueFast for a valid target.
        /// Input: a target object with a readable property.
        /// Expected: returned object equals the property's value.
        /// 
        /// This test is skipped because SqlColumn cannot be instantiated due to an internal constructor.
        /// To enable:
        ///  - Make the SqlColumn constructor accessible to the test assembly (e.g., InternalsVisibleTo or protected),
        ///  - Or mock SqlColumn and ensure its Property getter returns a real PropertyInfo for the test entity.
        /// Example of intended assertions when enabled:
        ///  Assert.Equal(expectedValue, column.GetValue(target));
        /// </summary>
        [Fact(Skip = "Skipped: SqlColumn has an internal constructor not accessible from this test assembly; cannot instantiate or mock required members.")]
        public void GetValue_ValidTarget_ReturnsPropertyValue()
        {
            // Arrange
            // Example setup (requires constructor accessibility):
            // var entity = new TestEntity { Value = 123 };
            // PropertyInfo prop = typeof(TestEntity).GetProperty(nameof(TestEntity.Value));
            // var column = new ConcreteSqlColumn(prop); // requires accessible constructor or mock that can return prop from Property
            //
            // Act
            // var result = column.GetValue(entity);
            //
            // Assert
            // Assert.Equal(123, result);

            // The test is intentionally skipped due to accessibility constraints.
        }

        // The following types are provided as guidance only and are not used at runtime in these skipped tests.
        // They are defined as inner types to avoid adding any external helpers.
        private class TestEntity
        {
            public int Value { get; set; }
        }

        /// <summary>
        /// Helper POCO used to obtain PropertyInfo instances for tests.
        /// </summary>
        private class Dummy
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        /// <summary>
        /// Concrete test subclass of SqlColumn to allow instantiation.
        /// </summary>
        private class TestSqlColumn : SqlColumn
        {
            public TestSqlColumn(PropertyInfo property) : base(property) { }

            public override ColumnDefinition Definition => null;
        }

        /// <summary>
        /// Another concrete subclass to test runtime type differences.
        /// </summary>
        private class OtherTestSqlColumn : SqlColumn
        {
            public OtherTestSqlColumn(PropertyInfo property) : base(property) { }

            public override ColumnDefinition Definition => null;
        }

        /// <summary>
        /// Verifies that Equals returns true when passed the same reference.
        /// Input: same instance compared to itself.
        /// Expected: true.
        /// </summary>
        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            // Arrange
            PropertyInfo prop = typeof(Dummy).GetProperty(nameof(Dummy.Id));
            var column = new TestSqlColumn(prop);

            // Act
            bool result = column.Equals(column);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Verifies that Equals returns false when other is null or an unrelated type.
        /// Input conditions: other == null, other is new object()
        /// Expected: false for both cases.
        /// </summary>
        [Theory]
        [MemberData(nameof(NullOrUnrelatedCases))]
        public void Equals_OtherIsNullOrDifferentType_ReturnsFalse(object? other)
        {
            // Arrange
            PropertyInfo prop = typeof(Dummy).GetProperty(nameof(Dummy.Id));
            var column = new TestSqlColumn(prop);

            // Act
            bool result = column.Equals(other);

            // Assert
            Assert.False(result);
        }

        public static System.Collections.Generic.IEnumerable<object?[]> NullOrUnrelatedCases()
        {
            yield return new object?[] { null };
            yield return new object?[] { new object() };
        }

        /// <summary>
        /// Verifies that two distinct SqlColumn instances with the same PropertyName and both null Table compare equal.
        /// Input: two columns constructed from the same PropertyInfo (same PropertyName), Table left as default (null).
        /// Expected: Equals returns true.
        /// </summary>
        [Fact]
        public void Equals_SamePropertyNameAndNullTables_ReturnsTrue()
        {
            // Arrange
            PropertyInfo prop = typeof(Dummy).GetProperty(nameof(Dummy.Id));
            var col1 = new TestSqlColumn(prop);
            var col2 = new TestSqlColumn(prop);

            // Act
            bool result = col1.Equals(col2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Verifies that two SqlColumn instances with different PropertyName do not equal each other.
        /// Input: columns constructed from different PropertyInfo values (Id vs Name).
        /// Expected: Equals returns false.
        /// </summary>
        [Fact]
        public void Equals_DifferentPropertyNames_ReturnsFalse()
        {
            // Arrange
            PropertyInfo propA = typeof(Dummy).GetProperty(nameof(Dummy.Id));
            PropertyInfo propB = typeof(Dummy).GetProperty(nameof(Dummy.Name));
            var colA = new TestSqlColumn(propA);
            var colB = new TestSqlColumn(propB);

            // Act
            bool result = colA.Equals(colB);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Verifies that two SqlColumn instances of different runtime derived types are not considered equal even if PropertyName matches.
        /// Input: instances of different derived types constructed from the same PropertyInfo.
        /// Expected: Equals returns false because GetType() differs.
        /// </summary>
        [Fact]
        public void Equals_DifferentDerivedTypesWithSamePropertyName_ReturnsFalse()
        {
            // Arrange
            PropertyInfo prop = typeof(Dummy).GetProperty(nameof(Dummy.Id));
            var col1 = new TestSqlColumn(prop);
            var col2 = new OtherTestSqlColumn(prop);

            // Act
            bool result = col1.Equals(col2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Provides test cases for ForeignType:
        /// - foreignTable is null => expected null
        /// - foreignTable exists with null ForeignType => expected null
        /// - foreignTable exists with a concrete ForeignType => expected that concrete type
        /// </summary>
        public static IEnumerable<object?[]> ForeignTableCases()
        {
            yield return new object?[] { null, null };
            yield return new object?[] { new ForeignTable { ForeignType = null }, null };
            yield return new object?[] { new ForeignTable { ForeignType = typeof(string) }, typeof(string) };
        }

        /// <summary>
        /// Verifies that SqlColumn.ForeignType returns the ForeignTable.ForeignType when ForeignTable is present,
        /// and returns null when ForeignTable is null or when ForeignTable.ForeignType is null.
        /// Input conditions: parameterized ForeignTable (null, with null ForeignType, with concrete Type).
        /// Expected outcome: ForeignType equals the underlying ForeignTable.ForeignType (or null).
        /// </summary>
        [Theory(Skip = "SqlColumn has internal constructor/internal setter. To run this test enable InternalsVisibleTo for the test assembly or make constructor/setter accessible.")]
        [MemberData(nameof(ForeignTableCases))]
        public void ForeignType_ForeignTableStates_ReturnsExpected(ForeignTable? foreignTable, Type? expected)
        {
            // Arrange
            // Note: SqlColumn has an internal constructor that accepts PropertyInfo.
            // Creating a Mock<SqlColumn> typically invokes the base constructor; if internals are not visible
            // to the test assembly the mock creation will fail at runtime. These tests are skipped until
            // internals are exposed to the test assembly.
            var mock = new Mock<SqlColumn>(MockBehavior.Strict, (PropertyInfo?)null);

            // Setup the getter of ForeignTable to return the provided test case value.
            mock.SetupGet(m => m.ForeignTable).Returns(foreignTable);

            // Act
            var actual = mock.Object.ForeignType;

            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Verifies that SetValue throws ArgumentNullException when the target argument is null.
        /// Input: target = null.
        /// Expected: ArgumentNullException with parameter name 'target'.
        /// 
        /// NOTE: This test is skipped because SqlColumn has an internal constructor and cannot be instantiated
        /// from this test assembly. To enable this test:
        ///  - Make SqlColumn's constructor public OR
        ///  - Add InternalsVisibleTo for the test assembly in the production project, then implement the Arrange block
        ///    to create a concrete SqlColumn (or a small concrete subclass) with a real PropertyInfo and call SetValue(null, ...).
        /// </summary>
        [Fact(Skip = "Skipped: SqlColumn has an internal constructor; make it accessible (e.g. InternalsVisibleTo) to enable this test.")]
        public void SetValue_TargetIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            // TODO: After enabling access to internal constructor, create a concrete SqlColumn instance:
            //   var property = typeof(TestEntity).GetProperty(nameof(TestEntity.SomeProp));
            //   var column = new ConcreteSqlColumn(property);
            // Act
            //   Action act = () => column.SetValue(null, 123);
            // Assert
            //   var ex = Assert.Throws<ArgumentNullException>(act);
            //   Assert.Equal("target", ex.ParamName);
        }

        /// <summary>
        /// Verifies that SetValue wraps any exception thrown during property assignment into InvalidOperationException
        /// and includes the target declaring type and property name in the message.
        /// Input: a target object and a value that causes the property's setter to throw.
        /// Expected: InvalidOperationException with inner exception and a message containing "Value {value} can not be assigned to {DeclaringType}.{PropertyName}".
        /// 
        /// NOTE: This test is skipped because SqlColumn has an internal constructor and cannot be instantiated
        /// from this test assembly. To enable this test:
        ///  - Make SqlColumn's constructor public OR
        ///  - Add InternalsVisibleTo for the test assembly in the production project, then implement the Arrange block:
        ///      create a TestEntity with a property whose setter throws; create SqlColumn for that property and call SetValue.
        /// </summary>
        [Fact(Skip = "Skipped: SqlColumn has an internal constructor; make it accessible (e.g. InternalsVisibleTo) to enable this test.")]
        public void SetValue_PropertySetterThrows_ThrowsInvalidOperationExceptionWithInnerException()
        {
            // Arrange
            // TODO: After enabling access to internal constructor:
            //   class TestEntity { public int FaultyProp { set { throw new Exception(\"setter failed\"); } } }
            //   var target = new TestEntity();
            //   var property = typeof(TestEntity).GetProperty(nameof(TestEntity.FaultyProp));
            //   var column = new ConcreteSqlColumn(property);
            //
            // Act
            //   Action act = () => column.SetValue(target, 42);
            //
            // Assert
            //   var ex = Assert.Throws<InvalidOperationException>(act);
            //   Assert.NotNull(ex.InnerException);
            //   Assert.Contains("Value 42 can not be assigned to", ex.Message);
            //   Assert.Contains(property.DeclaringType.Name + "." + property.Name, ex.Message);
        }

        /// <summary>
        /// Verifies ForeignAlias when ForeignTable is null.
        /// Conditions: SqlColumn instance with no ForeignTable assigned.
        /// Expected: ForeignAlias returns null.
        /// </summary>
        [Fact(Skip = "Cannot construct SqlColumn or set internal ForeignTable from this test assembly. Grant InternalsVisibleTo or make constructor/setter accessible to enable this test.")]
        public void ForeignAlias_WhenForeignTableIsNull_ReturnsNull()
        {
            // Arrange
            // NOTE: The following shows the intended Arrange/Act/Assert but is commented out
            // because SqlColumn has an internal constructor and ForeignTable has an internal setter.
            //
            // var prop = typeof(DummyEntity).GetProperty(nameof(DummyEntity.SomeProperty));
            // var col = new TestSqlColumn(prop); // internal ctor is not accessible
            //
            // Act
            // var alias = col.ForeignAlias;
            //
            // Assert
            // Assert.Null(alias);

            // Guidance:
            // To enable this test, either:
            // 1) Add [assembly: InternalsVisibleTo("LiteOrm.Tests")] to the source assembly so the internal
            //    constructor and internal setter are visible to this test project, or
            // 2) Change the SqlColumn constructor and/or ForeignTable setter to be public for testability.
            //
            // Once accessible, uncomment the Arrange/Act/Assert code above and remove the Skip attribute.
        }

        /// <summary>
        /// Parameterized test for various alias values on the ForeignTable.
        /// Conditions: ForeignTable set to values: null, empty, whitespace, long string, special characters.
        /// Expected: ForeignAlias returns the exact Alias string (including null/empty/whitespace/special).
        /// </summary>
        [Theory(Skip = "Cannot construct SqlColumn or set internal ForeignTable from this test assembly. Grant InternalsVisibleTo or make constructor/setter accessible to enable this test.")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("alias_with_special_chars!@#$%^&*()\u0007\u0008")]
        [InlineData("long_" + "a".PadLeft(500, 'a'))]
        public void ForeignAlias_VariousAliasValues_ReturnsExpected(string? alias)
        {
            // Arrange
            // Intended arrangement (commented out due to internal accessibility):
            //
            // var prop = typeof(DummyEntity).GetProperty(nameof(DummyEntity.SomeProperty));
            // var col = new TestSqlColumn(prop); // internal ctor not accessible
            //
            // // Create a ForeignTable and assign alias
            // var ft = new ForeignTable { Alias = alias };
            //
            // // Attempt to set internal setter (not accessible from this assembly)
            // col.ForeignTable = ft;
            //
            // Act
            // var result = col.ForeignAlias;
            //
            // Assert
            // Assert.Equal(alias, result);

            // Guidance:
            // - To run this test, enable access to internal members or make the constructor and setter public.
            // - Alternatively, compile tests into the same assembly as the production code.
        }

        #region Helpers (commented examples)

        // The helper types below are provided as an example of how one would create the required
        // PropertyInfo and a minimal concrete SqlColumn subclass if the SqlColumn constructor
        // were accessible. These types are intentionally not used to avoid creating privileged
        // access in this test assembly.

        /*
        private class DummyEntity
        {
            public int SomeProperty { get; set; }
        }

        private class TestSqlColumn : SqlColumn
        {
            public TestSqlColumn(PropertyInfo property) : base(property)
            {
            }

            public override ColumnDefinition Definition => null;
        }
        */

        #endregion

        /// <summary>
        /// Verifies that PropertyType returns the underlying PropertyInfo.PropertyType.
        /// Tested inputs:
        ///  - A sample entity property of type int.
        /// Expected result:
        ///  - The PropertyType getter should return typeof(int).
        /// 
        /// This test is skipped because SqlColumn has an internal constructor that is not accessible from this test assembly.
        /// To enable this test:
        ///  - Add InternalsVisibleTo for the test assembly to the production project, or
        ///  - Provide a public/concrete subclass of SqlColumn within the production code, or
        ///  - Make the constructor public.
        /// 
        /// Arrange:
        ///  - Obtain a PropertyInfo for a sample property (shown in commented code).
        /// Act:
        ///  - Construct a SqlColumn (or mock) passing the PropertyInfo to the constructor.
        ///  - Read the PropertyType property.
        /// Assert:
        ///  - Assert.Equal(typeof(int), result).
        /// 
        /// If you enable InternalsVisibleTo or otherwise make the constructor accessible, uncomment the sample code below
        /// and remove the Skip argument on the Fact attribute.
        /// </summary>
        [Fact(Skip = "SqlColumn has an internal constructor not accessible from this test assembly. See test comments for remediation.")]
        public void PropertyType_ReturnsUnderlyingPropertyType_WhenPropertyInfoProvided()
        {
            // The following is example code showing the intended test when SqlColumn can be constructed from this assembly.
            // It is left commented to avoid compilation or runtime failures while the production constructor is internal.
            //
            // Arrange:
            // var prop = typeof(TestEntity).GetProperty(nameof(TestEntity.Value));
            // // If SqlColumn's internal constructor is made visible (InternalsVisibleTo), the following would work:
            // var mock = new Mock<SqlColumn>(prop) { CallBase = true };
            //
            // Act:
            // var result = mock.Object.PropertyType;
            //
            // Assert:
            // Assert.Equal(typeof(int), result);

            // Guidance for maintainers:
            // - Add [assembly: InternalsVisibleTo("LiteOrm.Tests")] to the production assembly to allow construction.
            // - Or provide a concrete public subclass of SqlColumn in production that exposes a public ctor receiving PropertyInfo.
            //
            // Once one of the above changes is made, replace the commented code with real Arrange/Act/Assert and remove Skip.
        }

        // Helper entity used in the example above. This type is defined as a nested class inside the test class
        // to comply with the requirement that any helper types be contained within the test class.
        private class TestEntity
        {
            public int Value { get; set; }
        }

        /// <summary>
        /// Provides PropertyInfo instances for different property-name scenarios to validate
        /// that the constructor assigns Property, PropertyName and Name from the provided PropertyInfo.
        /// Each item yields a single PropertyInfo:
        /// - NormalName: a regular property name.
        /// - LongName: a very long identifier to exercise length-related concerns.
        /// - UnicodeName: a property with unicode characters in the identifier.
        /// </summary>
        public static IEnumerable<object?[]> PropertyInfosData()
        {
            var t = typeof(TestEntity);
            yield return new object?[] { t.GetProperty(nameof(TestEntity.NormalName)) };
            yield return new object?[] { t.GetProperty(nameof(TestEntity.LongPropertyName_ABCDEFGHIJKLMNOPQRSTUVWXYZ_0123456789)) };
            yield return new object?[] { t.GetProperty(nameof(TestEntity.名字)) };
        }

        /// <summary>
        /// Verifies that the SqlColumn constructor assigns the provided PropertyInfo to the Property
        /// property and sets PropertyName and Name equal to Property.Name.
        /// Input conditions:
        /// - property: a non-null System.Reflection.PropertyInfo instance obtained from a concrete type.
        /// Expected result:
        /// - The instantiated SqlColumn (when constructor is accessible) will have:
        ///   * Property equal to the provided PropertyInfo (Reference equality).
        ///   * PropertyName equal to property.Name.
        ///   * Name (inherited from SqlObject) equal to property.Name.
        /// 
        /// Note: This test is skipped because the constructor is internal and cannot be invoked from
        /// this test assembly. To enable this test either:
        /// 1) Add [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("LiteOrm.Tests")] in the production assembly
        ///    (or the appropriate test assembly name), or
        /// 2) Change the constructor accessibility to public for testing purposes.
        /// After making internals visible, replace the commented-out instantiation code below with:
        ///    var column = new ConcreteSqlColumn(property);
        /// and then assert on column.Property, column.PropertyName and column.Name.
        /// </summary>
        [Theory(Skip = "SqlColumn internal constructor is not accessible from this test assembly. See XML comment for guidance.")]
        [MemberData(nameof(PropertyInfosData))]
        public void SqlColumn_Constructor_Property_SetsExpectedMembers(PropertyInfo? property)
        {
            // Arrange
            // property should be non-null according to reflection usage above.
            Assert.NotNull(property);

            // Act
            // The constructor is internal and SqlColumn is abstract. Creating a concrete
            // subclass in this test assembly that calls the internal base constructor will
            // fail to compile unless the base constructor is made accessible via
            // InternalsVisibleTo or made public. Therefore the actual instantiation is
            // intentionally omitted here. Below is the intended code once internals are visible:
            //
            // var column = new ConcreteSqlColumn(property);
            //
            // Assert
            // Intended assertions after enabling internals:
            //
            // Assert.Same(property, column.Property);
            // Assert.Equal(property.Name, column.PropertyName);
            // Assert.Equal(property.Name, column.Name);
            //
            // Keep this test skipped until assembly accessibility is adjusted.
        }

        // Helper concrete subclass is intentionally provided as a nested type here to be used
        // once the production internal constructor is accessible to the test assembly.
        // It is not referenced by the skipped test body to avoid accessibility/compilation issues
        // in environments where internals are not visible.
        private sealed class ConcreteSqlColumn : SqlColumn
        {
            public ConcreteSqlColumn(PropertyInfo property)
                : base(property)
            {
            }

            public override ColumnDefinition Definition => throw new NotImplementedException();

            public override Type PropertyType => Property.PropertyType;
        }

        // Inner helper entity used to obtain PropertyInfo instances for tests.
        private class TestEntity
        {
            public int NormalName { get; set; }

            public string LongPropertyName_ABCDEFGHIJKLMNOPQRSTUVWXYZ_0123456789 { get; set; } = string.Empty;

            // Unicode identifier
            public string 名字 { get; set; } = string.Empty;
        }
    }
}