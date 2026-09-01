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
            BenchmarkRunner.Run<OrmBenchmark>(config, args);
            BenchmarkRunner.Run<OrmSingleBenchmark>(config, args);
        }
    }
}









