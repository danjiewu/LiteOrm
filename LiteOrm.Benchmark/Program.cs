using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace LiteOrm.Benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = ManualConfig.Create(DefaultConfig.Instance)
                .WithOptions(ConfigOptions.DisableOptimizationsValidator);
            var summary = BenchmarkRunner.Run<OrmBenchmark>(config, args);
        }
    }
}









