using System.Globalization;
using System.Text;
using LoadIx.Domain.Finance;

namespace LoadIx.Infrastructure.Finance;

public static class PastelPayrollBatchFormatter
{
    public static byte[] Format(PayrollBatch batch)
    {
        var builder = new StringBuilder();
        // Sage Pastel Payroll ASCII/CSV Transaction Import Header
        builder.AppendLine("EmployeeCode,TransactionCode,Amount,Period,Reference,Description,BalanceRemaining");

        var periodFormatted = $"{batch.PeriodStartDate:yyyy/MM}";

        foreach (var line in batch.Lines)
        {
            var empCode = Escape(line.DriverEmployeeNumber ?? line.DriverName);
            var transCode = Escape(line.TransactionCode);
            var amount = line.Amount.ToString("0.00", CultureInfo.InvariantCulture);
            var reference = Escape(line.PayrollBatchId.ToString()[..8].ToUpperInvariant());
            var description = Escape(line.Description);
            var balance = line.BalanceAfterDeduction.ToString("0.00", CultureInfo.InvariantCulture);

            builder.AppendLine(CultureInfo.InvariantCulture, $"{empCode},{transCode},{amount},{periodFormatted},{reference},{description},{balance}");
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public static byte[] FormatGenericCsv(PayrollBatch batch)
    {
        var builder = new StringBuilder();
        builder.AppendLine("BatchNumber,PeriodStart,PeriodEnd,DriverName,EmployeeNumber,TransactionCode,Amount,BalanceAfterDeduction,Description");

        foreach (var line in batch.Lines)
        {
            builder.AppendLine(string.Join(',',
                Escape(batch.BatchNumber),
                batch.PeriodStartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                batch.PeriodEndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Escape(line.DriverName),
                Escape(line.DriverEmployeeNumber ?? string.Empty),
                Escape(line.TransactionCode),
                line.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                line.BalanceAfterDeduction.ToString("0.00", CultureInfo.InvariantCulture),
                Escape(line.Description)));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
