using OrderAccumulator.Contracts;

namespace OrderAccumulator.Validation;

public static class OrderRequestValidator
{
    private static readonly HashSet<string> AtivosPermitidos = ["PETR4", "VALE3", "VIIA4"];
    private static readonly HashSet<string> LadosPermitidos = ["C", "V"];

    public static IReadOnlyList<string> Validate(OrderRequest? order)
    {
        if (order is null)
        {
            return ["O corpo da ordem é obrigatório."];
        }

        var errors = new List<string>();

        if (order.Ativo is null || !AtivosPermitidos.Contains(order.Ativo))
        {
            errors.Add("Ativo deve ser PETR4, VALE3 ou VIIA4.");
        }

        if (order.Lado is null || !LadosPermitidos.Contains(order.Lado))
        {
            errors.Add("Lado deve ser C (compra) ou V (venda).");
        }

        if (order.Quantidade is null or < 1 or > 99_999)
        {
            errors.Add("Quantidade deve ser um inteiro entre 1 e 99.999.");
        }

        if (order.Preco is null or < 0.01m or > 999.99m || order.Preco % 0.01m != 0)
        {
            errors.Add("Preço deve ser um múltiplo de 0,01 entre 0,01 e 999,99.");
        }

        return errors;
    }
}
