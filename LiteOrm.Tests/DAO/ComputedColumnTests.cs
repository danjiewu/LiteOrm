using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    [Collection("Database")]
    public class ComputedColumnTests : TestBase
    {
        public ComputedColumnTests(DatabaseFixture fixture) : base(fixture) { }

        [Fact]
        public void BuildCreateTableSql_ExcludesComputedColumn()
        {
            var table = new AttributeTableInfoProvider().GetTableDefinition(typeof(ComputedUserModel))!;
            var sql = SQLiteBuilder.Instance.BuildCreateTableSql(table.Name!, table.Columns);

            Assert.Contains("FirstName", sql);
            Assert.DoesNotContain("FullName", sql);
        }

        [Fact]
        public async Task ComputedColumn_InsertAndRead_ReturnsExpressionResult()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<ComputedUserModel>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<ComputedUserModel>>();

            var model = new ComputedUserModel { FirstName = "John", LastName = "Smith" };
            await service.InsertAsync(model, TestContext.Current.CancellationToken);

            var retrieved = await viewService.GetObjectAsync(model.Id, cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(retrieved);
            Assert.Equal("John Smith", retrieved.FullName);
        }

        [Fact]
        public async Task ComputedColumn_QueryCondition_UsesExpression()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<ComputedUserModel>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<ComputedUserModel>>();

            await service.InsertAsync(new ComputedUserModel { FirstName = "Alice", LastName = "Brown" }, TestContext.Current.CancellationToken);
            await service.InsertAsync(new ComputedUserModel { FirstName = "Bob", LastName = "White" }, TestContext.Current.CancellationToken);

            var result = await viewService.SearchAsync(u => u.FullName == "Alice Brown", cancellationToken: TestContext.Current.CancellationToken);

            Assert.Single(result);
            Assert.Equal("Alice", result[0].FirstName);
        }

        [Fact]
        public async Task ComputedColumn_Update_DoesNotWriteExpressionColumn()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<ComputedUserModel>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<ComputedUserModel>>();

            var model = new ComputedUserModel { FirstName = "A", LastName = "B" };
            await service.InsertAsync(model, TestContext.Current.CancellationToken);

            model.LastName = "C";
            await service.UpdateAsync(model, TestContext.Current.CancellationToken);

            var retrieved = await viewService.GetObjectAsync(model.Id, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("A C", retrieved.FullName);
        }

        [Table("ComputedUserModels")]
        public class ComputedUserModel
        {
            [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
            public int Id { get; set; }

            [Column("FirstName", AllowNull = true)]
            public string? FirstName { get; set; }

            [Column("LastName", AllowNull = true)]
            public string? LastName { get; set; }

            [Column("FullName", Expression = "{FirstName} || ' ' || {LastName}", ColumnMode = ColumnMode.Computed)]
            public string? FullName { get; set; }
        }
    }
}
