using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OrderAccumulator.Contracts;
using OrderAccumulator.Services;
using OrderAccumulator.Validation;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace OrderAccumulator.Endpoints;

public static class OrderEndpoints
{
    public static RouteHandlerBuilder MapOrderEndpoints(this IEndpointRouteBuilder app) => app
        .MapPost("/api/ordens", ProcessAsync)
        .WithName("ProcessOrder")
        .WithSummary("Processa uma ordem e retorna a exposição financeira do ativo.")
        .AddOpenApiOperationTransformer(async (operation, context, cancellationToken) =>
        {
            var schema = await context.GetOrCreateSchemaAsync(typeof(OrderRequest),
                cancellationToken: cancellationToken);
            context.Document?.AddComponent(nameof(OrderRequest), schema);
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new()
                    {
                        Schema = new OpenApiSchemaReference(nameof(OrderRequest), context.Document),
                    },
                },
            };
        })
        .Produces<OrderResponse>(StatusCodes.Status200OK)
        .Produces<OrderResponse>(StatusCodes.Status400BadRequest)
        .Produces<OrderResponse>(StatusCodes.Status422UnprocessableEntity);

    private static async Task<IResult> ProcessAsync(HttpRequest request, ExposureService service,
        IOptions<JsonOptions> options, CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return RejectInvalidRequest(service, null,
                "Content-Type deve ser application/json.");
        }

        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return RejectInvalidRequest(service, null,
                "O corpo deve conter um JSON válido.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return RejectInvalidRequest(service, null,
                    "O corpo deve ser um objeto JSON com os campos da ordem.");
            }

            OrderRequest? order;
            try
            {
                order = root.Deserialize<OrderRequest>(options.Value.SerializerOptions);
            }
            catch (JsonException)
            {
                var asset = root.TryGetProperty("ativo", out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
                return RejectInvalidRequest(service, asset,
                    "Tipos inválidos: ativo e lado devem ser textos, quantidade um inteiro e preco um número decimal.");
            }

            if (root.TryGetProperty("preco", out var price) && price.ValueKind == JsonValueKind.Number
                && !JsonPriceValidator.IsWholeCent(price))
            {
                return RejectInvalidRequest(service, order?.Ativo,
                    "Preço deve ser um múltiplo de 0,01 entre 0,01 e 999,99.");
            }

            return ToHttpResult(service.Process(order));
        }
    }

    private static IResult RejectInvalidRequest(ExposureService service, string? asset, string message) =>
        Results.BadRequest(new OrderResponse(false, service.FindExposure(asset) ?? 0m, message));

    private static IResult ToHttpResult(OrderProcessingResult result)
    {
        var response = new OrderResponse(result.Status == OrderProcessingStatus.Accepted,
            result.Exposure, result.ErrorMessage);

        return result.Status switch
        {
            OrderProcessingStatus.Accepted => Results.Ok(response),
            OrderProcessingStatus.InvalidInput => Results.BadRequest(response),
            OrderProcessingStatus.ExposureLimitExceeded => Results.UnprocessableEntity(response),
            _ => throw new InvalidOperationException($"Resultado de processamento desconhecido: {result.Status}."),
        };
    }
}
