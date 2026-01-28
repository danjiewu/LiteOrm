
# LiteOrm 性能评测报告

以下是根据 BenchmarkDotNet 测试结果整理的性能对比结果（单位：ms），每项最优结果已**加粗**显示：

### 1. 数据量：100 条 (BatchCount = 100)
| 测试项目 | EF Core | SqlSugar | **LiteOrm** | Dapper | FreeSql |
| :--- | :--- | :--- | :--- | :--- | :--- |
| 异步插入 (Insert) | 19.221 | 3.722 | **3.421** | 12.336 | 3.925 |
| 异步更新 (Update) | 18.649 | 5.317 | **4.281** | 16.069 | 4.849 |
| 插入或更新 (Upsert) | 18.979 | 8.527 | 4.540 | 14.927 | **4.201** |
| 关联查询 (Join) | 6.040 | 2.486 | 1.268 | **1.262** | 1.276 |

### 2. 数据量：1,000 条 (BatchCount = 1000)
| 测试项目 | EF Core | SqlSugar | **LiteOrm** | Dapper | FreeSql |
| :--- | :--- | :--- | :--- | :--- | :--- |
| 异步插入 (Insert) | 136.703 | 15.168 | **10.034** | 99.455 | 16.107 |
| 异步更新 (Update) | 118.352 | 33.440 | **15.388** | 114.382 | 27.434 |
| 插入或更新 (Upsert) | 128.021 | 59.400 | 14.833 | 122.159 | **14.269** |
| 关联查询 (Join) | 18.048 | 21.077 | **10.382** | 10.447 | 11.436 |

### 3. 数据量：5,000 条 (BatchCount = 5000)
| 测试项目 | EF Core | SqlSugar | **LiteOrm** | Dapper | FreeSql |
| :--- | :--- | :--- | :--- | :--- | :--- |
| 异步插入 (Insert) | 1544.159 | 71.612 | **36.195** | 477.352 | 66.990 |
| 异步更新 (Update) | 628.142 | 185.338 | **62.091** | 570.402 | 121.688 |
| 插入或更新 (Upsert) | 691.197 | 818.491 | 56.278 | 597.868 | **53.475** |
| 关联查询 (Join) | 84.667 | 103.115 | 73.850 | **68.451** | 86.928 |

---

## 原始测试报告 (BenchmarkDotNet)

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
13th Gen Intel Core i5-13400F 2.50GHz, 1 CPU, 16 logical and 10 physical cores
.NET SDK 10.0.102
  [Host]    : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  MediumRun : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3

Job=MediumRun  IterationCount=15  LaunchCount=2  
WarmupCount=10  

```

| Method                        | BatchCount | Mean         | Error         | StdDev        | Median     | Gen0       | Gen1      | Gen2     | Allocated    |
|------------------------------ |----------- |-------------:|--------------:|--------------:|-----------:|-----------:|----------:|---------:|-------------:|
| **EFCore_Insert_Async**           | **100**        |    **19.221 ms** |     **0.2066 ms** |     **0.3092 ms** |  **19.186 ms** |   **187.5000** |   **62.5000** |        **-** |   **1964.04 KB** |
| SqlSugar_Insert_Async         | 100        |     3.722 ms |     0.1304 ms |     0.1911 ms |   3.760 ms |    39.0625 |    7.8125 |        - |    476.09 KB |
| LiteOrm_Insert_Async          | 100        |     3.421 ms |     0.1478 ms |     0.2213 ms |   3.415 ms |    31.2500 |    7.8125 |        - |    381.55 KB |
| Dapper_Insert_Async           | 100        |    12.336 ms |     0.4940 ms |     0.7240 ms |  12.190 ms |    15.6250 |         - |        - |    254.84 KB |
| FreeSql_Insert_Async          | 100        |     3.925 ms |     0.1990 ms |     0.2854 ms |   3.926 ms |    39.0625 |    7.8125 |        - |    460.67 KB |
| EFCore_Update_Async           | 100        |    18.649 ms |     0.2803 ms |     0.4020 ms |  18.582 ms |   125.0000 |   31.2500 |        - |   1559.35 KB |
| SqlSugar_Update_Async         | 100        |     5.317 ms |     0.2329 ms |     0.3485 ms |   5.349 ms |    78.1250 |   23.4375 |        - |    800.68 KB |
| LiteOrm_Update_Async          | 100        |     4.281 ms |     0.1671 ms |     0.2449 ms |   4.270 ms |    46.8750 |    7.8125 |        - |    488.76 KB |
| Dapper_Update_Async           | 100        |    16.069 ms |     1.1376 ms |     1.6675 ms |  15.612 ms |    31.2500 |         - |        - |    321.05 KB |
| FreeSql_Update_Async          | 100        |     4.849 ms |     0.1878 ms |     0.2811 ms |   4.824 ms |    62.5000 |   15.6250 |        - |    692.93 KB |
| EFCore_UpdateOrInsert_Async   | 100        |    18.979 ms |     0.2238 ms |     0.3280 ms |  18.982 ms |   156.2500 |   31.2500 |        - |    1660.5 KB |
| SqlSugar_UpdateOrInsert_Async | 100        |     8.527 ms |     0.3101 ms |     0.4545 ms |   8.490 ms |   140.6250 |   46.8750 |        - |    1458.3 KB |
| LiteOrm_UpdateOrInsert_Async  | 100        |     4.540 ms |     0.2096 ms |     0.3138 ms |   4.581 ms |    54.6875 |         - |        - |    635.59 KB |
| Dapper_UpdateOrInsert_Async   | 100        |    14.927 ms |     0.2697 ms |     0.3953 ms |  14.894 ms |          - |         - |        - |    291.75 KB |
| FreeSql_UpdateOrInsert_Async  | 100        |     4.201 ms |     0.1902 ms |     0.2847 ms |   4.254 ms |    23.4375 |         - |        - |    265.72 KB |
| EFCore_JoinQuery_Async        | 100        |     6.040 ms |     0.1952 ms |     0.2862 ms |   6.174 ms |    46.8750 |         - |        - |     492.3 KB |
| SqlSugar_JoinQuery_Async      | 100        |     2.486 ms |     0.0260 ms |     0.0381 ms |   2.479 ms |   179.6875 |   23.4375 |        - |   1886.94 KB |
| LiteOrm_JoinQuery_Async       | 100        |     1.268 ms |     0.0142 ms |     0.0203 ms |   1.264 ms |    27.3438 |         - |        - |    296.66 KB |
| Dapper_JoinQuery_Async        | 100        |     1.262 ms |     0.0164 ms |     0.0240 ms |   1.258 ms |     7.8125 |         - |        - |     89.91 KB |
| FreeSql_JoinQuery_Async       | 100        |     1.276 ms |     0.0171 ms |     0.0246 ms |   1.271 ms |    17.5781 |    3.9063 |        - |    192.53 KB |
| **EFCore_Insert_Async**           | **1000**       |   **136.703 ms** |     **4.0234 ms** |     **5.8974 ms** | **135.257 ms** |  **1666.6667** |  **666.6667** |        **-** |  **18092.04 KB** |
| SqlSugar_Insert_Async         | 1000       |    15.168 ms |     0.4613 ms |     0.6904 ms |  15.109 ms |   437.5000 |  281.2500 |  31.2500 |   4571.35 KB |
| LiteOrm_Insert_Async          | 1000       |    10.034 ms |     0.5309 ms |     0.7613 ms |   9.897 ms |   156.2500 |   31.2500 |        - |   1728.97 KB |
| Dapper_Insert_Async           | 1000       |    99.455 ms |     2.0773 ms |     2.9120 ms | 100.209 ms |          - |         - |        - |   2476.51 KB |
| FreeSql_Insert_Async          | 1000       |    16.107 ms |     0.3400 ms |     0.5089 ms |  16.057 ms |   437.5000 |  187.5000 |        - |   4629.99 KB |
| EFCore_Update_Async           | 1000       |   118.352 ms |     1.9186 ms |     2.8122 ms | 117.966 ms |  1250.0000 |  750.0000 |        - |  15230.83 KB |
| SqlSugar_Update_Async         | 1000       |    33.440 ms |     1.3000 ms |     1.9055 ms |  32.846 ms |   687.5000 |  500.0000 |        - |   7677.71 KB |
| LiteOrm_Update_Async          | 1000       |    15.388 ms |     0.2676 ms |     0.3923 ms |  15.433 ms |   281.2500 |   93.7500 |        - |   2870.52 KB |
| Dapper_Update_Async           | 1000       |   114.382 ms |     1.9428 ms |     2.8477 ms | 113.998 ms |   250.0000 |         - |        - |   3094.63 KB |
| FreeSql_Update_Async          | 1000       |    27.434 ms |     0.3663 ms |     0.5253 ms |  27.393 ms |   718.7500 |  468.7500 | 125.0000 |   6877.56 KB |
| EFCore_UpdateOrInsert_Async   | 1000       |   128.021 ms |     2.7731 ms |     3.9770 ms | 127.336 ms |  1333.3333 |  666.6667 |        - |  14829.07 KB |
| SqlSugar_UpdateOrInsert_Async | 1000       |    59.400 ms |     1.4557 ms |     2.1789 ms |  59.673 ms |  3500.0000 |  750.0000 |        - |  35951.36 KB |
| LiteOrm_UpdateOrInsert_Async  | 1000       |    14.833 ms |     0.5159 ms |     0.7721 ms |  14.656 ms |   375.0000 |  156.2500 |        - |   3894.02 KB |
| Dapper_UpdateOrInsert_Async   | 1000       |   122.159 ms |     3.6863 ms |     5.2868 ms | 121.375 ms |          - |         - |        - |   2800.13 KB |
| FreeSql_UpdateOrInsert_Async  | 1000       |    14.269 ms |     0.4169 ms |     0.5979 ms |  14.113 ms |   218.7500 |   78.1250 |        - |   2246.89 KB |
| EFCore_JoinQuery_Async        | 1000       |    18.048 ms |     0.3691 ms |     0.5411 ms |  17.896 ms |   375.0000 |  250.0000 |  62.5000 |   3382.03 KB |
| SqlSugar_JoinQuery_Async      | 1000       |    21.077 ms |     0.2149 ms |     0.3081 ms |  21.030 ms |  1781.2500 |  781.2500 |        - |  18344.66 KB |
| LiteOrm_JoinQuery_Async       | 1000       |    10.382 ms |     0.2553 ms |     0.3822 ms |  10.339 ms |   265.6250 |   93.7500 |        - |    2774.7 KB |
| Dapper_JoinQuery_Async        | 1000       |    10.447 ms |     0.1655 ms |     0.2478 ms |  10.444 ms |    78.1250 |   31.2500 |        - |    821.07 KB |
| FreeSql_JoinQuery_Async       | 1000       |    11.436 ms |     0.3386 ms |     0.5068 ms |  11.448 ms |   156.2500 |   62.5000 |        - |   1674.28 KB |
| **EFCore_Insert_Async**           | **5000**       | **1,544.159 ms** | **1,008.6060 ms** | **1,509.6341 ms** | **660.315 ms** |  **7000.0000** | **2000.0000** |        **-** |  **80086.88 KB** |
| SqlSugar_Insert_Async         | 5000       |    71.612 ms |     0.9642 ms |     1.3829 ms |  71.474 ms |  2428.5714 | 1285.7143 | 428.5714 |  23196.54 KB |
| LiteOrm_Insert_Async          | 5000       |    36.195 ms |     1.1752 ms |     1.7590 ms |  36.430 ms |   769.2308 |  384.6154 |        - |   8378.48 KB |
| Dapper_Insert_Async           | 5000       |   477.352 ms |    12.5197 ms |    18.3512 ms | 475.401 ms |  1000.0000 |         - |        - |   12351.8 KB |
| FreeSql_Insert_Async          | 5000       |    66.990 ms |     1.0098 ms |     1.5115 ms |  66.895 ms |  2125.0000 | 1375.0000 |        - |   23333.6 KB |
| EFCore_Update_Async           | 5000       |   628.142 ms |   123.5672 ms |   160.6722 ms | 595.705 ms |  6000.0000 | 2000.0000 |        - |  66057.14 KB |
| SqlSugar_Update_Async         | 5000       |   185.338 ms |     9.4851 ms |    14.1969 ms | 185.147 ms |  3666.6667 | 1666.6667 | 333.3333 |  38810.78 KB |
| LiteOrm_Update_Async          | 5000       |    62.091 ms |     1.3839 ms |     2.0714 ms |  62.099 ms |  1285.7143 |  857.1429 |        - |  14284.68 KB |
| Dapper_Update_Async           | 5000       |   570.402 ms |    13.4973 ms |    18.9214 ms | 568.902 ms |  1000.0000 |         - |        - |  15476.74 KB |
| FreeSql_Update_Async          | 5000       |   121.688 ms |     1.7956 ms |     2.6319 ms | 121.359 ms |  3000.0000 |  750.0000 |        - |  34449.84 KB |
| EFCore_UpdateOrInsert_Async   | 5000       |   691.197 ms |   246.3843 ms |   320.3692 ms | 610.330 ms |  6000.0000 | 2000.0000 |        - |   63803.2 KB |
| SqlSugar_UpdateOrInsert_Async | 5000       |   818.491 ms |     9.2287 ms |    13.5273 ms | 815.633 ms | 82000.0000 | 2000.0000 |        - | 844276.99 KB |
| LiteOrm_UpdateOrInsert_Async  | 5000       |    56.278 ms |     0.9505 ms |     1.3932 ms |  56.042 ms |  1571.4286 |  428.5714 |        - |  16659.13 KB |
| Dapper_UpdateOrInsert_Async   | 5000       |   597.868 ms |    12.0379 ms |    18.0177 ms | 593.699 ms |  1000.0000 |         - |        - |     13987 KB |
| FreeSql_UpdateOrInsert_Async  | 5000       |    53.475 ms |     1.2739 ms |     1.8673 ms |  52.990 ms |   888.8889 |  777.7778 |        - |  11125.04 KB |
| EFCore_JoinQuery_Async        | 5000       |    84.667 ms |     6.0573 ms |     8.8787 ms |  85.811 ms |  1666.6667 | 1000.0000 | 333.3333 |   15616.7 KB |
| SqlSugar_JoinQuery_Async      | 5000       |   103.115 ms |     1.6956 ms |     2.4318 ms | 103.220 ms |  8750.0000 | 1500.0000 |        - |  91634.98 KB |
| LiteOrm_JoinQuery_Async       | 5000       |    73.850 ms |     7.9050 ms |    11.8319 ms |  73.768 ms |  1200.0000 |  400.0000 |        - |  13891.67 KB |
| Dapper_JoinQuery_Async        | 5000       |    68.451 ms |     6.5335 ms |     9.5768 ms |  68.783 ms |   333.3333 |  166.6667 |        - |   4170.17 KB |
| FreeSql_JoinQuery_Async       | 5000       |    86.928 ms |     7.0469 ms |    10.5475 ms |  86.476 ms |  1000.0000 |  600.0000 | 200.0000 |   8362.52 KB |
