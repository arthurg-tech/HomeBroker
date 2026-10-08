using System.Text.Json.Serialization;

namespace OrderAccumulator.Contracts;

public sealed record OrderResponse(
    [property: JsonPropertyName("sucesso")] bool Sucesso,
    [property: JsonPropertyName("exposicao_atual")] decimal ExposicaoAtual,
    [property: JsonPropertyName("msg_erro")] string MsgErro);
