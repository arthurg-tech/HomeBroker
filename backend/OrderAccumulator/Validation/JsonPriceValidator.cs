using System.Globalization;
using System.Text.Json;

namespace OrderAccumulator.Validation;

internal static class JsonPriceValidator
{
    public static bool IsWholeCent(JsonElement price)
    {
        if (price.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        var number = price.GetRawText().AsSpan();
        var exponentIndex = number.IndexOfAny('e', 'E');
        var exponent = 0;
        if (exponentIndex >= 0 && !int.TryParse(number[(exponentIndex + 1)..],
                NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
        {
            return false;
        }

        var mantissa = exponentIndex >= 0 ? number[..exponentIndex] : number;
        var decimalPoint = mantissa.IndexOf('.');
        var fractionalDigits = decimalPoint >= 0 ? mantissa.Length - decimalPoint - 1 : 0;

        // Ao multiplicar o valor exato por 100, só pode haver zeros nos
        // dígitos que ainda ficariam à direita do ponto decimal.
        var digitsToDiscard = (long)fractionalDigits - exponent - 2;
        for (var index = mantissa.Length - 1; index >= 0 && digitsToDiscard > 0; index--)
        {
            var digit = mantissa[index];
            if (digit is '.' or '-')
            {
                continue;
            }

            if (digit != '0')
            {
                return false;
            }

            digitsToDiscard--;
        }

        return true;
    }
}
