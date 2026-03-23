using ScottPlot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LiteOrm.ChartGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            var reportPath = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "..", "LiteOrm.Benchmark", "LiteOrm.Benchmark.OrmBenchmark-report-github.md");
            
            if (File.Exists(reportPath))
            {
                Console.WriteLine($"Generating charts from benchmark report: {reportPath}");
                var chartGenerator = new ChartGenerator(reportPath);
                chartGenerator.GenerateCharts();
            }
            else
            {
                Console.WriteLine($"Benchmark report not found at: {reportPath}");
                Console.WriteLine("Please provide the path to the benchmark report as a command line argument.");
            }
        }
    }

    public class ChartGenerator
    {
        private readonly string _reportPath;

        public ChartGenerator(string reportPath)
        {
            _reportPath = reportPath;
        }

        public void GenerateCharts()
        {
            var data = ParseReport();
            
            // Debug: Check if data is parsed correctly
            Console.WriteLine($"InsertData count: {data.InsertData.Count}");
            foreach (var framework in data.InsertData.Keys)
            {
                Console.WriteLine($"  {framework}: {data.InsertData[framework]["100"]}ms (100 rows)");
            }
            
            // Generate charts by batch size
            GeneratePerformanceByBatchSize(data, "100");
            GeneratePerformanceByBatchSize(data, "1000");
            GeneratePerformanceByBatchSize(data, "5000");
            
            // Generate memory charts by batch size
            GenerateMemoryByBatchSize(data, "100");
            GenerateMemoryByBatchSize(data, "1000");
            GenerateMemoryByBatchSize(data, "5000");
            
            Console.WriteLine("Charts generated successfully!");
        }

        private BenchmarkData ParseReport()
        {
            var lines = File.ReadAllLines(_reportPath);
            var data = new BenchmarkData();

            // Debug: Print first few lines to check file format
            Console.WriteLine("First 20 lines of the report:");
            for (int i = 0; i < Math.Min(20, lines.Length); i++)
            {
                Console.WriteLine($"{i+1}: {lines[i]}");
            }

            // Parse the summary tables
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("Insert (ms)"))
                {
                    Console.WriteLine($"Found Insert table at line {i+1}");
                    ParseTable(lines, i + 2, data.InsertData);
                }
                else if (lines[i].Contains("Update (ms)"))
                {
                    Console.WriteLine($"Found Update table at line {i+1}");
                    ParseTable(lines, i + 2, data.UpdateData);
                }
                else if (lines[i].Contains("Upsert (ms)"))
                {
                    Console.WriteLine($"Found Upsert table at line {i+1}");
                    ParseTable(lines, i + 2, data.UpsertData);
                }
                else if (lines[i].Contains("Join Query (ms)"))
                {
                    Console.WriteLine($"Found Join Query table at line {i+1}");
                    ParseTable(lines, i + 2, data.JoinQueryData);
                }
                else if (lines[i].Contains("Memory Allocation (100 rows, KB)"))
                {
                    Console.WriteLine($"Found Memory 100 table at line {i+1}");
                    ParseMemoryTable(lines, i + 2, data.MemoryData["100"]);
                }
                else if (lines[i].Contains("Memory Allocation (1000 rows, KB)"))
                {
                    Console.WriteLine($"Found Memory 1000 table at line {i+1}");
                    ParseMemoryTable(lines, i + 2, data.MemoryData["1000"]);
                }
                else if (lines[i].Contains("Memory Allocation (5000 rows, KB)"))
                {
                    Console.WriteLine($"Found Memory 5000 table at line {i+1}");
                    ParseMemoryTable(lines, i + 2, data.MemoryData["5000"]);
                }
            }

            return data;
        }

        private void ParseTable(string[] lines, int startIndex, Dictionary<string, Dictionary<string, double>> data)
        {
            Console.WriteLine($"Parsing table starting at line {startIndex+1}");
            int i = startIndex;
            
            // Skip header line
            if (i < lines.Length && lines[i].StartsWith("| Framework"))
            {
                Console.WriteLine($"Skipping header line {i+1}: {lines[i]}");
                i++;
            }
            
            // Skip separator line
            if (i < lines.Length && lines[i].Contains(":---"))
            {
                Console.WriteLine($"Skipping separator line {i+1}: {lines[i]}");
                i++;
            }
            else
            {
                Console.WriteLine("No separator line found");
                return;
            }

            // Parse data rows
            while (i < lines.Length && lines[i].StartsWith("| "))
            {
                Console.WriteLine($"Parsing line {i+1}: {lines[i]}");
                var parts = lines[i].Split('|').Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
                Console.WriteLine($"Parts count: {parts.Length}");
                for (int j = 0; j < parts.Length; j++)
                {
                    Console.WriteLine($"  Part {j}: '{parts[j]}'");
                }
                
                if (parts.Length >= 4)
                {
                    var framework = parts[0].Trim('*'); // Remove bold markers
                    try
                    {
                        var value100 = double.Parse(parts[1].Replace(",", "").Trim('*'));
                        var value1000 = double.Parse(parts[2].Replace(",", "").Trim('*'));
                        var value5000 = double.Parse(parts[3].Replace(",", "").Trim('*'));
                        
                        data[framework] = new Dictionary<string, double>
                        {
                            { "100", value100 },
                            { "1000", value1000 },
                            { "5000", value5000 }
                        };
                        Console.WriteLine($"Added framework: {framework} with values: {value100}, {value1000}, {value5000}");
                    }
                    catch (Exception ex)
                    {         
                        Console.WriteLine($"Error parsing data: {ex.Message}");
                    }
                }
                i++;
            }
            
            Console.WriteLine($"Parsed {data.Count} frameworks");
        }

        private void ParseMemoryTable(string[] lines, int startIndex, Dictionary<string, Dictionary<string, double>> data)
        {
            Console.WriteLine($"Parsing memory table starting at line {startIndex+1}");
            int i = startIndex;
            
            // Skip header line
            if (i < lines.Length && lines[i].StartsWith("| Framework"))
            {
                Console.WriteLine($"Skipping header line {i+1}: {lines[i]}");
                i++;
            }
            
            // Skip separator line
            if (i < lines.Length && lines[i].Contains(":---"))
            {
                Console.WriteLine($"Skipping separator line {i+1}: {lines[i]}");
                i++;
            }
            else
            {
                Console.WriteLine("No separator line found");
                return;
            }

            // Parse data rows
            while (i < lines.Length && lines[i].StartsWith("| "))
            {
                Console.WriteLine($"Parsing line {i+1}: {lines[i]}");
                var parts = lines[i].Split('|').Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
                Console.WriteLine($"Parts count: {parts.Length}");
                for (int j = 0; j < parts.Length; j++)
                {
                    Console.WriteLine($"  Part {j}: '{parts[j]}'");
                }
                
                if (parts.Length >= 5)
                {
                    var framework = parts[0].Trim('*'); // Remove bold markers
                    try
                    {
                        var insert = double.Parse(parts[1].Replace(",", "").Trim('*'));
                        var update = double.Parse(parts[2].Replace(",", "").Trim('*'));
                        var upsert = double.Parse(parts[3].Replace(",", "").Trim('*'));
                        var joinQuery = double.Parse(parts[4].Replace(",", "").Trim('*'));
                        
                        data[framework] = new Dictionary<string, double>
                        {
                            { "Insert", insert },
                            { "Update", update },
                            { "Upsert", upsert },
                            { "Join Query", joinQuery }
                        };
                        Console.WriteLine($"Added framework: {framework} with values: {insert}, {update}, {upsert}, {joinQuery}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing memory data: {ex.Message}");
                    }
                }
                i++;
            }
            
            Console.WriteLine($"Parsed {data.Count} frameworks for memory data");
        }

        private void GenerateInsertChart(BenchmarkData data)
        {
            var plt = new ScottPlot.Plot();
            var frameworks = data.InsertData.Keys.ToList();
            var batchSizes = new[] { "100", "1000", "5000" };

            // Use grouped bars with proper spacing
            for (int i = 0; i < frameworks.Count; i++)
            {
                var framework = frameworks[i];
                var times = batchSizes.Select(batch => data.InsertData[framework][batch]).ToArray();
                var positions = Enumerable.Range(0, times.Length).Select(x => (double)(x * (frameworks.Count + 1) + i)).ToArray();
                var bar = plt.Add.Bars(positions, times);
                bar.LegendText = framework;
            }

            // Set axis labels
            plt.Axes.Bottom.Label.Text = "Batch Size";
            plt.Axes.Left.Label.Text = "Time (ms) - Lower is Better";
            plt.Title("Insert Performance Comparison");
            plt.Legend.IsVisible = true;
            plt.Legend.Alignment = Alignment.UpperRight;
            
            plt.SavePng(Path.Combine(Path.GetDirectoryName(_reportPath)!, "InsertPerformance.png"), 800, 600);
        }

        private void GenerateUpdateChart(BenchmarkData data)
        {
            var plt = new ScottPlot.Plot();
            var frameworks = data.UpdateData.Keys.ToList();
            var batchSizes = new[] { "100", "1000", "5000" };

            // Use grouped bars with proper spacing
            for (int i = 0; i < frameworks.Count; i++)
            {
                var framework = frameworks[i];
                var times = batchSizes.Select(batch => data.UpdateData[framework][batch]).ToArray();
                var positions = Enumerable.Range(0, times.Length).Select(x => (double)(x * (frameworks.Count + 1) + i)).ToArray();
                var bar = plt.Add.Bars(positions, times);
                bar.LegendText = framework;
            }

            // Set axis labels
            plt.Axes.Bottom.Label.Text = "Batch Size";
            plt.Axes.Left.Label.Text = "Time (ms) - Lower is Better";
            plt.Title("Update Performance Comparison");
            plt.Legend.IsVisible = true;
            plt.Legend.Alignment = Alignment.UpperRight;
            
            plt.SavePng(Path.Combine(Path.GetDirectoryName(_reportPath)!, "UpdatePerformance.png"), 800, 600);
        }

        private void GenerateUpsertChart(BenchmarkData data)
        {
            var plt = new ScottPlot.Plot();
            var frameworks = data.UpsertData.Keys.ToList();
            var batchSizes = new[] { "100", "1000", "5000" };

            // Use grouped bars with proper spacing
            for (int i = 0; i < frameworks.Count; i++)
            {
                var framework = frameworks[i];
                var times = batchSizes.Select(batch => data.UpsertData[framework][batch]).ToArray();
                var positions = Enumerable.Range(0, times.Length).Select(x => (double)(x * (frameworks.Count + 1) + i)).ToArray();
                var bar = plt.Add.Bars(positions, times);
                bar.LegendText = framework;
            }

            // Set axis labels
            plt.Axes.Bottom.Label.Text = "Batch Size";
            plt.Axes.Left.Label.Text = "Time (ms) - Lower is Better";
            plt.Title("Upsert Performance Comparison");
            plt.Legend.IsVisible = true;
            plt.Legend.Alignment = Alignment.UpperRight;
            
            plt.SavePng(Path.Combine(Path.GetDirectoryName(_reportPath)!, "UpsertPerformance.png"), 800, 600);
        }

        private void GenerateJoinQueryChart(BenchmarkData data)
        {
            var plt = new ScottPlot.Plot();
            var frameworks = data.JoinQueryData.Keys.ToList();
            var batchSizes = new[] { "100", "1000", "5000" };

            // Use grouped bars with proper spacing
            for (int i = 0; i < frameworks.Count; i++)
            {
                var framework = frameworks[i];
                var times = batchSizes.Select(batch => data.JoinQueryData[framework][batch]).ToArray();
                var positions = Enumerable.Range(0, times.Length).Select(x => (double)(x * (frameworks.Count + 1) + i)).ToArray();
                var bar = plt.Add.Bars(positions, times);
                bar.LegendText = framework;
            }

            // Set axis labels
            plt.Axes.Bottom.Label.Text = "Batch Size";
            plt.Axes.Left.Label.Text = "Time (ms) - Lower is Better";
            plt.Title("Join Query Performance Comparison");
            plt.Legend.IsVisible = true;
            plt.Legend.Alignment = Alignment.UpperRight;
            
            plt.SavePng(Path.Combine(Path.GetDirectoryName(_reportPath)!, "JoinQueryPerformance.png"), 800, 600);
        }

        private void GeneratePerformanceByBatchSize(BenchmarkData data, string batchSize)
        {
            var plt = new ScottPlot.Plot();
            var frameworks = data.InsertData.Keys.ToList();
            var operations = new[] { "Insert", "Update", "Upsert", "Join Query" };

            // Use grouped bars with proper spacing
            for (int i = 0; i < frameworks.Count; i++)
            {
                var framework = frameworks[i];
                var times = operations.Select(op => GetOperationTime(data, op, framework, batchSize)).ToArray();
                var positions = Enumerable.Range(0, times.Length).Select(x => (double)(x * (frameworks.Count + 1) + i)).ToArray();
                var bar = plt.Add.Bars(positions, times);
                bar.LegendText = framework;
            }

            // Set axis labels
            plt.Axes.Bottom.Label.Text = "Operation";
            plt.Axes.Left.Label.Text = "Time (ms) - Lower is Better";
            plt.Title($"Performance Comparison by Operation ({batchSize} rows)");
            
            // Set X-axis tick labels
            var groupWidth = frameworks.Count + 1;
            var tickPositions = operations.Select((op, i) => i * groupWidth + (groupWidth - 1) / 2.0).ToArray();
            var tickLabels = operations;
            plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(tickPositions, tickLabels);
            
            // Position legend outside the plot area
            plt.Legend.IsVisible = true;
            plt.Legend.Alignment = Alignment.UpperRight;
            
            plt.SavePng(Path.Combine(Path.GetDirectoryName(_reportPath)!, $"PerformanceByOperation_{batchSize}.png"), 1000, 600);
        }

        private void GenerateMemoryByBatchSize(BenchmarkData data, string batchSize)
        {
            var plt = new ScottPlot.Plot();
            var frameworks = data.MemoryData[batchSize].Keys.ToList();
            var operations = new[] { "Insert", "Update", "Upsert", "Join Query" };

            // For 5000 rows, set Y-axis maximum before adding data
            if (batchSize == "5000")
            {
                // Set Y-axis maximum to 200000
                plt.Axes.Left.Max = 200000;
            }

            // Use grouped bars with proper spacing
            for (int i = 0; i < frameworks.Count; i++)
            {
                var framework = frameworks[i];
                var memory = operations.Select(op => data.MemoryData[batchSize][framework][op]).ToArray();
                var positions = Enumerable.Range(0, memory.Length).Select(x => (double)(x * (frameworks.Count + 1) + i)).ToArray();
                var bar = plt.Add.Bars(positions, memory);
                bar.LegendText = framework;
            }

            // Set axis labels
            plt.Axes.Bottom.Label.Text = "Operation";
            plt.Axes.Left.Label.Text = "Memory (KB) - Lower is Better";
            plt.Title($"Memory Allocation by Operation ({batchSize} rows)");
            
            // Set X-axis tick labels
            var groupWidth = frameworks.Count + 1;
            var tickPositions = operations.Select((op, i) => i * groupWidth + (groupWidth - 1) / 2.0).ToArray();
            var tickLabels = operations;
            plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(tickPositions, tickLabels);
            
            // Position legend outside the plot area
            plt.Legend.IsVisible = true;
            plt.Legend.Alignment = Alignment.UpperRight;
            
            plt.SavePng(Path.Combine(Path.GetDirectoryName(_reportPath)!, $"MemoryByOperation_{batchSize}.png"), 1000, 600);
        }

        private double GetOperationTime(BenchmarkData data, string operation, string framework, string batchSize)
        {
            switch (operation)
            {
                case "Insert": return data.InsertData[framework][batchSize];
                case "Update": return data.UpdateData[framework][batchSize];
                case "Upsert": return data.UpsertData[framework][batchSize];
                case "Join Query": return data.JoinQueryData[framework][batchSize];
                default: return 0;
            }
        }
    }

    public class BenchmarkData
    {
        public Dictionary<string, Dictionary<string, double>> InsertData { get; } = new();
        public Dictionary<string, Dictionary<string, double>> UpdateData { get; } = new();
        public Dictionary<string, Dictionary<string, double>> UpsertData { get; } = new();
        public Dictionary<string, Dictionary<string, double>> JoinQueryData { get; } = new();
        public Dictionary<string, Dictionary<string, Dictionary<string, double>>> MemoryData { get; } = new()
        {
            { "100", new() },
            { "1000", new() },
            { "5000", new() }
        };
    }
}