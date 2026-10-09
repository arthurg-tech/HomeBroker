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
            return Results.BadRequest(service.RejectInvalidRequest(null,
                "Content-Type deve ser application/json."));
        }

        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest(service.RejectInvalidRequest(null,
                "O corpo deve conter um JSON válido."));
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Results.BadRequest(service.RejectInvalidRequest(null,
                    "O corpo deve ser um objeto JSON com os campos da ordem."));
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
                return Results.BadRequest(service.RejectInvalidRequest(asset,
                    "Tipos inválidos: ativo e lado devem ser textos, quantidade um inteiro e preco um número decimal."));
            }

            var response = service.Process(order);
            if (response.Sucesso)
            {
                return Results.Ok(response);
            }

            return OrderRequestValidator.Validate(order).Count > 0
                ? Results.BadRequest(response)
                : Results.UnprocessableEntity(response);
        }
    }
}
