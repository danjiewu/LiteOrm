using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// 
    /// </summary>
    public static class DAOContextExtensions
    {
        /// <summary>
        /// 批量确保多个实体类型对应的表结构在数据库中存在。
        /// </summary>
        public static void EnsureTable(this DAOContext daoContext, Type objectType, string[] tableArgs = null)
        {
            DAOContextPool pool = daoContext?.Pool?.MasterPool;
            if (pool != daoContext.Pool || !pool.SyncTable) return;
            pool.DatabaseSync.EnsureTable(daoContext, objectType, tableArgs);
        }

        /// <summary>
        /// 批量确保多个实体类型对应的表结构在数据库中存在（异步版本）。
        /// </summary>
        public static async Task EnsureTableAsync(this DAOContext daoContext, Type objectType, string[] tableArgs = null)
        {
            DAOContextPool pool = daoContext?.Pool?.MasterPool;
            if (pool != daoContext.Pool || !pool.SyncTable) return;
            await pool.DatabaseSync.EnsureTableAsync(daoContext, objectType, tableArgs).ConfigureAwait(false);
        }

        /// <summary>
        /// 批量确保多个实体类型对应的表结构在数据库中存在。
        /// </summary>
        public static void EnsureTables(this DAOContext daoContext, IEnumerable<Type> objectTypes)
        {
            DAOContextPool pool = daoContext?.Pool?.MasterPool;
            if (pool != daoContext.Pool || !pool.SyncTable) return;
            foreach (var type in objectTypes)
            {
                daoContext.Pool.DatabaseSync.EnsureTable(daoContext, type);
            }
        }
        /// <summary>
        /// 批量确保多个实体类型对应的表结构在数据库中存在（异步版本）。
        /// </summary>
        public static async Task EnsureTablesAsync(this DAOContext daoContext, IEnumerable<Type> objectTypes)
        {
            DAOContextPool pool = daoContext?.Pool?.MasterPool;
            if (pool != daoContext.Pool || !pool.SyncTable) return;
            foreach (var type in objectTypes)
            {
                await pool.DatabaseSync.EnsureTableAsync(daoContext, type).ConfigureAwait(false);
            }
        }
    }
}
