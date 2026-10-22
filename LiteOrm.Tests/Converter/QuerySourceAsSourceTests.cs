using System;
using System.Collections.Generic;
using System.Linq;
using LiteOrm.Common;
using LiteOrm.Tests.Models;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// 校验 <see cref="QuerySourceExtensions.AsSource{T}(IQueryable{T})"/> 的类型收窄行为。
    /// 取数部分在 <c>QuerySourceDaoTests</c> 中用真实数据库覆盖。
    /// </summary>
    public class QuerySourceAsSourceTests
    {
        [Fact]
        public void AsSource_OnLiteOrmChain_ReturnsSameInstance()
        {
            IQueryable<TestUser> query = Expr.From<TestUser>().Where(u => u.Age > 18);
            QuerySource<TestUser> source = query.AsSource();

            // 运行时对象本来就是 QuerySource，收窄不产生新实例
            Assert.Same(query, source);
            Assert.Same(query.Expression, source.Expression);
        }

        [Fact]
        public void AsSource_AfterProjection_ElementTypeFollowsProjection()
        {
            IQueryable<string> query = Expr.From<TestUser>()
                .Where(u => u.Age > 18)
                .Select(u => u.Name!);

            QuerySource<string> source = query.AsSource();

            Assert.Same(query, source);
            Assert.Equal(typeof(string), source.ElementType);
        }

        [Fact]
        public void AsSource_WithoutExecutor_StillBuildsQueryExpr()
        {
            // Expr.From<T>() 的链没有执行器，AsSource 仍应收窄成功，用于继续拼装表达式
            IOrderedQueryable<TestUser> ordered = Expr.From<TestUser>()
                .Where(u => u.Age > 18)
                .OrderBy(u => u.Id);

            QuerySource<TestUser> source = ordered.AsSource();

            Assert.IsType<OrderByExpr>(source.GetQueryExpr());
            // 取数仍然需要执行器
            Assert.Throws<InvalidOperationException>(() => source.ToList());
        }

        [Fact]
        public void AsSource_OnNonLiteOrmQueryable_Throws()
        {
            IQueryable<int> query = new List<int> { 1, 2, 3 }.AsQueryable();

            var ex = Assert.Throws<InvalidOperationException>(() => query.AsSource());
            Assert.Contains("not a LiteOrm QuerySource", ex.Message);
        }

        [Fact]
        public void AsSource_WithExecutor_BindsExecutorWithoutRunning()
        {
            // Expr.From<T>() 的链没有执行器，传入执行器后应补上绑定
            var chain = Expr.From<TestUser>().Where(u => u.Age > 18);
            var probe = new ProbeExecutor();

            QuerySource<TestUser> source = chain.AsSource(probe);

            Assert.NotSame(chain, source);
            Assert.Same(chain.Expression, source.Expression);
            Assert.Equal(typeof(TestUser), source.ElementType);
            // 只绑定不执行
            Assert.Equal(0, probe.CallCount);
        }

        /// <summary>
        /// 记录调用次数的执行器探针，用于确认 <c>AsSource(executor)</c> 不会真的执行查询。
        /// </summary>
        private sealed class ProbeExecutor : IQuerySourceExecutor<TestUser>
        {
            public int CallCount { get; private set; }

            public IEnumerable<TestUser> AsEnumerable(Expr query) { CallCount++; return Array.Empty<TestUser>(); }
            public IAsyncEnumerable<TestUser> AsAsyncEnumerable(Expr query, System.Threading.CancellationToken cancellationToken = default) { CallCount++; return Empty<TestUser>(); }
            public System.Threading.Tasks.Task<List<TestUser>> ToListAsync(Expr query, System.Threading.CancellationToken cancellationToken = default) { CallCount++; return System.Threading.Tasks.Task.FromResult(new List<TestUser>()); }
            public TestUser FirstOrDefault(Expr query) { CallCount++; return null!; }
            public System.Threading.Tasks.Task<TestUser> FirstOrDefaultAsync(Expr query, System.Threading.CancellationToken cancellationToken = default) { CallCount++; return System.Threading.Tasks.Task.FromResult<TestUser>(null!); }

            IEnumerable<TResult> IQuerySourceExecutor.AsEnumerable<TResult>(Expr query) { CallCount++; return Array.Empty<TResult>(); }
            IAsyncEnumerable<TResult> IQuerySourceExecutor.AsAsyncEnumerable<TResult>(Expr query, System.Threading.CancellationToken cancellationToken) { CallCount++; return Empty<TResult>(); }
            System.Threading.Tasks.Task<List<TResult>> IQuerySourceExecutor.ToListAsync<TResult>(Expr query, System.Threading.CancellationToken cancellationToken) { CallCount++; return System.Threading.Tasks.Task.FromResult(new List<TResult>()); }
            TResult IQuerySourceExecutor.FirstOrDefault<TResult>(Expr query) { CallCount++; return default!; }
            System.Threading.Tasks.Task<TResult> IQuerySourceExecutor.FirstOrDefaultAsync<TResult>(Expr query, System.Threading.CancellationToken cancellationToken) { CallCount++; return System.Threading.Tasks.Task.FromResult<TResult>(default!); }

            private static async IAsyncEnumerable<T> Empty<T>()
            {
                await System.Threading.Tasks.Task.CompletedTask;
                yield break;
            }
        }
    }
}