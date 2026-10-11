using System.Text.Json;

namespace OrderAccumulator.Endpoints;

internal static class OrderRequestBodyReader
{
    public const int MaxBodySizeBytes = 1_000;

    // null indica tamanho excedido; JSON inválido mantém o tratamento de JsonException do endpoint.
    public static async Task<JsonDocument?> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxBodySizeBytes)
        {
            return null;
        }

        var buffer = new byte[MaxBodySizeBytes + 1];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await request.Body.ReadAsync(buffer.AsMemory(length), cancellationToken);
            if (read == 0)
            {
                return JsonDocument.Parse(buffer.AsMemory(0, length));
            }

            length += read;
        }

        return null;
    }
}
