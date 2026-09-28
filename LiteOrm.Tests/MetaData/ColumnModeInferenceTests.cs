using System.Linq;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// 列模式推导：计算列不自动可读，是否参与 SELECT 只由 <see cref="ColumnMode.Read"/> 位决定。
    /// 未显式声明 <see cref="ColumnAttribute.ColumnMode"/> 时，设置 <see cref="ColumnAttribute.Expression"/>
    /// 即视为计算列，可写属性推导为 <c>Read | Computed</c>（按表达式取值并回填），只读属性推导为
    /// <c>Computed</c>（只用于查询条件）；显式只写 <c>Computed</c> 同样为仅查询、不读出。
    /// </summary>
    public class ColumnModeInferenceTests
    {
        [Table("ModeInferenceEntities")]
        public class ModeInferenceEntity
        {
            [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
            public int Id { get; set; }

            /// <summary>未显式声明、无表达式：普通列。</summary>
            [Column("Plain")]
            public string? Plain { get; set; }

            /// <summary>未显式声明、有表达式、可写：计算列，默认可读。</summary>
            [Column("ExprWritable", Expression = "{Plain}")]
            public string? ExprWritable { get; set; }

            /// <summary>未显式声明、有表达式、只读：计算列，不回填，只用于查询条件。</summary>
            [Column("ExprReadOnly", Expression = "{Plain}")]
            public string? ExprReadOnly => Plain;

            /// <summary>显式只有 Computed：计算列，不自动可读。</summary>
            [Column("ExplicitComputed", Expression = "{Plain}", ColumnMode = ColumnMode.Computed)]
            public string? ExplicitComputed { get; set; }

            /// <summary>显式 Read | Computed：计算列，可读。</summary>
            [Column("ExplicitReadComputed", Expression = "{Plain}", ColumnMode = ColumnMode.Read | ColumnMode.Computed)]
            public string? ExplicitReadComputed { get; set; }

            /// <summary>显式 Final：计算列位不接受可访问性掩码裁剪，读取由 Read 位保留。</summary>
            [Column("ExplicitFinal", ColumnMode = ColumnMode.Final)]
            public string? ExplicitFinal { get; set; }

            /// <summary>未显式声明、无表达式、只读：按可访问性推导，只写不读。</summary>
            [Column("ReadOnlyPlain")]
            public string? ReadOnlyPlain => Plain;
        }

        private static TableDefinition Table => new AttributeTableInfoProvider().GetTableDefinition(typeof(ModeInferenceEntity))!;

        private static ColumnDefinition Column(string name) => Table.Columns.First(c => c.Name == name);

        [Fact]
        public void PlainColumn_NoExplicitMode_DerivesFull()
        {
            Assert.Equal(ColumnMode.Full, Column("Plain").Mode);
        }

        [Fact]
        public void ExpressionWithoutExplicitMode_WritableProperty_DerivesReadComputed()
        {
            ColumnDefinition column = Column("ExprWritable");

            Assert.Equal(ColumnMode.Read | ColumnMode.Computed, column.Mode);
            Assert.True(column.IsComputed);
            Assert.True(column.Mode.CanRead());
        }

        [Fact]
        public void ExpressionWithoutExplicitMode_ReadOnlyProperty_DerivesComputedOnly()
        {
            ColumnDefinition column = Column("ExprReadOnly");

            Assert.Equal(ColumnMode.Computed, column.Mode);
            Assert.True(column.IsComputed);
            Assert.False(column.Mode.CanRead());
        }

        [Fact]
        public void ExplicitComputed_DoesNotAutoRead()
        {
            ColumnDefinition column = Column("ExplicitComputed");

            Assert.Equal(ColumnMode.Computed, column.Mode);
            Assert.False(column.Mode.CanRead());
        }

        [Fact]
        public void ExplicitReadComputed_KeepsRead()
        {
            ColumnDefinition column = Column("ExplicitReadComputed");

            Assert.Equal(ColumnMode.Read | ColumnMode.Computed, column.Mode);
            Assert.True(column.Mode.CanRead());
        }

        [Fact]
        public void ExplicitFinal_KeepsReadAndInsert()
        {
            Assert.Equal(ColumnMode.Final, Column("ExplicitFinal").Mode);
        }

        [Fact]
        public void ReadOnlyColumnWithoutExpression_DerivesWriteOnly()
        {
            Assert.Equal(ColumnMode.Write, Column("ReadOnlyPlain").Mode);
        }

        [Fact]
        public void SelectColumns_IncludeOnlyReadableColumns()
        {
            string[] selected = Table.SelectColumns.Select(c => c.Name!).ToArray();

            Assert.Contains("Plain", selected);
            Assert.Contains("ExprWritable", selected);
            Assert.Contains("ExplicitReadComputed", selected);
            // 计算列没有 Read 位时不参与 SELECT，只用于查询条件
            Assert.DoesNotContain("ExprReadOnly", selected);
            Assert.DoesNotContain("ExplicitComputed", selected);
            // 只写列也不参与 SELECT
            Assert.DoesNotContain("ReadOnlyPlain", selected);
        }
    }
}
