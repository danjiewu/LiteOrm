# LiteOrm ORM Benchmark Results

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04 LTS (Noble Numbat)  
Intel Xeon Silver 4314 CPU 2.40GHz, .NET 10.0.4, X64 RyuJIT x86-64-v4  
Job=MediumRun  IterationCount=15  LaunchCount=2  WarmupCount=10

## ⚡ Performance Summary

### Insert (ms) — lower is better

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| **LiteOrm** | **3.98** | **16.39** | **75.62** |
| SqlSugar | 4.33 | 19.12 | 98.15 |
| FreeSql | 4.36 | 18.48 | 85.00 |
| EF Core | 18.50 | 150.35 | 670.19 |
| Dapper | 26.19 | 215.12 | 1,129.57 |

### Update (ms) — lower is better

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| **LiteOrm** | **4.84** | **25.36** | **118.70** |
| SqlSugar | 6.39 | 42.62 | 232.66 |
| FreeSql | 5.88 | 40.31 | 175.58 |
| EF Core | 17.26 | 126.44 | 575.32 |
| Dapper | 28.63 | 248.71 | 1,213.51 |

### Upsert (ms) — lower is better

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| LiteOrm | 7.54 | 23.72 | 103.52 |
| SqlSugar | 10.36 | 106.11 | 1,741.49 |
| **FreeSql** | **5.53** | **19.11** | **103.06** |
| EF Core | 19.05 | 135.88 | 589.07 |
| Dapper | 29.09 | 247.51 | 1,248.91 |

### Join Query (ms) — lower is better

| Framework | 100 rows | 1000 rows | 5000 rows |
|:---|---:|---:|---:|
| **LiteOrm** | **1.36** | 9.35 | 43.94 |
| SqlSugar | 2.29 | 22.10 | 89.97 |
| FreeSql | 1.75 | 9.10 | **43.89** |
| EF Core | 4.93 | 15.62 | 55.16 |
| Dapper | 1.48 | **9.07** | 45.64 |

### Memory Allocation (100 rows, KB) — lower is better
| Framework | Insert | Update | Upsert | Join Query |
|:---|---:|---:|---:|---:|
| LiteOrm | 290.08 | **318.52** | 441.16 | 50.86 |
| SqlSugar | 479.02 | 803.66 | 1,460.08 | 997.83 |
| FreeSql | 469.24 | 705.93 | **275.95** | 120.83 |
| EF Core | 1,244.27 | 1,181.75 | 1,084.38 | 384.54 |
| **Dapper** | **254.84** | 321.28 | 292.02 | **50.50** |

### Memory Allocation (1000 rows, KB) — lower is better

| Framework | Insert | Update | Upsert | Join Query |
|:---|---:|---:|---:|---:|
| **LiteOrm** | **862.82** | **1,189.03** | **1,973.38** | **230.38** |
| SqlSugar | 4,573.59 | 7,679.63 | 35,952.88 | 9,228.26 |
| FreeSql | 4,667.20 | 6,917.50 | 2,256.36 | 866.52 |
| EF Core | 12,503.04 | 9,044.24 | 9,005.39 | 2,198.05 |
| Dapper | 2,476.36 | 3,093.19 | 2,798.36 | 418.43 |

### Memory Allocation (5000 rows, KB) — lower is better
| Framework | Insert | Update | Upsert | Join Query |
|:---|---:|---:|---:|---:|
| **LiteOrm** | **4,069.66** | **5,885.12** | **7,224.39** | **1,075.65** |
| SqlSugar | 23,197.09 | 38,817.56 | 844,266.38 | 45,874.42 |
| FreeSql | 23,495.33 | 34,611.63 | 11,133.98 | 4,231.70 |
| EF Core | 58,881.58 | 43,942.40 | 43,927.71 | 10,608.86 |
| Dapper | 12,350.21 | 15,473.38 | 13,987.52 | 2,103.56 |

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
| Method                            | BatchCount | Mean         | Error      | StdDev     | Median       | Gen0       | Gen1      | Gen2      | Allocated    |
|---------------------------------- |----------- |-------------:|-----------:|-----------:|-------------:|-----------:|----------:|----------:|-------------:|
| **EFCore_Insert_Async**               | **100**        |    **18.499 ms** |  **0.6309 ms** |  **0.9248 ms** |    **18.338 ms** |    **93.7500** |   **31.2500** |         **-** |   **1244.27 KB** |
| SqlSugar_Insert_Async             | 100        |     4.329 ms |  0.0329 ms |  0.0451 ms |     4.333 ms |    39.0625 |    7.8125 |         - |    479.02 KB |
| SqlSugar_Fastest_Insert_Async     | 100        |     5.540 ms |  1.1019 ms |  1.6493 ms |     4.901 ms |     7.8125 |         - |         - |    140.48 KB |
| LiteOrm_Insert_Async              | 100        |     3.975 ms |  0.2098 ms |  0.3140 ms |     3.830 ms |    23.4375 |         - |         - |    290.08 KB |
| Dapper_Insert_Async               | 100        |    26.191 ms |  1.9506 ms |  2.9196 ms |    26.256 ms |          - |         - |         - |    254.84 KB |
| FreeSql_Insert_Async              | 100        |     4.355 ms |  0.0485 ms |  0.0679 ms |     4.352 ms |    31.2500 |         - |         - |    469.24 KB |
| EFCore_Update_Async               | 100        |    17.262 ms |  0.7764 ms |  1.1134 ms |    17.196 ms |    62.5000 |         - |         - |   1181.75 KB |
| EFCore_NoTracking_Update_Async    | 100        |    18.165 ms |  0.6295 ms |  0.9422 ms |    18.147 ms |    93.7500 |         - |         - |   1219.25 KB |
| SqlSugar_Update_Async             | 100        |     6.389 ms |  0.1379 ms |  0.1933 ms |     6.340 ms |    62.5000 |   15.6250 |         - |    803.66 KB |
| SqlSugar_Fastest_Update_Async     | 100        |     6.588 ms |  0.0333 ms |  0.0488 ms |     6.586 ms |    15.6250 |         - |         - |    277.88 KB |
| LiteOrm_Update_Async              | 100        |     4.841 ms |  0.0505 ms |  0.0657 ms |     4.845 ms |    23.4375 |    7.8125 |         - |    318.52 KB |
| Dapper_Update_Async               | 100        |    28.628 ms |  2.8953 ms |  4.3336 ms |    28.071 ms |          - |         - |         - |    321.28 KB |
| FreeSql_Update_Async              | 100        |     5.883 ms |  0.0435 ms |  0.0609 ms |     5.876 ms |    46.8750 |   15.6250 |         - |    705.93 KB |
| EFCore_Upsert_Async               | 100        |    19.047 ms |  1.2974 ms |  1.8608 ms |    18.543 ms |    62.5000 |         - |         - |   1084.38 KB |
| SqlSugar_Upsert_Async             | 100        |    10.355 ms |  0.0618 ms |  0.0906 ms |    10.332 ms |   109.3750 |   31.2500 |         - |   1460.08 KB |
| SqlSugar_Fastest_Upsert_Async     | 100        |    11.461 ms |  0.1036 ms |  0.1519 ms |    11.480 ms |    93.7500 |         - |         - |   1282.79 KB |
| LiteOrm_Upsert_Async              | 100        |     7.537 ms |  0.0500 ms |  0.0717 ms |     7.527 ms |    31.2500 |         - |         - |    441.16 KB |
| Dapper_Upsert_Async               | 100        |    29.094 ms |  2.6581 ms |  3.9785 ms |    29.391 ms |          - |         - |         - |    292.02 KB |
| FreeSql_Upsert_Async              | 100        |     5.526 ms |  0.0581 ms |  0.0834 ms |     5.532 ms |    15.6250 |         - |         - |    275.95 KB |
| EFCore_JoinQuery_Async            | 100        |     4.932 ms |  0.0615 ms |  0.0921 ms |     4.900 ms |    31.2500 |         - |         - |    384.54 KB |
| EFCore_NoTracking_JoinQuery_Async | 100        |     4.571 ms |  0.0165 ms |  0.0242 ms |     4.570 ms |    15.6250 |         - |         - |    272.41 KB |
| SqlSugar_JoinQuery_Async          | 100        |     2.286 ms |  0.0286 ms |  0.0429 ms |     2.277 ms |    78.1250 |    7.8125 |         - |    997.83 KB |
| LiteOrm_JoinQuery_Async           | 100        |     1.360 ms |  0.0122 ms |  0.0170 ms |     1.359 ms |     3.9063 |    1.9531 |         - |     50.86 KB |
| Dapper_JoinQuery_Async            | 100        |     1.482 ms |  0.0183 ms |  0.0269 ms |     1.484 ms |     3.9063 |         - |         - |      50.5 KB |
| FreeSql_JoinQuery_Async           | 100        |     1.754 ms |  0.0122 ms |  0.0183 ms |     1.749 ms |     7.8125 |         - |         - |    120.83 KB |
| **EFCore_Insert_Async**               | **1000**       |   **150.353 ms** | **15.1954 ms** | **22.7438 ms** |   **149.102 ms** |  **1000.0000** |  **500.0000** |         **-** |  **12503.04 KB** |
| SqlSugar_Insert_Async             | 1000       |    19.118 ms |  1.8782 ms |  2.7530 ms |    17.435 ms |   375.0000 |  218.7500 |   31.2500 |   4573.59 KB |
| SqlSugar_Fastest_Insert_Async     | 1000       |    10.846 ms |  0.1239 ms |  0.1816 ms |    10.797 ms |    62.5000 |   15.6250 |         - |   1033.79 KB |
| LiteOrm_Insert_Async              | 1000       |    16.386 ms |  0.7763 ms |  1.1133 ms |    16.617 ms |    62.5000 |         - |         - |    862.82 KB |
| Dapper_Insert_Async               | 1000       |   215.121 ms | 17.8466 ms | 26.7119 ms |   214.070 ms |          - |         - |         - |   2476.36 KB |
| FreeSql_Insert_Async              | 1000       |    18.481 ms |  0.8590 ms |  1.2042 ms |    17.975 ms |   343.7500 |  125.0000 |         - |    4667.2 KB |
| EFCore_Update_Async               | 1000       |   126.444 ms | 11.1610 ms | 16.7053 ms |   120.978 ms |   666.6667 |  333.3333 |         - |   9044.24 KB |
| EFCore_NoTracking_Update_Async    | 1000       |   132.126 ms | 10.3744 ms | 15.2066 ms |   131.644 ms |   666.6667 |  333.3333 |         - |  10531.74 KB |
| SqlSugar_Update_Async             | 1000       |    42.620 ms |  2.1582 ms |  3.1635 ms |    41.237 ms |   500.0000 |  250.0000 |         - |   7679.63 KB |
| SqlSugar_Fastest_Update_Async     | 1000       |    27.922 ms |  2.0713 ms |  3.1002 ms |    29.366 ms |    83.3333 |         - |         - |   1797.23 KB |
| LiteOrm_Update_Async              | 1000       |    25.357 ms |  1.4406 ms |  2.1563 ms |    26.088 ms |    93.7500 |   31.2500 |         - |   1189.03 KB |
| Dapper_Update_Async               | 1000       |   248.707 ms | 21.9446 ms | 32.8456 ms |   239.409 ms |          - |         - |         - |   3093.19 KB |
| FreeSql_Update_Async              | 1000       |    40.309 ms |  3.4708 ms |  5.1949 ms |    37.083 ms |   625.0000 |  500.0000 |  125.0000 |    6917.5 KB |
| EFCore_Upsert_Async               | 1000       |   135.878 ms | 11.7301 ms | 17.5571 ms |   135.760 ms |   666.6667 |  333.3333 |         - |   9005.39 KB |
| SqlSugar_Upsert_Async             | 1000       |   106.107 ms |  3.5658 ms |  5.2268 ms |   103.542 ms |  2500.0000 |  500.0000 |         - |  35952.88 KB |
| SqlSugar_Fastest_Upsert_Async     | 1000       |   102.201 ms |  4.7864 ms |  7.1640 ms |   100.054 ms |  2666.6667 |  666.6667 |         - |  33214.54 KB |
| LiteOrm_Upsert_Async              | 1000       |    23.718 ms |  1.5011 ms |  2.1529 ms |    24.184 ms |   156.2500 |   62.5000 |         - |   1973.38 KB |
| Dapper_Upsert_Async               | 1000       |   247.505 ms | 20.3557 ms | 30.4674 ms |   249.554 ms |          - |         - |         - |   2798.36 KB |
| FreeSql_Upsert_Async              | 1000       |    19.106 ms |  1.0630 ms |  1.5581 ms |    18.445 ms |   156.2500 |   31.2500 |         - |   2256.36 KB |
| EFCore_JoinQuery_Async            | 1000       |    15.621 ms |  1.7218 ms |  2.5771 ms |    15.692 ms |   153.8462 |   76.9231 |         - |   2198.05 KB |
| EFCore_NoTracking_JoinQuery_Async | 1000       |    12.498 ms |  0.2302 ms |  0.3228 ms |    12.459 ms |    62.5000 |         - |         - |   1068.47 KB |
| SqlSugar_JoinQuery_Async          | 1000       |    22.099 ms |  3.6640 ms |  5.4841 ms |    19.569 ms |   750.0000 |  218.7500 |         - |   9228.26 KB |
| LiteOrm_JoinQuery_Async           | 1000       |     9.347 ms |  0.5311 ms |  0.7445 ms |     9.233 ms |    15.6250 |         - |         - |    230.38 KB |
| Dapper_JoinQuery_Async            | 1000       |     9.068 ms |  0.0932 ms |  0.1367 ms |     9.097 ms |    31.2500 |         - |         - |    418.43 KB |
| FreeSql_JoinQuery_Async           | 1000       |     9.095 ms |  0.1189 ms |  0.1742 ms |     9.190 ms |    62.5000 |   15.6250 |         - |    866.52 KB |
| **EFCore_Insert_Async**               | **5000**       |   **670.185 ms** | **43.0209 ms** | **64.3916 ms** |   **655.273 ms** |  **5000.0000** | **2000.0000** | **1000.0000** |  **58881.58 KB** |
| SqlSugar_Insert_Async             | 5000       |    98.150 ms |  3.4907 ms |  5.2247 ms |    97.787 ms |  1750.0000 | 1000.0000 |  250.0000 |  23197.09 KB |
| SqlSugar_Fastest_Insert_Async     | 5000       |    48.449 ms |  1.5908 ms |  2.3811 ms |    48.752 ms |   333.3333 |  111.1111 |         - |   5223.09 KB |
| LiteOrm_Insert_Async              | 5000       |    75.623 ms |  4.9798 ms |  6.9809 ms |    72.752 ms |   285.7143 |         - |         - |   4069.66 KB |
| Dapper_Insert_Async               | 5000       | 1,129.568 ms | 46.2148 ms | 69.1721 ms | 1,149.990 ms |  1000.0000 |         - |         - |  12350.21 KB |
| FreeSql_Insert_Async              | 5000       |    85.000 ms |  5.7782 ms |  8.2869 ms |    79.693 ms |  1833.3333 | 1166.6667 |         - |  23495.33 KB |
| EFCore_Update_Async               | 5000       |   575.322 ms | 29.1352 ms | 43.6083 ms |   576.873 ms |  3000.0000 | 1000.0000 |         - |   43942.4 KB |
| EFCore_NoTracking_Update_Async    | 5000       |   608.305 ms | 42.3348 ms | 63.3647 ms |   600.572 ms |  4000.0000 | 1000.0000 |         - |  56536.63 KB |
| SqlSugar_Update_Async             | 5000       |   232.655 ms |  3.3132 ms |  4.9590 ms |   233.276 ms |  3000.0000 | 1500.0000 |  500.0000 |  38817.56 KB |
| SqlSugar_Fastest_Update_Async     | 5000       |   103.002 ms |  2.9948 ms |  4.4825 ms |   102.842 ms |   600.0000 |  200.0000 |         - |   8830.52 KB |
| LiteOrm_Update_Async              | 5000       |   118.703 ms |  5.3054 ms |  7.9409 ms |   118.669 ms |   400.0000 |  200.0000 |         - |   5885.12 KB |
| Dapper_Update_Async               | 5000       | 1,213.506 ms | 58.9291 ms | 88.2024 ms | 1,220.095 ms |  1000.0000 |         - |         - |  15473.38 KB |
| FreeSql_Update_Async              | 5000       |   175.578 ms | 19.5000 ms | 27.3362 ms |   161.352 ms |  2500.0000 |  500.0000 |         - |  34611.63 KB |
| EFCore_Upsert_Async               | 5000       |   589.073 ms | 35.8232 ms | 53.6185 ms |   586.178 ms |  3000.0000 | 1000.0000 |         - |  43927.71 KB |
| SqlSugar_Upsert_Async             | 5000       | 1,741.490 ms | 16.0265 ms | 22.9847 ms | 1,736.522 ms | 68000.0000 | 1000.0000 |         - | 844266.38 KB |
| SqlSugar_Fastest_Upsert_Async     | 5000       |   641.520 ms | 10.2878 ms | 15.3983 ms |   640.865 ms | 25000.0000 | 2000.0000 |         - | 319017.85 KB |
| LiteOrm_Upsert_Async              | 5000       |   103.517 ms |  5.0943 ms |  7.4672 ms |   100.387 ms |   400.0000 |  200.0000 |         - |   7224.39 KB |
| Dapper_Upsert_Async               | 5000       | 1,248.906 ms | 51.6637 ms | 77.3279 ms | 1,245.969 ms |  1000.0000 |         - |         - |  13987.52 KB |
| FreeSql_Upsert_Async              | 5000       |   103.058 ms | 11.1679 ms | 16.7156 ms |   103.234 ms |   750.0000 |  500.0000 |         - |  11133.98 KB |
| EFCore_JoinQuery_Async            | 5000       |    55.159 ms |  2.8886 ms |  4.3236 ms |    53.339 ms |   833.3333 |  500.0000 |  166.6667 |  10608.86 KB |
| EFCore_NoTracking_JoinQuery_Async | 5000       |    48.918 ms |  1.1083 ms |  1.6589 ms |    49.196 ms |   300.0000 |  100.0000 |         - |   4658.25 KB |
| SqlSugar_JoinQuery_Async          | 5000       |    89.971 ms |  2.7604 ms |  4.1317 ms |    90.765 ms |  3500.0000 |  250.0000 |         - |  45874.42 KB |
| LiteOrm_JoinQuery_Async           | 5000       |    43.941 ms |  2.2064 ms |  3.3024 ms |    44.099 ms |    76.9231 |         - |         - |   1075.65 KB |
| Dapper_JoinQuery_Async            | 5000       |    45.639 ms |  0.7288 ms |  1.0909 ms |    45.508 ms |    90.9091 |         - |         - |   2103.56 KB |
| FreeSql_JoinQuery_Async           | 5000       |    43.893 ms |  1.3534 ms |  1.9838 ms |    42.636 ms |   333.3333 |  250.0000 |         - |    4231.7 KB |
