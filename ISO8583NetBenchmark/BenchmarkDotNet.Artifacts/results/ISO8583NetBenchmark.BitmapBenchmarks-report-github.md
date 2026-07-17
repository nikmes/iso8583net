```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8875/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3


```
| Method             | Mean         | Error       | StdDev      | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------- |-------------:|------------:|------------:|------:|--------:|-------:|-------:|----------:|------------:|
| IsBitSet_Loop      |   197.156 ns |   3.0092 ns |   2.8148 ns |  1.00 |    0.02 | 0.0143 |      - |     272 B |        1.00 |
| FieldIdEnumerator  |   108.383 ns |   0.9945 ns |   0.8816 ns |  0.55 |    0.01 | 0.0174 |      - |     328 B |        1.21 |
| GetSetFields_Alloc |    94.046 ns |   1.9147 ns |   4.5506 ns |  0.48 |    0.02 | 0.0595 |      - |    1120 B |        4.12 |
| BitEnumerator      |   326.058 ns |   3.1574 ns |   2.9534 ns |  1.65 |    0.03 | 0.0167 |      - |     320 B |        1.18 |
| ToHumanReadable    | 6,501.727 ns | 128.8181 ns | 171.9685 ns | 32.98 |    0.97 | 1.1902 | 0.0153 |   22424 B |       82.44 |
| ToHexString        |    25.180 ns |   0.5023 ns |   0.3922 ns |  0.13 |    0.00 | 0.0063 |      - |     120 B |        0.44 |
| GetByteArray       |     3.908 ns |   0.1072 ns |   0.1317 ns |  0.02 |    0.00 | 0.0025 |      - |      48 B |        0.18 |
| ToString_          | 6,342.657 ns | 120.2459 ns | 176.2549 ns | 32.18 |    0.98 | 1.4343 | 0.0381 |   27104 B |       99.65 |
