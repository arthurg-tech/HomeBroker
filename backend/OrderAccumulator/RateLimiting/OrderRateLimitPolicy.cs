using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using OrderAccumulator.Contracts;

namespace OrderAccumulator.RateLimiting;

public sealed class OrderRateLimitPolicy(IOptions<OrderRateLimitOptions> options) : IRateLimiterPolicy<string>
{
    public const string PolicyName = "Orders";

    private readonly OrderRateLimitOptions _options = options.Value;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        // IPv4 e IPv4 mapeado em IPv6 compartilham a mesma cota.
        // Sem IP identificado, as requisições compartilham uma cota de fallback.
        var clientIp = httpContext.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown";

        return RateLimitPartition.GetSlidingWindowLimiter(clientIp, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = _options.PermitLimit,
            Window = TimeSpan.FromSeconds(_options.WindowSeconds),
            SegmentsPerWindow = _options.SegmentsPerWindow,
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    }

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } =
        static async (context, cancellationToken) =>
        {
            var response = context.HttpContext.Response;
            response.StatusCode = StatusCodes.Status429TooManyRequests;

            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                response.Headers.RetryAfter = Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds))
                    .ToString(CultureInfo.InvariantCulture);
            }

            // O corpo não foi lido; zero é uma convenção de erro, não uma exposição consultada.
            await response.WriteAsJsonAsync(new OrderResponse(false, 0m,
                "Limite de requisições excedido. Aguarde antes de enviar outra ordem."),
                cancellationToken: cancellationToken);
        };
}
