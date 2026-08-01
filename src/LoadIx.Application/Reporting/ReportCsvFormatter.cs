using System.Globalization;

namespace LoadIx.Application.Reporting;

public static class ReportCsvFormatter
{
    public static string FormatCell(object? value) => value switch
    {
        null => string.Empty,
        DateTimeOffset dateTime => dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty,
    };

    public static string HumanizeHeader(string name)
    {
        var result = new global::System.Text.StringBuilder(name.Length + 8);
        foreach (var character in name)
        {
            if (result.Length > 0 && char.IsUpper(character)) result.Append(' ');
            result.Append(character);
        }
        return result.ToString();
    }

    public static string Escape(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '*') value = "'" + value;
        return value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\r', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;
    }
}
