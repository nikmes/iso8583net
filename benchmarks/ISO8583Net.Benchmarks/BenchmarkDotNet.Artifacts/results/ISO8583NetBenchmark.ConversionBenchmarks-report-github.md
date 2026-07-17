```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8875/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3


```
| Method            | Mean       | Error     | StdDev    | Median     | Gen0   | Allocated |
|------------------ |-----------:|----------:|----------:|-----------:|-------:|----------:|
| Hex2Bytes_16      |   7.813 ns | 0.2049 ns | 0.4092 ns |   7.667 ns | 0.0017 |      32 B |
| Hex2Bytes_64      |  25.140 ns | 0.4855 ns | 0.4304 ns |  25.035 ns | 0.0030 |      56 B |
| Bytes2Hex_8       |  19.100 ns | 3.3771 ns | 9.9576 ns |  13.089 ns | 0.0030 |      56 B |
| Bytes2Hex_32      |  39.978 ns | 0.7980 ns | 1.7178 ns |  39.728 ns | 0.0080 |     152 B |
| Ascii2Bcd_16      |   6.064 ns | 0.1562 ns | 0.1220 ns |   6.038 ns | 0.0021 |      40 B |
| Bcd2Ascii_16      |  37.259 ns | 0.5700 ns | 0.5332 ns |  37.151 ns | 0.0051 |      96 B |
| Ascii2Bytes_32    |  23.982 ns | 0.5162 ns | 1.0309 ns |  24.033 ns | 0.0046 |      88 B |
| Bytes2Ascii_32    |  59.392 ns | 1.2174 ns | 1.5396 ns |  59.548 ns | 0.0093 |     176 B |
| HexToByteArray_32 | 310.458 ns | 5.4875 ns | 7.6927 ns | 311.412 ns | 0.0572 |    1080 B |
