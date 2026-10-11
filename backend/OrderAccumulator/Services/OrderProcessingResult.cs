namespace OrderAccumulator.Services;

public sealed record OrderProcessingResult(
    OrderProcessingStatus Status,
    decimal Exposure,
    string ErrorMessage);
