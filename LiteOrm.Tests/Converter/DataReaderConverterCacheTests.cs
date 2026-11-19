using LiteOrm.Common;
using System.Data;
using Xunit;

namespace LiteOrm.Tests.Converter
{
    public class DataReaderConverterCacheTests
    {
        private sealed class Dto
        {
            public int A { get; set; }
        }

        [Fact]
        public void SchemaCache_EvictsOldestEntries_WhenOverCapacity()
        {
            int originalSize = DataReaderConverter.SchemaCacheSize;
            try
            {
                DataReaderConverter.SchemaCacheSize = 2;

                for (int i = 0; i < 6; i++)
                {
                    using var reader = CreateReader($"C{i}");
                    var mapper = DataReaderConverter.GetConverterBySchema<Dto>(reader, SqlBuilder.Instance);
                    Assert.NotNull(mapper);
                }

                Assert.Equal(2, DataReaderConverter.SchemaCacheCount);
            }
            finally
            {
                DataReaderConverter.SchemaCacheSize = originalSize;
            }
        }

        [Fact]
        public void SchemaCache_ReusesConverter_ForSameSchema()
        {
            using var first = CreateReader("A");
            using var second = CreateReader("A");

            var firstMapper = DataReaderConverter.GetConverterBySchema<Dto>(first, SqlBuilder.Instance);
            var secondMapper = DataReaderConverter.GetConverterBySchema<Dto>(second, SqlBuilder.Instance);

            Assert.Same(firstMapper, secondMapper);
        }

        private static DataTableReader CreateReader(string columnName)
        {
            var table = new DataTable();
            table.Columns.Add(columnName, typeof(int));
            table.Rows.Add(1);
            return table.CreateDataReader();
        }
    }
}
