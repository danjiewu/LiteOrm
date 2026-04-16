using LiteOrm.WebDemo.Infrastructure;
using LiteOrm.WebDemo.Models;
using LiteOrm.Service;
using LiteOrm.WebDemo.Services;

namespace LiteOrm.WebDemo.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var departmentService = services.GetRequiredService<IDemoDepartmentService>();
        var userService = services.GetRequiredService<IDemoUserService>();
        var orderService = services.GetRequiredService<IDemoOrderService>();
        var sessionService = services.GetRequiredService<IDemoAuthSessionService>();
        var customerService = services.GetRequiredService<IEntityServiceAsync<DemoCustomer>>();
        var customerViewService = services.GetRequiredService<IEntityViewServiceAsync<DemoCustomer>>();
        var productService = services.GetRequiredService<IEntityServiceAsync<DemoProduct>>();
        var productViewService = services.GetRequiredService<IEntityViewServiceAsync<DemoProduct>>();
        var passwordService = services.GetRequiredService<PasswordService>();

        var departmentCount = await departmentService.CountAsync();
        var userCount = await userService.CountAsync();
        var orderCount = await orderService.CountAsync();
        var customerCount = await customerViewService.CountAsync();
        var productCount = await productViewService.CountAsync();

        if (departmentCount > 0 && userCount > 0 && orderCount > 0 && customerCount > 0 && productCount > 0)
        {
            return;
        }

        if (departmentCount == 0)
        {
            var departments = new[]
            {
                new DemoDepartment { Id = 1, Name = "Sales", Code = "SALES" },
                new DemoDepartment { Id = 2, Name = "Operations", Code = "OPS" },
                new DemoDepartment { Id = 3, Name = "Key Accounts", Code = "KA" }
            };
            await departmentService.BatchInsertAsync(departments);
        }

        DemoUser CreateUser(int id, string userName, string displayName, string role, int departmentId, string password)
        {
            var hash = passwordService.Hash(password);
            return new DemoUser
            {
                Id = id,
                UserName = userName,
                DisplayName = displayName,
                Role = role,
                DepartmentId = departmentId,
                PasswordHash = hash.Hash,
                PasswordSalt = hash.Salt,
                CreatedTime = DateTime.UtcNow.AddDays(-30 + id)
            };
        }

        if (userCount == 0)
        {
            var users = new[]
            {
                CreateUser(1, "admin", "Demo Admin", "Admin", 2, "admin123"),
                CreateUser(2, "alice", "Alice Chen", "Sales", 1, "demo123"),
                CreateUser(3, "bob", "Bob Wang", "Sales", 1, "demo123"),
                CreateUser(4, "cathy", "Cathy Liu", "Operations", 2, "demo123"),
                CreateUser(5, "david", "David Zhao", "AccountManager", 3, "demo123")
            };
            await userService.BatchInsertAsync(users);
        }

        var now = DateTime.UtcNow;
        var statuses = DemoOrderStatuses.All;
        var customerPool = new[] { "Contoso", "Fabrikam", "Northwind", "Adventure Works", "Woodgrove" };
        var productPool = new[] { "Laptop", "Monitor", "Keyboard", "Dock", "Chair", "Camera" };
        var notes = new[] { "priority", "demo customer", "follow-up", "standard", "expedite", "bulk order" };

        if (orderCount == 0)
        {
            var orders = Enumerable.Range(1, 24)
                .Select(index =>
                {
                    var quantity = index % 5 + 1;
                    var unitPrice = 80m + index * 15m;
                    return new DemoOrder
                    {
                        Id = index,
                        OrderNo = $"ORD-2026-{index:000}",
                        CustomerName = customerPool[(index - 1) % customerPool.Length],
                        ProductName = productPool[(index - 1) % productPool.Length],
                        Quantity = quantity,
                        UnitPrice = unitPrice,
                        TotalAmount = quantity * unitPrice,
                        Status = statuses[(index - 1) % statuses.Length],
                        Note = notes[(index - 1) % notes.Length],
                        CreatedTime = now.AddDays(-index),
                        UpdatedTime = now.AddDays(-index).AddHours(index % 7),
                        CreatedByUserId = index % 4 switch
                        {
                            0 => 2,
                            1 => 3,
                            2 => 4,
                            _ => 5
                        }
                    };
                })
                .ToArray();

            await orderService.BatchInsertAsync(orders);
        }

        if (customerCount == 0)
        {
            var customers = new[]
            {
                new DemoCustomer { Id = 1, Name = "Contoso Retail", Level = "A", City = "Shanghai", IsVip = true, CreatedTime = now.AddDays(-120) },
                new DemoCustomer { Id = 2, Name = "Fabrikam Stores", Level = "B", City = "Beijing", IsVip = false, CreatedTime = now.AddDays(-100) },
                new DemoCustomer { Id = 3, Name = "Northwind Foods", Level = "A", City = "Shenzhen", IsVip = true, CreatedTime = now.AddDays(-90) },
                new DemoCustomer { Id = 4, Name = "Adventure Works", Level = "C", City = "Guangzhou", IsVip = false, CreatedTime = now.AddDays(-60) },
                new DemoCustomer { Id = 5, Name = "Woodgrove Bank", Level = "S", City = "Hangzhou", IsVip = true, CreatedTime = now.AddDays(-30) }
            };
            await customerService.BatchInsertAsync(customers);
        }

        if (productCount == 0)
        {
            var products = new[]
            {
                new DemoProduct { Id = 1, Sku = "NB-001", Name = "Business Laptop", Category = "Computers", UnitPrice = 5999m, StockQuantity = 18, IsActive = true, PublishedTime = now.AddDays(-80) },
                new DemoProduct { Id = 2, Sku = "MN-002", Name = "4K Monitor", Category = "Displays", UnitPrice = 1999m, StockQuantity = 36, IsActive = true, PublishedTime = now.AddDays(-70) },
                new DemoProduct { Id = 3, Sku = "KB-003", Name = "Mechanical Keyboard", Category = "Accessories", UnitPrice = 499m, StockQuantity = 120, IsActive = true, PublishedTime = now.AddDays(-50) },
                new DemoProduct { Id = 4, Sku = "DC-004", Name = "USB-C Dock", Category = "Accessories", UnitPrice = 799m, StockQuantity = 42, IsActive = true, PublishedTime = now.AddDays(-40) },
                new DemoProduct { Id = 5, Sku = "CM-005", Name = "Conference Camera", Category = "Collaboration", UnitPrice = 1599m, StockQuantity = 12, IsActive = false, PublishedTime = now.AddDays(-20) }
            };
            await productService.BatchInsertAsync(products);
        }

        var sessions = await sessionService.SearchAsync();
        if (sessions.Count > 0)
        {
            await sessionService.BatchDeleteAsync(sessions);
        }
    }
}
