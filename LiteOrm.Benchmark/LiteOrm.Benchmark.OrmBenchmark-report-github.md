# LiteOrm ORM Benchmark Results

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04 LTS (Noble Numbat)  
Intel Xeon Silver 4314 CPU 2.40GHz, .NET 10.0.3, X64 RyuJIT x86-64-v4  
Job=MediumRun  IterationCount=15  LaunchCount=2  WarmupCount=10

## ⚡ Performance Summary

### Insert (ms) — lower is better

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| **LiteOrm** | **3.90** | **17.04** | **85.59** |
| SqlSugar | 4.24 | 18.68 | 97.62 |
| FreeSql | 4.80 | 22.15 | 93.15 |
| EF Core | 19.79 | 152.76 | 650.50 |
| Dapper | 25.75 | 223.07 | 1,119.63 |

### Update (ms) — lower is better

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| **LiteOrm** | **4.94** | **28.45** | **118.09** |
| SqlSugar | 6.25 | 44.56 | 241.85 |
| FreeSql | 6.03 | 45.03 | 183.02 |
| EF Core | 18.01 | 129.63 | 559.45 |
| Dapper | 28.78 | 243.24 | 1,209.53 |

### Upsert (ms) — lower is better

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| LiteOrm | 7.56 | 24.46 | 114.92 |
| SqlSugar | 10.36 | 111.55 | 1,756.85 |
| **FreeSql** | **6.25** | **20.29** | **88.06** |
| EF Core | 19.00 | 140.63 | 562.72 |
| Dapper | 29.92 | 246.36 | 1,213.77 |

### Join Query (ms) — lower is better

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| LiteOrm | 1.61 | 9.47 | **41.76** |
| SqlSugar | 2.26 | 24.70 | 94.43 |
| **FreeSql** | **1.41** | 9.28 | 43.90 |
| EF Core | 4.97 | 14.72 | 53.85 |
| Dapper | 1.51 | **9.09** | 42.62 |

### Memory Allocation (100 rows, KB) — lower is better
| Framework | Insert | Update | Upsert | Join Query |
|:---|---:|---:|---:|---:|
| LiteOrm | 289.60 | **320.85** | 444.08 | 54.11 |
| SqlSugar | 478.94 | 803.01 | 1,461.07 | 997.68 |
| FreeSql | 463.58 | 695.92 | **269.20** | 115.56 |
| EF Core | 1,921.48 | 1,528.45 | 1,500.96 | 388.93 |
| **Dapper** | **254.23** | 321.15 | 292.07 | **50.50** |

### Memory Allocation (1000 rows, KB) — lower is better

| Framework | Insert | Update | Upsert | Join Query |
|:---|---:|---:|---:|---:|
| **LiteOrm** | **862.79** | **1,191.74** | **1,976.28** | **233.24** |
| SqlSugar | 4,573.21 | 7,679.06 | 35,952.93 | 9,227.87 |
| FreeSql | 4,633.32 | 6,881.13 | 2,250.28 | 856.67 |
| EF Core | 17,909.04 | 13,463.64 | 14,371.72 | 2,202.94 |
| Dapper | 2,475.93 | 3,093.50 | 2,799.34 | 418.43 |

### Memory Allocation (5000 rows, KB) — lower is better
| Framework | Insert | Update | Upsert | Join Query |
|:---|---:|---:|---:|---:|
| **LiteOrm** | **4,071.06** | **5,887.70** | **7,222.66** | **1,079.28** |
| SqlSugar | 23,197.40 | 38,814.78 | 844,266.17 | 45,756.84 |
| FreeSql | 23,337.15 | 34,451.57 | 11,128.46 | 4,201.26 |
| EF Core | 81,468.98 | 67,862.54 | 64,645.75 | 10,619.44 |
| Dapper | 12,350.50 | 15,466.74 | 13,984.48 | 2,103.82 |

---

## 📊 Full BenchmarkDotNet Report

```
BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04 LTS (Noble Numbat)
Intel Xeon Silver 4314 CPU 2.40GHz (Max: 2.39GHz), 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.103
  [Host]    : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v4
  MediumRun : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v4

Job=MediumRun  IterationCount=15  LaunchCount=2  
WarmupCount=10  

```
| Method                   | BatchCount | Mean         | Error      | StdDev     | Median       | Gen0       | Gen1      | Gen2     | Allocated    |
|------------------------- |----------- |-------------:|-----------:|-----------:|-------------:|-----------:|----------:|---------:|-------------:|
| **EFCore_Insert_Async**      | **100**        |    **19.785 ms** |  **1.5072 ms** |  **2.0630 ms** |    **19.642 ms** |   **142.8571** |   **71.4286** |        **-** |   **1921.48 KB** |
| SqlSugar_Insert_Async    | 100        |     4.243 ms |  0.0455 ms |  0.0667 ms |     4.253 ms |    39.0625 |    7.8125 |        - |    478.94 KB |
| LiteOrm_Insert_Async     | 100        |     3.895 ms |  0.1915 ms |  0.2557 ms |     3.826 ms |    23.4375 |         - |        - |     289.6 KB |
| Dapper_Insert_Async      | 100        |    25.751 ms |  2.3546 ms |  3.5242 ms |    25.211 ms |          - |         - |        - |    254.23 KB |
| FreeSql_Insert_Async     | 100        |     4.796 ms |  0.5838 ms |  0.8737 ms |     4.187 ms |    31.2500 |         - |        - |    463.58 KB |
| EFCore_Update_Async      | 100        |    18.005 ms |  0.5612 ms |  0.7867 ms |    18.090 ms |    62.5000 |         - |        - |   1528.45 KB |
| SqlSugar_Update_Async    | 100        |     6.254 ms |  0.0642 ms |  0.0940 ms |     6.280 ms |    62.5000 |   15.6250 |        - |    803.01 KB |
| LiteOrm_Update_Async     | 100        |     4.936 ms |  0.1141 ms |  0.1561 ms |     4.901 ms |    23.4375 |         - |        - |    320.85 KB |
| Dapper_Update_Async      | 100        |    28.782 ms |  2.3441 ms |  3.5086 ms |    28.152 ms |          - |         - |        - |    321.15 KB |
| FreeSql_Update_Async     | 100        |     6.028 ms |  0.6649 ms |  0.9321 ms |     5.593 ms |    46.8750 |         - |        - |    695.92 KB |
| EFCore_Upsert_Async      | 100        |    18.999 ms |  1.2947 ms |  1.7722 ms |    18.562 ms |    66.6667 |         - |        - |   1500.96 KB |
| SqlSugar_Upsert_Async    | 100        |    10.362 ms |  0.0621 ms |  0.0910 ms |    10.366 ms |   109.3750 |   31.2500 |        - |   1461.07 KB |
| LiteOrm_Upsert_Async     | 100        |     7.557 ms |  0.0648 ms |  0.0909 ms |     7.568 ms |    31.2500 |         - |        - |    444.08 KB |
| Dapper_Upsert_Async      | 100        |    29.917 ms |  2.8066 ms |  4.2008 ms |    29.713 ms |          - |         - |        - |    292.07 KB |
| FreeSql_Upsert_Async     | 100        |     6.252 ms |  1.0284 ms |  1.5392 ms |     5.221 ms |    15.6250 |         - |        - |     269.2 KB |
| EFCore_JoinQuery_Async   | 100        |     4.966 ms |  0.0619 ms |  0.0927 ms |     4.948 ms |    31.2500 |         - |        - |    388.93 KB |
| SqlSugar_JoinQuery_Async | 100        |     2.262 ms |  0.0105 ms |  0.0147 ms |     2.259 ms |    78.1250 |    7.8125 |        - |    997.68 KB |
| LiteOrm_JoinQuery_Async  | 100        |     1.607 ms |  0.1174 ms |  0.1757 ms |     1.620 ms |     3.9063 |         - |        - |     54.11 KB |
| Dapper_JoinQuery_Async   | 100        |     1.514 ms |  0.0219 ms |  0.0314 ms |     1.512 ms |     3.9063 |         - |        - |      50.5 KB |
| FreeSql_JoinQuery_Async  | 100        |     1.408 ms |  0.0056 ms |  0.0083 ms |     1.407 ms |     7.8125 |         - |        - |    115.56 KB |
| **EFCore_Insert_Async**      | **1000**       |   **152.755 ms** | **11.6878 ms** | **17.4937 ms** |   **153.562 ms** |  **1000.0000** |  **500.0000** |        **-** |  **17909.04 KB** |
| SqlSugar_Insert_Async    | 1000       |    18.675 ms |  0.7108 ms |  1.0419 ms |    18.638 ms |   375.0000 |  218.7500 |  31.2500 |   4573.21 KB |
| LiteOrm_Insert_Async     | 1000       |    17.035 ms |  0.7057 ms |  1.0563 ms |    16.946 ms |    62.5000 |         - |        - |    862.79 KB |
| Dapper_Insert_Async      | 1000       |   223.069 ms | 17.2081 ms | 25.7563 ms |   223.047 ms |          - |         - |        - |   2475.93 KB |
| FreeSql_Insert_Async     | 1000       |    22.147 ms |  3.0803 ms |  4.6105 ms |    22.315 ms |   343.7500 |  125.0000 |        - |   4633.32 KB |
| EFCore_Update_Async      | 1000       |   129.628 ms | 10.1418 ms | 14.8658 ms |   128.717 ms |  1000.0000 |  666.6667 |        - |  13463.64 KB |
| SqlSugar_Update_Async    | 1000       |    44.556 ms |  2.7469 ms |  4.1114 ms |    44.050 ms |   500.0000 |  250.0000 |        - |   7679.06 KB |
| LiteOrm_Update_Async     | 1000       |    28.454 ms |  1.1905 ms |  1.7819 ms |    28.342 ms |    66.6667 |         - |        - |   1191.74 KB |
| Dapper_Update_Async      | 1000       |   243.242 ms | 20.5655 ms | 30.7815 ms |   231.531 ms |          - |         - |        - |    3093.5 KB |
| FreeSql_Update_Async     | 1000       |    45.028 ms |  6.7595 ms | 10.1173 ms |    44.016 ms |   625.0000 |  500.0000 | 125.0000 |   6881.13 KB |
| EFCore_Upsert_Async      | 1000       |   140.630 ms | 14.1752 ms | 21.2168 ms |   136.360 ms |  1000.0000 |  666.6667 |        - |  14371.72 KB |
| SqlSugar_Upsert_Async    | 1000       |   111.554 ms |  6.7443 ms |  9.6725 ms |   109.374 ms |  2500.0000 |  500.0000 |        - |  35952.93 KB |
| LiteOrm_Upsert_Async     | 1000       |    24.455 ms |  1.8002 ms |  2.6944 ms |    24.858 ms |   142.8571 |   71.4286 |        - |   1976.28 KB |
| Dapper_Upsert_Async      | 1000       |   246.362 ms | 20.5249 ms | 30.7208 ms |   240.327 ms |          - |         - |        - |   2799.34 KB |
| FreeSql_Upsert_Async     | 1000       |    20.287 ms |  1.9494 ms |  2.7958 ms |    19.213 ms |   156.2500 |   31.2500 |        - |   2250.28 KB |
| EFCore_JoinQuery_Async   | 1000       |    14.715 ms |  1.4427 ms |  2.1594 ms |    14.645 ms |   153.8462 |   76.9231 |        - |   2202.94 KB |
| SqlSugar_JoinQuery_Async | 1000       |    24.697 ms |  1.9347 ms |  2.8957 ms |    25.312 ms |   727.2727 |  181.8182 |        - |   9227.87 KB |
| LiteOrm_JoinQuery_Async  | 1000       |     9.467 ms |  1.1383 ms |  1.6325 ms |     8.559 ms |    15.6250 |         - |        - |    233.24 KB |
| Dapper_JoinQuery_Async   | 1000       |     9.089 ms |  0.2799 ms |  0.4014 ms |     9.081 ms |    31.2500 |         - |        - |    418.43 KB |
| FreeSql_JoinQuery_Async  | 1000       |     9.282 ms |  0.3267 ms |  0.4789 ms |     9.021 ms |    62.5000 |   15.6250 |        - |    856.67 KB |
| **EFCore_Insert_Async**      | **5000**       |   **650.498 ms** | **40.1846 ms** | **60.1464 ms** |   **636.538 ms** |  **6000.0000** | **2000.0000** |        **-** |  **81468.98 KB** |
| SqlSugar_Insert_Async    | 5000       |    97.618 ms |  3.6523 ms |  5.3535 ms |    97.610 ms |  2000.0000 | 1200.0000 | 400.0000 |   23197.4 KB |
| LiteOrm_Insert_Async     | 5000       |    85.586 ms |  5.0063 ms |  7.4932 ms |    88.961 ms |   285.7143 |         - |        - |   4071.06 KB |
| Dapper_Insert_Async      | 5000       | 1,119.627 ms | 53.0895 ms | 79.4619 ms | 1,121.288 ms |  1000.0000 |         - |        - |   12350.5 KB |
| FreeSql_Insert_Async     | 5000       |    93.149 ms |  3.8715 ms |  5.7946 ms |    94.765 ms |  1833.3333 | 1000.0000 |        - |  23337.15 KB |
| EFCore_Update_Async      | 5000       |   559.450 ms | 27.6936 ms | 41.4506 ms |   540.853 ms |  5000.0000 | 1000.0000 |        - |  67862.54 KB |
| SqlSugar_Update_Async    | 5000       |   241.847 ms |  4.2724 ms |  6.3947 ms |   242.760 ms |  3000.0000 | 1500.0000 | 500.0000 |  38814.78 KB |
| LiteOrm_Update_Async     | 5000       |   118.087 ms |  4.0463 ms |  5.9311 ms |   118.722 ms |   250.0000 |         - |        - |    5887.7 KB |
| Dapper_Update_Async      | 5000       | 1,209.526 ms | 50.9021 ms | 76.1879 ms | 1,219.818 ms |  1000.0000 |         - |        - |  15466.74 KB |
| FreeSql_Update_Async     | 5000       |   183.016 ms | 11.4233 ms | 16.7442 ms |   184.895 ms |  2333.3333 |  333.3333 |        - |  34451.57 KB |
| EFCore_Upsert_Async      | 5000       |   562.722 ms | 27.4118 ms | 39.3132 ms |   550.635 ms |  5000.0000 | 1000.0000 |        - |  64645.75 KB |
| SqlSugar_Upsert_Async    | 5000       | 1,756.847 ms | 15.6916 ms | 23.4864 ms | 1,754.695 ms | 68000.0000 | 1000.0000 |        - | 844266.17 KB |
| LiteOrm_Upsert_Async     | 5000       |   114.921 ms |  3.6123 ms |  5.4068 ms |   116.837 ms |   400.0000 |  200.0000 |        - |   7222.66 KB |
| Dapper_Upsert_Async      | 5000       | 1,213.774 ms | 58.8733 ms | 88.1188 ms | 1,216.936 ms |  1000.0000 |         - |        - |  13984.48 KB |
| FreeSql_Upsert_Async     | 5000       |    88.063 ms |  4.1082 ms |  6.0217 ms |    89.437 ms |   600.0000 |  400.0000 |        - |  11128.46 KB |
| EFCore_JoinQuery_Async   | 5000       |    53.845 ms |  1.5238 ms |  2.2808 ms |    54.206 ms |   600.0000 |  200.0000 |        - |  10619.44 KB |
| SqlSugar_JoinQuery_Async | 5000       |    94.427 ms |  3.5534 ms |  5.3186 ms |    94.182 ms |  3500.0000 |  250.0000 |        - |  45756.84 KB |
| LiteOrm_JoinQuery_Async  | 5000       |    41.758 ms |  1.1681 ms |  1.7484 ms |    41.646 ms |    83.3333 |         - |        - |   1079.28 KB |
| Dapper_JoinQuery_Async   | 5000       |    42.623 ms |  0.3636 ms |  0.5443 ms |    42.639 ms |   166.6667 |   83.3333 |        - |   2103.82 KB |
| FreeSql_JoinQuery_Async  | 5000       |    43.902 ms |  1.3715 ms |  2.0103 ms |    42.546 ms |   272.7273 |  181.8182 |        - |   4201.26 KB |
