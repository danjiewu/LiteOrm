using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace LiteOrm.Benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = ManualConfig.Create(DefaultConfig.Instance)
                .WithOptions(ConfigOptions.DisableOptimizationsValidator);

            var job = Environment.GetEnvironmentVariable("LITEORM_BENCH_JOB")?.ToLowerInvariant() == "short"
                ? Job.ShortRun
                : Job.MediumRun;

            // BenchmarkDotNet 0.15.8 尚未识别 .NET 11 的 RuntimeMoniker，CsProj 工具链会在校验阶段直接抛异常，
            // 因此 net11.0 目标固定使用进程内工具链；net10.0 可通过 LITEORM_BENCH_INPROCESS=1 切换到相同工具链，
            // 以保证 net10.0 / net11.0 的对比口径一致。
#if NET11_0
            job = job.WithToolchain(InProcessEmitToolchain.Instance).WithLaunchCount(1);
#else
            if (Environment.GetEnvironmentVariable("LITEORM_BENCH_INPROCESS") == "1")
                job = job.WithToolchain(InProcessEmitToolchain.Instance).WithLaunchCount(1);
#endif

            config.AddJob(job);

            BenchmarkRunner.Run<OrmBenchmark>(config, args);
            BenchmarkRunner.Run<OrmSingleBenchmark>(config, args);
        }
    }
}
