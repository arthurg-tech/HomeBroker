namespace OrderAccumulator.Application.Orders;

public enum OrderProcessingStatus
{
    Accepted,
    InvalidInput,
    ExposureLimitExceeded,
}
