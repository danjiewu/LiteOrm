using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using Xunit;

namespace LiteOrm.Tests
{
    [Collection("Database")]
    public class ServiceTests : TestBase
    {
        public ServiceTests(DatabaseFixture fixture) : base(fixture) { }

        [Fact]
        public void DataSource_Configuration_ShouldBeCorrect()
        {
            var dataSourceProvider = ServiceProvider.GetRequiredService<IDataSourceProvider>();
            var config = dataSourceProvider.GetDataSource(null);

            Assert.NotNull(config);
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
            bool inserted = await service.InsertAsync(user, TestContext.Current.CancellationToken);
            var retrievedUser = await viewService.GetObjectAsync(user.Id, cancellationToken: TestContext.Current.CancellationToken);

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
            await service.InsertAsync(new TestUser { Name = "User 1", Age = 20, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await service.InsertAsync(new TestUser { Name = "User 2", Age = 30, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            // Act
            var users = await viewService.SearchAsync(u => u.Age > 25, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(users);
            Assert.Equal("User 2", users[0].Name);
        }

        [Fact]
        public async Task EntityService_Update_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();
            var user = new TestUser { Name = "Original", Age = 20, CreateTime = DateTime.Now };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            // Act
            user.Name = "Updated";
            bool updated = await service.UpdateAsync(user, TestContext.Current.CancellationToken);
            var retrieved = await viewService.GetObjectAsync(user.Id, cancellationToken: TestContext.Current.CancellationToken);

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
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            // Act
            bool deleted = await service.DeleteAsync(user, TestContext.Current.CancellationToken);
            var retrieved = await viewService.GetObjectAsync(user.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(deleted);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task EntityService_BatchInsert_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var users = Enumerable.Range(1, 10).Select(i => new TestUser
            {
                Name = $"Batch User {i}",
                Age = 20 + i,
                CreateTime = DateTime.Now
            }).ToList();

            // Act
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);
            var retrievedUsers = await viewService.SearchAsync(u => u.Name.StartsWith("Batch User"), cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(10, retrievedUsers.Count);
            foreach (var user in retrievedUsers)
            {
                Assert.Contains("Batch User", user.Name);
            }
        }

        [Fact]
        public async Task EntityViewService_JoinQuery_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            var dept = new TestDepartment { Name = "IT Department" };
            await deptService.InsertAsync(dept, TestContext.Current.CancellationToken);

            var user = new TestUser
            {
                Name = "John Joined",
                Age = 30,
                CreateTime = DateTime.Now,
                DeptId = dept.Id
            };
            await userService.InsertAsync(user, TestContext.Current.CancellationToken);

            // Act
            var viewUser = await viewService.GetObjectAsync(user.Id, cancellationToken: TestContext.Current.CancellationToken);

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
            await service.InsertAsync(new TestUser { Name = "Alice", Age = 25, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await service.InsertAsync(new TestUser { Name = "Bob", Age = 30, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await service.InsertAsync(new TestUser { Name = "Charlie", Age = 35, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await service.InsertAsync(new TestUser { Name = "David", Age = 40, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();

            // Act & Assert

            // 1. Expr.In
            var inList = await viewService.SearchAsync(Expr.Prop("Name").In("Alice", "Bob"), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(2, inList.Count);

            // 2. Expr.Between
            var betweenList = await viewService.SearchAsync(Expr.Prop("Age").Between(30, 35), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(2, betweenList.Count);

            // 3. Expr.Like
            var likeList = await viewService.SearchAsync(Expr.Prop("Name").Like("Cha%"), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Single(likeList);
            Assert.Equal("Charlie", likeList[0].Name);

            // 4. Combined And/Or
            var combinedList = await viewService.SearchAsync(
                (Expr.Prop("Age") < 30) | (Expr.Prop("Age") >= 40), cancellationToken: TestContext.Current.CancellationToken
            );
            Assert.Equal(2, combinedList.Count);

            // 5. Lambda complex
            var lambdaList = await viewService.SearchAsync(Expr.Lambda<TestUser>(u => u.Age > 30 && u.Name!.Contains("i")), cancellationToken: TestContext.Current.CancellationToken);
            // Charlie(35), David(40) -> both contain 'i'
            Assert.Equal(2, lambdaList.Count);
        }

        [Fact]
        public async Task Hierarchical_Query_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var root = new TestDepartment { Name = "Headquarters" };
            await deptService.InsertAsync(root, TestContext.Current.CancellationToken);

            var sub1 = new TestDepartment { Name = "HR", ParentId = root.Id };
            var sub2 = new TestDepartment { Name = "IT", ParentId = root.Id };
            await deptService.InsertAsync(sub1, TestContext.Current.CancellationToken);
            await deptService.InsertAsync(sub2, TestContext.Current.CancellationToken);

            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestDepartment>>();

            // Act
            var subDepts = await viewService.SearchAsync(Expr.Lambda<TestDepartment>(d => d.ParentId == root.Id), cancellationToken: TestContext.Current.CancellationToken);

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
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);
            var inserted = await viewService.SearchAsync(Expr.Lambda<TestUser>(u => u.Name!.StartsWith("Batch")), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(2, inserted.Count);

            // Act - Batch Update
            foreach (var u in inserted) u.Age += 5;
            await service.BatchUpdateAsync(inserted, TestContext.Current.CancellationToken);
            var updated = await viewService.SearchAsync(Expr.Lambda<TestUser>(u => u.Name!.StartsWith("Batch")), cancellationToken: TestContext.Current.CancellationToken);
            Assert.All(updated, u => Assert.True(u.Age == 15 || u.Age == 25));

            // Act - Batch Delete
            await service.BatchDeleteAsync(updated, TestContext.Current.CancellationToken);
            var deletedCount = await viewService.CountAsync(Expr.Lambda<TestUser>(u => u.Name!.StartsWith("Batch")), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(0, deletedCount);
        }

        [Fact]
        public async Task EntityService_BatchInsert_WithIdentity_ShouldPopulateIds()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();

            var users = new List<TestUser>();
            for (int i = 1; i <= 5; i++)
            {
                users.Add(new TestUser { Name = $"BatchId {i}", Age = 20 + i, CreateTime = DateTime.Now });
            }

            // Act
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            // Assert
            Assert.All(users, u => Assert.True(u.Id > 0));
            // Verify sequential IDs
            for (int i = 1; i < users.Count; i++)
            {
                Assert.Equal(users[i - 1].Id + 1, users[i].Id);
            }

            // Cleanup
            await service.BatchDeleteAsync(users, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task EntityService_BatchUpdateOrInsert_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            // 1. Initial insert
            var users = new List<TestUser>
            {
                new TestUser { Name = "Upsert A", Age = 10, CreateTime = DateTime.Now },
                new TestUser { Name = "Upsert B", Age = 20, CreateTime = DateTime.Now }
            };
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            // 2. Prepare mixed batch: one update, one new
            var existingUser = users[0];
            existingUser.Age = 15; // Changed

            var newUser = new TestUser { Name = "Upsert C", Age = 30, CreateTime = DateTime.Now };

            var batch = new List<TestUser> { existingUser, newUser };

            // Act
            await service.BatchUpdateOrInsertAsync(batch, TestContext.Current.CancellationToken);

            // Assert
            var allUsers = await viewService.SearchAsync(u => u.Name!.StartsWith("Upsert"), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(3, allUsers.Count);

            var retrievedA = allUsers.FirstOrDefault(u => u.Name == "Upsert A");
            Assert.NotNull(retrievedA);
            Assert.Equal(15, retrievedA.Age);

            var retrievedC = allUsers.FirstOrDefault(u => u.Name == "Upsert C");
            Assert.NotNull(retrievedC);
            Assert.True(retrievedC.Id > 0);

            // Cleanup
            await service.BatchDeleteAsync(allUsers, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task EntityService_UpdateValues_ShouldWork()

        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateValue", Age = 10, CreateTime = DateTime.Now };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            // Act
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = await dataDao.UpdateAllValues(updateValues, Expr.Lambda<TestUser>(u => u.Name == "UpdateValue")).GetResultAsync(TestContext.Current.CancellationToken);
            var retrieved = await viewService.GetObjectAsync(user.Id, cancellationToken: TestContext.Current.CancellationToken);

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
            await service.InsertAsync(new TestUser { Name = "Unique", Age = 50, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            // Act
            var one = await viewService.SearchOneAsync(Expr.Lambda<TestUser>(u => u.Name == "Unique"), cancellationToken: TestContext.Current.CancellationToken);
            bool exists = await viewService.ExistsAsync(Expr.Lambda<TestUser>(u => u.Name == "Unique"), cancellationToken: TestContext.Current.CancellationToken);
            int count = await viewService.CountAsync(Expr.Lambda<TestUser>(u => u.Age >= 50), cancellationToken: TestContext.Current.CancellationToken);

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
            }, TestContext.Current.CancellationToken);

            // Act - Order
            var ordered = await viewService.SearchAsync(
                Expr.From<TestUser>().Where<TestUser>(u => u.Name!.StartsWith("Order")).OrderBy(("Age", false)), cancellationToken: TestContext.Current.CancellationToken
            );

            // Act - Section
            var section = await viewService.SearchAsync(
                Expr.From<TestUser>().Where<TestUser>(u => u.Name!.StartsWith("Order")).OrderBy(("Age", false)).Section(0, 2), cancellationToken: TestContext.Current.CancellationToken
            );

            // Assert
            Assert.Equal("Order 3", ordered[0].Name);
            Assert.Equal(2, section.Count);
            Assert.Equal("Order 3", section[0].Name);
        }

        [Fact]
        public async Task EntityViewService_TestDepartmentView_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestDepartmentView>>();

            var root = new TestDepartment { Name = "Root" };
            await service.InsertAsync(root, TestContext.Current.CancellationToken);

            var child = new TestDepartment { Name = "Child", ParentId = root.Id };
            await service.InsertAsync(child, TestContext.Current.CancellationToken);

            // Act
            var view = await viewService.SearchOneAsync(Expr.Lambda<TestDepartmentView>(d => d.Id == child.Id), cancellationToken: TestContext.Current.CancellationToken);

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
            bool inserted = await service.UpdateOrInsertAsync(user, TestContext.Current.CancellationToken);
            Assert.True(inserted);
            Assert.True(user.Id > 0);

            // Act - UpdateOrInsert (Update)
            user.Age = 35;
            bool updated = await service.UpdateOrInsertAsync(user, TestContext.Current.CancellationToken);
            Assert.True(updated);
            var retrieved = await viewService.GetObjectAsync(user.Id, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(35, retrieved?.Age);

            // Act - Batch (Mixed)
            var newUser = new TestUser { Name = "Mixed 1", Age = 10, CreateTime = DateTime.Now };
            var ops = new List<EntityOperation<TestUser>>
            {
                new EntityOperation<TestUser> { Entity = newUser, Operation = OpDef.Insert },
                new EntityOperation<TestUser> { Entity = user, Operation = OpDef.Delete }
            };
            await service.BatchAsync(ops, TestContext.Current.CancellationToken);

            var mixedRetrieved = await viewService.SearchOneAsync(Expr.Lambda<TestUser>(u => u.Name == "Mixed 1"), cancellationToken: TestContext.Current.CancellationToken);
            var deletedRetrieved = await viewService.GetObjectAsync(user.Id, cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(mixedRetrieved);
            Assert.Null(deletedRetrieved);

            // Act - ForEachAsync
            int forEachCount = 0;
            await viewService.ForEachAsync(Expr.Lambda<TestUser>(u => u.Name == "Mixed 1"), async u =>
            {
                forEachCount++;
                await Task.CompletedTask;
            }, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(1, forEachCount);
        }

        [Fact]
        public async Task ForeignExpr_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            var dept = new TestDepartment { Name = "Foreign Dept" };
            await deptService.InsertAsync(dept, TestContext.Current.CancellationToken);

            await userService.InsertAsync(new TestUser { Name = "User In Dept", DeptId = dept.Id, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "User Outside", DeptId = -1, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            // Act
            // 使用 ForeignExpr 进行关联查询 (使用 EXISTS 子查询)
            // 需要在 InnerExpr 中添加外键关联条件：TestUser.DeptId = TestDepartment.Id
            var users = await viewService.SearchAsync(Expr.Exists<TestDepartment>(
                (Expr.Prop("Name") == "Foreign Dept") &
                (Expr.Prop("T0", "DeptId") == Expr.Prop("Id"))
            ), cancellationToken: TestContext.Current.CancellationToken);

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
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            var dept1 = new TestDepartment { Name = "Dept 1" };
            await deptService.InsertAsync(dept1, TestContext.Current.CancellationToken);
            var dept2 = new TestDepartment { Name = "Dept 2" };
            await deptService.InsertAsync(dept2, TestContext.Current.CancellationToken);

            await userService.InsertAsync(new TestUser { Name = "User A", Age = 20, DeptId = dept1.Id, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "User B", Age = 30, DeptId = dept1.Id, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "User C", Age = 30, DeptId = dept2.Id, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var users = await viewService.SearchAsync(
                (Expr.Prop("Age") == 30) & Expr.Exists<TestDepartment>(
                    (Expr.Prop("Name") == "Dept 1") &
                    (Expr.Prop("T0", "DeptId") == Expr.Prop("Id")) &
                    (Expr.Prop("T0", "Name") != Expr.Prop("Name"))), cancellationToken: TestContext.Current.CancellationToken
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
            string uniqueName = "Custom_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var testUser = new TestUser { Name = uniqueName, Age = 100, CreateTime = DateTime.Now };

            // Act
            await customService.InsertAsync(testUser, TestContext.Current.CancellationToken);
            var latest = await customService.GetLatestUserAsync();

            // Assert
            Assert.NotNull(latest);
            // If GetLatestUserAsync returns our user, great. 
            // If it returns another one (only if serial mode fails), we specifically check if OUR user was inserted correctly.
            var retrieved = await customService.SearchOneAsync(u => u.Name == uniqueName, cancellationToken: TestContext.Current.CancellationToken);
            Assert.NotNull(retrieved);
            Assert.Equal(100, retrieved.Age);

            // To ensure GetLatestUserAsync logic is also correct and returns at least ONE user
            Assert.NotNull(latest);
            Assert.True(latest.Id > 0);
            Assert.True(latest.Name == uniqueName);
        }

        [Fact]
        public async Task EntityViewService_MultiLevelJoin_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            var rootDept = new TestDepartment { Name = "Root Dept" };
            await deptService.InsertAsync(rootDept, TestContext.Current.CancellationToken);

            var subDept = new TestDepartment { Name = "Sub Dept", ParentId = rootDept.Id };
            await deptService.InsertAsync(subDept, TestContext.Current.CancellationToken);

            var user = new TestUser
            {
                Name = "MultiJoin User",
                Age = 25,
                CreateTime = DateTime.Now,
                DeptId = subDept.Id
            };
            await userService.InsertAsync(user, TestContext.Current.CancellationToken);

            // Act
            var viewUser = await viewService.GetObjectAsync(user.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(viewUser);
            Assert.Equal("MultiJoin User", viewUser.Name);
            Assert.Equal("Sub Dept", viewUser.DeptName);
            Assert.Equal("Root Dept", viewUser.ParentDeptName);

            // ���ݹ������ֶβ�ѯ (Act & Assert)

            // 1. ����һ���������ֶβ�ѯ
            var usersByDept = await viewService.SearchAsync(u => u.DeptName == "Sub Dept", cancellationToken: TestContext.Current.CancellationToken);
            Assert.Contains(usersByDept, u => u.Id == user.Id);

            // 2. ���ݶ����������ֶβ�ѯ (������)
            var usersByParentDept = await viewService.SearchAsync(u => u.ParentDeptName == "Root Dept", cancellationToken: TestContext.Current.CancellationToken);
            Assert.Contains(usersByParentDept, u => u.Id == user.Id);

            // 3. ��ϲ�ѯ
            var combinedUsers = await viewService.SearchAsync(u => u.DeptName == "Sub Dept" && u.ParentDeptName == "Root Dept", cancellationToken: TestContext.Current.CancellationToken);
            Assert.Single(combinedUsers);
            Assert.Equal(user.Id, combinedUsers[0].Id);

            // 4. Count ��֤
            int count = await viewService.CountAsync(u => u.ParentDeptName == "Root Dept", cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task EntityViewService_MultiTableJoin_SortingPagination_WithForeignColumn_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            // 创建测试部门
            var dept1 = new TestDepartment { Name = "A Department" };
            var dept2 = new TestDepartment { Name = "B Department" };
            var dept3 = new TestDepartment { Name = "C Department" };
            await deptService.InsertAsync(dept1, TestContext.Current.CancellationToken);
            await deptService.InsertAsync(dept2, TestContext.Current.CancellationToken);
            await deptService.InsertAsync(dept3, TestContext.Current.CancellationToken);

            // 创建测试用户，分布在不同部门
            await userService.InsertAsync(new TestUser { Name = "User 1", Age = 20, CreateTime = DateTime.Now, DeptId = dept1.Id }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "User 2", Age = 25, CreateTime = DateTime.Now, DeptId = dept2.Id }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "User 3", Age = 30, CreateTime = DateTime.Now, DeptId = dept3.Id }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "User 4", Age = 35, CreateTime = DateTime.Now, DeptId = dept1.Id }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "User 5", Age = 40, CreateTime = DateTime.Now, DeptId = dept2.Id }, TestContext.Current.CancellationToken);

            // Act 1: 使用 ForeignColumn (DeptName) 作为查询条件和排序条件，同时分页
            var expr1 = Expr.From<TestUserView>()
                .Where<TestUserView>(u => u.DeptName != null)
                .OrderBy((nameof(TestUserView.DeptName), true))  // 按部门名称升序
                .OrderBy((nameof(TestUser.Age), false))          // 再按年龄降序
                .Section(0, 3);                                  // 分页，取前3条
            var users1 = await viewService.SearchAsync(expr1, cancellationToken: TestContext.Current.CancellationToken);

            // Assert 1
            Assert.Equal(3, users1.Count);
            // 验证排序顺序：A Department 的用户应该在前面，且同一部门内按年龄降序
            Assert.Contains(users1, u => u.DeptName == "A Department" && u.Age == 35); // User 4
            Assert.Contains(users1, u => u.DeptName == "A Department" && u.Age == 20); // User 1
            Assert.Contains(users1, u => u.DeptName == "B Department" && u.Age == 40); // User 5

            // Act 2: 使用 ForeignColumn (ParentDeptName) 作为查询条件和排序条件
            // 首先创建有父部门的部门结构
            var parentDept = new TestDepartment { Name = "Parent Dept" };
            await deptService.InsertAsync(parentDept, TestContext.Current.CancellationToken);

            var childDept1 = new TestDepartment { Name = "Child Dept 1", ParentId = parentDept.Id };
            var childDept2 = new TestDepartment { Name = "Child Dept 2", ParentId = parentDept.Id };
            await deptService.InsertAsync(childDept1, TestContext.Current.CancellationToken);
            await deptService.InsertAsync(childDept2, TestContext.Current.CancellationToken);

            // 创建属于子部门的用户
            await userService.InsertAsync(new TestUser { Name = "Child User 1", Age = 22, CreateTime = DateTime.Now, DeptId = childDept1.Id }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "Child User 2", Age = 28, CreateTime = DateTime.Now, DeptId = childDept2.Id }, TestContext.Current.CancellationToken);

            // 使用 ParentDeptName 作为查询和排序条件
            var expr2 = Expr.From<TestUserView>()
                .Where<TestUserView>(u => u.ParentDeptName == "Parent Dept")
                .OrderBy(nameof(TestUserView.ParentDeptName))  // 按父部门名称升序
                .OrderBy(nameof(TestUserView.DeptName))        // 再按部门名称升序
                .OrderBy(nameof(TestUser.Age))                 // 再按年龄升序
                .Section(0, 5);                                         // 分页，取前5条
            var users2 = await viewService.SearchAsync(expr2, cancellationToken: TestContext.Current.CancellationToken);

            // Assert 2
            Assert.True(users2.Count >= 2); // 至少有2个用户
            // 验证所有结果的 ParentDeptName 都是 "Parent Dept"
            Assert.All(users2, u => Assert.Equal("Parent Dept", u.ParentDeptName));
        }

        #region 分表测试 - 使用 TestLog 的多种方式

        /// <summary>
        /// 方式一：简单 Lambda + 显式 TableArgs 参数
        /// </summary>
        [Fact]
        public async Task Sharding_SimpleLambda_Search_WithExplicitTableArgs_ShouldWork()
        {
            // Arrange - 简单 Lambda 分表查询
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestLog>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestLog>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            var user = new TestUser { Name = "Shard User 1", Age = 25, CreateTime = DateTime.Now };
            await userService.InsertAsync(user, TestContext.Current.CancellationToken);

            // 插入测试数据到 TestLog_202401 表
            var log1 = new TestLog { Event = "Login", Amount = 100, CreateTime = new DateTime(2024, 1, 15), UserID = user.Id };
            var log2 = new TestLog { Event = "Purchase", Amount = 200, CreateTime = new DateTime(2024, 1, 20), UserID = user.Id };
            await service.InsertAsync(log1, TestContext.Current.CancellationToken);
            await service.InsertAsync(log2, TestContext.Current.CancellationToken);

            // Act - 方式一：简单 Lambda + 显式 TableArgs
            var logs = await viewService.SearchAsync(
                l => l.Amount > 150,
                tableArgs: new[] { "202401" },
                cancellationToken: TestContext.Current.CancellationToken
            );

            // Assert
            Assert.NotNull(logs);
            Assert.True(logs.Count > 0);
            Assert.All(logs, log => Assert.True(log.Amount > 150));
        }

        /// <summary>
        /// 方式一的延伸：简单 Lambda + 计数
        /// </summary>
        [Fact]
        public async Task Sharding_SimpleLambda_Count_WithTableArgs_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestLog>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestLog>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            var user = new TestUser { Name = "Count User " + Guid.NewGuid().ToString("N").Substring(0, 8), Age = 30, CreateTime = DateTime.Now };
            await userService.InsertAsync(user, TestContext.Current.CancellationToken);

            var log1 = new TestLog { Event = "CountEvent1", Amount = 100, CreateTime = new DateTime(2024, 1, 10), UserID = user.Id };
            var log2 = new TestLog { Event = "CountEvent2", Amount = 200, CreateTime = new DateTime(2024, 1, 15), UserID = user.Id };
            var log3 = new TestLog { Event = "CountEvent3", Amount = 300, CreateTime = new DateTime(2024, 1, 20), UserID = user.Id };
            await service.InsertAsync(log1, TestContext.Current.CancellationToken);
            await service.InsertAsync(log2, TestContext.Current.CancellationToken);
            await service.InsertAsync(log3, TestContext.Current.CancellationToken);

            // Act - 计算 Amount > 150 且特定用户的日志数量（加入 UserID 过滤以隔离数据）
            int count = await viewService.CountAsync(
                l => l.Amount > 150 && l.UserID == user.Id,
                tableArgs: new[] { "202401" },
                cancellationToken: TestContext.Current.CancellationToken
            );

            // Assert
            Assert.Equal(2, count); // log2, log3
        }

        /// <summary>
        /// 方式一的延伸：简单 Lambda + 存在性检查
        /// </summary>
        [Fact]
        public async Task Sharding_SimpleLambda_Exists_WithTableArgs_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestLog>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestLog>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            var user = new TestUser { Name = "Exists User", Age = 35, CreateTime = DateTime.Now };
            await userService.InsertAsync(user, TestContext.Current.CancellationToken);

            var log = new TestLog { Event = "Exists Event", Amount = 250, CreateTime = new DateTime(2024, 1, 12), UserID = user.Id };
            await service.InsertAsync(log, TestContext.Current.CancellationToken);

            // Act - 检查是否存在 Amount = 250 的日志
            bool exists = await viewService.ExistsAsync(
                l => l.Amount == 250,
                tableArgs: new[] { "202401" },
                cancellationToken: TestContext.Current.CancellationToken
            );

            // Assert
            Assert.True(exists);
        }

        /// <summary>
        /// 方式一的延伸：简单 Lambda + 删除
        /// </summary>
        [Fact]
        public async Task Sharding_SimpleLambda_Delete_WithTableArgs_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestLog>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestLog>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            var user = new TestUser { Name = "Delete User " + Guid.NewGuid().ToString("N").Substring(0, 8), Age = 40, CreateTime = DateTime.Now };
            await userService.InsertAsync(user, TestContext.Current.CancellationToken);

            var log1 = new TestLog { Event = "KeepEvent", Amount = 100, CreateTime = new DateTime(2024, 1, 10), UserID = user.Id };
            var log2 = new TestLog { Event = "DeleteEvent", Amount = 500, CreateTime = new DateTime(2024, 1, 20), UserID = user.Id };
            await service.InsertAsync(log1, TestContext.Current.CancellationToken);
            await service.InsertAsync(log2, TestContext.Current.CancellationToken);

            // Act - 删除 Amount > 400 且 Event 为 "DeleteEvent" 的日志记录
            int deleted = await service.DeleteAsync(
                l => l.Amount > 400 && l.Event == "DeleteEvent",
                tableArgs: new[] { "202401" },
                cancellationToken: TestContext.Current.CancellationToken
            );

            // Assert
            Assert.Equal(1, deleted);
            var remaining = await viewService.SearchAsync(
                l => l.Event == "KeepEvent" && l.UserID == user.Id,
                tableArgs: new[] { "202401" },
                cancellationToken: TestContext.Current.CancellationToken
            );
            Assert.Single(remaining);
        }

        /// <summary>
        /// 方式二：IQueryable 链式查询
        /// </summary>
        [Fact]
        public async Task Sharding_IQueryable_ChainedQuery_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestLog>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestLog>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            var user = new TestUser { Name = "Query User " + Guid.NewGuid().ToString("N").Substring(0, 8), Age = 28, CreateTime = DateTime.Now };
            await userService.InsertAsync(user, TestContext.Current.CancellationToken);

            for (int i = 1; i <= 5; i++)
            {
                var log = new TestLog
                {
                    Event = $"QueryEvent{i}",
                    Amount = i * 100,
                    CreateTime = new DateTime(2024, 1, 10 + i),
                    UserID = user.Id
                };
                await service.InsertAsync(log, TestContext.Current.CancellationToken);
            }

            // Act - 方式二：IQueryable 链式查询（支持排序、分页等）
            var logs = await viewService.SearchAsync(
                q => q.Where(l => l.Amount >= 200 && l.UserID == user.Id)
                      .OrderBy(l => l.Amount),
                tableArgs: new[] { "202401" },
                cancellationToken: TestContext.Current.CancellationToken
            );

            // Assert
            Assert.True(logs.Count >= 2);
            // 验证排序：应该按 Amount 升序
            for (int i = 1; i < logs.Count; i++)
            {
                Assert.True(logs[i - 1].Amount <= logs[i].Amount);
            }
        }

        /// <summary>
        /// 方式三：TableArgs 赋值方式（显式指定分表参数）
        /// </summary>
        [Fact]
        public async Task Sharding_TableArgsAssignment_ExplicitTableArgs_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestLog>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestLog>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            var user = new TestUser { Name = "TableArgs User " + Guid.NewGuid().ToString("N").Substring(0, 8), Age = 32, CreateTime = DateTime.Now };
            await userService.InsertAsync(user, TestContext.Current.CancellationToken);

            var log1 = new TestLog { Event = "Feb Event", Amount = 150, CreateTime = new DateTime(2024, 2, 10), UserID = user.Id };
            var log2 = new TestLog { Event = "Feb Event 2", Amount = 250, CreateTime = new DateTime(2024, 2, 15), UserID = user.Id };
            await service.InsertAsync(log1, TestContext.Current.CancellationToken);
            await service.InsertAsync(log2, TestContext.Current.CancellationToken);

            // Act - 方式三：在 Lambda 中显式赋值 TableArgs（不需要显式传递 tableArgs 参数）
            var logs = await viewService.SearchAsync(
                l => l.TableArgs == new[] { "202402" } && l.Amount > 200,
                cancellationToken: TestContext.Current.CancellationToken
            );

            // Assert
            Assert.NotNull(logs);
            Assert.Contains(logs, log => log.Amount == 250 && log.UserID == user.Id);
        }

        /// <summary>
        /// 方式三的延伸：TableArgs 赋值 + 复杂条件 + 删除
        /// </summary>
        [Fact]
        public async Task Sharding_TableArgsAssignment_ComplexCondition_Delete_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestLog>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestLog>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            var user = new TestUser { Name = "Complex Delete User " + Guid.NewGuid().ToString("N").Substring(0, 8), Age = 45, CreateTime = DateTime.Now };
            await userService.InsertAsync(user, TestContext.Current.CancellationToken);

            var log1 = new TestLog { Event = "Important", Amount = 500, CreateTime = new DateTime(2024, 3, 10), UserID = user.Id };
            var log2 = new TestLog { Event = "Temp", Amount = 50, CreateTime = new DateTime(2024, 3, 15), UserID = user.Id };
            var log3 = new TestLog { Event = "Temp", Amount = 30, CreateTime = new DateTime(2024, 3, 20), UserID = user.Id };
            await service.InsertAsync(log1, TestContext.Current.CancellationToken);
            await service.InsertAsync(log2, TestContext.Current.CancellationToken);
            await service.InsertAsync(log3, TestContext.Current.CancellationToken);

            // Act - 删除所有临时记录（Amount < 100 且 Event 为 "Temp"）
            // 使用 Lambda 中的 TableArgs 赋值方式来指定分表
            int deleted = await service.DeleteAsync(
                l => l.TableArgs == new[] { "202403" } && l.Event == "Temp" && l.Amount < 100 && l.UserID == user.Id,
                cancellationToken: TestContext.Current.CancellationToken
            );

            // Assert
            Assert.Equal(2, deleted); // 删除 log2 和 log3
            var remaining = await viewService.SearchAsync(
                l => l.TableArgs == new[] { "202403" } && l.Event == "Important" && l.UserID == user.Id,
                cancellationToken: TestContext.Current.CancellationToken
            );
            Assert.Single(remaining);
            Assert.Equal(500, remaining[0].Amount);
        }

        /// <summary>
        /// 综合测试：多表查询和操作
        /// </summary>
        [Fact]
        public async Task Sharding_MultiTable_Query_And_Delete_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestLog>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestLog>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            var user = new TestUser { Name = "Multi Table User " + Guid.NewGuid().ToString("N").Substring(0, 8), Age = 50, CreateTime = DateTime.Now };
            await userService.InsertAsync(user, TestContext.Current.CancellationToken);

            // 向多个月份的表插入数据
            // 2024-01 数据
            var jan1 = new TestLog { Event = "JAN_MultiDelete", Amount = 100, CreateTime = new DateTime(2024, 1, 10), UserID = user.Id };
            var jan2 = new TestLog { Event = "JAN_MultiDelete", Amount = 200, CreateTime = new DateTime(2024, 1, 20), UserID = user.Id };
            await service.InsertAsync(jan1, TestContext.Current.CancellationToken);
            await service.InsertAsync(jan2, TestContext.Current.CancellationToken);

            // 2024-04 数据
            var apr1 = new TestLog { Event = "APR_Multi", Amount = 300, CreateTime = new DateTime(2024, 4, 10), UserID = user.Id };
            var apr2 = new TestLog { Event = "APR_Multi", Amount = 400, CreateTime = new DateTime(2024, 4, 20), UserID = user.Id };
            await service.InsertAsync(apr1, TestContext.Current.CancellationToken);
            await service.InsertAsync(apr2, TestContext.Current.CancellationToken);

            // Act 1 - 查询 2024-01 的高额日志（Amount > 150）
            var janLogs = await viewService.SearchAsync(
                l => l.Amount > 150 && l.UserID == user.Id,
                tableArgs: new[] { "202401" },
                cancellationToken: TestContext.Current.CancellationToken
            );

            // Act 2 - 查询 2024-04 的所有日志
            var aprLogs = await viewService.SearchAsync(
                l => l.Event == "APR_Multi" && l.UserID == user.Id,
                tableArgs: new[] { "202404" },
                TestContext.Current.CancellationToken
            );

            // Act 3 - 删除 2024-04 中的低额日志（Amount < 350）
            int deletedApr = await service.DeleteAsync(
                l => l.Amount < 350 && l.UserID == user.Id,
                tableArgs: new[] { "202404" },
                TestContext.Current.CancellationToken
            );

            // Act 4 - 验证删除后的数据
            var aprRemaining = await viewService.SearchAsync(
                l => l.Event == "APR_Multi" && l.UserID == user.Id,
                tableArgs: new[] { "202404" },
                TestContext.Current.CancellationToken
            );

            // Assert
            Assert.Single(janLogs); // 只有 jan2 满足 Amount > 150
            Assert.Equal(2, aprLogs.Count); // 两条 APR 日志
            Assert.Equal(1, deletedApr); // 删除了 apr1
            Assert.Single(aprRemaining); // 只剩 apr2
            Assert.Equal(400, aprRemaining[0].Amount);
        }

        /// <summary>
        /// 分表查询结合外键联接
        /// </summary>
        [Fact]
        public async Task Sharding_WithForeignJoin_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestLog>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestLogView>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            var user1 = new TestUser { Name = "LogUser1", Age = 20, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "LogUser2", Age = 30, CreateTime = DateTime.Now };
            await userService.InsertAsync(user1, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user2, TestContext.Current.CancellationToken);

            // 为 user1 插入日志
            var log1 = new TestLog { Event = "U1_Event", Amount = 100, CreateTime = new DateTime(2024, 5, 10), UserID = user1.Id };
            var log2 = new TestLog { Event = "U1_Event", Amount = 200, CreateTime = new DateTime(2024, 5, 15), UserID = user1.Id };
            await service.InsertAsync(log1, TestContext.Current.CancellationToken);
            await service.InsertAsync(log2, TestContext.Current.CancellationToken);

            // 为 user2 插入日志
            var log3 = new TestLog { Event = "U2_Event", Amount = 300, CreateTime = new DateTime(2024, 5, 20), UserID = user2.Id };
            await service.InsertAsync(log3, TestContext.Current.CancellationToken);

            // Act - 查询某个用户的所有日志（通过外键关联）
            // 使用 TestLogView 可以获取关联的用户名
            var user1Logs = await viewService.SearchAsync(
                l => l.UserName == "LogUser1",
                tableArgs: new[] { "202405" },
                cancellationToken: TestContext.Current.CancellationToken
            );

            var user2Logs = await viewService.SearchAsync(
                l => l.UserName == "LogUser2",
                tableArgs: new[] { "202405" },
                cancellationToken: TestContext.Current.CancellationToken
            );

            // Assert
            Assert.Equal(2, user1Logs.Count);
            Assert.Single(user2Logs);
            Assert.All(user1Logs, log => Assert.Equal("LogUser1", log.UserName));
            Assert.All(user2Logs, log => Assert.Equal("LogUser2", log.UserName));
        }

        #endregion

        [Fact]
        public async Task EntityService_Update_UpdateExpr_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityService<TestUser>>();
            var asyncService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateExprTest", Age = 10, CreateTime = DateTime.Now };
            await asyncService.InsertAsync(user, TestContext.Current.CancellationToken);

            // Act - 使用FunctionExpr和PropertyExpr增加复杂度
            var updateExpr = new UpdateExpr
            {
                Table = new TableExpr(typeof(TestUser)),
                Sets = new ()
                {
                    new (Expr.Prop("Age"), Expr.Prop("Age") + Expr.Const(5)), // 使用运算符重载和Expr.Const，Age = Age + 5
                    new (Expr.Prop("Name"), new FunctionExpr("UPPER", Expr.Prop("Name"))) // 使用UPPER函数，参数为Name属性
                },
                Where = Expr.Lambda<TestUser>(u => u.Name == "UpdateExprTest")
            };
            int affected = service.Update(updateExpr);
            var retrieved = await viewService.GetObjectAsync(user.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, affected);
            Assert.Equal(15, retrieved?.Age); // Age + 5 = 15
            Assert.Equal("UPDATEEXPRTEST", retrieved?.Name); // UPPER("UpdateExprTest") = "UPDATEEXPRTEST"
        }

        [Fact]
        public async Task EntityService_UpdateAsync_UpdateExpr_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateExprAsyncTest", Age = 10, CreateTime = DateTime.Now };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            // Act - 使用FunctionExpr和PropertyExpr增加复杂度
            var updateExpr = new UpdateExpr
            {
                Table = new TableExpr(typeof(TestUser)),
                Sets = new ()
                {
                    new (Expr.Prop("Age"), Expr.Prop("Age") + Expr.Const(10)), // 使用运算符重载和Expr.Const，Age = Age + 10
                    new (Expr.Prop("Name"), new FunctionExpr("CONCAT", Expr.Prop("Name"), Expr.Const("_Updated"))) // 使用CONCAT函数，参数为Name属性和字符串
                },
                Where = Expr.Lambda<TestUser>(u => u.Name == "UpdateExprAsyncTest")
            };
            int affected = await service.UpdateAsync(updateExpr, cancellationToken: TestContext.Current.CancellationToken);
            var retrieved = await viewService.GetObjectAsync(user.Id, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, affected);
            Assert.Equal(20, retrieved?.Age); // Age + 10 = 20
            Assert.Equal("UpdateExprAsyncTest_Updated", retrieved?.Name); // CONCAT("UpdateExprAsyncTest", "_Updated")
        }

        [Fact]
        public async Task EntityViewService_ExistsLambda_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();

            // 创建测试部门
            var dept = new TestDepartment { Name = "ExistsTestDept" };
            await deptService.InsertAsync(dept, TestContext.Current.CancellationToken);

            // 创建测试用户
            var userWithDept = new TestUser { Name = "UserWithDept", Age = 25, CreateTime = DateTime.Now, DeptId = dept.Id };
            await userService.InsertAsync(userWithDept, TestContext.Current.CancellationToken);

            var userWithoutDept = new TestUser { Name = "UserWithoutDept", Age = 30, CreateTime = DateTime.Now, DeptId = -1 };
            await userService.InsertAsync(userWithoutDept, TestContext.Current.CancellationToken);

            // Act - 使用 Expr.Exists lambda 方式查询
            var usersWithDept = await viewService.SearchAsync(
                Expr.Lambda<TestUser>(u => Expr.Exists<TestDepartment>(d => d.Id == u.DeptId)),
                cancellationToken: TestContext.Current.CancellationToken
            );

            var usersWithSpecificDept = await viewService.SearchAsync(
                Expr.Lambda<TestUser>(u => Expr.Exists<TestDepartment>(d => d.Id == u.DeptId && d.Name == "ExistsTestDept")),
                cancellationToken: TestContext.Current.CancellationToken
            );

            // Assert
            Assert.Single(usersWithDept);
            Assert.Equal("UserWithDept", usersWithDept[0].Name);

            Assert.Single(usersWithSpecificDept);
            Assert.Equal("UserWithDept", usersWithSpecificDept[0].Name);
        }

        #region Duration 字段测试

        /// <summary>
        /// Duration 字段插入和读取测试
        /// </summary>
        [Fact]
        public async Task Duration_InsertAndRetrieve_ShouldWork()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestLog>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestLog>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            var user = new TestUser { Name = "Duration_" + Guid.NewGuid().ToString("N")[..6], Age = 25, CreateTime = DateTime.Now };
            await userService.InsertAsync(user, TestContext.Current.CancellationToken);

            var duration = TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(30));
            var log = new TestLog
            {
                Event = "DurationTest",
                Amount = 100,
                CreateTime = new DateTime(2024, 6, 1),
                Duration = duration,
                UserID = user.Id
            };
            bool inserted = await service.InsertAsync(log, TestContext.Current.CancellationToken);
            Assert.True(inserted);

            var results = await viewService.SearchAsync(
                l => l.Id == log.Id && l.UserID == user.Id,
                tableArgs: new[] { "202406" },
                cancellationToken: TestContext.Current.CancellationToken
            );
            Assert.Single(results);
            Assert.Equal(duration, results[0].Duration);
        }

        /// <summary>
        /// Duration 字段过滤测试
        /// </summary>
        [Fact]
        public async Task Duration_Filter_GreaterThan_ShouldWork()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestLog>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestLog>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            var user = new TestUser { Name = "DurFilter_" + Guid.NewGuid().ToString("N")[..6], Age = 30, CreateTime = DateTime.Now };
            await userService.InsertAsync(user, TestContext.Current.CancellationToken);

            var log1 = new TestLog { Event = "Short", Amount = 100, CreateTime = new DateTime(2024, 6, 10), Duration = TimeSpan.FromHours(1), UserID = user.Id };
            var log2 = new TestLog { Event = "Long",  Amount = 200, CreateTime = new DateTime(2024, 6, 15), Duration = TimeSpan.FromHours(5), UserID = user.Id };
            await service.InsertAsync(log1, TestContext.Current.CancellationToken);
            await service.InsertAsync(log2, TestContext.Current.CancellationToken);

            var threshold = TimeSpan.FromHours(2);
            var results = await viewService.SearchAsync(
                l => l.Duration > threshold && l.UserID == user.Id,
                tableArgs: new[] { "202406" },
                cancellationToken: TestContext.Current.CancellationToken
            );
            Assert.Single(results);
            Assert.Equal(log2.Id, results[0].Id);
            Assert.Equal(TimeSpan.FromHours(5), results[0].Duration);
        }

        [Fact]
        public void EntityViewService_SyncMembers_ShouldWork()
        {
            var entityService = ServiceProvider.GetRequiredService<IEntityService<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewService<TestUser>>();

            var user1 = new TestUser { Name = "SyncViewService_A", Age = 20, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "SyncViewService_B", Age = 30, CreateTime = DateTime.Now };

            Assert.True(entityService.Insert(user1));
            Assert.True(entityService.Insert(user2));

            var fetched = viewService.GetObject(user1.Id);
            Assert.NotNull(fetched);
            Assert.Equal("SyncViewService_A", fetched.Name);
            Assert.True(viewService.ExistsID(user1.Id));
            Assert.True(viewService.Exists(Expr.Lambda<TestUser>(u => u.Name == "SyncViewService_B")));
            Assert.Equal(2, viewService.Count(Expr.Lambda<TestUser>(u => u.Name!.StartsWith("SyncViewService_"))));

            var one = viewService.SearchOne(Expr.Lambda<TestUser>(u => u.Name == "SyncViewService_B"));
            Assert.NotNull(one);
            Assert.Equal(user2.Id, one.Id);

            var all = viewService.Search(Expr.Lambda<TestUser>(u => u.Name!.StartsWith("SyncViewService_")));
            Assert.Equal(2, all.Count);

            var iteratedNames = new List<string>();
            viewService.ForEach(Expr.Lambda<TestUser>(u => u.Name!.StartsWith("SyncViewService_")), u => iteratedNames.Add(u.Name!));
            Assert.Equal(2, iteratedNames.Count);
        }

        [Fact]
        public async Task EntityService_SyncBatchAndDeleteMembers_ShouldWork()
        {
            var service = ServiceProvider.GetRequiredService<IEntityService<TestUser>>();
            var asyncViewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();

            var users = new List<TestUser>
            {
                new TestUser { Name = "SyncEntityBatch_A", Age = 18, CreateTime = DateTime.Now },
                new TestUser { Name = "SyncEntityBatch_B", Age = 28, CreateTime = DateTime.Now }
            };

            service.BatchInsert(users);
            Assert.All(users, user => Assert.True(user.Id > 0));

            users[0].Age = 19;
            users[1].Age = 29;
            service.BatchUpdate(users);

            users[0].Name = "SyncEntityBatch_A_Upserted";
            users[0].Age = 25;
            var insertedByUpsert = new TestUser { Name = "SyncEntityBatch_C", Age = 35, CreateTime = DateTime.Now };
            service.BatchUpdateOrInsert([users[0], insertedByUpsert]);
            Assert.True(insertedByUpsert.Id > 0);

            Assert.True(service.UpdateOrInsert(users[0]));

            users[1].Age = 39;
            var mixed = new[]
            {
                new EntityOperation<TestUser> { Operation = OpDef.Update, Entity = users[1] },
                new EntityOperation<TestUser> { Operation = OpDef.Insert, Entity = new TestUser { Name = "SyncEntityBatch_D", Age = 45, CreateTime = DateTime.Now } }
            };
            service.Batch(mixed);

            var afterBatch = await asyncViewService.SearchAsync(Expr.Prop("Name").Like("SyncEntityBatch%"), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Contains(afterBatch, user => user.Name == "SyncEntityBatch_A_Upserted" && user.Age == 25);
            Assert.Contains(afterBatch, user => user.Name == "SyncEntityBatch_B" && user.Age == 39);
            Assert.Contains(afterBatch, user => user.Name == "SyncEntityBatch_C");
            Assert.Contains(afterBatch, user => user.Name == "SyncEntityBatch_D");

            service.BatchDelete([insertedByUpsert]);
            Assert.False(await asyncViewService.ExistsIDAsync(insertedByUpsert.Id, cancellationToken: TestContext.Current.CancellationToken));

            var syncEntityBatchD = afterBatch.Find(user => user.Name == "SyncEntityBatch_D");
            Assert.NotNull(syncEntityBatchD);
            service.BatchDeleteID(new object[] { syncEntityBatchD.Id });
            Assert.False(await asyncViewService.ExistsIDAsync(syncEntityBatchD.Id, cancellationToken: TestContext.Current.CancellationToken));

            Assert.True(service.DeleteID(users[1].Id));
            Assert.False(await asyncViewService.ExistsIDAsync(users[1].Id, cancellationToken: TestContext.Current.CancellationToken));

            var deleteByEntity = new TestUser { Name = "SyncEntityDelete_Entity", Age = 50, CreateTime = DateTime.Now };
            Assert.True(service.Insert(deleteByEntity));
            Assert.True(service.Delete(deleteByEntity));

            var deleteByExpr = new TestUser { Name = "SyncEntityDelete_Expr", Age = 60, CreateTime = DateTime.Now };
            Assert.True(service.Insert(deleteByExpr));
            Assert.Equal(1, service.Delete(Expr.Lambda<TestUser>(u => u.Name == "SyncEntityDelete_Expr")));
        }

        [Fact]
        public async Task EntityService_AsyncDeleteIdAndExistsIdMembers_ShouldWork()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();

            var user1 = new TestUser { Name = "AsyncDeleteId_A", Age = 22, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "AsyncDeleteId_B", Age = 32, CreateTime = DateTime.Now };
            var user3 = new TestUser { Name = "AsyncDeleteId_C", Age = 42, CreateTime = DateTime.Now };

            await service.InsertAsync(user1, TestContext.Current.CancellationToken);
            await service.InsertAsync(user2, TestContext.Current.CancellationToken);
            await service.InsertAsync(user3, TestContext.Current.CancellationToken);

            Assert.True(await viewService.ExistsIDAsync(user1.Id, cancellationToken: TestContext.Current.CancellationToken));
            Assert.True(await service.DeleteIDAsync(user1.Id, cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(await viewService.ExistsIDAsync(user1.Id, cancellationToken: TestContext.Current.CancellationToken));

            await service.BatchDeleteIDAsync(new object[] { user2.Id, user3.Id }, TestContext.Current.CancellationToken);
            Assert.False(await viewService.ExistsIDAsync(user2.Id, cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(await viewService.ExistsIDAsync(user3.Id, cancellationToken: TestContext.Current.CancellationToken));
        }

        #endregion
    }
}
