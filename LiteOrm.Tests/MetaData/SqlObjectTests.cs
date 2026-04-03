using System;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="SqlObject"/> class.
    /// </summary>
    public class SqlObjectTests
    {
        /// <summary>
        /// Tests that the Name property getter returns the value set by the setter.
        /// </summary>
        /// <param name="name">The name value to set and verify.</param>
        [Theory]
        [InlineData("ValidName")]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t")]
        [InlineData("\n")]
        [InlineData("Name with spaces")]
        [InlineData("Name_With_Underscores")]
        [InlineData("Name123")]
        [InlineData("名前")] // Unicode characters
        [InlineData("Name\u0000WithNull")] // Control character
        [InlineData("A very long name that exceeds typical length expectations to test boundary conditions for string handling in the Name property")]
        public void Name_SetValue_ReturnsSetValue(string name)
        {
            // Arrange
            var sqlObject = new TestSqlObject();

            // Act
            sqlObject.SetName(name);

            // Assert
            Assert.Equal(name, sqlObject.Name);
        }

        /// <summary>
        /// Tests that the Name property can be set to null and returns null when accessed.
        /// </summary>
        [Fact]
        public void Name_SetNull_ReturnsNull()
        {
            // Arrange
            var sqlObject = new TestSqlObject();

            // Act
            sqlObject.SetName(null);

            // Assert
            Assert.Null(sqlObject.Name);
        }

        /// <summary>
        /// Tests that the Name property returns null by default when not explicitly set.
        /// </summary>
        [Fact]
        public void Name_DefaultValue_ReturnsNull()
        {
            // Arrange & Act
            var sqlObject = new TestSqlObject();

            // Assert
            Assert.Null(sqlObject.Name);
        }

        /// <summary>
        /// Tests that the Name property can be reassigned multiple times.
        /// </summary>
        [Fact]
        public void Name_SetMultipleTimes_ReturnsLastSetValue()
        {
            // Arrange
            var sqlObject = new TestSqlObject();
            var firstName = "FirstName";
            var secondName = "SecondName";

            // Act
            sqlObject.SetName(firstName);
            sqlObject.SetName(secondName);

            // Assert
            Assert.Equal(secondName, sqlObject.Name);
        }

        /// <summary>
        /// Tests that the Name property setter can transition from null to a value.
        /// </summary>
        [Fact]
        public void Name_SetFromNullToValue_ReturnsValue()
        {
            // Arrange
            var sqlObject = new TestSqlObject();
            sqlObject.SetName(null);
            var expectedName = "NewName";

            // Act
            sqlObject.SetName(expectedName);

            // Assert
            Assert.Equal(expectedName, sqlObject.Name);
        }

        /// <summary>
        /// Tests that the Name property setter can transition from a value to null.
        /// </summary>
        [Fact]
        public void Name_SetFromValueToNull_ReturnsNull()
        {
            // Arrange
            var sqlObject = new TestSqlObject();
            sqlObject.SetName("InitialName");

            // Act
            sqlObject.SetName(null);

            // Assert
            Assert.Null(sqlObject.Name);
        }

        /// <summary>
        /// Concrete test implementation of SqlObject for testing purposes.
        /// </summary>
        private class TestSqlObject : SqlObject
        {
            /// <summary>
            /// Exposes the protected internal Name setter for testing.
            /// </summary>
            /// <param name="name">The name to set.</param>
            public void SetName(string? name)
            {
                Name = name;
            }
        }

        /// <summary>
        /// Tests that GetHashCode returns 0 when Name is null.
        /// </summary>
        [Fact]
        public void GetHashCode_WhenNameIsNull_ReturnsZero()
        {
            // Arrange
            var sqlObject = new TestSqlObject { Name = null };

            // Act
            int hashCode = sqlObject.GetHashCode();

            // Assert
            Assert.Equal(0, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns the hash code of the Name string for various non-null values.
        /// </summary>
        /// <param name="name">The name value to test.</param>
        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("TestName")]
        [InlineData("A")]
        [InlineData("   Whitespace   ")]
        [InlineData("Special!@#$%^&*()Characters")]
        [InlineData("Unicode中文")]
        [InlineData("VeryLongStringVeryLongStringVeryLongStringVeryLongStringVeryLongStringVeryLongStringVeryLongStringVeryLongString")]
        public void GetHashCode_WhenNameIsNotNull_ReturnsNameHashCode(string name)
        {
            // Arrange
            var sqlObject = new TestSqlObject { Name = name };
            int expectedHashCode = name.GetHashCode();

            // Act
            int actualHashCode = sqlObject.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, actualHashCode);
        }

        /// <summary>
        /// Tests that two SqlObject instances with the same Name return the same hash code.
        /// This ensures consistency with the Equals method.
        /// </summary>
        /// <param name="name">The name value to test.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("SameName")]
        [InlineData("AnotherName")]
        public void GetHashCode_WithEqualObjects_ReturnsSameHashCode(string name)
        {
            // Arrange
            var sqlObject1 = new TestSqlObject { Name = name };
            var sqlObject2 = new TestSqlObject { Name = name };

            // Act
            int hashCode1 = sqlObject1.GetHashCode();
            int hashCode2 = sqlObject2.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode returns consistent results when called multiple times on the same instance.
        /// </summary>
        [Fact]
        public void GetHashCode_CalledMultipleTimes_ReturnsConsistentValue()
        {
            // Arrange
            var sqlObject = new TestSqlObject { Name = "ConsistentTest" };

            // Act
            int hashCode1 = sqlObject.GetHashCode();
            int hashCode2 = sqlObject.GetHashCode();
            int hashCode3 = sqlObject.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
            Assert.Equal(hashCode2, hashCode3);
        }

        /// <summary>
        /// Tests that GetHashCode returns 0 when Name is changed to null after initialization.
        /// </summary>
        [Fact]
        public void GetHashCode_AfterSettingNameToNull_ReturnsZero()
        {
            // Arrange
            var sqlObject = new TestSqlObject { Name = "InitialName" };
            sqlObject.Name = null;

            // Act
            int hashCode = sqlObject.GetHashCode();

            // Assert
            Assert.Equal(0, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns the updated hash code when Name is changed.
        /// </summary>
        [Fact]
        public void GetHashCode_AfterChangingName_ReturnsUpdatedHashCode()
        {
            // Arrange
            var sqlObject = new TestSqlObject { Name = "OriginalName" };
            string newName = "UpdatedName";
            int expectedHashCode = newName.GetHashCode();

            // Act
            sqlObject.Name = newName;
            int actualHashCode = sqlObject.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, actualHashCode);
        }

        /// <summary>
        /// Concrete implementation of SqlObject for testing purposes.
        /// </summary>
        private class TestSqlObject : SqlObject
        {
            public new string Name
            {
                get => base.Name;
                set => base.Name = value;
            }
        }

        /// <summary>
        /// Verifies that ToString returns the value of the Name property for various string inputs including null, empty, whitespace, and normal values.
        /// </summary>
        /// <param name="name">The name value to set on the SqlObject instance.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("  \t\n")]
        [InlineData("TestName")]
        [InlineData("Complex Name With Spaces")]
        [InlineData("SpecialChars!@#$%^&*()")]
        [InlineData("Very long name that contains many characters to test the behavior with longer strings that might be used in real scenarios")]
        public void ToString_WithVariousNameValues_ReturnsNamePropertyValue(string? name)
        {
            // Arrange
            var sqlObject = new TestSqlObject { Name = name! };

            // Act
            var result = sqlObject.ToString();

            // Assert
            Assert.Equal(name, result);
        }

        /// <summary>
        /// Helper class to test the abstract SqlObject class.
        /// </summary>
        private class TestSqlObject : SqlObject
        {
            /// <summary>
            /// Gets or sets the name, exposing the protected internal setter for testing.
            /// </summary>
            public new string? Name
            {
                get => base.Name;
                set => base.Name = value!;
            }
        }

        /// <summary>
        /// Tests that Equals returns true when comparing an object to itself (same reference).
        /// </summary>
        [Fact]
        public void Equals_WithSameReference_ReturnsTrue()
        {
            // Arrange
            var sqlObject = new TestSqlObject { Name = "TestName" };

            // Act
            bool result = sqlObject.Equals(sqlObject);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing to null.
        /// </summary>
        [Fact]
        public void Equals_WithNull_ReturnsFalse()
        {
            // Arrange
            var sqlObject = new TestSqlObject { Name = "TestName" };

            // Act
            bool result = sqlObject.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing to an object of a different type.
        /// </summary>
        [Fact]
        public void Equals_WithDifferentType_ReturnsFalse()
        {
            // Arrange
            var sqlObject = new TestSqlObject { Name = "TestName" };
            var differentTypeObject = "TestName";

            // Act
            bool result = sqlObject.Equals(differentTypeObject);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing two objects with the same name value.
        /// </summary>
        /// <param name="name">The name value to test.</param>
        [Theory]
        [InlineData("TestName")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Name with spaces")]
        [InlineData("特殊字符")]
        public void Equals_WithSameNameValue_ReturnsTrue(string name)
        {
            // Arrange
            var sqlObject1 = new TestSqlObject { Name = name };
            var sqlObject2 = new TestSqlObject { Name = name };

            // Act
            bool result = sqlObject1.Equals(sqlObject2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing two objects with different name values.
        /// </summary>
        /// <param name="name1">The first name value.</param>
        /// <param name="name2">The second name value.</param>
        [Theory]
        [InlineData("TestName1", "TestName2")]
        [InlineData("Name", "name")]
        [InlineData("", "TestName")]
        [InlineData("TestName", "")]
        [InlineData("   ", "")]
        public void Equals_WithDifferentNameValue_ReturnsFalse(string name1, string name2)
        {
            // Arrange
            var sqlObject1 = new TestSqlObject { Name = name1 };
            var sqlObject2 = new TestSqlObject { Name = name2 };

            // Act
            bool result = sqlObject1.Equals(sqlObject2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both objects have null Name values.
        /// </summary>
        [Fact]
        public void Equals_WithBothNamesNull_ReturnsTrue()
        {
            // Arrange
            var sqlObject1 = new TestSqlObject();
            var sqlObject2 = new TestSqlObject();

            // Act
            bool result = sqlObject1.Equals(sqlObject2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one object has null Name and the other has a non-null Name.
        /// </summary>
        /// <param name="name">The non-null name value to test.</param>
        [Theory]
        [InlineData("TestName")]
        [InlineData("")]
        [InlineData("   ")]
        public void Equals_WithOneNullName_ReturnsFalse(string name)
        {
            // Arrange
            var sqlObject1 = new TestSqlObject { Name = name };
            var sqlObject2 = new TestSqlObject();

            // Act
            bool result1 = sqlObject1.Equals(sqlObject2);
            bool result2 = sqlObject2.Equals(sqlObject1);

            // Assert
            Assert.False(result1);
            Assert.False(result2);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing objects of different derived types even with the same name.
        /// </summary>
        [Fact]
        public void Equals_WithDifferentDerivedTypes_ReturnsFalse()
        {
            // Arrange
            var sqlObject1 = new TestSqlObject { Name = "TestName" };
            var sqlObject2 = new AnotherTestSqlObject { Name = "TestName" };

            // Act
            bool result = sqlObject1.Equals(sqlObject2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Concrete test implementation of SqlObject for testing purposes.
        /// </summary>
        private class TestSqlObject : SqlObject
        {
            public new string Name
            {
                get => base.Name;
                set => base.Name = value;
            }
        }

        /// <summary>
        /// Another concrete test implementation of SqlObject for testing type comparison.
        /// </summary>
        private class AnotherTestSqlObject : SqlObject
        {
            public new string Name
            {
                get => base.Name;
                set => base.Name = value;
            }
        }
    }
}