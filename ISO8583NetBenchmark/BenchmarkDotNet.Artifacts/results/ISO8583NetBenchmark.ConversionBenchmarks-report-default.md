
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8875/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), X64 RyuJIT x86-64-v3


 Method            | Mean      | Error    | StdDev    | Median    | Gen0   | Allocated |
------------------ |----------:|---------:|----------:|----------:|-------:|----------:|
 Hex2Bytes_16      |  20.14 ns | 1.713 ns |  4.943 ns |  19.98 ns | 0.0017 |      32 B |
 Hex2Bytes_64      |  41.63 ns | 6.867 ns | 20.249 ns |  29.16 ns | 0.0029 |      56 B |
 Bytes2Hex_8       |  21.84 ns | 1.918 ns |  5.153 ns |  23.42 ns | 0.0030 |      56 B |
 Bytes2Hex_32      |  60.74 ns | 1.196 ns |  1.175 ns |  60.63 ns | 0.0080 |     152 B |
 Ascii2Bcd_16      |  12.30 ns | 0.295 ns |  0.539 ns |  12.31 ns | 0.0021 |      40 B |
 Bcd2Ascii_16      |  36.89 ns | 0.804 ns |  1.045 ns |  36.98 ns | 0.0051 |      96 B |
 Ascii2Bytes_32    |  26.56 ns | 0.583 ns |  0.758 ns |  26.67 ns | 0.0046 |      88 B |
 Bytes2Ascii_32    |  64.30 ns | 1.185 ns |  1.109 ns |  64.14 ns | 0.0093 |     176 B |
 HexToByteArray_32 | 317.60 ns | 6.340 ns | 11.105 ns | 318.29 ns | 0.0572 |    1080 B |
