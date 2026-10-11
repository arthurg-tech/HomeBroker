using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OrderAccumulator.Tests.Integration;

public class OrderApiTests
{
    [Fact]
    public async Task SuccessfulOrdersShareStateAcrossRequestsAndClients()
    {
        await using var factory = new OrderApiFactory();
        using var buyer = factory.CreateClient();
        using var seller = factory.CreateClient();

        using var purchase = await buyer.PostAsJsonAsync("/api/ordens",
            new { ativo = "PETR4", lado = "C", quantidade = 584, preco = 54.87m });
        await AssertResponse(purchase, HttpStatusCode.OK, true, 32_044.08m);

        using var sale = await seller.PostAsJsonAsync("/api/ordens",
            new { ativo = "PETR4", lado = "V", quantidade = 100, preco = 50m });
        await AssertResponse(sale, HttpStatusCode.OK, true, 27_044.08m);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(99_999)]
    public async Task AcceptsQuantityBoundariesWithTheMinimumPrice(int quantity)
    {
        await using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/ordens",
            new { ativo = "PETR4", lado = "C", quantidade = quantity, preco = 0.01m });

        await AssertResponse(response, HttpStatusCode.OK, true, quantity * 0.01m);
    }

    [Fact]
    public async Task ValidMaximumFieldsAreRejectedByTheLimitWithoutChangingAnyAsset()
    {
        await using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        using var rejected = await client.PostAsJsonAsync("/api/ordens",
            new { ativo = "PETR4", lado = "C", quantidade = 99_999, preco = 999.99m });
        await AssertResponse(rejected, HttpStatusCode.UnprocessableEntity, false, 0m);

        foreach (var asset in new[] { "PETR4", "VALE3", "VIIA4" })
        {
            using var probe = await client.PostAsJsonAsync("/api/ordens",
                new { ativo = asset, lado = "C", quantidade = 1, preco = 0.01m });
            await AssertResponse(probe, HttpStatusCode.OK, true, 0.01m);
        }
    }

    [Fact]
    public async Task AcceptedAndRejectedOrdersKeepTheOtherAssetsIndependent()
    {
        await using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();
        await SeedExposure(client);

        using var purchase = await client.PostAsJsonAsync("/api/ordens",
            new { ativo = "PETR4", lado = "C", quantidade = 1, preco = 10m });
        await AssertResponse(purchase, HttpStatusCode.OK, true, 64.87m);

        using var rejected = await client.PostAsJsonAsync("/api/ordens",
            new { ativo = "PETR4", lado = "C", quantidade = 99_999, preco = 999.99m });
        await AssertResponse(rejected, HttpStatusCode.UnprocessableEntity, false, 64.87m);
        await AssertExposureWasPreserved(client, 64.87m);
    }

    [Theory]
    [InlineData("C")]
    [InlineData("V")]
    public async Task ConcurrentHttpOrdersShareOneExposureAndRespectBothLimits(string side)
    {
        await using var factory = new OrderApiFactory();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expectedLimit = side == "C" ? 1_000_000m : -1_000_000m;

        // Todas as solicitações utilizam o mesmo host; dois clientes também seguem o ciclo de vida Singleton.
        var tasks = Enumerable.Range(0, 20).Select(async index =>
        {
            await start.Task;
            var client = index % 2 == 0 ? firstClient : secondClient;
            using var response = await client.PostAsJsonAsync("/api/ordens",
                new { ativo = "PETR4", lado = side, quantidade = 1_000, preco = 100m });
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var exposure = document.RootElement.GetProperty("exposicao_atual").GetDecimal();
            var accepted = response.StatusCode == HttpStatusCode.OK;
            await AssertResponse(response,
                accepted ? HttpStatusCode.OK : HttpStatusCode.UnprocessableEntity,
                accepted, accepted ? exposure : expectedLimit);
            return (Accepted: accepted, Exposure: exposure);
        }).ToArray();

        start.SetResult();
        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(10, results.Count(result => result.Accepted));
        Assert.Equal(10, results.Count(result => !result.Accepted));
        Assert.Equal(Enumerable.Range(1, 10).Select(index => index * expectedLimit / 10).Order(),
            results.Where(result => result.Accepted).Select(result => result.Exposure).Order());

        // Uma nova solicitação observa o estado persistido final, inclusive após cada rejeição.
        using var reduced = await firstClient.PostAsJsonAsync("/api/ordens",
            new { ativo = "PETR4", lado = side == "C" ? "V" : "C", quantidade = 1, preco = 0.01m });
        await AssertResponse(reduced, HttpStatusCode.OK, true,
            side == "C" ? 999_999.99m : -999_999.99m);
    }

    [Theory]
    [InlineData("C")]
    [InlineData("V")]
    public async Task LimitRejectionUses422AndPreservesTheExposure(string side)
    {
        await using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();
        var limit = side == "C" ? 1_000_000m : -1_000_000m;

        using var atLimit = await client.PostAsJsonAsync("/api/ordens",
            new { ativo = "PETR4", lado = side, quantidade = 10_000, preco = 100m });
        await AssertResponse(atLimit, HttpStatusCode.OK, true, limit);

        using var rejected = await client.PostAsJsonAsync("/api/ordens",
            new { ativo = "PETR4", lado = side, quantidade = 1, preco = 0.01m });
        await AssertResponse(rejected, HttpStatusCode.UnprocessableEntity, false, limit);

        var opposite = side == "C" ? "V" : "C";
        using var reduced = await client.PostAsJsonAsync("/api/ordens",
            new { ativo = "PETR4", lado = opposite, quantidade = 1, preco = 0.01m });
        await AssertResponse(reduced, HttpStatusCode.OK, true,
            side == "C" ? 999_999.99m : -999_999.99m);
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("10")]
    [InlineData("10.000")]
    [InlineData("999.99")]
    [InlineData("1e-2")]
    public async Task AcceptsPricesByNumericValueInsteadOfWrittenDecimalPlaces(string price)
    {
        await using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();
        using var response = await PostJson(client,
            $$"""{"ativo":"PETR4","lado":"C","quantidade":1,"preco":{{price}}}""");

        await AssertResponse(response, HttpStatusCode.OK, true,
            decimal.Parse(price, NumberStyles.Float, CultureInfo.InvariantCulture));
    }

    public static TheoryData<string> InvalidFields => new()
    {
        """ "lado":"Compra","quantidade":1,"preco":1 """,
        """ "lado":"c","quantidade":1,"preco":1 """,
        """ "lado":"","quantidade":1,"preco":1 """,
        """ "lado":true,"quantidade":1,"preco":1 """,
        """ "lado":null,"quantidade":1,"preco":1 """,
        """ "quantidade":1,"preco":1 """,
        """ "lado":"C","quantidade":"1","preco":1 """,
        """ "lado":"C","quantidade":1.5,"preco":1 """,
        """ "lado":"C","quantidade":true,"preco":1 """,
        """ "lado":"C","quantidade":null,"preco":1 """,
        """ "lado":"C","quantidade":{},"preco":1 """,
        """ "lado":"C","quantidade":0,"preco":1 """,
        """ "lado":"C","quantidade":-1,"preco":1 """,
        """ "lado":"C","quantidade":100000,"preco":1 """,
        """ "lado":"C","quantidade":2147483648,"preco":1 """,
        """ "lado":"C","preco":1 """,
        """ "lado":"C","quantidade":1,"preco":"1.00" """,
        """ "lado":"C","quantidade":1,"preco":true """,
        """ "lado":"C","quantidade":1,"preco":null """,
        """ "lado":"C","quantidade":1,"preco":[] """,
        """ "lado":"C","quantidade":1,"preco":0 """,
        """ "lado":"C","quantidade":1,"preco":-0.01 """,
        """ "lado":"C","quantidade":1,"preco":1000 """,
        """ "lado":"C","quantidade":1,"preco":10.001 """,
        """ "lado":"C","quantidade":1,"preco":1e100 """,
        """ "lado":"C","quantidade":1 """,
    };

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public async Task InvalidFieldsUse400AndReturnTheIdentifiedAssetsExposure(string fields)
    {
        await using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();
        await SeedExposure(client);

        using var response = await PostJson(client, $$"""{"ativo":"PETR4",{{fields}}}""");
        await AssertResponse(response, HttpStatusCode.BadRequest, false, 54.87m);
        await AssertExposureWasPreserved(client);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("{\"ativo\":\"PETR4\",")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("""{"lado":"C","quantidade":1,"preco":1}""")]
    [InlineData("""{"ativo":"","lado":"C","quantidade":1,"preco":1}""")]
    [InlineData("""{"ativo":"petr4","lado":"C","quantidade":1,"preco":1}""")]
    [InlineData("""{"ativo":"ABEV3","lado":"C","quantidade":1,"preco":1}""")]
    [InlineData("""{"ativo":null,"lado":"C","quantidade":1,"preco":1}""")]
    [InlineData("""{"ativo":true,"lado":"C","quantidade":1,"preco":1}""")]
    [InlineData("""{"ativo":[],"lado":"C","quantidade":1,"preco":1}""")]
    [InlineData("""{"ativo":"ABEV3","lado":"C","quantidade":"1","preco":1}""")]
    public async Task MalformedBodiesOrUnidentifiableAssetsUseTheZeroErrorConvention(string json)
    {
        await using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();
        await SeedExposure(client);

        using var response = await PostJson(client, json);
        await AssertResponse(response, HttpStatusCode.BadRequest, false, 0m);
        await AssertExposureWasPreserved(client);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData(null)]
    public async Task NonJsonContentTypeUsesTheUniformErrorContract(string? contentType)
    {
        await using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();
        await SeedExposure(client);
        using var body = new StringContent(
            """{"ativo":"PETR4","lado":"C","quantidade":1,"preco":1}""", Encoding.UTF8);
        body.Headers.ContentType = contentType is null ? null : new(contentType);

        using var response = await client.PostAsync("/api/ordens", body);
        await AssertResponse(response, HttpStatusCode.BadRequest, false, 0m);
        await AssertExposureWasPreserved(client);
    }

    [Theory]
    [InlineData("http://localhost:5173", true)]
    [InlineData("http://localhost:5174", false)]
    [InlineData("https://example.com", false)]
    public async Task DevelopmentCorsAllowsOnlyTheConfiguredOrigin(string origin, bool allowed)
    {
        await using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/ordens");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        if (allowed)
        {
            Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Contains("POST", response.Headers.GetValues("Access-Control-Allow-Methods"));
        }
        else
        {
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }
    }

    [Fact]
    public async Task OpenApiDescribesTheOrderEndpointAndTheTemplateEndpointIsRemoved()
    {
        await using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        var post = paths.GetProperty("/api/ordens").GetProperty("post");

        Assert.Equal("ProcessOrder", post.GetProperty("operationId").GetString());
        Assert.True(post.GetProperty("requestBody").GetProperty("content").TryGetProperty("application/json", out _));
        foreach (var status in new[] { "200", "400", "422" })
        {
            Assert.True(post.GetProperty("responses").GetProperty(status)
                .GetProperty("content").TryGetProperty("application/json", out _));
        }

        Assert.False(paths.TryGetProperty("/weatherforecast", out _));
        using var removed = await client.GetAsync("/weatherforecast");
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostJson(HttpClient client, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PostAsync("/api/ordens", content);
    }

    private static async Task SeedExposure(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/ordens",
            new { ativo = "PETR4", lado = "C", quantidade = 1, preco = 54.87m });
        await AssertResponse(response, HttpStatusCode.OK, true, 54.87m);

        using var sale = await client.PostAsJsonAsync("/api/ordens",
            new { ativo = "VALE3", lado = "V", quantidade = 1, preco = 20m });
        await AssertResponse(sale, HttpStatusCode.OK, true, -20m);

        using var purchase = await client.PostAsJsonAsync("/api/ordens",
            new { ativo = "VIIA4", lado = "C", quantidade = 1, preco = 30m });
        await AssertResponse(purchase, HttpStatusCode.OK, true, 30m);
    }

    private static async Task AssertExposureWasPreserved(HttpClient client, decimal petrExposure = 54.87m)
    {
        foreach (var (asset, exposure) in new[] { ("PETR4", petrExposure), ("VALE3", -20m), ("VIIA4", 30m) })
        {
            using var response = await client.PostAsJsonAsync("/api/ordens",
                new { ativo = asset, lado = "C", quantidade = 1, preco = 0.01m });
            await AssertResponse(response, HttpStatusCode.OK, true, exposure + 0.01m);
        }
    }

    private static async Task AssertResponse(HttpResponseMessage response, HttpStatusCode status,
        bool success, decimal exposure)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(new[] { "exposicao_atual", "msg_erro", "sucesso" },
            root.EnumerateObject().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(success, root.GetProperty("sucesso").GetBoolean());
        Assert.Equal(JsonValueKind.Number, root.GetProperty("exposicao_atual").ValueKind);
        Assert.Equal(exposure, root.GetProperty("exposicao_atual").GetDecimal());
        var message = root.GetProperty("msg_erro").GetString();
        if (success)
        {
            Assert.Equal("", message);
        }
        else
        {
            Assert.False(string.IsNullOrWhiteSpace(message));
            Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class OrderApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");
    }
}
