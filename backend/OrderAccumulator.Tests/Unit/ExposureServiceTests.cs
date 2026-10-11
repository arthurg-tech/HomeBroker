using OrderAccumulator.Application.Orders;
using OrderAccumulator.Contracts;
using OrderAccumulator.Services;
using OrderAccumulator.Validation;

namespace OrderAccumulator.Tests.Unit;

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
        Assert.Equal(0m, service.FindExposure(asset));
    }

    [Theory]
    [InlineData("C")]
    [InlineData("V")]
    public void AppliesTheFullOrderValueWithTheCorrectSign(string side)
    {
        var service = new ExposureService();
        var expected = side == "C" ? 32_044.08m : -32_044.08m;

        var response = service.Process(Order(side, 584, 54.87m));

        Assert.Equal(OrderProcessingStatus.Accepted, response.Status);
        Assert.Equal(expected, response.Exposure);
        Assert.Equal(expected, service.GetExposure("PETR4"));
        Assert.Equal(expected, service.FindExposure("PETR4"));
        Assert.Equal("", response.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("petr4")]
    [InlineData("ABEV3")]
    public void FindingAnUnknownAssetReturnsNullWithoutChangingState(string? asset)
    {
        var service = new ExposureService();
        Assert.Equal(OrderProcessingStatus.Accepted, service.Process(Order("C", 1, 54.87m)).Status);

        Assert.Null(service.FindExposure(asset));

        Assert.Equal(54.87m, service.GetExposure("PETR4"));
        Assert.Equal(0m, service.GetExposure("VALE3"));
        Assert.Equal(0m, service.GetExposure("VIIA4"));
    }

    [Fact]
    public void AccumulatesTheReferencePurchaseAndSaleSequence()
    {
        var service = new ExposureService();

        var purchase = service.Process(Order("C", 584, 54.87m));
        var sale = service.Process(Order("V", 100, 50m));

        Assert.Equal(OrderProcessingStatus.Accepted, purchase.Status);
        Assert.Equal(32_044.08m, purchase.Exposure);
        Assert.Equal(OrderProcessingStatus.Accepted, sale.Status);
        Assert.Equal(27_044.08m, sale.Exposure);
        Assert.Equal(27_044.08m, service.GetExposure("PETR4"));
    }

    [Fact]
    public void AllowsTheReferenceSaleFromZero()
    {
        var service = new ExposureService();

        var response = service.Process(Order("V", 10, 20m));

        Assert.Equal(OrderProcessingStatus.Accepted, response.Status);
        Assert.Equal(-200m, response.Exposure);
        Assert.Equal(-200m, service.GetExposure("PETR4"));
    }

    [Fact]
    public void RejectsValidMaximumFieldsByTheLimitWithoutMutatingState()
    {
        var service = new ExposureService();
        var order = Order("C", 99_999, 999.99m);
        Assert.Empty(OrderRequestValidator.Validate(order));

        var response = service.Process(order);

        Assert.Equal(OrderProcessingStatus.ExposureLimitExceeded, response.Status);
        Assert.Equal(0m, response.Exposure);
        Assert.Equal("A ordem ultrapassa o limite de exposição do ativo PETR4.", response.ErrorMessage);
        Assert.All(new[] { "PETR4", "VALE3", "VIIA4" }, asset => Assert.Equal(0m, service.GetExposure(asset)));
    }

    [Fact]
    public void AccumulatesSequencesAndAllowsSalesToCrossZero()
    {
        var service = new ExposureService();

        var purchase = service.Process(Order("C", 3, 0.10m));
        var sale = service.Process(Order("V", 2, 0.10m));
        var negative = service.Process(Order("V", 2, 0.10m));
        var zero = service.Process(Order("C", 1, 0.10m));

        Assert.All(new[] { purchase, sale, negative, zero }, response => Assert.Equal(OrderProcessingStatus.Accepted, response.Status));
        Assert.Equal(0.30m, purchase.Exposure);
        Assert.Equal(0.10m, sale.Exposure);
        Assert.Equal(-0.10m, negative.Exposure);
        Assert.Equal(0m, zero.Exposure);
        Assert.Equal(0m, service.GetExposure("PETR4"));
    }

    [Fact]
    public void CountsEachRepeatedOrderAsANewOrder()
    {
        var service = new ExposureService();
        var order = Order("C", 2, 1.23m);

        Assert.Equal(2.46m, service.Process(order).Exposure);
        Assert.Equal(4.92m, service.Process(order).Exposure);
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

        Assert.Equal(OrderProcessingStatus.Accepted, atLimit.Status);
        Assert.Equal(expected, atLimit.Exposure);
        Assert.Equal(OrderProcessingStatus.ExposureLimitExceeded, rejected.Status);
        Assert.Equal(expected, rejected.Exposure);
        Assert.Equal("A ordem ultrapassa o limite de exposição do ativo PETR4.", rejected.ErrorMessage);
        Assert.Equal(expected, service.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData("C")]
    [InlineData("V")]
    public void RejectsAnOrderExceedingTheLimitFromZero(string side)
    {
        var service = new ExposureService();

        var response = service.Process(Order(side, 10_001, 100m));

        Assert.Equal(OrderProcessingStatus.ExposureLimitExceeded, response.Status);
        Assert.Equal(0m, response.Exposure);
        Assert.NotEmpty(response.ErrorMessage);
        Assert.Equal(0m, service.GetExposure("PETR4"));
    }

    [Theory]
    [InlineData("C", "V")]
    [InlineData("V", "C")]
    public void AcceptsOrdersThatReduceExposureAtTheLimit(string initialSide, string reducingSide)
    {
        var service = new ExposureService();
        Assert.Equal(OrderProcessingStatus.Accepted, service.Process(Order(initialSide, 10_000, 100m)).Status);
        var expected = initialSide == "C" ? 999_999.99m : -999_999.99m;

        var response = service.Process(Order(reducingSide, 1, 0.01m));

        Assert.Equal(OrderProcessingStatus.Accepted, response.Status);
        Assert.Equal(expected, response.Exposure);
        Assert.Equal(expected, service.GetExposure("PETR4"));
    }

    [Fact]
    public void KeepsAssetsIndependentOnAcceptanceAndRejection()
    {
        var service = new ExposureService();

        Assert.Equal(OrderProcessingStatus.Accepted, service.Process(Order("C", 10_000, 100m, "PETR4")).Status);
        Assert.Equal(OrderProcessingStatus.Accepted, service.Process(Order("V", 10_000, 100m, "VALE3")).Status);
        Assert.Equal(OrderProcessingStatus.Accepted, service.Process(Order("C", 1, 54.87m, "VIIA4")).Status);
        var rejected = service.Process(Order("C", 1, 0.01m, "PETR4"));

        Assert.Equal(OrderProcessingStatus.ExposureLimitExceeded, rejected.Status);
        Assert.Equal(1_000_000m, rejected.Exposure);
        Assert.Equal(1_000_000m, service.GetExposure("PETR4"));
        Assert.Equal(-1_000_000m, service.GetExposure("VALE3"));
        Assert.Equal(54.87m, service.GetExposure("VIIA4"));

        var reduced = service.Process(Order("V", 1, 10m, "PETR4"));
        Assert.Equal(OrderProcessingStatus.Accepted, reduced.Status);
        Assert.Equal(999_990m, service.GetExposure("PETR4"));
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
        Assert.Equal(OrderProcessingStatus.Accepted, service.Process(Order("C", 1, 54.87m)).Status);

        var response = service.Process(order);

        Assert.Equal(OrderProcessingStatus.InvalidInput, response.Status);
        Assert.NotEmpty(response.ErrorMessage);
        Assert.Equal(order?.Ativo == "PETR4" ? 54.87m : 0m, response.Exposure);
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
        var accepted = responses.Where(response => response.Status == OrderProcessingStatus.Accepted).ToArray();
        var rejected = responses.Where(response => response.Status != OrderProcessingStatus.Accepted).ToArray();

        Assert.Equal(10, accepted.Length);
        Assert.Equal(10, rejected.Length);
        Assert.Equal(expectedExposure, service.GetExposure("PETR4"));

        // Compare the values as a set of sorted results, not by task order.
        var expectedAcceptedExposures = Enumerable.Range(1, 10)
            .Select(index => index * expectedExposure / 10)
            .OrderBy(exposure => exposure);
        Assert.Equal(expectedAcceptedExposures,
            accepted.Select(response => response.Exposure).OrderBy(exposure => exposure));
        Assert.All(accepted, response => Assert.Equal("", response.ErrorMessage));
        Assert.All(rejected, response =>
        {
            Assert.Equal(OrderProcessingStatus.ExposureLimitExceeded, response.Status);
            Assert.Equal(expectedExposure, response.Exposure);
            Assert.Equal("A ordem ultrapassa o limite de exposição do ativo PETR4.", response.ErrorMessage);
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
