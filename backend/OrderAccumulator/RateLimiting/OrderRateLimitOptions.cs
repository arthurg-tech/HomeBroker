using System.ComponentModel.DataAnnotations;

namespace OrderAccumulator.RateLimiting;

public sealed class OrderRateLimitOptions
{
    public const string SectionName = "RateLimiting:Orders";

    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int WindowSeconds { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int SegmentsPerWindow { get; set; } = 6;
}
