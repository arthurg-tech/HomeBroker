using OrderAccumulator.Application.Orders;
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

    public OrderProcessingResult Process(OrderRequest? order)
    {
        lock (_sync)
        {
            var errors = OrderRequestValidator.Validate(order);
            if (errors.Count > 0)
            {
                var current = order?.Ativo is not null && _exposures.TryGetValue(order.Ativo, out var exposure)
                    ? exposure
                    : 0m;
                return new OrderProcessingResult(OrderProcessingStatus.InvalidInput, current,
                    string.Join(" ", errors));
            }

            var validAsset = order!.Ativo!;
            var currentExposure = _exposures[validAsset];
            var amount = order.Preco!.Value * order.Quantidade!.Value;
            var resultingExposure = order.Lado == "C"
                ? currentExposure + amount
                : currentExposure - amount;

            if (Math.Abs(resultingExposure) > ExposureLimit)
            {
                return new OrderProcessingResult(OrderProcessingStatus.ExposureLimitExceeded, currentExposure,
                    $"A ordem ultrapassa o limite de exposição do ativo {validAsset}.");
            }

            _exposures[validAsset] = resultingExposure;
            return new OrderProcessingResult(OrderProcessingStatus.Accepted, resultingExposure, "");
        }
    }

    public decimal? FindExposure(string? asset)
    {
        lock (_sync)
        {
            return asset is not null && _exposures.TryGetValue(asset, out var exposure) ? exposure : null;
        }
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
