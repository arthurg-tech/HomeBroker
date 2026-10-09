using OrderAccumulator.Contracts;
using OrderAccumulator.Validation;

namespace OrderAccumulator.Services;

public sealed class ExposureService
{
    public const decimal ExposureLimit = 1_000_000m;

    private readonly object _sync = new();
    private readonly Dictionary<string, decimal> _exposures = new(StringComparer.Ordinal)
    {
        ["PETR4"] = 0m,
        ["VALE3"] = 0m,
        ["VIIA4"] = 0m,
    };

    public OrderResponse Process(OrderRequest? order)
    {
        lock (_sync)
        {
            var errors = OrderRequestValidator.Validate(order);
            if (errors.Count > 0)
            {
                return CreateInvalidResponse(order?.Ativo, string.Join(" ", errors));
            }

            var validAsset = order!.Ativo!;
            var currentExposure = _exposures[validAsset];
            var amount = order.Preco!.Value * order.Quantidade!.Value;
            var resultingExposure = order.Lado == "C"
                ? currentExposure + amount
                : currentExposure - amount;

            if (Math.Abs(resultingExposure) > ExposureLimit)
            {
                return new OrderResponse(false, currentExposure,
                    $"A ordem ultrapassa o limite de exposição do ativo {validAsset}.");
            }

            _exposures[validAsset] = resultingExposure;
            return new OrderResponse(true, resultingExposure, "");
        }
    }

    public OrderResponse RejectInvalidRequest(string? asset, string message)
    {
        lock (_sync)
        {
            return CreateInvalidResponse(asset, message);
        }
    }

    private OrderResponse CreateInvalidResponse(string? asset, string message)
    {
        var current = asset is not null && _exposures.TryGetValue(asset, out var exposure) ? exposure : 0m;
        return new OrderResponse(false, current, message);
    }

    public decimal GetExposure(string asset)
    {
        lock (_sync)
        {
            if (!_exposures.TryGetValue(asset, out var exposure))
            {
                throw new ArgumentException("Ativo deve ser PETR4, VALE3 ou VIIA4.", nameof(asset));
            }

            return exposure;
        }
    }
}
