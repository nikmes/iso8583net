```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8875/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3


```
| Method                      | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------------- |-----------:|----------:|----------:|-----------:|------:|--------:|-------:|-------:|----------:|------------:|
| PackUnpack_1stBitmap        | 2,074.7 ns |  47.37 ns | 138.92 ns | 2,021.4 ns |  1.00 |    0.09 | 0.5150 | 0.0038 |   9.48 KB |        1.00 |
| PackUnpack_2ndBitmap        | 2,340.5 ns |  50.87 ns | 131.30 ns | 2,304.6 ns |  1.13 |    0.10 | 0.5302 | 0.0076 |    9.8 KB |        1.03 |
| PackUnpack_3rdBitmap        | 3,853.2 ns | 338.03 ns | 996.69 ns | 4,450.2 ns |  1.87 |    0.50 | 0.5417 | 0.0038 |   9.97 KB |        1.05 |
| PackUnpack_WithSubfields    | 2,553.6 ns |  63.08 ns | 184.99 ns | 2,525.2 ns |  1.24 |    0.12 | 0.6409 | 0.0076 |  11.78 KB |        1.24 |
| PackUnpack_1stBitmap_Pooled | 1,942.5 ns |  38.44 ns |  87.54 ns | 1,928.0 ns |  0.94 |    0.07 | 0.4044 | 0.0038 |   7.45 KB |        0.79 |
| PackOnly_1stBitmap          |   971.0 ns |  34.57 ns |  92.87 ns |   948.6 ns |  0.47 |    0.05 | 0.2918 | 0.0029 |   5.37 KB |        0.57 |
| PackOnly_1stBitmap_Pooled   |   858.8 ns |  10.97 ns |   9.16 ns |   860.7 ns |  0.42 |    0.03 | 0.1812 | 0.0010 |   3.34 KB |        0.35 |
| PackOnly_2ndBitmap          | 1,018.3 ns |  20.01 ns |  33.97 ns | 1,010.6 ns |  0.49 |    0.04 | 0.2995 | 0.0019 |   5.51 KB |        0.58 |
| PackOnly_3rdBitmap          | 1,058.8 ns |  21.22 ns |  21.79 ns | 1,053.5 ns |  0.51 |    0.03 | 0.3033 | 0.0019 |   5.59 KB |        0.59 |
| PackOnly_WithSubfields      | 1,170.3 ns |  23.12 ns |  37.99 ns | 1,156.4 ns |  0.57 |    0.04 | 0.3471 | 0.0038 |    6.4 KB |        0.68 |
| UnpackOnly_1stBitmap        | 1,002.9 ns |  17.08 ns |  22.80 ns |   995.5 ns |  0.49 |    0.03 | 0.2270 | 0.0019 |   4.18 KB |        0.44 |
| UnpackOnly_2ndBitmap        | 1,040.6 ns |  19.96 ns |  22.18 ns | 1,032.8 ns |  0.50 |    0.03 | 0.2365 | 0.0019 |   4.36 KB |        0.46 |
| UnpackOnly_3rdBitmap        | 1,064.0 ns |  11.60 ns |   9.69 ns | 1,064.9 ns |  0.52 |    0.03 | 0.2403 | 0.0019 |   4.45 KB |        0.47 |
| UnpackOnly_WithSubfields    | 1,182.4 ns |  23.55 ns |  34.51 ns | 1,171.4 ns |  0.57 |    0.04 | 0.2956 | 0.0038 |   5.45 KB |        0.58 |
| ToString_1stBitmap          | 5,364.0 ns |  82.17 ns |  68.61 ns | 5,336.2 ns |  2.60 |    0.17 | 2.0752 | 0.0839 |  38.18 KB |        4.03 |
