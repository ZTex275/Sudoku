namespace Sudoku.Core;

public static class DigitSymbols
{
    public static string Of(int value)
    {
        if (value <= 0)
            return string.Empty;
        if (value <= 9)
            return value.ToString();
        return ((char)('A' + value - 10)).ToString();
    }

    public static int Parse(string? symbol, int size)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return 0;
        var ch = char.ToUpperInvariant(symbol.Trim()[0]);
        if (ch is >= '1' and <= '9')
        {
            var v = ch - '0';
            return v <= size ? v : 0;
        }
        if (ch is >= 'A' and <= 'G')
        {
            var v = ch - 'A' + 10;
            return v <= size ? v : 0;
        }
        return 0;
    }
}
