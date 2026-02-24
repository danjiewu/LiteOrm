# LiteOrm 性能评测报告

## 测试环境

- **BenchmarkDotNet**: v0.15.8
- **操作系统**: Linux Ubuntu 24.04 LTS (Noble Numbat)
- **CPU**: Intel Xeon Silver 4314 CPU 2.40GHz (Max: 2.39GHz), 1 CPU, 4 logical and 4 physical cores
- **.NET SDK**: 10.0.100
- **运行时**: .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v4
- **测试配置**: MediumRun, IterationCount=15, LaunchCount=2, WarmupCount=10

---

## 性能对比分析

### 1. Insert (批量插入)

| 框架 | 100 条 | 1000 条 | 5000 条 |
|------|--------|---------|---------|
| **LiteOrm_Insert_Async** | **4.121** | **14.421** | **58.925** |
| FreeSql_Insert_Async | 4.466 | 22.123 | 93.185 |
| SqlSugar_Insert_Async | 4.422 | 18.993 | 96.946 |
| Dapper_Insert_Async | 25.682 | 220.316 | 1,144.153 |
| EFCore_Insert_Async | 21.097 | 155.787 | 635.561 |

### 2. Update (批量更新)

| 框架 | 100 条 | 1000 条 | 5000 条 |
|------|--------|---------|---------|
| **LiteOrm_Update_Async** | **5.271** | **24.342** | **104.380** |
| FreeSql_Update_Async | 6.424 | 42.261 | 215.220 |
| SqlSugar_Update_Async | 6.530 | 46.280 | 235.362 |
| Dapper_Update_Async | 28.510 | 236.501 | 1,183.557 |
| EFCore_Update_Async | 20.182 | 136.900 | 567.423 |

### 3. UpdateOrInsert (批量更新或插入)

| 框架 | 100 条 | 1000 条 | 5000 条 |
|------|--------|---------|---------|
| LiteOrm_UpdateOrInsert_Async | 5.512 | 21.138 | **89.760** |
| **FreeSql_UpdateOrInsert_Async** | **5.071** | **22.006** | 92.640 |
| SqlSugar_UpdateOrInsert_Async | 10.567 | 106.873 | 1,755.484 |
| Dapper_UpdateOrInsert_Async | 29.409 | 246.259 | 1,183.150 |
| EFCore_UpdateOrInsert_Async | 22.434 | 141.613 | 585.999 |

### 4. JoinQuery (关联查询)

| 框架 | 100 条 | 1000 条 | 5000 条 |
|------|--------|---------|---------|
| **LiteOrm_JoinQuery_Async** | 2.153 | 16.933 | **77.800** |
| FreeSql_JoinQuery_Async | **2.107** | 17.261 | 91.026 |
| SqlSugar_JoinQuery_Async | 3.760 | 40.103 | 174.957 |
| Dapper_JoinQuery_Async | 2.121 | **16.584** | 80.345 |
| EFCore_JoinQuery_Async | 6.451 | 29.384 | 95.833 |

---

## 性能总结

### 各测试项目最优性能对比

| 测试项目 | 100 条 | 1000 条 | 5000 条 |
|----------|--------|---------|---------|
| **Insert** | **LiteOrm** (4.121 ms) | **LiteOrm** (14.421 ms) | **LiteOrm** (58.925 ms) |
| **Update** | **LiteOrm** (5.271 ms) | **LiteOrm** (24.342 ms) | **LiteOrm** (104.380 ms) |
| **UpdateOrInsert** | **FreeSql** (5.071 ms) | **FreeSql** (22.006 ms) | **LiteOrm** (89.760 ms) |
| **JoinQuery** | **FreeSql** (2.107 ms) | **Dapper** (16.584 ms) | **LiteOrm** (77.800 ms) |

### 关键发现

1. **Insert 性能**: LiteOrm 在所有数据量级别下均表现最优，性能领先明显
2. **Update 性能**: LiteOrm 在所有数据量级别下均表现最优
3. **UpdateOrInsert 性能**: 小数据量下 FreeSql 略优，大数据量下 LiteOrm 表现最佳
4. **JoinQuery 性能**: 小数据量下 FreeSql 和 Dapper 略优，大数据量下 LiteOrm 表现最佳
5. **内存分配**: LiteOrm 在大多数测试中内存分配最少，GC 压力最小
6. **稳定性**: LiteOrm 的 Error 和 StdDev 值普遍较低，性能稳定性好

---

## 原始测试数据

```
BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04 LTS (Noble Numbat)
Intel Xeon Silver 4314 CPU 2.40GHz (Max: 2.39GHz), 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.100
  [Host]    : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v4
  MediumRun : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v4

Job=MediumRun  IterationCount=15  LaunchCount=2  
WarmupCount=10  

```
| Method                        | BatchCount | Mean         | Error      | StdDev     | Median       | Gen0       | Gen1      | Gen2     | Allocated    |
|------------------------------ |----------- |-------------:|-----------:|-----------:|-------------:|-----------:|----------:|---------:|-------------:|
| EFCore_Insert_Async           | 100        |    21.097 ms |  2.1857 ms |  3.0640 ms |    20.151 ms |   142.8571 |   71.4286 |        - |   1794.26 KB |
| SqlSugar_Insert_Async         | 100        |     4.422 ms |  0.1040 ms |  0.1423 ms |     4.388 ms |    31.2500 |    7.8125 |        - |     475.3 KB |
| LiteOrm_Insert_Async          | 100        |     4.121 ms |  0.0224 ms |  0.0329 ms |     4.131 ms |    23.4375 |         - |        - |     295.4 KB |
| Dapper_Insert_Async           | 100        |    25.682 ms |  2.7710 ms |  4.1474 ms |    25.494 ms |          - |         - |        - |    254.08 KB |
| FreeSql_Insert_Async          | 100        |     4.466 ms |  0.4407 ms |  0.6178 ms |     4.112 ms |    31.2500 |    7.8125 |        - |    459.78 KB |
| EFCore_Update_Async           | 100        |    20.182 ms |  2.6941 ms |  3.7767 ms |    18.786 ms |   125.0000 |   62.5000 |        - |   1623.89 KB |
| SqlSugar_Update_Async         | 100        |     6.530 ms |  0.4169 ms |  0.5845 ms |     6.279 ms |    62.5000 |   15.6250 |        - |    799.42 KB |
| LiteOrm_Update_Async          | 100        |     5.271 ms |  0.0345 ms |  0.0494 ms |     5.276 ms |    23.4375 |    7.8125 |        - |    354.82 KB |
| Dapper_Update_Async           | 100        |    28.510 ms |  2.5820 ms |  3.8647 ms |    28.231 ms |          - |         - |        - |    320.73 KB |
| FreeSql_Update_Async          | 100        |     6.424 ms |  0.5972 ms |  0.8938 ms |     6.536 ms |    46.8750 |   15.6250 |        - |    692.13 KB |
| EFCore_UpdateOrInsert_Async   | 100        |    22.434 ms |  3.5278 ms |  4.9455 ms |    20.465 ms |    71.4286 |         - |        - |   1567.44 KB |
| SqlSugar_UpdateOrInsert_Async | 100        |    10.567 ms |  0.1899 ms |  0.2843 ms |    10.484 ms |   109.3750 |   31.2500 |        - |   1457.41 KB |
| LiteOrm_UpdateOrInsert_Async  | 100        |     5.512 ms |  0.1490 ms |  0.2137 ms |     5.459 ms |    31.2500 |         - |        - |    462.04 KB |
| Dapper_UpdateOrInsert_Async   | 100        |    29.409 ms |  3.6633 ms |  5.4831 ms |    28.016 ms |          - |         - |        - |    291.75 KB |
| FreeSql_UpdateOrInsert_Async  | 100        |     5.071 ms |  0.0456 ms |  0.0609 ms |     5.077 ms |    15.6250 |         - |        - |    265.33 KB |
| EFCore_JoinQuery_Async        | 100        |     6.451 ms |  0.0279 ms |  0.0400 ms |     6.465 ms |    31.2500 |         - |        - |    492.47 KB |
| SqlSugar_JoinQuery_Async      | 100        |     3.760 ms |  0.0689 ms |  0.0895 ms |     3.728 ms |   140.6250 |   15.6250 |        - |   1885.89 KB |
| LiteOrm_JoinQuery_Async       | 100        |     2.153 ms |  0.0617 ms |  0.0802 ms |     2.132 ms |     7.8125 |         - |        - |     145.2 KB |
| Dapper_JoinQuery_Async        | 100        |     2.121 ms |  0.0062 ms |  0.0089 ms |     2.122 ms |     3.9063 |         - |        - |     90.26 KB |
| FreeSql_JoinQuery_Async       | 100        |     2.107 ms |  0.0261 ms |  0.0375 ms |     2.099 ms |    11.7188 |         - |        - |    190.96 KB |
| EFCore_Insert_Async           | 1000       |   155.787 ms | 15.1884 ms | 22.2630 ms |   151.236 ms |  1000.0000 |  500.0000 |        - |  16265.64 KB |
| SqlSugar_Insert_Async         | 1000       |    18.993 ms |  1.1013 ms |  1.5438 ms |    18.892 ms |   375.0000 |  250.0000 |  31.2500 |    4569.7 KB |
| LiteOrm_Insert_Async          | 1000       |    14.421 ms |  0.9452 ms |  1.3556 ms |    14.248 ms |    62.5000 |         - |        - |    868.15 KB |
| Dapper_Insert_Async           | 1000       |   220.316 ms | 15.8191 ms | 23.6772 ms |   223.962 ms |          - |         - |        - |   2475.62 KB |
| FreeSql_Insert_Async          | 1000       |    22.123 ms |  1.7255 ms |  2.5293 ms |    22.913 ms |   343.7500 |  156.2500 |        - |   4629.54 KB |
| EFCore_Update_Async           | 1000       |   136.900 ms | 14.8569 ms | 21.7771 ms |   132.370 ms |  1000.0000 |  666.6667 |        - |  13843.37 KB |
| SqlSugar_Update_Async         | 1000       |    46.280 ms |  2.4429 ms |  3.6564 ms |    47.989 ms |   500.0000 |  250.0000 |        - |   7675.84 KB |
| LiteOrm_Update_Async          | 1000       |    24.342 ms |  1.7700 ms |  2.5944 ms |    25.789 ms |    93.7500 |   31.2500 |        - |   1513.02 KB |
| Dapper_Update_Async           | 1000       |   236.501 ms | 18.1753 ms | 27.2039 ms |   229.039 ms |          - |         - |        - |   3092.04 KB |
| FreeSql_Update_Async          | 1000       |    42.261 ms |  3.8046 ms |  5.6945 ms |    44.665 ms |   625.0000 |  500.0000 | 125.0000 |   6876.75 KB |
| EFCore_UpdateOrInsert_Async   | 1000       |   141.613 ms | 11.8732 ms | 17.7712 ms |   140.325 ms |  1000.0000 |  666.6667 |        - |  13896.33 KB |
| SqlSugar_UpdateOrInsert_Async | 1000       |   106.873 ms |  3.2266 ms |  4.7296 ms |   106.268 ms |  2666.6667 |  666.6667 |        - |  35950.27 KB |
| LiteOrm_UpdateOrInsert_Async  | 1000       |    21.138 ms |  1.5950 ms |  2.2876 ms |    20.881 ms |   156.2500 |   62.5000 |        - |   2137.68 KB |
| Dapper_UpdateOrInsert_Async   | 1000       |   246.259 ms | 20.0719 ms | 30.0427 ms |   246.474 ms |          - |         - |        - |   2799.84 KB |
| FreeSql_UpdateOrInsert_Async  | 1000       |    22.006 ms |  1.5314 ms |  2.2922 ms |    22.929 ms |   156.2500 |   62.5000 |        - |   2246.49 KB |
| EFCore_JoinQuery_Async        | 1000       |    29.384 ms |  2.5155 ms |  3.7651 ms |    28.533 ms |   250.0000 |  125.0000 |        - |   3382.58 KB |
| SqlSugar_JoinQuery_Async      | 1000       |    40.103 ms |  1.3512 ms |  1.9806 ms |    40.213 ms |  1333.3333 |  333.3333 |        - |  18343.87 KB |
| LiteOrm_JoinQuery_Async       | 1000       |    16.933 ms |  0.3646 ms |  0.5229 ms |    16.782 ms |    93.7500 |   31.2500 |        - |   1216.84 KB |
| Dapper_JoinQuery_Async        | 1000       |    16.584 ms |  0.1433 ms |  0.2146 ms |    16.579 ms |    62.5000 |   31.2500 |        - |    826.07 KB |
| FreeSql_JoinQuery_Async       | 1000       |    17.261 ms |  0.2243 ms |  0.3358 ms |    17.316 ms |   125.0000 |   31.2500 |        - |   1673.35 KB |
| EFCore_Insert_Async           | 5000       |   635.561 ms | 29.0092 ms | 43.4196 ms |   645.413 ms |  6000.0000 | 2000.0000 |        - |  80515.95 KB |
| SqlSugar_Insert_Async         | 5000       |    96.946 ms |  4.5844 ms |  6.7197 ms |    99.348 ms |  2000.0000 | 1200.0000 | 400.0000 |  23193.83 KB |
| LiteOrm_Insert_Async          | 5000       |    58.925 ms |  4.1262 ms |  6.1759 ms |    59.335 ms |   272.7273 |         - |        - |   4076.66 KB |
| Dapper_Insert_Async           | 5000       | 1,144.153 ms | 65.4269 ms | 97.9280 ms | 1,187.821 ms |  1000.0000 |         - |        - |   12351.8 KB |
| FreeSql_Insert_Async          | 5000       |    93.185 ms |  5.5173 ms |  8.2580 ms |    94.685 ms |  1833.3333 | 1000.0000 |        - |  23333.36 KB |
| EFCore_Update_Async           | 5000       |   567.423 ms | 33.5545 ms | 50.2228 ms |   555.257 ms |  5000.0000 | 1000.0000 |        - |  69492.13 KB |
| SqlSugar_Update_Async         | 5000       |   235.362 ms |  7.0588 ms | 10.5654 ms |   235.923 ms |  3000.0000 | 1500.0000 | 500.0000 |  38811.18 KB |
| LiteOrm_Update_Async          | 5000       |   104.380 ms |  4.4711 ms |  6.6921 ms |   105.045 ms |   600.0000 |  200.0000 |        - |   7489.21 KB |
| Dapper_Update_Async           | 5000       | 1,183.557 ms | 55.5075 ms | 83.0810 ms | 1,205.818 ms |  1000.0000 |         - |        - |  15469.94 KB |
| FreeSql_Update_Async          | 5000       |   215.220 ms | 19.2783 ms | 28.8548 ms |   218.119 ms |  2333.3333 |  333.3333 |        - |  34445.34 KB |
| EFCore_UpdateOrInsert_Async   | 5000       |   585.999 ms | 30.9044 ms | 46.2563 ms |   581.689 ms |  5000.0000 | 1000.0000 |        - |  68241.38 KB |
| SqlSugar_UpdateOrInsert_Async | 5000       | 1,755.484 ms | 14.2336 ms | 21.3042 ms | 1,752.695 ms | 68000.0000 | 1000.0000 |        - | 844261.64 KB |
| LiteOrm_UpdateOrInsert_Async  | 5000       |    89.760 ms |  5.3694 ms |  8.0367 ms |    93.503 ms |   500.0000 |  166.6667 |        - |   8026.47 KB |
| Dapper_UpdateOrInsert_Async   | 5000       | 1,183.150 ms | 55.1167 ms | 82.4961 ms | 1,169.153 ms |  1000.0000 |         - |        - |  13987.04 KB |
| FreeSql_UpdateOrInsert_Async  | 5000       |    92.640 ms |  5.0920 ms |  7.6214 ms |    90.903 ms |   666.6667 |  500.0000 |        - |  11124.21 KB |
| EFCore_JoinQuery_Async        | 5000       |    95.833 ms |  2.2923 ms |  3.3601 ms |    94.818 ms |  1000.0000 |  333.3333 |        - |  15616.35 KB |
| SqlSugar_JoinQuery_Async      | 5000       |   174.957 ms |  3.4634 ms |  5.0765 ms |   173.706 ms |  7000.0000 | 1000.0000 |        - |  91633.18 KB |
| LiteOrm_JoinQuery_Async       | 5000       |    77.800 ms |  0.7592 ms |  1.1363 ms |    77.873 ms |   428.5714 |  142.8571 |        - |   6081.98 KB |
| Dapper_JoinQuery_Async        | 5000       |    80.345 ms |  0.6674 ms |  0.9572 ms |    80.350 ms |   285.7143 |  142.8571 |        - |   4194.78 KB |
| FreeSql_JoinQuery_Async       | 5000       |    91.026 ms |  2.1656 ms |  3.2413 ms |    90.782 ms |   833.3333 |  500.0000 | 166.6667 |   8360.38 KB |

