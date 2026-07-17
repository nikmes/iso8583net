
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8875/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3


 Method             | Mean         | Error       | StdDev      | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
------------------- |-------------:|------------:|------------:|------:|--------:|-------:|-------:|----------:|------------:|
 IsBitSet_Loop      |   208.460 ns |   3.3877 ns |   3.0031 ns |  1.00 |    0.02 | 0.0143 |      - |     272 B |        1.00 |
 FieldIdEnumerator  |   100.331 ns |   1.3954 ns |   1.2370 ns |  0.48 |    0.01 | 0.0174 |      - |     328 B |        1.21 |
 GetSetFields_Alloc |    64.292 ns |   1.3148 ns |   1.4069 ns |  0.31 |    0.01 | 0.0595 |      - |    1120 B |        4.12 |
 BitEnumerator      |   325.954 ns |   4.2951 ns |   3.8075 ns |  1.56 |    0.03 | 0.0167 |      - |     320 B |        1.18 |
 ToHumanReadable    | 6,599.169 ns | 117.8085 ns | 126.0538 ns | 31.66 |    0.73 | 1.1902 | 0.0153 |   22424 B |       82.44 |
 ToHexString        |    24.799 ns |   0.5112 ns |   0.5020 ns |  0.12 |    0.00 | 0.0063 |      - |     120 B |        0.44 |
 GetByteArray       |     3.558 ns |   0.0860 ns |   0.0718 ns |  0.02 |    0.00 | 0.0025 |      - |      48 B |        0.18 |
 ToString_          | 6,223.189 ns |  95.0315 ns |  88.8925 ns | 29.86 |    0.58 | 1.4343 | 0.0381 |   27104 B |       99.65 |
