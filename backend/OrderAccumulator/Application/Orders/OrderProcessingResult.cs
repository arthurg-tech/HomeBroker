namespace OrderAccumulator.Application.Orders;

public sealed record OrderProcessingResult(
    OrderProcessingStatus Status,
    decimal Exposure,
    string ErrorMessage);
