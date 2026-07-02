using LiteOrm.Common;
using LiteOrm.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// 测试 DatabaseSync 的 SyncTableDeciding 事件与实体类级同步判定逻辑。
    /// </summary>
    [Collection("Database")]
    public class DatabaseSyncTests : TestBase
    {
        private readonly DAOContextPool _pool;

        public DatabaseSyncTests(DatabaseFixture fixture) : base(fixture)
        {
            _pool = ServiceProvider.GetRequiredService<DAOContextPoolFactory>().GetPool("SQLite");
        }

        /// <summary>
        /// 当订阅者将 Sync 置为 false 时，EnsureTable 应跳过建表。
        /// </summary>
        [Fact]
        public void EnsureTable_EventReturnsFalse_DoesNotCreateTable()
        {
            DropTestTable();
            _pool.DatabaseSync.ClearTableCache();

            EventHandler<TableSyncingEventArgs> handler = (s, e) =>
            {
                if (e.ObjectType == typeof(SyncDecidingTestModel))
                    e.ShouldSync = false;
            };
            _pool.DatabaseSync.OnTableSyncing += handler;
            try
            {
                var context = _pool.PeekContext();
                try
                {
                    _pool.DatabaseSync.EnsureTable(context, typeof(SyncDecidingTestModel));
                }
                finally
                {
                    _pool.ReturnContext(context);
                }

                Assert.False(TableExists(), "表不应被创建，但实际已存在。");
            }
            finally
            {
                _pool.DatabaseSync.OnTableSyncing -= handler;
            }
        }

        /// <summary>
        /// 当连接池 SyncTable=false 但订阅者将 Sync 置为 true 时，EnsureTable 仍应建表。
        /// </summary>
        [Fact]
        public void EnsureTable_PoolDisabledButEventReturnsTrue_CreatesTable()
        {
            DropTestTable();
            _pool.DatabaseSync.ClearTableCache();

            bool originalSync = _pool.SyncTable;
            _pool.SyncTable = false;
            EventHandler<TableSyncingEventArgs> handler = (s, e) =>
            {
                if (e.ObjectType == typeof(SyncDecidingTestModel))
                    e.ShouldSync = true;
            };
            _pool.DatabaseSync.OnTableSyncing += handler;
            try
            {
                var context = _pool.PeekContext();
                try
                {
                    _pool.DatabaseSync.EnsureTable(context, typeof(SyncDecidingTestModel));
                }
                finally
                {
                    _pool.ReturnContext(context);
                }

                Assert.True(TableExists(), "表应被创建，但实际不存在。");
            }
            finally
            {
                _pool.DatabaseSync.OnTableSyncing -= handler;
                _pool.SyncTable = originalSync;
            }
        }

        /// <summary>
        /// 无订阅者时，事件参数默认值应等于连接池级 SyncTable 配置。
        /// </summary>
        [Fact]
        public void EnsureTable_NoSubscriber_UsesPoolSyncTableAsDefault()
        {
            DropTestTable();
            _pool.DatabaseSync.ClearTableCache();

            bool originalSync = _pool.SyncTable;
            _pool.SyncTable = false;
            try
            {
                var context = _pool.PeekContext();
                try
                {
                    _pool.DatabaseSync.EnsureTable(context, typeof(SyncDecidingTestModel));
                }
                finally
                {
                    _pool.ReturnContext(context);
                }

                Assert.False(TableExists(), "连接池 SyncTable=false 时不应建表。");
            }
            finally
            {
                _pool.SyncTable = originalSync;
            }
        }

        private bool TableExists()
        {
            var context = _pool.PeekContext();
            try
            {
                using var cmd = context.CreateCommand();
                cmd.CommandText = "SELECT 1 FROM \"SyncDecidingTestTable\" WHERE 1=0";
                cmd.ExecuteNonQuery();
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                _pool.ReturnContext(context);
            }
        }

        private void DropTestTable()
        {
            var context = _pool.PeekContext();
            try
            {
                using var cmd = context.CreateCommand();
                cmd.CommandText = "DROP TABLE IF EXISTS \"SyncDecidingTestTable\"";
                cmd.ExecuteNonQuery();
            }
            finally
            {
                _pool.ReturnContext(context);
            }
        }
    }

    [Table("SyncDecidingTestTable")]
    public class SyncDecidingTestModel
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Name")]
        public string? Name { get; set; }
    }
}
