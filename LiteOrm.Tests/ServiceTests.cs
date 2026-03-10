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
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();
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
            await service.BatchInsertAsync(users);
            var retrievedUsers = await viewService.SearchAsync(u => u.Name.StartsWith("Batch User"));

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
            var inList = await viewService.SearchAsync(Expr.Prop("Name").In("Alice", "Bob"));
            Assert.Equal(2, inList.Count);

            // 2. Expr.Between
            var betweenList = await viewService.SearchAsync(Expr.Prop("Age").Between(30, 35));
            Assert.Equal(2, betweenList.Count);

            // 3. Expr.Like
            var likeList = await viewService.SearchAsync(Expr.Prop("Name").Like("Cha%"));
            Assert.Single(likeList);
            Assert.Equal("Charlie", likeList[0].Name);

            // 4. Combined And/Or
            var combinedList = await viewService.SearchAsync(
                (Expr.Prop("Age") < 30) | (Expr.Prop("Age") >= 40)
            );
            Assert.Equal(2, combinedList.Count);

            // 5. Lambda complex
            var lambdaList = await viewService.SearchAsync(Expr.Lambda<TestUser>(u => u.Age > 30 && u.Name!.Contains("i")));
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
            var subDepts = await viewService.SearchAsync(Expr.Lambda<TestDepartment>(d => d.ParentId == root.Id));

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
            var inserted = await viewService.SearchAsync(Expr.Lambda<TestUser>(u => u.Name!.StartsWith("Batch")));
            Assert.Equal(2, inserted.Count);

            // Act - Batch Update
            foreach (var u in inserted) u.Age += 5;
            await service.BatchUpdateAsync(inserted);
            var updated = await viewService.SearchAsync(Expr.Lambda<TestUser>(u => u.Name!.StartsWith("Batch")));
            Assert.All(updated, u => Assert.True(u.Age == 15 || u.Age == 25));

            // Act - Batch Delete
            await service.BatchDeleteAsync(updated);
            var deletedCount = await viewService.CountAsync(Expr.Lambda<TestUser>(u => u.Name!.StartsWith("Batch")));
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
            await service.BatchInsertAsync(users);

            // Assert
            Assert.All(users, u => Assert.True(u.Id > 0));
            // Verify sequential IDs
            for (int i = 1; i < users.Count; i++)
            {
                Assert.Equal(users[i - 1].Id + 1, users[i].Id);
            }

            // Cleanup
            await service.BatchDeleteAsync(users);
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
            await service.BatchInsertAsync(users);

            // 2. Prepare mixed batch: one update, one new
            var existingUser = users[0];
            existingUser.Age = 15; // Changed

            var newUser = new TestUser { Name = "Upsert C", Age = 30, CreateTime = DateTime.Now };

            var batch = new List<TestUser> { existingUser, newUser };

            // Act
            await service.BatchUpdateOrInsertAsync(batch);

            // Assert
            var allUsers = await viewService.SearchAsync(u => u.Name!.StartsWith("Upsert"));
            Assert.Equal(3, allUsers.Count);

            var retrievedA = allUsers.FirstOrDefault(u => u.Name == "Upsert A");
            Assert.NotNull(retrievedA);
            Assert.Equal(15, retrievedA.Age);

            var retrievedC = allUsers.FirstOrDefault(u => u.Name == "Upsert C");
            Assert.NotNull(retrievedC);
            Assert.True(retrievedC.Id > 0);

            // Cleanup
            await service.BatchDeleteAsync(allUsers);
        }

        [Fact]
        public async Task EntityService_UpdateValues_ShouldWork()

        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateValue", Age = 10, CreateTime = DateTime.Now };
            await service.InsertAsync(user);

            // Act
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = await dataDao.UpdateAllValues(updateValues, Expr.Lambda<TestUser>(u => u.Name == "UpdateValue")).GetResultAsync();
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
            var one = await viewService.SearchOneAsync(Expr.Lambda<TestUser>(u => u.Name == "Unique"));
            bool exists = await viewService.ExistsAsync(Expr.Lambda<TestUser>(u => u.Name == "Unique"));
            int count = await viewService.CountAsync(Expr.Lambda<TestUser>(u => u.Age >= 50));

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
            var ordered = await viewService.SearchAsync(
                Expr.Where<TestUser>(u => u.Name!.StartsWith("Order")).OrderBy(("Age", false))
            );

            // Act - Section
            var section = await viewService.SearchAsync(
                Expr.Where<TestUser>(u => u.Name!.StartsWith("Order")).OrderBy(("Age", false)).Section(0, 2)
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
            await service.InsertAsync(root);

            var child = new TestDepartment { Name = "Child", ParentId = root.Id };
            await service.InsertAsync(child);

            // Act
            var view = await viewService.SearchOneAsync(Expr.Lambda<TestDepartmentView>(d => d.Id == child.Id));

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

            var mixedRetrieved = await viewService.SearchOneAsync(Expr.Lambda<TestUser>(u => u.Name == "Mixed 1"));
            var deletedRetrieved = await viewService.GetObjectAsync(user.Id);

            Assert.NotNull(mixedRetrieved);
            Assert.Null(deletedRetrieved);

            // Act - ForEachAsync
            int forEachCount = 0;
            await viewService.ForEachAsync(Expr.Lambda<TestUser>(u => u.Name == "Mixed 1"), async u =>
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
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            var dept = new TestDepartment { Name = "Foreign Dept" };
            await deptService.InsertAsync(dept);

            await userService.InsertAsync(new TestUser { Name = "User In Dept", DeptId = dept.Id, CreateTime = DateTime.Now });
            await userService.InsertAsync(new TestUser { Name = "User Outside", DeptId = -1, CreateTime = DateTime.Now });

            // Act
            // 使用 ForeignExpr 进行关联查询 (使用 EXISTS 子查询)
            // 需要在 InnerExpr 中添加外键关联条件：TestUser.DeptId = TestDepartment.Id
            var users = await viewService.SearchAsync(Expr.Foreign<TestDepartment>(
                (Expr.Prop("Name") == "Foreign Dept") &
                (Expr.Prop("T0.DeptId") == Expr.Prop("Id"))
            ));

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
            await deptService.InsertAsync(dept1);
            var dept2 = new TestDepartment { Name = "Dept 2" };
            await deptService.InsertAsync(dept2);

            await userService.InsertAsync(new TestUser { Name = "User A", Age = 20, DeptId = dept1.Id, CreateTime = DateTime.Now });
            await userService.InsertAsync(new TestUser { Name = "User B", Age = 30, DeptId = dept1.Id, CreateTime = DateTime.Now });
            await userService.InsertAsync(new TestUser { Name = "User C", Age = 30, DeptId = dept2.Id, CreateTime = DateTime.Now });

            var users = await viewService.SearchAsync(
                (Expr.Prop("Age") == 30) & Expr.Foreign<TestDepartment>("Dept",
                    (Expr.Prop("Name") == "Dept 1") &
                    (Expr.Prop("T0.DeptId") == Expr.Prop("Id")) &
                    (Expr.Prop("T0.Name") != Expr.Prop("Name")))
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
            await customService.InsertAsync(testUser);
            var latest = await customService.GetLatestUserAsync();

            // Assert
            Assert.NotNull(latest);
            // If GetLatestUserAsync returns our user, great. 
            // If it returns another one (only if serial mode fails), we specifically check if OUR user was inserted correctly.
            var retrieved = await customService.SearchOneAsync(u => u.Name == uniqueName);
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

            // ���ݹ������ֶβ�ѯ (Act & Assert)

            // 1. ����һ���������ֶβ�ѯ
            var usersByDept = await viewService.SearchAsync(u => u.DeptName == "Sub Dept");
            Assert.Contains(usersByDept, u => u.Id == user.Id);

            // 2. ���ݶ����������ֶβ�ѯ (������)
            var usersByParentDept = await viewService.SearchAsync(u => u.ParentDeptName == "Root Dept");
            Assert.Contains(usersByParentDept, u => u.Id == user.Id);

            // 3. ��ϲ�ѯ
            var combinedUsers = await viewService.SearchAsync(u => u.DeptName == "Sub Dept" && u.ParentDeptName == "Root Dept");
            Assert.Single(combinedUsers);
            Assert.Equal(user.Id, combinedUsers[0].Id);

            // 4. Count ��֤
            int count = await viewService.CountAsync(u => u.ParentDeptName == "Root Dept");
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
            await deptService.InsertAsync(dept1);
            await deptService.InsertAsync(dept2);
            await deptService.InsertAsync(dept3);

            // 创建测试用户，分布在不同部门
            await userService.InsertAsync(new TestUser { Name = "User 1", Age = 20, CreateTime = DateTime.Now, DeptId = dept1.Id });
            await userService.InsertAsync(new TestUser { Name = "User 2", Age = 25, CreateTime = DateTime.Now, DeptId = dept2.Id });
            await userService.InsertAsync(new TestUser { Name = "User 3", Age = 30, CreateTime = DateTime.Now, DeptId = dept3.Id });
            await userService.InsertAsync(new TestUser { Name = "User 4", Age = 35, CreateTime = DateTime.Now, DeptId = dept1.Id });
            await userService.InsertAsync(new TestUser { Name = "User 5", Age = 40, CreateTime = DateTime.Now, DeptId = dept2.Id });

            // Act 1: 使用 ForeignColumn (DeptName) 作为查询条件和排序条件，同时分页
            var expr1 = Expr.Where<TestUserView>(u => u.DeptName != null)
                .OrderBy((nameof(TestUserView.DeptName), true))  // 按部门名称升序
                .OrderBy((nameof(TestUser.Age), false))          // 再按年龄降序
                .Section(0, 3);                                  // 分页，取前3条
            var users1 = await viewService.SearchAsync(expr1);

            // Assert 1
            Assert.Equal(3, users1.Count);
            // 验证排序顺序：A Department 的用户应该在前面，且同一部门内按年龄降序
            Assert.Contains(users1, u => u.DeptName == "A Department" && u.Age == 35); // User 4
            Assert.Contains(users1, u => u.DeptName == "A Department" && u.Age == 20); // User 1
            Assert.Contains(users1, u => u.DeptName == "B Department" && u.Age == 40); // User 5

            // Act 2: 使用 ForeignColumn (ParentDeptName) 作为查询条件和排序条件
            // 首先创建有父部门的部门结构
            var parentDept = new TestDepartment { Name = "Parent Dept" };
            await deptService.InsertAsync(parentDept);

            var childDept1 = new TestDepartment { Name = "Child Dept 1", ParentId = parentDept.Id };
            var childDept2 = new TestDepartment { Name = "Child Dept 2", ParentId = parentDept.Id };
            await deptService.InsertAsync(childDept1);
            await deptService.InsertAsync(childDept2);

            // 创建属于子部门的用户
            await userService.InsertAsync(new TestUser { Name = "Child User 1", Age = 22, CreateTime = DateTime.Now, DeptId = childDept1.Id });
            await userService.InsertAsync(new TestUser { Name = "Child User 2", Age = 28, CreateTime = DateTime.Now, DeptId = childDept2.Id });

            // 使用 ParentDeptName 作为查询和排序条件
            var expr2 = Expr.Where<TestUserView>(u => u.ParentDeptName == "Parent Dept")
                .OrderBy(nameof(TestUserView.ParentDeptName))  // 按父部门名称升序
                .OrderBy(nameof(TestUserView.DeptName))        // 再按部门名称升序
                .OrderBy(nameof(TestUser.Age))                 // 再按年龄升序
                .Section(0, 5);                                         // 分页，取前5条
            var users2 = await viewService.SearchAsync(expr2);

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
            await userService.InsertAsync(user);

            // 插入测试数据到 TestLog_202401 表
            var log1 = new TestLog { Event = "Login", Amount = 100, CreateTime = new DateTime(2024, 1, 15), UserID = user.Id };
            var log2 = new TestLog { Event = "Purchase", Amount = 200, CreateTime = new DateTime(2024, 1, 20), UserID = user.Id };
            await service.InsertAsync(log1);
            await service.InsertAsync(log2);

            // Act - 方式一：简单 Lambda + 显式 TableArgs
            var logs = await viewService.SearchAsync(
                l => l.Amount > 150,
                tableArgs: new[] { "202401" }
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
            await userService.InsertAsync(user);

            var log1 = new TestLog { Event = "CountEvent1", Amount = 100, CreateTime = new DateTime(2024, 1, 10), UserID = user.Id };
            var log2 = new TestLog { Event = "CountEvent2", Amount = 200, CreateTime = new DateTime(2024, 1, 15), UserID = user.Id };
            var log3 = new TestLog { Event = "CountEvent3", Amount = 300, CreateTime = new DateTime(2024, 1, 20), UserID = user.Id };
            await service.InsertAsync(log1);
            await service.InsertAsync(log2);
            await service.InsertAsync(log3);

            // Act - 计算 Amount > 150 且特定用户的日志数量（加入 UserID 过滤以隔离数据）
            int count = await viewService.CountAsync(
                l => l.Amount > 150 && l.UserID == user.Id,
                tableArgs: new[] { "202401" }
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
            await userService.InsertAsync(user);

            var log = new TestLog { Event = "Exists Event", Amount = 250, CreateTime = new DateTime(2024, 1, 12), UserID = user.Id };
            await service.InsertAsync(log);

            // Act - 检查是否存在 Amount = 250 的日志
            bool exists = await viewService.ExistsAsync(
                l => l.Amount == 250,
                tableArgs: new[] { "202401" }
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
            await userService.InsertAsync(user);

            var log1 = new TestLog { Event = "KeepEvent", Amount = 100, CreateTime = new DateTime(2024, 1, 10), UserID = user.Id };
            var log2 = new TestLog { Event = "DeleteEvent", Amount = 500, CreateTime = new DateTime(2024, 1, 20), UserID = user.Id };
            await service.InsertAsync(log1);
            await service.InsertAsync(log2);

            // Act - 删除 Amount > 400 且 Event 为 "DeleteEvent" 的日志记录
            int deleted = await service.DeleteAsync(
                l => l.Amount > 400 && l.Event == "DeleteEvent",
                tableArgs: new[] { "202401" }
            );

            // Assert
            Assert.Equal(1, deleted);
            var remaining = await viewService.SearchAsync(
                l => l.Event == "KeepEvent" && l.UserID == user.Id,
                tableArgs: new[] { "202401" }
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
            await userService.InsertAsync(user);

            for (int i = 1; i <= 5; i++)
            {
                var log = new TestLog
                {
                    Event = $"QueryEvent{i}",
                    Amount = i * 100,
                    CreateTime = new DateTime(2024, 1, 10 + i),
                    UserID = user.Id
                };
                await service.InsertAsync(log);
            }

            // Act - 方式二：IQueryable 链式查询（支持排序、分页等）
            var logs = await viewService.SearchAsync(
                q => q.Where(l => l.Amount >= 200 && l.UserID == user.Id)
                      .OrderBy(l => l.Amount),
                tableArgs: new[] { "202401" }
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
            await userService.InsertAsync(user);

            var log1 = new TestLog { Event = "Feb Event", Amount = 150, CreateTime = new DateTime(2024, 2, 10), UserID = user.Id };
            var log2 = new TestLog { Event = "Feb Event 2", Amount = 250, CreateTime = new DateTime(2024, 2, 15), UserID = user.Id };
            await service.InsertAsync(log1);
            await service.InsertAsync(log2);

            // Act - 方式三：在 Lambda 中显式赋值 TableArgs（不需要显式传递 tableArgs 参数）
            var logs = await viewService.SearchAsync(
                l => l.TableArgs == new[] { "202402" } && l.Amount > 200
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
            await userService.InsertAsync(user);

            var log1 = new TestLog { Event = "Important", Amount = 500, CreateTime = new DateTime(2024, 3, 10), UserID = user.Id };
            var log2 = new TestLog { Event = "Temp", Amount = 50, CreateTime = new DateTime(2024, 3, 15), UserID = user.Id };
            var log3 = new TestLog { Event = "Temp", Amount = 30, CreateTime = new DateTime(2024, 3, 20), UserID = user.Id };
            await service.InsertAsync(log1);
            await service.InsertAsync(log2);
            await service.InsertAsync(log3);

            // Act - 删除所有临时记录（Amount < 100 且 Event 为 "Temp"）
            // 使用 Lambda 中的 TableArgs 赋值方式来指定分表
            int deleted = await service.DeleteAsync(
                l => l.TableArgs == new[] { "202403" } && l.Event == "Temp" && l.Amount < 100 && l.UserID == user.Id
            );

            // Assert
            Assert.Equal(2, deleted); // 删除 log2 和 log3
            var remaining = await viewService.SearchAsync(
                l => l.TableArgs == new[] { "202403" } && l.Event == "Important" && l.UserID == user.Id
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
            await userService.InsertAsync(user);

            // 向多个月份的表插入数据
            // 2024-01 数据
            var jan1 = new TestLog { Event = "JAN_MultiDelete", Amount = 100, CreateTime = new DateTime(2024, 1, 10), UserID = user.Id };
            var jan2 = new TestLog { Event = "JAN_MultiDelete", Amount = 200, CreateTime = new DateTime(2024, 1, 20), UserID = user.Id };
            await service.InsertAsync(jan1);
            await service.InsertAsync(jan2);

            // 2024-04 数据
            var apr1 = new TestLog { Event = "APR_Multi", Amount = 300, CreateTime = new DateTime(2024, 4, 10), UserID = user.Id };
            var apr2 = new TestLog { Event = "APR_Multi", Amount = 400, CreateTime = new DateTime(2024, 4, 20), UserID = user.Id };
            await service.InsertAsync(apr1);
            await service.InsertAsync(apr2);

            // Act 1 - 查询 2024-01 的高额日志（Amount > 150）
            var janLogs = await viewService.SearchAsync(
                l => l.Amount > 150 && l.UserID == user.Id,
                tableArgs: new[] { "202401" }
            );

            // Act 2 - 查询 2024-04 的所有日志
            var aprLogs = await viewService.SearchAsync(
                l => l.Event == "APR_Multi" && l.UserID == user.Id,
                tableArgs: new[] { "202404" }
            );

            // Act 3 - 删除 2024-04 中的低额日志（Amount < 350）
            int deletedApr = await service.DeleteAsync(
                l => l.Amount < 350 && l.UserID == user.Id,
                tableArgs: new[] { "202404" }
            );

            // Act 4 - 验证删除后的数据
            var aprRemaining = await viewService.SearchAsync(
                l => l.Event == "APR_Multi" && l.UserID == user.Id,
                tableArgs: new[] { "202404" }
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
            await userService.InsertAsync(user1);
            await userService.InsertAsync(user2);

            // 为 user1 插入日志
            var log1 = new TestLog { Event = "U1_Event", Amount = 100, CreateTime = new DateTime(2024, 5, 10), UserID = user1.Id };
            var log2 = new TestLog { Event = "U1_Event", Amount = 200, CreateTime = new DateTime(2024, 5, 15), UserID = user1.Id };
            await service.InsertAsync(log1);
            await service.InsertAsync(log2);

            // 为 user2 插入日志
            var log3 = new TestLog { Event = "U2_Event", Amount = 300, CreateTime = new DateTime(2024, 5, 20), UserID = user2.Id };
            await service.InsertAsync(log3);

            // Act - 查询某个用户的所有日志（通过外键关联）
            // 使用 TestLogView 可以获取关联的用户名
            var user1Logs = await viewService.SearchAsync(
                l => l.UserName == "LogUser1",
                tableArgs: new[] { "202405" }
            );

            var user2Logs = await viewService.SearchAsync(
                l => l.UserName == "LogUser2",
                tableArgs: new[] { "202405" }
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
            await asyncService.InsertAsync(user);

            // Act - 使用FunctionExpr和PropertyExpr增加复杂度
            var updateExpr = new UpdateExpr
            {
                Source = Expr.From<TestUser>(),
                Sets = new List<(string, ValueTypeExpr)>
                {
                    ("Age", Expr.Prop("Age") + Expr.Const(5)), // 使用运算符重载和Expr.Const，Age = Age + 5
                    ("Name", new FunctionExpr("UPPER", new PropertyExpr("Name"))) // 使用UPPER函数，参数为Name属性
                },
                Where = Expr.Lambda<TestUser>(u => u.Name == "UpdateExprTest")
            };
            int affected = service.Update(updateExpr);
            var retrieved = await viewService.GetObjectAsync(user.Id);

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
            await service.InsertAsync(user);

            // Act - 使用FunctionExpr和PropertyExpr增加复杂度
            var updateExpr = new UpdateExpr
            {
                Source = Expr.From<TestUser>(),
                Sets = new List<(string, ValueTypeExpr)>
                {
                    ("Age", Expr.Prop("Age") + Expr.Const(10)), // 使用运算符重载和Expr.Const，Age = Age + 10
                    ("Name", new FunctionExpr("CONCAT", new PropertyExpr("Name"), "_Updated")) // 使用CONCAT函数，参数为Name属性和字符串
                },
                Where = Expr.Lambda<TestUser>(u => u.Name == "UpdateExprAsyncTest")
            };
            int affected = await service.UpdateAsync(updateExpr);
            var retrieved = await viewService.GetObjectAsync(user.Id);

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
            await deptService.InsertAsync(dept);

            // 创建测试用户
            var userWithDept = new TestUser { Name = "UserWithDept", Age = 25, CreateTime = DateTime.Now, DeptId = dept.Id };
            await userService.InsertAsync(userWithDept);

            var userWithoutDept = new TestUser { Name = "UserWithoutDept", Age = 30, CreateTime = DateTime.Now, DeptId = -1 };
            await userService.InsertAsync(userWithoutDept);

            // Act - 使用 Expr.Exists lambda 方式查询
            var usersWithDept = await viewService.SearchAsync(
                Expr.Lambda<TestUser>(u => Expr.Exists<TestDepartment>(d => d.Id == u.DeptId))
            );

            var usersWithSpecificDept = await viewService.SearchAsync(
                Expr.Lambda<TestUser>(u => Expr.Exists<TestDepartment>(d => d.Id == u.DeptId && d.Name == "ExistsTestDept"))
            );

            // Assert
            Assert.Single(usersWithDept);
            Assert.Equal("UserWithDept", usersWithDept[0].Name);

            Assert.Single(usersWithSpecificDept);
            Assert.Equal("UserWithDept", usersWithSpecificDept[0].Name);
        }
    }
}
