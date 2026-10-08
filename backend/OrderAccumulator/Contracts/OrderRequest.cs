using System.Text.Json.Serialization;

namespace OrderAccumulator.Contracts;

public sealed record OrderRequest
{
    [JsonPropertyName("ativo")]
    public string? Ativo { get; init; }

    [JsonPropertyName("lado")]
    public string? Lado { get; init; }

    [JsonPropertyName("quantidade")]
    public int? Quantidade { get; init; }

    [JsonPropertyName("preco")]
    public decimal? Preco { get; init; }
}
