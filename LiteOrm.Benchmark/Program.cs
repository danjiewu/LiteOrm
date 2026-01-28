using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using System;

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









