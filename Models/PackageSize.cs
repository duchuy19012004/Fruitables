namespace Fruitables.Models;

/// <summary>
/// Kích thước và khối lượng gói hàng dùng cho GHN.
/// </summary>
public sealed record PackageSize(
    int WeightGrams,
    int Length,
    int Width,
    int Height);
