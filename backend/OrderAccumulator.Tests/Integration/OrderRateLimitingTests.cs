using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OrderAccumulator.Contracts;
using OrderAccumulator.RateLimiting;
using OrderAccumulator.Services;

namespace OrderAccumulator.Tests.Integration;

public class OrderRateLimitingTests
{
    [Fact]
    public async Task RejectsOrdersAboveQuotaWithContractHeadersAndNoExposureChange()
    {
        await using var factory = new RateLimitedApiFactory(permitLimit: 3);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");

        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/ordens");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");
        using var preflightResponse = await client.SendAsync(preflight);
        Assert.Equal(HttpStatusCode.NoContent, preflightResponse.StatusCode);

        foreach (var (asset, side, price) in new[] { ("PETR4", "C", 10m), ("VALE3", "V", 20m), ("VIIA4", "C", 30m) })
        {
            using var accepted = await client.PostAsJsonAsync("/api/ordens",
                new { ativo = asset, lado = side, quantidade = 1, preco = price });
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        using var rejected = await client.PostAsJsonAsync("/api/ordens",
            new { ativo = "PETR4", lado = "C", quantidade = 1, preco = 100m });

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new OrderResponse(false, 0m,
            "Limite de requisições excedido. Aguarde antes de enviar outra ordem."),
            await rejected.Content.ReadFromJsonAsync<OrderResponse>());

        if (rejected.Headers.Contains("Retry-After"))
        {
            Assert.NotNull(rejected.Headers.RetryAfter?.Delta);
            Assert.True(rejected.Headers.RetryAfter!.Delta > TimeSpan.Zero);
        }
        Assert.Equal("http://localhost:5173", Assert.Single(rejected.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("Retry-After", rejected.Headers.GetValues("Access-Control-Expose-Headers"));

        var service = factory.Services.GetRequiredService<ExposureService>();
        Assert.Equal(10m, service.GetExposure("PETR4"));
        Assert.Equal(-20m, service.GetExposure("VALE3"));
        Assert.Equal(30m, service.GetExposure("VIIA4"));
    }

    [Fact]
    public async Task KeepsQuotasIndependentForDifferentClientIps()
    {
        await using var factory = new RateLimitedApiFactory(permitLimit: 1);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var firstIp = IPAddress.Parse("192.0.2.1");
        var secondIp = IPAddress.Parse("192.0.2.2");

        Assert.Equal(StatusCodes.Status200OK, await SendOrderFromIp(factory, firstIp, timeout.Token));
        Assert.Equal(StatusCodes.Status429TooManyRequests, await SendOrderFromIp(factory, firstIp, timeout.Token));
        Assert.Equal(StatusCodes.Status200OK, await SendOrderFromIp(factory, secondIp, timeout.Token));
        Assert.Equal(StatusCodes.Status429TooManyRequests, await SendOrderFromIp(factory, secondIp, timeout.Token));

        Assert.Equal(20m, factory.Services.GetRequiredService<ExposureService>().GetExposure("PETR4"));
    }

    [Fact]
    public async Task AcceptsOrdersAgainAfterWindowExpires()
    {
        await using var factory = new RateLimitedApiFactory(permitLimit: 1, windowSeconds: 2);
        using var client = factory.CreateClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var order = new { ativo = "PETR4", lado = "C", quantidade = 1, preco = 10m };

        using var accepted = await client.PostAsJsonAsync("/api/ordens", order, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var rejected = await client.PostAsJsonAsync("/api/ordens", order, timeout.Token);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);

        while (true)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
            using var retry = await client.PostAsJsonAsync("/api/ordens", order, timeout.Token);
            if (retry.StatusCode == HttpStatusCode.OK)
            {
                Assert.Equal(new OrderResponse(true, 20m, ""),
                    await retry.Content.ReadFromJsonAsync<OrderResponse>(timeout.Token));
                break;
            }

            Assert.Equal(HttpStatusCode.TooManyRequests, retry.StatusCode);
        }
    }

    private static async Task<int> SendOrderFromIp(RateLimitedApiFactory factory, IPAddress clientIp,
        CancellationToken cancellationToken)
    {
        using var body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(
            new { ativo = "PETR4", lado = "C", quantidade = 1, preco = 10m }));

        var context = await factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = clientIp;
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = "/api/ordens";
            context.Request.ContentType = "application/json";
            context.Request.Body = body;
        }, cancellationToken);

        using var responseBody = context.Response.Body;
        await responseBody.CopyToAsync(Stream.Null, cancellationToken);
        return context.Response.StatusCode;
    }

    private sealed class RateLimitedApiFactory(int permitLimit, int windowSeconds = 60) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services => services.PostConfigure<OrderRateLimitOptions>(options =>
            {
                options.PermitLimit = permitLimit;
                options.WindowSeconds = windowSeconds;
                options.SegmentsPerWindow = 1;
            }));
        }
    }
}
