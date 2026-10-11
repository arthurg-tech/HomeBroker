using OrderAccumulator.Contracts;

namespace OrderAccumulator.Tests.Support;

internal static class ConcurrentOrderScenarios
{
    public static (OrderRequest[] Seeds, OrderRequest[] Orders,
        IReadOnlyDictionary<string, decimal> InitialExposures) CreateMixed(string initialSide, bool multipleAssets)
    {
        var seeds = new List<OrderRequest> { Order("PETR4", initialSide, 9_000, 100m) };
        var initialExposures = new Dictionary<string, decimal>
        {
            ["PETR4"] = initialSide == "C" ? 900_000m : -900_000m,
        };

        if (multipleAssets)
        {
            seeds.Add(Order("VALE3", "V", 9_000, 100m));
            seeds.Add(Order("VIIA4", "C", 1, 54.87m));
            initialExposures.Add("VALE3", -900_000m);
            initialExposures.Add("VIIA4", 54.87m);
        }

        var orders = seeds.SelectMany(seed => Enumerable.Range(0, 12)
            .Select(index => Order(seed.Ativo!, index % 2 == 0 ? "C" : "V", 1_000,
                index % 2 == 0 ? 100m : 99.99m))
            .Concat([
                Order(seed.Ativo!, "C", 99_999, 999.99m),
                Order(seed.Ativo!, "V", 99_999, 999.99m),
                Order(seed.Ativo!, "C", 0, 1m),
            ])).ToArray();

        return (seeds.ToArray(), orders, initialExposures);
    }

    public static decimal SignedAmount(OrderRequest order) =>
        order.Quantidade!.Value * order.Preco!.Value * (order.Lado == "C" ? 1m : -1m);

    private static OrderRequest Order(string asset, string side, int quantity, decimal price) => new()
    {
        Ativo = asset,
        Lado = side,
        Quantidade = quantity,
        Preco = price,
    };
}
