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
.NET SDK 10.0.104
  [Host]    : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v4
  MediumRun : .NET 10.0.4 (10.0.4, 10.0.426.12010), X64 RyuJIT x86-64-v4

Job=MediumRun  IterationCount=15  LaunchCount=2  
WarmupCount=10  

```
| Method                   | BatchCount | Mean         | Error      | StdDev     | Median       | Gen0       | Gen1      | Gen2     | Allocated    |
|------------------------- |----------- |-------------:|-----------:|-----------:|-------------:|-----------:|----------:|---------:|-------------:|
| **EFCore_Insert_Async**      | **100**        |    **20.355 ms** |  **2.4642 ms** |  **3.5340 ms** |    **18.947 ms** |   **142.8571** |   **71.4286** |        **-** |   **1901.63 KB** |
| SqlSugar_Insert_Async    | 100        |     4.332 ms |  0.0861 ms |  0.1179 ms |     4.294 ms |    39.0625 |    7.8125 |        - |    478.84 KB |
| LiteOrm_Insert_Async     | 100        |     3.838 ms |  0.1317 ms |  0.1889 ms |     3.783 ms |    23.4375 |         - |        - |    290.14 KB |
| Dapper_Insert_Async      | 100        |    26.717 ms |  1.7880 ms |  2.6762 ms |    26.842 ms |          - |         - |        - |    254.05 KB |
| FreeSql_Insert_Async     | 100        |     4.691 ms |  0.5241 ms |  0.7347 ms |     4.148 ms |    31.2500 |         - |        - |    463.58 KB |
| EFCore_Update_Async      | 100        |    17.959 ms |  0.7625 ms |  1.1413 ms |    17.664 ms |   125.0000 |         - |        - |   1552.35 KB |
| SqlSugar_Update_Async    | 100        |     6.266 ms |  0.0625 ms |  0.0935 ms |     6.289 ms |    62.5000 |   15.6250 |        - |    803.48 KB |
| LiteOrm_Update_Async     | 100        |     4.905 ms |  0.0806 ms |  0.1182 ms |     4.924 ms |    15.6250 |         - |        - |    319.86 KB |
| Dapper_Update_Async      | 100        |    28.594 ms |  3.4079 ms |  5.1008 ms |    27.045 ms |          - |         - |        - |     320.9 KB |
| FreeSql_Update_Async     | 100        |     5.751 ms |  0.3457 ms |  0.5067 ms |     5.532 ms |    46.8750 |         - |        - |    695.93 KB |
| EFCore_Upsert_Async      | 100        |    18.974 ms |  1.0163 ms |  1.4247 ms |    18.756 ms |   125.0000 |         - |        - |    1623.8 KB |
| SqlSugar_Upsert_Async    | 100        |    10.415 ms |  0.0678 ms |  0.0973 ms |    10.432 ms |   109.3750 |   31.2500 |        - |   1458.95 KB |
| LiteOrm_Upsert_Async     | 100        |     7.631 ms |  0.1480 ms |  0.2026 ms |     7.610 ms |    31.2500 |         - |        - |    443.06 KB |
| Dapper_Upsert_Async      | 100        |    28.449 ms |  2.2773 ms |  3.3380 ms |    28.039 ms |          - |         - |        - |    291.38 KB |
| FreeSql_Upsert_Async     | 100        |     5.281 ms |  0.2780 ms |  0.3986 ms |     5.160 ms |    15.6250 |         - |        - |    269.19 KB |
| EFCore_JoinQuery_Async   | 100        |     4.976 ms |  0.0587 ms |  0.0878 ms |     4.966 ms |    31.2500 |         - |        - |    388.91 KB |
| SqlSugar_JoinQuery_Async | 100        |     2.274 ms |  0.0095 ms |  0.0139 ms |     2.277 ms |    78.1250 |    7.8125 |        - |    997.72 KB |
| LiteOrm_JoinQuery_Async  | 100        |     1.541 ms |  0.1011 ms |  0.1482 ms |     1.508 ms |     3.9063 |         - |        - |     52.62 KB |
| Dapper_JoinQuery_Async   | 100        |     1.518 ms |  0.0080 ms |  0.0115 ms |     1.517 ms |     3.9063 |         - |        - |     50.51 KB |
| FreeSql_JoinQuery_Async  | 100        |     1.392 ms |  0.0123 ms |  0.0177 ms |     1.390 ms |     7.8125 |         - |        - |    115.56 KB |
| **EFCore_Insert_Async**      | **1000**       |   **155.922 ms** | **12.7210 ms** | **19.0402 ms** |   **157.251 ms** |  **1000.0000** |  **500.0000** |        **-** |  **17480.02 KB** |
| SqlSugar_Insert_Async    | 1000       |    18.586 ms |  0.6299 ms |  0.9034 ms |    18.619 ms |   375.0000 |  250.0000 |  31.2500 |   4573.35 KB |
| LiteOrm_Insert_Async     | 1000       |    16.365 ms |  0.9915 ms |  1.4841 ms |    16.231 ms |    62.5000 |         - |        - |    862.79 KB |
| Dapper_Insert_Async      | 1000       |   221.845 ms | 15.3884 ms | 23.0326 ms |   228.479 ms |          - |         - |        - |   2475.72 KB |
| FreeSql_Insert_Async     | 1000       |    22.892 ms |  3.1593 ms |  4.7287 ms |    22.123 ms |   343.7500 |  125.0000 |        - |   4633.35 KB |
| EFCore_Update_Async      | 1000       |   135.034 ms | 12.0360 ms | 18.0149 ms |   139.554 ms |  1000.0000 |  666.6667 |        - |  13480.38 KB |
| SqlSugar_Update_Async    | 1000       |    46.776 ms |  2.4895 ms |  3.6491 ms |    49.254 ms |   571.4286 |  285.7143 |        - |   7679.01 KB |
| LiteOrm_Update_Async     | 1000       |    27.767 ms |  1.3641 ms |  2.0417 ms |    27.566 ms |    93.7500 |   31.2500 |        - |   1190.46 KB |
| Dapper_Update_Async      | 1000       |   250.966 ms | 23.5164 ms | 35.1983 ms |   243.388 ms |          - |         - |        - |   3093.55 KB |
| FreeSql_Update_Async     | 1000       |    42.132 ms |  3.2329 ms |  4.8388 ms |    43.359 ms |   625.0000 |  500.0000 | 125.0000 |   6880.53 KB |
| EFCore_Upsert_Async      | 1000       |   136.624 ms | 10.3397 ms | 15.1558 ms |   138.764 ms |  1000.0000 |  666.6667 |        - |   13090.7 KB |
| SqlSugar_Upsert_Async    | 1000       |   108.496 ms |  4.0156 ms |  5.7590 ms |   107.216 ms |  2666.6667 |  666.6667 |        - |  35949.35 KB |
| LiteOrm_Upsert_Async     | 1000       |    23.660 ms |  1.7826 ms |  2.6681 ms |    23.574 ms |    90.9091 |         - |        - |   1975.14 KB |
| Dapper_Upsert_Async      | 1000       |   244.508 ms | 20.1471 ms | 30.1552 ms |   240.528 ms |          - |         - |        - |   2797.83 KB |
| FreeSql_Upsert_Async     | 1000       |    21.545 ms |  1.8815 ms |  2.7578 ms |    22.700 ms |   156.2500 |   62.5000 |        - |   2250.27 KB |
| EFCore_JoinQuery_Async   | 1000       |    13.937 ms |  1.3309 ms |  1.9920 ms |    12.653 ms |   142.8571 |   71.4286 |        - |   2202.85 KB |
| SqlSugar_JoinQuery_Async | 1000       |    25.817 ms |  1.6161 ms |  2.4189 ms |    25.357 ms |   727.2727 |  181.8182 |        - |    9227.8 KB |
| LiteOrm_JoinQuery_Async  | 1000       |     9.412 ms |  0.4480 ms |  0.6706 ms |     9.302 ms |    15.6250 |         - |        - |    232.07 KB |
| Dapper_JoinQuery_Async   | 1000       |     8.923 ms |  0.0611 ms |  0.0896 ms |     8.884 ms |    31.2500 |         - |        - |    418.42 KB |
| FreeSql_JoinQuery_Async  | 1000       |     9.161 ms |  0.1613 ms |  0.2314 ms |     9.028 ms |    62.5000 |   15.6250 |        - |     856.7 KB |
| **EFCore_Insert_Async**      | **5000**       |   **662.380 ms** | **35.9051 ms** | **53.7410 ms** |   **674.913 ms** |  **6000.0000** | **2000.0000** |        **-** |  **83651.84 KB** |
| SqlSugar_Insert_Async    | 5000       |    98.826 ms |  3.7228 ms |  5.5721 ms |    99.357 ms |  2000.0000 | 1200.0000 | 400.0000 |  23197.37 KB |
| LiteOrm_Insert_Async     | 5000       |    81.071 ms |  6.0767 ms |  8.7150 ms |    81.100 ms |   250.0000 |         - |        - |   4068.76 KB |
| Dapper_Insert_Async      | 5000       | 1,120.032 ms | 48.9258 ms | 73.2299 ms | 1,131.958 ms |  1000.0000 |         - |        - |   12350.5 KB |
| FreeSql_Insert_Async     | 5000       |    88.503 ms |  4.5467 ms |  6.8053 ms |    88.559 ms |  1833.3333 | 1000.0000 |        - |     23337 KB |
| EFCore_Update_Async      | 5000       |   598.255 ms | 39.6842 ms | 59.3974 ms |   610.915 ms |  5000.0000 | 1000.0000 |        - |  66190.59 KB |
| SqlSugar_Update_Async    | 5000       |   243.376 ms |  3.4880 ms |  5.0024 ms |   243.873 ms |  3000.0000 | 1500.0000 | 500.0000 |  38814.96 KB |
| LiteOrm_Update_Async     | 5000       |   121.571 ms |  4.4724 ms |  6.6941 ms |   123.441 ms |   400.0000 |  200.0000 |        - |   5911.89 KB |
| Dapper_Update_Async      | 5000       | 1,214.468 ms | 60.8932 ms | 89.2564 ms | 1,225.272 ms |  1000.0000 |         - |        - |  15470.66 KB |
| FreeSql_Update_Async     | 5000       |   210.552 ms | 10.6049 ms | 14.8665 ms |   215.282 ms |  2333.3333 |  333.3333 |        - |  34449.19 KB |
| EFCore_Upsert_Async      | 5000       |   576.366 ms | 33.8061 ms | 50.5994 ms |   575.590 ms |  5000.0000 | 1000.0000 |        - |  68193.49 KB |
| SqlSugar_Upsert_Async    | 5000       | 1,743.241 ms | 10.7241 ms | 15.3802 ms | 1,743.387 ms | 68000.0000 | 1000.0000 |        - | 844267.11 KB |
| LiteOrm_Upsert_Async     | 5000       |   104.261 ms |  5.6859 ms |  7.9708 ms |   100.750 ms |   400.0000 |  200.0000 |        - |   7224.49 KB |
| Dapper_Upsert_Async      | 5000       | 1,190.843 ms | 60.5552 ms | 90.6361 ms | 1,188.027 ms |  1000.0000 |         - |        - |   13986.3 KB |
| FreeSql_Upsert_Async     | 5000       |    89.560 ms |  2.4944 ms |  3.7335 ms |    90.659 ms |   666.6667 |  500.0000 |        - |  11127.98 KB |
| EFCore_JoinQuery_Async   | 5000       |    54.141 ms |  2.1875 ms |  3.2741 ms |    55.632 ms |   875.0000 |  625.0000 | 125.0000 |  10613.52 KB |
| SqlSugar_JoinQuery_Async | 5000       |    89.343 ms |  1.9970 ms |  2.8640 ms |    89.483 ms |  3500.0000 |  250.0000 |        - |  45756.75 KB |
| LiteOrm_JoinQuery_Async  | 5000       |    41.672 ms |  1.1265 ms |  1.6512 ms |    42.186 ms |    83.3333 |         - |        - |   1078.22 KB |
| Dapper_JoinQuery_Async   | 5000       |    45.035 ms |  1.0079 ms |  1.4456 ms |    44.358 ms |   166.6667 |   83.3333 |        - |   2103.71 KB |
| FreeSql_JoinQuery_Async  | 5000       |    42.994 ms |  0.6188 ms |  0.9071 ms |    43.062 ms |   307.6923 |  230.7692 |        - |   4201.37 KB |
