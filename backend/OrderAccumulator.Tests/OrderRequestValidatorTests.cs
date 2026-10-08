using System.Text.Json;
using OrderAccumulator.Contracts;
using OrderAccumulator.Validation;

namespace OrderAccumulator.Tests;

public class OrderRequestValidatorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(99_999)]
    public void AcceptsQuantityAtInclusiveBoundaries(int quantity)
    {
        var errors = OrderRequestValidator.Validate(ValidOrder() with { Quantidade = quantity });

        Assert.DoesNotContain(errors, error => error.StartsWith("Quantidade", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100_000)]
    public void RejectsQuantityOutsideRange(int quantity)
    {
        var errors = OrderRequestValidator.Validate(ValidOrder() with { Quantidade = quantity });

        Assert.Contains(errors, error => error.StartsWith("Quantidade", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(10)]
    [InlineData(999.99)]
    public void AcceptsPriceAtBoundariesAndWholeCentMultiples(decimal price)
    {
        var errors = OrderRequestValidator.Validate(ValidOrder() with { Preco = price });

        Assert.DoesNotContain(errors, error => error.StartsWith("Preço", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(10.001)]
    [InlineData(-0.01)]
    public void RejectsPriceOutsideRangeOrNotWholeCent(decimal price)
    {
        var errors = OrderRequestValidator.Validate(ValidOrder() with { Preco = price });

        Assert.Contains(errors, error => error.StartsWith("Preço", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("PETR4")]
    [InlineData("VALE3")]
    [InlineData("VIIA4")]
    public void AcceptsOnlyDefinedAssets(string asset)
    {
        var errors = OrderRequestValidator.Validate(ValidOrder() with { Ativo = asset });

        Assert.DoesNotContain(errors, error => error.StartsWith("Ativo", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("petr4")]
    [InlineData("ABEV3")]
    [InlineData("")]
    public void RejectsUnknownOrIncorrectlyCasedAssets(string? asset)
    {
        var errors = OrderRequestValidator.Validate(ValidOrder() with { Ativo = asset });

        Assert.Contains(errors, error => error.StartsWith("Ativo", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("C")]
    [InlineData("V")]
    public void AcceptsContractSideCodes(string side)
    {
        var errors = OrderRequestValidator.Validate(ValidOrder() with { Lado = side });

        Assert.DoesNotContain(errors, error => error.StartsWith("Lado", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Compra")]
    [InlineData("Venda")]
    [InlineData("c")]
    [InlineData("")]
    public void RejectsLabelsAndUnknownSideCodes(string? side)
    {
        var errors = OrderRequestValidator.Validate(ValidOrder() with { Lado = side });

        Assert.Contains(errors, error => error.StartsWith("Lado", StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsMissingValuesInsteadOfTreatingThemAsDefaults()
    {
        var errors = OrderRequestValidator.Validate(new OrderRequest());

        Assert.Equal(4, errors.Count);
    }

    [Fact]
    public void RejectsMissingRequestBody()
    {
        Assert.NotEmpty(OrderRequestValidator.Validate(null));
    }

    [Fact]
    public void UsesTheDefinedJsonContractWithNumericFields()
    {
        var json = JsonSerializer.Serialize(ValidOrder());
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("PETR4", root.GetProperty("ativo").GetString());
        Assert.Equal("C", root.GetProperty("lado").GetString());
        Assert.Equal(JsonValueKind.Number, root.GetProperty("quantidade").ValueKind);
        Assert.Equal(JsonValueKind.Number, root.GetProperty("preco").ValueKind);
        Assert.Equal(584, root.GetProperty("quantidade").GetInt32());
        Assert.Equal(54.87m, root.GetProperty("preco").GetDecimal());

        var responseJson = JsonSerializer.Serialize(new OrderResponse(true, 32_043.08m, ""));
        using var responseDocument = JsonDocument.Parse(responseJson);
        var response = responseDocument.RootElement;

        Assert.True(response.GetProperty("sucesso").GetBoolean());
        Assert.Equal(JsonValueKind.Number, response.GetProperty("exposicao_atual").ValueKind);
        Assert.Equal(32_043.08m, response.GetProperty("exposicao_atual").GetDecimal());
        Assert.Equal("", response.GetProperty("msg_erro").GetString());
    }

    private static OrderRequest ValidOrder() => new()
    {
        Ativo = "PETR4",
        Lado = "C",
        Quantidade = 584,
        Preco = 54.87m,
    };
}
