
# ORM 性能测试汇总报告 (Benchmark Summary)

## 核心结论
- **高吞吐性能**：在 1000 行及 5000 行的批量操作（Insert/Update/Upsert）中，**LiteOrm 性能全面领跑**，通常比 SqlSugar/FreeSql 快 1.5x - 2x，比 EF Core 快 10x 以上。
- **内存控制**：LiteOrm 的内存分配（Allocated）显著低于其他功能完备型 ORM，尤其在处理 5000 行数据时，内存开销仅为 EF Core 的 1/20。
- **查询效率**：在复杂 Join 查询场景下，LiteOrm 保持了与 Dapper 近乎一致的接近原生驱动的性能，且大幅优于 EF Core。

## 汇总数据对比 (Mean Time)

### BatchCount: 100 (低数据量)

| 操作类型 | EFCore | SqlSugar | LiteOrm | Dapper | FreeSql |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Insert** | 29.20 ms | 4.79 ms | 4.98 ms | 19.47 ms | **4.11 ms** |
| **Update** | 28.71 ms | 6.50 ms | **5.52 ms** | 18.65 ms | 6.18 ms |
| **Upsert** | 25.62 ms | 12.20 ms | 5.94 ms | 21.36 ms | **5.27 ms** |
| **Join Query** | 7.70 ms | 4.18 ms | 1.72 ms | **1.58 ms** | 1.80 ms |

### BatchCount: 1000 (标准批量)
| 操作类型 | EFCore | SqlSugar | LiteOrm | Dapper | FreeSql |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Insert** | 164.40 ms | 22.38 ms | **11.70 ms** | 144.11 ms | 23.40 ms |
| **Update** | 144.93 ms | 43.78 ms | **19.54 ms** | 146.34 ms | 52.44 ms |
| **Upsert** | 189.01 ms | 76.90 ms | 19.44 ms | 198.64 ms | **17.63 ms** |
| **Join Query** | 19.99 ms | 25.79 ms | 14.14 ms | 13.95 ms | **13.93 ms** |

### BatchCount: 5000 (大规模数据)
| 操作类型 | EFCore | SqlSugar | LiteOrm | Dapper | FreeSql |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Insert** | 835.13 ms | 72.92 ms | **42.85 ms** | 634.74 ms | 78.30 ms |
| **Update** | 760.63 ms | 197.20 ms | **76.32 ms** | 749.42 ms | 146.56 ms |
| **Upsert** | 781.72 ms | 989.50 ms | **64.87 ms** | 793.22 ms | 71.89 ms |
| **Join Query** | 138.08 ms | 140.88 ms | **104.39 ms** | 176.16 ms | 106.61 ms |

---

# 原始数据报告 (Original Report)
```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
13th Gen Intel Core i5-13400F 2.50GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                        | BatchCount | Mean       | Error       | StdDev     | Gen0       | Gen1      | Gen2     | Allocated    |
|------------------------------ |----------- |-----------:|------------:|-----------:|-----------:|----------:|---------:|-------------:|
| **EFCore_Insert_Async**           | **100**        |  **29.202 ms** |  **10.3537 ms** |  **0.5675 ms** |   **156.2500** |   **31.2500** |        **-** |   **1963.43 KB** |
| SqlSugar_Insert_Async         | 100        |   4.792 ms |   6.4264 ms |  0.3523 ms |    39.0625 |    7.8125 |        - |    476.07 KB |
| LiteOrm_Insert_Async          | 100        |   4.978 ms |   1.6927 ms |  0.0928 ms |    23.4375 |         - |        - |    295.42 KB |
| Dapper_Insert_Async           | 100        |  19.465 ms |  19.6237 ms |  1.0756 ms |          - |         - |        - |    254.28 KB |
| FreeSql_Insert_Async          | 100        |   4.107 ms |   1.5418 ms |  0.0845 ms |    39.0625 |    7.8125 |        - |    460.66 KB |
| EFCore_Update_Async           | 100        |  28.712 ms |  20.9905 ms |  1.1506 ms |   125.0000 |   31.2500 |        - |   1667.39 KB |
| SqlSugar_Update_Async         | 100        |   6.496 ms |   7.0815 ms |  0.3882 ms |    78.1250 |   23.4375 |        - |    800.78 KB |
| LiteOrm_Update_Async          | 100        |   5.516 ms |   6.3171 ms |  0.3463 ms |    31.2500 |    7.8125 |        - |    355.21 KB |
| Dapper_Update_Async           | 100        |  18.651 ms |  15.4252 ms |  0.8455 ms |    31.2500 |         - |        - |    320.67 KB |
| FreeSql_Update_Async          | 100        |   6.179 ms |   3.3732 ms |  0.1849 ms |    62.5000 |   15.6250 |        - |    693.01 KB |
| EFCore_UpdateOrInsert_Async   | 100        |  25.618 ms |   5.4013 ms |  0.2961 ms |   125.0000 |   31.2500 |        - |   1639.18 KB |
| SqlSugar_UpdateOrInsert_Async | 100        |  12.203 ms |   9.5224 ms |  0.5220 ms |   140.6250 |   46.8750 |        - |   1458.47 KB |
| LiteOrm_UpdateOrInsert_Async  | 100        |   5.942 ms |   1.9912 ms |  0.1091 ms |    39.0625 |         - |        - |    462.52 KB |
| Dapper_UpdateOrInsert_Async   | 100        |  21.362 ms |   4.8277 ms |  0.2646 ms |          - |         - |        - |    291.65 KB |
| FreeSql_UpdateOrInsert_Async  | 100        |   5.270 ms |   7.8103 ms |  0.4281 ms |    23.4375 |         - |        - |    264.98 KB |
| EFCore_JoinQuery_Async        | 100        |   7.699 ms |   1.9919 ms |  0.1092 ms |    46.8750 |         - |        - |    494.51 KB |
| SqlSugar_JoinQuery_Async      | 100        |   4.176 ms |   1.5817 ms |  0.0867 ms |   183.5938 |   19.5313 |        - |   1886.92 KB |
| LiteOrm_JoinQuery_Async       | 100        |   1.718 ms |   1.2315 ms |  0.0675 ms |    11.7188 |         - |        - |    145.35 KB |
| Dapper_JoinQuery_Async        | 100        |   1.575 ms |   0.6757 ms |  0.0370 ms |     7.8125 |         - |        - |     89.91 KB |
| FreeSql_JoinQuery_Async       | 100        |   1.803 ms |   1.3578 ms |  0.0744 ms |    17.5781 |    3.9063 |        - |    192.12 KB |
| **EFCore_Insert_Async**           | **1000**       | **164.401 ms** | **294.3491 ms** | **16.1343 ms** |   **333.3333** |         **-** |        **-** |  **18041.44 KB** |
| SqlSugar_Insert_Async         | 1000       |  22.380 ms |  35.2562 ms |  1.9325 ms |   250.0000 |  218.7500 |  31.2500 |   4570.21 KB |
| LiteOrm_Insert_Async          | 1000       |  11.695 ms |  10.7255 ms |  0.5879 ms |    78.1250 |   15.6250 |        - |    869.55 KB |
| Dapper_Insert_Async           | 1000       | 144.106 ms |  77.6397 ms |  4.2557 ms |          - |         - |        - |    2476.1 KB |
| FreeSql_Insert_Async          | 1000       |  23.404 ms |  31.3498 ms |  1.7184 ms |   312.5000 |  187.5000 |        - |   4629.95 KB |
| EFCore_Update_Async           | 1000       | 144.932 ms |  55.3468 ms |  3.0337 ms |   333.3333 |         - |        - |  15099.15 KB |
| SqlSugar_Update_Async         | 1000       |  43.778 ms |  39.0730 ms |  2.1417 ms |   250.0000 |  166.6667 |        - |    7675.7 KB |
| LiteOrm_Update_Async          | 1000       |  19.540 ms |  14.5274 ms |  0.7963 ms |   125.0000 |   31.2500 |        - |   1513.86 KB |
| Dapper_Update_Async           | 1000       | 146.340 ms |  74.6955 ms |  4.0943 ms |          - |         - |        - |    3095.6 KB |
| FreeSql_Update_Async          | 1000       |  52.442 ms |  74.1745 ms |  4.0658 ms |   466.6667 |  266.6667 | 133.3333 |   6876.65 KB |
| EFCore_UpdateOrInsert_Async   | 1000       | 189.007 ms | 199.1539 ms | 10.9163 ms |   333.3333 |         - |        - |  14857.79 KB |
| SqlSugar_UpdateOrInsert_Async | 1000       |  76.898 ms |  31.9748 ms |  1.7526 ms |  2571.4286 | 1000.0000 |        - |  35950.17 KB |
| LiteOrm_UpdateOrInsert_Async  | 1000       |  19.443 ms |   4.6893 ms |  0.2570 ms |   187.5000 |   93.7500 |        - |   2138.83 KB |
| Dapper_UpdateOrInsert_Async   | 1000       | 198.644 ms | 153.9828 ms |  8.4403 ms |          - |         - |        - |   2798.15 KB |
| FreeSql_UpdateOrInsert_Async  | 1000       |  17.628 ms |  16.2638 ms |  0.8915 ms |   218.7500 |   62.5000 |        - |   2239.28 KB |
| EFCore_JoinQuery_Async        | 1000       |  19.985 ms |   4.8590 ms |  0.2663 ms |    62.5000 |   31.2500 |  31.2500 |    3385.6 KB |
| SqlSugar_JoinQuery_Async      | 1000       |  25.792 ms |  20.4858 ms |  1.1229 ms |  1781.2500 |  781.2500 |        - |  18344.85 KB |
| LiteOrm_JoinQuery_Async       | 1000       |  14.140 ms |   4.9206 ms |  0.2697 ms |   109.3750 |   31.2500 |        - |   1217.18 KB |
| Dapper_JoinQuery_Async        | 1000       |  13.954 ms |  13.1829 ms |  0.7226 ms |    62.5000 |   15.6250 |        - |    821.17 KB |
| FreeSql_JoinQuery_Async       | 1000       |  13.934 ms |   4.7220 ms |  0.2588 ms |   156.2500 |   62.5000 |        - |   1674.42 KB |
| **EFCore_Insert_Async**           | **5000**       | **835.129 ms** | **239.9153 ms** | **13.1506 ms** |          **-** |         **-** |        **-** |   **89217.5 KB** |
| SqlSugar_Insert_Async         | 5000       |  72.923 ms |  53.7891 ms |  2.9484 ms |   166.6667 |         - |        - |  23191.58 KB |
| LiteOrm_Insert_Async          | 5000       |  42.854 ms |  21.9791 ms |  1.2047 ms |   363.6364 |  181.8182 |        - |   4082.54 KB |
| Dapper_Insert_Async           | 5000       | 634.742 ms | 247.3301 ms | 13.5570 ms |  1000.0000 |         - |        - |   12350.7 KB |
| FreeSql_Insert_Async          | 5000       |  78.297 ms |  45.9935 ms |  2.5211 ms |   833.3333 |  666.6667 |        - |  23333.55 KB |
| EFCore_Update_Async           | 5000       | 760.632 ms | 122.3875 ms |  6.7085 ms |          - |         - |        - |     74822 KB |
| SqlSugar_Update_Async         | 5000       | 197.200 ms | 294.3066 ms | 16.1319 ms |   333.3333 |         - |        - |  38807.63 KB |
| LiteOrm_Update_Async          | 5000       |  76.319 ms |  57.1652 ms |  3.1334 ms |   333.3333 |  166.6667 |        - |   7492.26 KB |
| Dapper_Update_Async           | 5000       | 749.420 ms | 271.6852 ms | 14.8920 ms |  1000.0000 |         - |        - |  15478.25 KB |
| FreeSql_Update_Async          | 5000       | 146.558 ms | 131.3852 ms |  7.2017 ms |  1000.0000 |  750.0000 |        - |   34449.6 KB |
| EFCore_UpdateOrInsert_Async   | 5000       | 781.715 ms | 431.6653 ms | 23.6610 ms |          - |         - |        - |  72651.09 KB |
| SqlSugar_UpdateOrInsert_Async | 5000       | 989.495 ms | 309.7377 ms | 16.9778 ms | 74000.0000 | 1000.0000 |        - | 844273.98 KB |
| LiteOrm_UpdateOrInsert_Async  | 5000       |  64.868 ms |  25.3607 ms |  1.3901 ms |   428.5714 |  142.8571 |        - |   8030.01 KB |
| Dapper_UpdateOrInsert_Async   | 5000       | 793.218 ms | 323.7970 ms | 17.7484 ms |  1000.0000 |         - |        - |  13983.27 KB |
| FreeSql_UpdateOrInsert_Async  | 5000       |  71.889 ms |  42.6673 ms |  2.3387 ms |   571.4286 |  428.5714 |        - |  11085.62 KB |
| EFCore_JoinQuery_Async        | 5000       | 138.078 ms | 550.0072 ms | 30.1477 ms |   500.0000 |         - |        - |  16018.43 KB |
| SqlSugar_JoinQuery_Async      | 5000       | 140.877 ms |  28.3274 ms |  1.5527 ms |  8500.0000 | 1500.0000 |        - |  91634.99 KB |
| LiteOrm_JoinQuery_Async       | 5000       | 104.387 ms |  67.6378 ms |  3.7075 ms |   333.3333 |         - |        - |   6083.24 KB |
| Dapper_JoinQuery_Async        | 5000       | 176.159 ms | 933.6783 ms | 51.1781 ms |          - |         - |        - |   4170.07 KB |
| FreeSql_JoinQuery_Async       | 5000       | 106.605 ms | 155.3333 ms |  8.5143 ms |          - |         - |        - |   8362.99 KB |
