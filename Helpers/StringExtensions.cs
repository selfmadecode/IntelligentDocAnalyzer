using System.Globalization;

namespace IntelligentDocAnalyzer.Helpers;

public static class StringExtensions
{
    public static string NormalizeHeader(this string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var clean = text.Trim().ToLowerInvariant();

        if (clean.Contains("date"))
            return "date";

        if (clean.Contains("description") || clean.Contains("details") || clean.Contains("transaction"))
            return "description";

        if (clean.Contains("amount") || clean.Contains("debit") || clean.Contains("credit") || clean.Contains("payment"))
            return "amount";

        if (clean.Contains("balance"))
            return "balance";

        if (clean.Contains("merchant") || clean.Contains("payee"))
            return "merchant";

        return clean;
    }

    public static DateTime? TryParseDate(this string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Trim().Trim('\u200E', '\u200F');

        DateTime dt;
        string[] formats = { "M/d/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "M/d/yy", "MM/dd/yy" };

        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            return dt;

        if (DateTime.TryParse(text, out dt))
            return dt;

        return null;
    }

    public static decimal TryParseDecimal(this string? text)
    {
        var n = text.TryParseNullableDecimal();
        return n ?? 0m;
    }

    public static decimal? TryParseNullableDecimal(this string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var cleaned = text.Replace("$", string.Empty).Replace(",", string.Empty).Replace("(", "-").Replace(")", "").Trim();

        if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var val))
            return val;

        if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out val)) return val;
        return null;
    }
}
