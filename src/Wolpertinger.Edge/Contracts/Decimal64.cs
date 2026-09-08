using System.Globalization;

namespace Wolpertinger.Edge.Contracts;

public readonly record struct Decimal64(long Coefficient, sbyte Exponent10)
{
    public const sbyte MinExponent10 = -18;
    public const sbyte MaxExponent10 = 18;

    public static Decimal64 Parse(string lexical)
    {
        ArgumentNullException.ThrowIfNull(lexical);
        if (lexical.Length == 0)
        {
            throw new FormatException("Decimal64 input is empty.");
        }

        var chars = lexical.AsSpan();
        var digits = new char[chars.Length];
        var index = 0;
        var digitCount = 0;
        var fractionalDigits = 0;
        var negative = false;

        if (chars[index] is '+' or '-')
        {
            negative = chars[index++] == '-';
        }

        var integerDigits = ReadDigits(chars, ref index, digits, ref digitCount);
        if (integerDigits == 0)
        {
            throw new FormatException("Decimal64 requires digits before the decimal point.");
        }

        if (index < chars.Length && chars[index] == '.')
        {
            index++;
            var beforeFraction = digitCount;
            var fractionCount = ReadDigits(chars, ref index, digits, ref digitCount);
            if (fractionCount == 0)
            {
                throw new FormatException("Decimal64 requires digits after the decimal point.");
            }

            fractionalDigits = digitCount - beforeFraction;
        }

        var explicitExponent = 0;
        if (index < chars.Length && chars[index] is 'e' or 'E')
        {
            index++;
            explicitExponent = ReadExponent(chars, ref index);
        }

        if (index != chars.Length)
        {
            throw new FormatException("Decimal64 contains unsupported characters.");
        }

        var first = 0;
        while (first < digitCount && digits[first] == '0')
        {
            first++;
        }

        if (first == digitCount)
        {
            return new Decimal64(0, 0);
        }

        var last = digitCount - 1;
        var removedTrailingZeros = 0;
        while (last >= first && digits[last] == '0')
        {
            last--;
            removedTrailingZeros++;
        }

        var exponent = checked(explicitExponent - fractionalDigits + removedTrailingZeros);
        if (exponent is < MinExponent10 or > MaxExponent10)
        {
            throw new OverflowException("Decimal64 exponent is outside -18..18.");
        }

        var magnitude = ParseMagnitude(digits.AsSpan(first, last - first + 1), negative);
        var coefficient = ToSignedCoefficient(magnitude, negative);
        return new Decimal64(coefficient, (sbyte)exponent);
    }

    public override string ToString()
    {
        if (Coefficient == 0)
        {
            return "0";
        }

        var raw = Coefficient.ToString(CultureInfo.InvariantCulture);
        var negative = raw[0] == '-';
        var digits = negative ? raw[1..] : raw;
        var sign = negative ? "-" : string.Empty;

        if (Exponent10 >= 0)
        {
            return sign + digits + new string('0', Exponent10);
        }

        var decimalIndex = digits.Length + Exponent10;
        if (decimalIndex > 0)
        {
            return sign + digits[..decimalIndex] + "." + digits[decimalIndex..];
        }

        return sign + "0." + new string('0', -decimalIndex) + digits;
    }
    private static int ReadDigits(ReadOnlySpan<char> chars, ref int index, char[] destination, ref int count)
    {
        var start = count;
        while (index < chars.Length && chars[index] is >= '0' and <= '9')
        {
            destination[count++] = chars[index++];
        }

        return count - start;
    }

    private static int ReadExponent(ReadOnlySpan<char> chars, ref int index)
    {
        var negative = false;
        if (index < chars.Length && chars[index] is '+' or '-')
        {
            negative = chars[index++] == '-';
        }

        if (index >= chars.Length || chars[index] is < '0' or > '9')
        {
            throw new FormatException("Decimal64 exponent requires digits.");
        }

        var value = 0;
        while (index < chars.Length && chars[index] is >= '0' and <= '9')
        {
            var digit = chars[index++] - '0';
            if (value > 100_000)
            {
                throw new OverflowException("Decimal64 exponent is too large.");
            }

            value = checked((value * 10) + digit);
        }

        return negative ? -value : value;
    }

    private static ulong ParseMagnitude(ReadOnlySpan<char> digits, bool negative)
    {
        const ulong negativeLimit = 9_223_372_036_854_775_808UL;
        var limit = negative ? negativeLimit : (ulong)long.MaxValue;
        ulong magnitude = 0;

        foreach (var c in digits)
        {
            var digit = (uint)(c - '0');
            if (magnitude > (limit - digit) / 10)
            {
                throw new OverflowException("Decimal64 coefficient exceeds Int64.");
            }

            magnitude = (magnitude * 10) + digit;
        }

        return magnitude;
    }

    private static long ToSignedCoefficient(ulong magnitude, bool negative)
    {
        if (!negative)
        {
            return (long)magnitude;
        }

        return magnitude == 9_223_372_036_854_775_808UL
            ? long.MinValue
            : -(long)magnitude;
    }
}
