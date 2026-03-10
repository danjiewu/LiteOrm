# LiteOrm ORM Benchmark Results

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04 LTS (Noble Numbat)  
Intel Xeon Silver 4314 CPU 2.40GHz, .NET 10.0.3, X64 RyuJIT x86-64-v4  
Job=MediumRun  IterationCount=15  LaunchCount=2  WarmupCount=10

## ⚡ Performance Summary

### Insert (ms) — lower is better

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| **LiteOrm** | **4.16** | **12.21** | **54.27** |
| SqlSugar | 4.37 | 19.30 | 100.73 |
| FreeSql | 5.00 | 21.09 | 90.11 |
| EF Core | 20.18 | 150.28 | 663.25 |
| Dapper | 26.15 | 216.81 | 1,124.73 |

### Update (ms) — lower is better

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| **LiteOrm** | **5.55** | **20.20** | **94.34** |
| SqlSugar | 6.23 | 44.90 | 243.68 |
| FreeSql | 6.38 | 39.87 | 204.43 |
| EF Core | 18.92 | 133.85 | 574.62 |
| Dapper | 28.37 | 243.36 | 1,209.28 |

### Upsert (ms) — lower is better

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| **FreeSql** | **5.22** | **17.51** | 91.97 |
| LiteOrm | 6.02 | 19.06 | **80.58** |
| SqlSugar | 10.43 | 108.56 | 1,784.72 |
| EF Core | 21.04 | 137.46 | 571.41 |
| Dapper | 28.58 | 242.39 | 1,211.96 |

### Join Query (ms) — lower is better

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| **FreeSql** | **1.43** | 9.21 | **42.80** |
| LiteOrm | 1.45 | 9.67 | 43.11 |
| Dapper | 1.45 | **8.76** | 45.37 |
| SqlSugar | 2.32 | 24.25 | 95.99 |
| EF Core | 5.04 | 14.10 | 54.02 |

### Memory Allocation (100 rows, KB) — lower is better
| Framework | Insert | Update | Upsert | Join Query |
|:---|---:|---:|---:|---:|
| LiteOrm | 299.56 | 331.03 | 454.38 | 58.5 |
| SqlSugar | 478.37 | 803.29 | 1458.99 | 997.95 |
| FreeSql | 463.58 | 695.93 | 269.2 | 115.56 |
| **Dapper** | **254.76** | **321.22** | **292.06** | **50.49** |
| EF Core | 1796.65 | 1526.63 | 1600.25 | 389.59 |

### Memory Allocation (1000 rows, KB) — lower is better

| Framework | Insert | Update | Upsert | Join Query |
|:---|---:|---:|---:|---:|
| **LiteOrm** | **873.32** | **1,202.67** | **1,987.09** | **238.35** |
| Dapper | 2,476.27 | 3,093.32 | 2,799.01 | 418.43 |
| SqlSugar | 4,573.20 | 7,679.01 | 35,951.71 | 9,228.01 |
| FreeSql | 4,633.33 | 6,880.93 | 2,250.44 | 856.68 |
| EF Core | 16,708.90 | 13,450.93 | 13,629.90 | 2,203.24 |

### Memory Allocation (5000 rows, KB) — lower is better
| Framework | Insert | Update | Upsert | Join Query |
|:---|---:|---:|---:|---:|
| **LiteOrm** | **1,987.09** | **2,138.49** | **2,250.44** | **238.35** |
| Dapper | 2,799.01 | 3,093.32 | 2,799.01 | 418.43 |
| SqlSugar | 35,951.71 | 7,679.01 | 35,951.71 | 9,228.01 |
| FreeSql | 2,250.44 | 6,880.93 | 2,250.44 | 856.68 |
| EF Core | 13,629.90 | 13,450.93 | 13,629.90 | 2,203.24 |

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
| Method                        | BatchCount | Mean         | Error      | StdDev     | Median       | Gen0       | Gen1      | Gen2     | Allocated    |
|------------------------------ |----------- |-------------:|-----------:|-----------:|-------------:|-----------:|----------:|---------:|-------------:|
| **EFCore_Insert_Async**           | **100**        |    **20.176 ms** |  **1.6277 ms** |  **2.1729 ms** |    **19.592 ms** |   **133.3333** |   **66.6667** |        **-** |   **1796.65 KB** |
| SqlSugar_Insert_Async         | 100        |     4.369 ms |  0.0551 ms |  0.0754 ms |     4.357 ms |    39.0625 |    7.8125 |        - |    478.37 KB |
| LiteOrm_Insert_Async          | 100        |     4.162 ms |  0.0250 ms |  0.0350 ms |     4.154 ms |    23.4375 |         - |        - |    299.56 KB |
| Dapper_Insert_Async           | 100        |    26.152 ms |  2.1954 ms |  3.2860 ms |    25.872 ms |          - |         - |        - |    254.76 KB |
| FreeSql_Insert_Async          | 100        |     5.004 ms |  0.6188 ms |  0.9262 ms |     5.078 ms |    31.2500 |         - |        - |    463.58 KB |
| EFCore_Update_Async           | 100        |    18.915 ms |  1.5580 ms |  2.2344 ms |    18.409 ms |    93.7500 |         - |        - |   1526.63 KB |
| SqlSugar_Update_Async         | 100        |     6.229 ms |  0.0615 ms |  0.0902 ms |     6.239 ms |    62.5000 |   15.6250 |        - |    803.29 KB |
| LiteOrm_Update_Async          | 100        |     5.548 ms |  0.0295 ms |  0.0432 ms |     5.556 ms |    23.4375 |    7.8125 |        - |    331.03 KB |
| Dapper_Update_Async           | 100        |    28.367 ms |  3.1833 ms |  4.7647 ms |    27.048 ms |          - |         - |        - |    321.22 KB |
| FreeSql_Update_Async          | 100        |     6.379 ms |  0.6962 ms |  0.9530 ms |     5.758 ms |    46.8750 |         - |        - |    695.93 KB |
| EFCore_Upsert_Async   | 100        |    21.037 ms |  2.6424 ms |  3.9550 ms |    19.344 ms |    71.4286 |         - |        - |   1600.25 KB |
| SqlSugar_Upsert_Async | 100        |    10.433 ms |  0.0654 ms |  0.0917 ms |    10.423 ms |    93.7500 |   31.2500 |        - |   1458.99 KB |
| LiteOrm_Upsert_Async  | 100        |     6.022 ms |  0.0373 ms |  0.0558 ms |     6.031 ms |    31.2500 |         - |        - |    454.38 KB |
| Dapper_Upsert_Async   | 100        |    28.575 ms |  2.0510 ms |  3.0063 ms |    28.311 ms |          - |         - |        - |    292.06 KB |
| FreeSql_Upsert_Async  | 100        |     5.219 ms |  0.2258 ms |  0.3090 ms |     5.093 ms |    15.6250 |         - |        - |     269.2 KB |
| EFCore_JoinQuery_Async        | 100        |     5.037 ms |  0.0806 ms |  0.1130 ms |     5.009 ms |    31.2500 |         - |        - |    389.59 KB |
| SqlSugar_JoinQuery_Async      | 100        |     2.316 ms |  0.0109 ms |  0.0163 ms |     2.318 ms |    78.1250 |    7.8125 |        - |    997.95 KB |
| LiteOrm_JoinQuery_Async       | 100        |     1.448 ms |  0.0145 ms |  0.0208 ms |     1.442 ms |     3.9063 |         - |        - |      58.5 KB |
| Dapper_JoinQuery_Async        | 100        |     1.452 ms |  0.0090 ms |  0.0134 ms |     1.447 ms |     3.9063 |         - |        - |     50.49 KB |
| FreeSql_JoinQuery_Async       | 100        |     1.431 ms |  0.0148 ms |  0.0207 ms |     1.443 ms |     7.8125 |         - |        - |    115.56 KB |
| **EFCore_Insert_Async**           | **1000**       |   **150.275 ms** | **14.8614 ms** | **22.2439 ms** |   **145.434 ms** |  **1000.0000** |  **500.0000** |        **-** |   **16708.9 KB** |
| SqlSugar_Insert_Async         | 1000       |    19.295 ms |  1.8639 ms |  2.7899 ms |    17.972 ms |   375.0000 |  250.0000 |  31.2500 |    4573.2 KB |
| LiteOrm_Insert_Async          | 1000       |    12.213 ms |  0.2802 ms |  0.4018 ms |    12.133 ms |    62.5000 |         - |        - |    873.32 KB |
| Dapper_Insert_Async           | 1000       |   216.810 ms | 19.2958 ms | 28.8811 ms |   222.271 ms |          - |         - |        - |   2476.27 KB |
| FreeSql_Insert_Async          | 1000       |    21.088 ms |  2.0590 ms |  3.0818 ms |    21.221 ms |   363.6364 |   90.9091 |        - |   4633.33 KB |
| EFCore_Update_Async           | 1000       |   133.852 ms | 12.2484 ms | 17.9536 ms |   133.571 ms |  1000.0000 |  666.6667 |        - |  13450.93 KB |
| SqlSugar_Update_Async         | 1000       |    44.896 ms |  2.7515 ms |  4.1183 ms |    46.042 ms |   500.0000 |  250.0000 |        - |   7679.01 KB |
| LiteOrm_Update_Async          | 1000       |    20.200 ms |  0.1812 ms |  0.2656 ms |    20.175 ms |    93.7500 |   31.2500 |        - |   1202.67 KB |
| Dapper_Update_Async           | 1000       |   243.364 ms | 22.3204 ms | 33.4081 ms |   222.957 ms |          - |         - |        - |   3093.32 KB |
| FreeSql_Update_Async          | 1000       |    39.871 ms |  4.0868 ms |  6.1170 ms |    35.018 ms |   625.0000 |  500.0000 | 125.0000 |   6880.93 KB |
| EFCore_Upsert_Async   | 1000       |   137.457 ms | 12.8456 ms | 19.2268 ms |   133.534 ms |  1000.0000 |  666.6667 |        - |   13629.9 KB |
| SqlSugar_Upsert_Async | 1000       |   108.559 ms |  3.5849 ms |  5.1414 ms |   106.485 ms |  2500.0000 |  500.0000 |        - |  35951.71 KB |
| LiteOrm_Upsert_Async  | 1000       |    19.061 ms |  0.2498 ms |  0.3419 ms |    19.069 ms |   142.8571 |   71.4286 |        - |   1987.09 KB |
| Dapper_Upsert_Async   | 1000       |   242.386 ms | 24.0863 ms | 36.0513 ms |   218.962 ms |          - |         - |        - |   2799.01 KB |
| FreeSql_Upsert_Async  | 1000       |    17.509 ms |  0.2709 ms |  0.3616 ms |    17.501 ms |   156.2500 |   31.2500 |        - |   2250.44 KB |
| EFCore_JoinQuery_Async        | 1000       |    14.097 ms |  1.2128 ms |  1.8153 ms |    13.554 ms |   153.8462 |   76.9231 |        - |   2203.24 KB |
| SqlSugar_JoinQuery_Async      | 1000       |    24.252 ms |  4.2719 ms |  6.3940 ms |    21.646 ms |   750.0000 |  250.0000 |        - |   9228.01 KB |
| LiteOrm_JoinQuery_Async       | 1000       |     9.672 ms |  0.4283 ms |  0.6278 ms |     9.997 ms |    15.6250 |         - |        - |    238.35 KB |
| Dapper_JoinQuery_Async        | 1000       |     8.763 ms |  0.0405 ms |  0.0606 ms |     8.770 ms |    31.2500 |         - |        - |    418.43 KB |
| FreeSql_JoinQuery_Async       | 1000       |     9.210 ms |  0.2638 ms |  0.3698 ms |     8.965 ms |    62.5000 |   15.6250 |        - |    856.68 KB |
| **EFCore_Insert_Async**           | **5000**       |   **663.247 ms** | **43.4447 ms** | **65.0260 ms** |   **648.313 ms** |  **6000.0000** | **2000.0000** |        **-** |   **85234.6 KB** |
| SqlSugar_Insert_Async         | 5000       |   100.732 ms |  3.3370 ms |  4.8913 ms |    99.970 ms |  2000.0000 | 1200.0000 | 400.0000 |  23197.36 KB |
| LiteOrm_Insert_Async          | 5000       |    54.270 ms |  3.7737 ms |  5.4122 ms |    51.536 ms |   250.0000 |  125.0000 |        - |   4083.02 KB |
| Dapper_Insert_Async           | 5000       | 1,124.733 ms | 56.1768 ms | 84.0828 ms | 1,120.083 ms |  1000.0000 |         - |        - |  12350.25 KB |
| FreeSql_Insert_Async          | 5000       |    90.108 ms |  4.3087 ms |  6.3156 ms |    90.861 ms |  1800.0000 | 1000.0000 |        - |     23337 KB |
| EFCore_Update_Async           | 5000       |   574.623 ms | 38.8841 ms | 58.1998 ms |   566.892 ms |  5000.0000 | 1000.0000 |        - |  66371.04 KB |
| SqlSugar_Update_Async         | 5000       |   243.679 ms |  4.6392 ms |  6.9438 ms |   243.739 ms |  3000.0000 | 1500.0000 | 500.0000 |  38814.91 KB |
| LiteOrm_Update_Async          | 5000       |    94.338 ms |  3.9928 ms |  5.8525 ms |    94.395 ms |   333.3333 |  166.6667 |        - |   5903.57 KB |
| Dapper_Update_Async           | 5000       | 1,209.282 ms | 49.8341 ms | 74.5893 ms | 1,244.671 ms |  1000.0000 |         - |        - |  15465.05 KB |
| FreeSql_Update_Async          | 5000       |   204.432 ms | 13.1867 ms | 18.4859 ms |   204.386 ms |  2333.3333 |  333.3333 |        - |  34449.79 KB |
| EFCore_Upsert_Async   | 5000       |   571.406 ms | 30.5137 ms | 45.6715 ms |   566.120 ms |  5000.0000 | 1000.0000 |        - |  64301.73 KB |
| SqlSugar_Upsert_Async | 5000       | 1,784.717 ms | 12.7286 ms | 19.0515 ms | 1,781.724 ms | 68000.0000 | 1000.0000 |        - | 844273.31 KB |
| LiteOrm_Upsert_Async  | 5000       |    80.578 ms |  2.9372 ms |  4.3962 ms |    79.301 ms |   500.0000 |  333.3333 |        - |   7236.94 KB |
| Dapper_Upsert_Async   | 5000       | 1,211.955 ms | 54.1446 ms | 81.0411 ms | 1,214.047 ms |  1000.0000 |         - |        - |  13985.76 KB |
| FreeSql_Upsert_Async  | 5000       |    91.973 ms |  2.8011 ms |  4.1058 ms |    90.639 ms |   666.6667 |  500.0000 |        - |  11127.95 KB |
| EFCore_JoinQuery_Async        | 5000       |    54.015 ms |  2.6401 ms |  3.8699 ms |    53.190 ms |   600.0000 |  200.0000 |        - |  10614.03 KB |
| SqlSugar_JoinQuery_Async      | 5000       |    95.987 ms |  2.4289 ms |  3.5603 ms |    96.720 ms |  3500.0000 |  250.0000 |        - |  45757.22 KB |
| LiteOrm_JoinQuery_Async       | 5000       |    43.106 ms |  1.2814 ms |  1.8782 ms |    42.830 ms |    76.9231 |         - |        - |   1083.98 KB |
| Dapper_JoinQuery_Async        | 5000       |    45.374 ms |  1.6649 ms |  2.4403 ms |    45.267 ms |   166.6667 |   83.3333 |        - |   2103.72 KB |
| FreeSql_JoinQuery_Async       | 5000       |    42.803 ms |  0.4919 ms |  0.7210 ms |    42.550 ms |   333.3333 |  250.0000 |        - |   4200.98 KB |
