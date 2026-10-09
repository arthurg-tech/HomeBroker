using OrderAccumulator.Contracts;
using OrderAccumulator.Services;

namespace OrderAccumulator.Tests;

public class ExposureServiceTests
{
    [Theory]
    [InlineData("PETR4")]
    [InlineData("VALE3")]
    [InlineData("VIIA4")]
    public void StartsEveryAssetAtZero(string asset)
    {
        var service = new ExposureService();

        Assert.Equal(0m, service.GetExposure(asset));
    }

    [Theory]
    [InlineData("C")]
    [InlineData("V")]
    public void AppliesTheFullOrderValueWithTheCorrectSign(string side)
    {
        var service = new ExposureService();
        var expected = side == "C" ? 32_044.08m : -32_044.08m;

        var response = service.Process(Order(side, 584, 54.87m));

        Assert.True(response.Sucesso);
        Assert.Equal(expected, response.ExposicaoAtual);
        Assert.Equal(expected, service.GetExposure("PETR4"));
        Assert.Equal("", response.MsgErro);
    }

    [Fact]
    public void AccumulatesSequencesAndAllowsSalesToCrossZero()
    {
        var service = new ExposureService();

        var purchase = service.Process(Order("C", 3, 0.10m));
        var sale = service.Process(Order("V", 2, 0.10m));
        var negative = service.Process(Order("V", 2, 0.10m));
        var zero = service.Process(Order("C", 1, 0.10m));

        Assert.All(new[] { purchase, sale, negative, zero }, response => Assert.True(response.Sucesso));
        Assert.Equal(0.30m, purchase.ExposicaoAtual);
        Assert.Equal(0.10m, sale.ExposicaoAtual);
        Assert.Equal(-0.10m, negative.ExposicaoAtual);
        Assert.Equal(0m, zero.ExposicaoAtual);
        Assert.Equal(0m, service.GetExposure("PETR4"));
    }

    [Fact]
    public void CountsEachRepeatedOrderAsANewOrder()
    {
        var service = new ExposureService();
        var order = Order("C", 2, 1.23m);

        Assert.Equal(2.46m, service.Process(order).ExposicaoAtual);
        Assert.Equal(4.92m, service.Process(order).ExposicaoAtual);
        Assert.Equal(4.92m, service.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData("C")]
    [InlineData("V")]
    public void AcceptsTheExactLimitAndRejectsOneCentBeyondWithoutMutation(string side)
    {
        var service = new ExposureService();
        var expected = side == "C" ? 1_000_000m : -1_000_000m;

        var atLimit = service.Process(Order(side, 10_000, 100m));
        var rejected = service.Process(Order(side, 1, 0.01m));

        Assert.True(atLimit.Sucesso);
        Assert.Equal(expected, atLimit.ExposicaoAtual);
        Assert.False(rejected.Sucesso);
        Assert.Equal(expected, rejected.ExposicaoAtual);
        Assert.Equal("A ordem ultrapassa o limite de exposição do ativo PETR4.", rejected.MsgErro);
        Assert.Equal(expected, service.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData("C")]
    [InlineData("V")]
    public void RejectsAnOrderExceedingTheLimitFromZero(string side)
    {
        var service = new ExposureService();

        var response = service.Process(Order(side, 10_001, 100m));

        Assert.False(response.Sucesso);
        Assert.Equal(0m, response.ExposicaoAtual);
        Assert.NotEmpty(response.MsgErro);
        Assert.Equal(0m, service.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData("C", "V")]
    [InlineData("V", "C")]
    public void AcceptsOrdersThatReduceExposureAtTheLimit(string initialSide, string reducingSide)
    {
        var service = new ExposureService();
        Assert.True(service.Process(Order(initialSide, 10_000, 100m)).Sucesso);
        var expected = initialSide == "C" ? 999_999.99m : -999_999.99m;

        var response = service.Process(Order(reducingSide, 1, 0.01m));

        Assert.True(response.Sucesso);
        Assert.Equal(expected, response.ExposicaoAtual);
        Assert.Equal(expected, service.GetExposure("PETR4"));
    }

    [Fact]
    public void KeepsAssetsIndependentOnAcceptanceAndRejection()
    {
        var service = new ExposureService();

        Assert.True(service.Process(Order("C", 10_000, 100m, "PETR4")).Sucesso);
        Assert.True(service.Process(Order("V", 10_000, 100m, "VALE3")).Sucesso);
        Assert.True(service.Process(Order("C", 1, 54.87m, "VIIA4")).Sucesso);
        var rejected = service.Process(Order("C", 1, 0.01m, "PETR4"));

        Assert.False(rejected.Sucesso);
        Assert.Equal(1_000_000m, rejected.ExposicaoAtual);
        Assert.Equal(1_000_000m, service.GetExposure("PETR4"));
        Assert.Equal(-1_000_000m, service.GetExposure("VALE3"));
        Assert.Equal(54.87m, service.GetExposure("VIIA4"));
    }

    public static TheoryData<OrderRequest?> InvalidOrders => new()
    {
        null,
        new OrderRequest(),
        Order("C", 1, 1m) with { Ativo = "petr4" },
        Order("Compra", 1, 1m),
        Order("C", 0, 1m),
        Order("C", 100_000, 1m),
        Order("C", int.MaxValue, decimal.MaxValue),
        Order("C", 1, 0m),
        Order("C", 1, 1_000m),
        Order("C", 1, 10.001m),
        Order("C", 1, 1m) with { Quantidade = null },
        Order("C", 1, 1m) with { Preco = null },
    };

    [Theory]
    [MemberData(nameof(InvalidOrders))]
    public void RejectsInvalidOrdersBeforeCalculationAndPreservesState(OrderRequest? order)
    {
        var service = new ExposureService();
        Assert.True(service.Process(Order("C", 1, 54.87m)).Sucesso);

        var response = service.Process(order);

        Assert.False(response.Sucesso);
        Assert.NotEmpty(response.MsgErro);
        Assert.Equal(order?.Ativo == "PETR4" ? 54.87m : 0m, response.ExposicaoAtual);
        Assert.Equal(54.87m, service.GetExposure("PETR4"));
        Assert.Equal(0m, service.GetExposure("VALE3"));
        Assert.Equal(0m, service.GetExposure("VIIA4"));
    }

    [Fact]
    public async Task ConcurrentPurchasesAcceptTenOrdersAndRejectTenAtThePositiveLimit()
    {
        await AssertConcurrentOrders("C", 1_000_000m);
    }

    [Fact]
    public async Task ConcurrentSalesAcceptTenOrdersAndRejectTenAtTheNegativeLimit()
    {
        await AssertConcurrentOrders("V", -1_000_000m);
    }

    private static async Task AssertConcurrentOrders(string side, decimal expectedExposure)
    {
        var service = new ExposureService();
        var order = Order(side, 1_000, 100m);
        using var startBarrier = new Barrier(21);

        Assert.Equal(0m, service.GetExposure("PETR4"));

        // Dedicated threads allow all 20 participants to reach the barrier
        // without depending on thread-pool growth while workers are blocked.
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Factory.StartNew(() =>
            {
                startBarrier.SignalAndWait();
                return service.Process(order);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
            .ToArray();

        startBarrier.SignalAndWait();
        var responses = await Task.WhenAll(tasks);
        var accepted = responses.Where(response => response.Sucesso).ToArray();
        var rejected = responses.Where(response => !response.Sucesso).ToArray();

        Assert.Equal(10, accepted.Length);
        Assert.Equal(10, rejected.Length);
        Assert.Equal(expectedExposure, service.GetExposure("PETR4"));

        // Compare the values as a set of sorted results, not by task order.
        var expectedAcceptedExposures = Enumerable.Range(1, 10)
            .Select(index => index * expectedExposure / 10)
            .OrderBy(exposure => exposure);
        Assert.Equal(expectedAcceptedExposures,
            accepted.Select(response => response.ExposicaoAtual).OrderBy(exposure => exposure));
        Assert.All(accepted, response => Assert.Equal("", response.MsgErro));
        Assert.All(rejected, response =>
        {
            Assert.Equal(expectedExposure, response.ExposicaoAtual);
            Assert.Equal("A ordem ultrapassa o limite de exposição do ativo PETR4.", response.MsgErro);
        });
    }

    private static OrderRequest Order(string side, int quantity, decimal price, string asset = "PETR4") => new()
    {
        Ativo = asset,
        Lado = side,
        Quantidade = quantity,
        Preco = price,
    };
}
