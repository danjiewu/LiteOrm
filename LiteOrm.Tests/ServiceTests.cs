using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using System.ComponentModel;

namespace LiteOrm.Tests
{
    public class ServiceTests : TestBase
    {
        [Fact]
        public void DataSource_Configuration_ShouldBeCorrect()
        {
            var dataSourceProvider = ServiceProvider.GetRequiredService<IDataSourceProvider>();
            var config = dataSourceProvider.GetDataSource(null);

            Assert.NotNull(config);
            Assert.Equal("DefaultConnection", config.Name);
            Assert.True(config.SyncTable);
        }

        [Fact]
        public async Task EntityService_InsertAndGetObject_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser
            {
                Name = "Test User",
                Age = 25,
                CreateTime = DateTime.Now
            };

            // Act
            bool inserted = await service.InsertAsync(user);
            var retrievedUser = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.True(inserted);
            Assert.NotNull(retrievedUser);
            Assert.Equal(user.Name, retrievedUser.Name);
            Assert.Equal(user.Age, retrievedUser.Age);
        }

        [Fact]
        public async Task EntityViewService_Search_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            await service.InsertAsync(new TestUser { Name = "User 1", Age = 20, CreateTime = DateTime.Now });
            await service.InsertAsync(new TestUser { Name = "User 2", Age = 30, CreateTime = DateTime.Now });

            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            // Act
            var users = await viewService.SearchAsync(u => u.Age > 25);

            // Assert
            Assert.Single(users);
            Assert.Equal("User 2", users[0].Name);
        }

        [Fact]
        public async Task EntityService_Update_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "Original", Age = 20, CreateTime = DateTime.Now };
            await service.InsertAsync(user);

            // Act
            user.Name = "Updated";
            bool updated = await service.UpdateAsync(user);
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.True(updated);
            Assert.Equal("Updated", retrieved.Name);
        }

        [Fact]
        public async Task EntityService_Delete_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "To Delete", Age = 20, CreateTime = DateTime.Now };
            await service.InsertAsync(user);

            // Act
            bool deleted = await service.DeleteAsync(user);
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.True(deleted);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task EntityViewService_JoinQuery_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            var dept = new TestDepartment { Name = "IT Department" };
            await deptService.InsertAsync(dept);

            var user = new TestUser
            {
                Name = "John Joined",
                Age = 30,
                CreateTime = DateTime.Now,
                DeptId = dept.Id
            };
            await userService.InsertAsync(user);

            // Act
            var viewUser = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.NotNull(viewUser);
            Assert.Equal("John Joined", viewUser.Name);
            Assert.Equal("IT Department", viewUser.DeptName);
        }

        [Fact]
        public async Task Expr_ComplexQueries_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            await service.InsertAsync(new TestUser { Name = "Alice", Age = 25, CreateTime = DateTime.Now });
            await service.InsertAsync(new TestUser { Name = "Bob", Age = 30, CreateTime = DateTime.Now });
            await service.InsertAsync(new TestUser { Name = "Charlie", Age = 35, CreateTime = DateTime.Now });
            await service.InsertAsync(new TestUser { Name = "David", Age = 40, CreateTime = DateTime.Now });

            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();

            // Act & Assert
            
            // 1. Expr.In
            var inList = await viewService.SearchAsync(Expr.Property("Name").In("Alice", "Bob"));
            Assert.Equal(2, inList.Count);

            // 2. Expr.Between
            var betweenList = await viewService.SearchAsync(Expr.Property("Age").Between(30, 35));
            Assert.Equal(2, betweenList.Count);

            // 3. Expr.Like
            var likeList = await viewService.SearchAsync(Expr.Property("Name").Like("Cha%"));
            Assert.Single(likeList);
            Assert.Equal("Charlie", likeList[0].Name);

            // 4. Combined And/Or
            var combinedList = await viewService.SearchAsync(
                (Expr.Property("Age") < 30) | (Expr.Property("Age") >= 40)
            );
            Assert.Equal(2, combinedList.Count);

            // 5. Lambda complex
            var lambdaList = await viewService.SearchAsync(Expr.Exp<TestUser>(u => u.Age > 30 && u.Name!.Contains("i")));
            // Charlie(35), David(40) -> both contain 'i'
            Assert.Equal(2, lambdaList.Count);
        }

        [Fact]
        public async Task Hierarchical_Query_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var root = new TestDepartment { Name = "Headquarters" };
            await deptService.InsertAsync(root);

            var sub1 = new TestDepartment { Name = "HR", ParentId = root.Id };
            var sub2 = new TestDepartment { Name = "IT", ParentId = root.Id };
            await deptService.InsertAsync(sub1);
            await deptService.InsertAsync(sub2);

            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestDepartment>>();

            // Act
            var subDepts = await viewService.SearchAsync(Expr.Exp<TestDepartment>(d => d.ParentId == root.Id));

            // Assert
            Assert.Equal(2, subDepts.Count);
            Assert.Contains(subDepts, d => d.Name == "HR");
            Assert.Contains(subDepts, d => d.Name == "IT");
        }

        [Fact]
        public async Task EntityService_BatchOperations_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var users = new List<TestUser>
            {
                new TestUser { Name = "Batch 1", Age = 10, CreateTime = DateTime.Now },
                new TestUser { Name = "Batch 2", Age = 20, CreateTime = DateTime.Now }
            };

            // Act - Batch Insert
            await service.BatchInsertAsync(users);
            var inserted = await viewService.SearchAsync(Expr.Exp<TestUser>(u => u.Name!.StartsWith("Batch")));
            Assert.Equal(2, inserted.Count);

            // Act - Batch Update
            foreach (var u in inserted) u.Age += 5;
            await service.BatchUpdateAsync(inserted);
            var updated = await viewService.SearchAsync(Expr.Exp<TestUser>(u => u.Name!.StartsWith("Batch")));
            Assert.All(updated, u => Assert.True(u.Age == 15 || u.Age == 25));

            // Act - Batch Delete
            await service.BatchDeleteAsync(updated);
            var deletedCount = await viewService.CountAsync(Expr.Exp<TestUser>(u => u.Name!.StartsWith("Batch")));
            Assert.Equal(0, deletedCount);
        }

        [Fact]
        public async Task EntityService_UpdateValues_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateValue", Age = 10, CreateTime = DateTime.Now };
            await service.InsertAsync(user);

            // Act
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = await service.UpdateValuesAsync(updateValues, Expr.Exp<TestUser>(u => u.Name == "UpdateValue"), null);
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.Equal(1, affected);
            Assert.Equal(99, retrieved?.Age);
        }

        [Fact]
        public async Task EntityViewService_SearchOne_Exists_Count_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            await service.InsertAsync(new TestUser { Name = "Unique", Age = 50, CreateTime = DateTime.Now });

            // Act
            var one = await viewService.SearchOneAsync(Expr.Exp<TestUser>(u => u.Name == "Unique"));
            bool exists = await viewService.ExistsAsync(Expr.Exp<TestUser>(u => u.Name == "Unique"));
            int count = await viewService.CountAsync(Expr.Exp<TestUser>(u => u.Age >= 50));

            // Assert
            Assert.NotNull(one);
            Assert.True(exists);
            Assert.True(count >= 1);
        }

        [Fact]
        public async Task EntityViewService_SearchWithOrder_Section_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            await service.BatchInsertAsync(new[]
            {
                new TestUser { Name = "Order 1", Age = 10, CreateTime = DateTime.Now },
                new TestUser { Name = "Order 2", Age = 20, CreateTime = DateTime.Now },
                new TestUser { Name = "Order 3", Age = 30, CreateTime = DateTime.Now }
            });

            // Act - Order
            var ordered = await viewService.SearchWithOrderAsync(
                Expr.Exp<TestUser>(u => u.Name!.StartsWith("Order")), 
                new[] { new Sorting("Age", ListSortDirection.Descending) }
            );

            // Act - Section
            var section = await viewService.SearchSectionAsync(
                Expr.Exp<TestUser>(u => u.Name!.StartsWith("Order")),
                new PageSection(0, 2, new Sorting("Age", ListSortDirection.Ascending))
            );

            // Assert
            Assert.Equal("Order 3", ordered[0].Name);
            Assert.Equal(2, section.Count);
            Assert.Equal("Order 1", section[0].Name);
        }

        [Fact]
        public async Task EntityViewService_TestDepartmentView_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestDepartmentView>>();
            
            var root = new TestDepartment { Name = "Root" };
            await service.InsertAsync(root);
            
            var child = new TestDepartment { Name = "Child", ParentId = root.Id };
            await service.InsertAsync(child);

            // Act
            var view = await viewService.SearchOneAsync(Expr.Exp<TestDepartmentView>(d => d.Id == child.Id));

            // Assert
            Assert.NotNull(view);
            Assert.Equal("Root", view.ParentName);
        }

        [Fact]
        public async Task EntityService_UpdateOrInsert_Batch_ForEach_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            
            var user = new TestUser { Name = "Upsert Me", Age = 30, CreateTime = DateTime.Now };

            // Act - UpdateOrInsert (Insert)
            bool inserted = await service.UpdateOrInsertAsync(user);
            Assert.True(inserted);
            Assert.True(user.Id > 0);

            // Act - UpdateOrInsert (Update)
            user.Age = 35;
            bool updated = await service.UpdateOrInsertAsync(user);
            Assert.True(updated);
            var retrieved = await viewService.GetObjectAsync(user.Id);
            Assert.Equal(35, retrieved?.Age);

            // Act - Batch (Mixed)
            var newUser = new TestUser { Name = "Mixed 1", Age = 10, CreateTime = DateTime.Now };
            var ops = new List<EntityOperation<TestUser>>
            {
                new EntityOperation<TestUser> { Entity = newUser, Operation = OpDef.Insert },
                new EntityOperation<TestUser> { Entity = user, Operation = OpDef.Delete }
            };
            await service.BatchAsync(ops);

            var mixedRetrieved = await viewService.SearchOneAsync(Expr.Exp<TestUser>(u => u.Name == "Mixed 1"));
            var deletedRetrieved = await viewService.GetObjectAsync(user.Id);

            Assert.NotNull(mixedRetrieved);
            Assert.Null(deletedRetrieved);

            // Act - ForEachAsync
            int forEachCount = 0;
            await viewService.ForEachAsync(Expr.Exp<TestUser>(u => u.Name == "Mixed 1"), async u => 
            {
                forEachCount++;
                await Task.CompletedTask;
            });
            Assert.Equal(1, forEachCount);
        }

        [Fact]
        public async Task ForeignExpr_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();

            var dept = new TestDepartment { Name = "Foreign Dept" };
            await deptService.InsertAsync(dept);

            await userService.InsertAsync(new TestUser { Name = "User In Dept", DeptId = dept.Id, CreateTime = DateTime.Now });
            await userService.InsertAsync(new TestUser { Name = "User Outside", DeptId = -1, CreateTime = DateTime.Now });

            // Act
            // 使用 ForeignExpr 进行关联查询 (基于 EXISTS 子查询)
            // 查找所属部门名称为 "Foreign Dept" 的用户
            var users = await viewService.SearchAsync(Expr.Foreign("DeptId", Expr.Property("Name") == "Foreign Dept"));

            // Assert
            Assert.Single(users);
            Assert.Equal("User In Dept", users[0].Name);
        }

        [Fact]
        public async Task ForeignExpr_Combined_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();

            var dept1 = new TestDepartment { Name = "Dept 1" };
            await deptService.InsertAsync(dept1);
            var dept2 = new TestDepartment { Name = "Dept 2" };
            await deptService.InsertAsync(dept2);

            await userService.InsertAsync(new TestUser { Name = "User A", Age = 20, DeptId = dept1.Id, CreateTime = DateTime.Now });
            await userService.InsertAsync(new TestUser { Name = "User B", Age = 30, DeptId = dept1.Id, CreateTime = DateTime.Now });
            await userService.InsertAsync(new TestUser { Name = "User C", Age = 30, DeptId = dept2.Id, CreateTime = DateTime.Now });

            // Act
            // 查找年龄为 30 且所属部门名为 "Dept 1" 的用户
            var users = await viewService.SearchAsync(
                (Expr.Property("Age") == 30) & Expr.Foreign("DeptId", Expr.Property("Name") == "Dept 1")
            );

            // Assert
            Assert.Single(users);
            Assert.Equal("User B", users[0].Name);
        }

        [Fact]
        public async Task CustomService_ShouldBeResolvable()
        {
            // Arrange
            var customService = ServiceProvider.GetRequiredService<ITestUserService>();
            var testUser = new TestUser { Name = "CustomServiceUser", Age = 100, CreateTime = DateTime.Now };

            // Act
            await customService.InsertAsync(testUser);
            var latest = await customService.GetLatestUserAsync();

            // Assert
            Assert.NotNull(latest);
            Assert.Equal("CustomServiceUser", latest.Name);
            Assert.Equal(100, latest.Age);
        }

        [Fact]
        public async Task EntityViewService_MultiLevelJoin_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            var rootDept = new TestDepartment { Name = "Root Dept" };
            await deptService.InsertAsync(rootDept);

            var subDept = new TestDepartment { Name = "Sub Dept", ParentId = rootDept.Id };
            await deptService.InsertAsync(subDept);

            var user = new TestUser
            {
                Name = "MultiJoin User",
                Age = 25,
                CreateTime = DateTime.Now,
                DeptId = subDept.Id
            };
            await userService.InsertAsync(user);

            // Act
            var viewUser = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.NotNull(viewUser);
            Assert.Equal("MultiJoin User", viewUser.Name);
            Assert.Equal("Sub Dept", viewUser.DeptName);
            Assert.Equal("Root Dept", viewUser.ParentDeptName);

            // 根据关联表字段查询 (Act & Assert)
            
            // 1. 根据一级关联表字段查询
            var usersByDept = await viewService.SearchAsync(u => u.DeptName == "Sub Dept");
            Assert.Contains(usersByDept, u => u.Id == user.Id);

            // 2. 根据二级关联表字段查询 (多层关联)
            var usersByParentDept = await viewService.SearchAsync(u => u.ParentDeptName == "Root Dept");
            Assert.Contains(usersByParentDept, u => u.Id == user.Id);

            // 3. 组合查询
            var combinedUsers = await viewService.SearchAsync(u => u.DeptName == "Sub Dept" && u.ParentDeptName == "Root Dept");
            Assert.Single(combinedUsers);
            Assert.Equal(user.Id, combinedUsers[0].Id);

            // 4. Count 验证
            int count = await viewService.CountAsync(u => u.ParentDeptName == "Root Dept");
            Assert.Equal(1, count);
        }
    }
}
