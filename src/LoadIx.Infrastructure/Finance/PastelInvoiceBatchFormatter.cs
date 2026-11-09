using System.Globalization;
using System.Text;
using LoadIx.Application.Finance;

namespace LoadIx.Infrastructure.Finance;

public static class PastelInvoiceBatchFormatter
{
    public static byte[] Format(InvoiceBatchDetail batch)
    {
        var builder = new StringBuilder();
        // Sage Pastel Partner / Evolution Customer Document Batch Import Format
        builder.AppendLine("CustomerCode,DocumentDate,OrderNumber,DocumentNumber,Inclusive,LineType,ItemCode,Description,Quantity,UnitPrice,Discount,TaxType");

        var docDate = DateTime.UtcNow.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        foreach (var line in batch.Lines)
        {
            var customerCode = Escape(batch.CustomerName.Length > 8 ? batch.CustomerName[..8].Trim().ToUpperInvariant() : batch.CustomerName.Trim().ToUpperInvariant());
            var orderNumber = Escape(batch.ContractNumber);
            var docNumber = Escape(batch.BatchNumber);
            var inclusive = "N"; // Exclusive pricing
            var lineType = "4"; // Inventory / Service line
            var itemCode = "FREIGHT";
            var description = Escape($"Trip {line.TripNumber} - {line.BillableTonnes:0.000} tonnes");
            var quantity = line.BillableTonnes.ToString("0.000", CultureInfo.InvariantCulture);
            var unitPrice = line.BillableTonnes > 0
                ? (line.TaxExclusiveAmount / line.BillableTonnes).ToString("0.00", CultureInfo.InvariantCulture)
                : line.TaxExclusiveAmount.ToString("0.00", CultureInfo.InvariantCulture);
            var discount = "0.00";
            // Pastel Tax Type: 01 standard rated 15%, 00 exempt/zero-rated
            var taxType = line.TaxAmount > 0 ? "01" : "00";

            builder.AppendLine(CultureInfo.InvariantCulture, $"{customerCode},{docDate},{orderNumber},{docNumber},{inclusive},{lineType},{itemCode},{description},{quantity},{unitPrice},{discount},{taxType}");
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
