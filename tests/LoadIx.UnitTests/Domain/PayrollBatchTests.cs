using System.Text;
using FluentAssertions;
using LoadIx.Application.Finance;
using LoadIx.Domain.Common;
using LoadIx.Domain.Finance;
using LoadIx.Infrastructure.Finance;

namespace LoadIx.UnitTests.Domain;

public sealed class PayrollBatchTests
{
    private static readonly TenantId Tenant =
        TenantId.From(Guid.Parse("70000000-0000-0000-0000-000000000001"));
    private static readonly Guid Driver1 = Guid.Parse("70000000-0000-0000-0000-000000000002");
    private static readonly Guid Driver2 = Guid.Parse("70000000-0000-0000-0000-000000000003");
    private static readonly Guid UserId = Guid.Parse("70000000-0000-0000-0000-000000000004");
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatesAndApprovesPayrollBatch()
    {
        var batchId = Guid.NewGuid();
        var batch = PayrollBatch.Create(
            batchId,
            Tenant,
            "PR-2026-08",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            PayrollFrequency.Monthly,
            "ZAR",
            Now).Value;

        var line1 = PayrollBatchLine.Create(
            Guid.NewGuid(),
            Tenant,
            batchId,
            Driver1,
            "EMP001",
            "John Ndlovu",
            Guid.NewGuid(),
            "FINE",
            "Speeding fine",
            500m,
            1000m).Value;

        var line2 = PayrollBatchLine.Create(
            Guid.NewGuid(),
            Tenant,
            batchId,
            Driver2,
            "EMP002",
            "Sipho Zulu",
            Guid.NewGuid(),
            "DMG",
            "Mirror repair",
            350m,
            0m).Value;

        batch.AddLine(line1).IsSuccess.Should().BeTrue();
        batch.AddLine(line2).IsSuccess.Should().BeTrue();

        batch.TotalDeductionsAmount.Should().Be(850m);
        batch.Status.Should().Be(PayrollBatchStatus.Draft);

        batch.Approve(UserId, Now).IsSuccess.Should().BeTrue();
        batch.Status.Should().Be(PayrollBatchStatus.Approved);
        batch.ApprovedByUserId.Should().Be(UserId);

        batch.MarkExported(UserId, Now).IsSuccess.Should().BeTrue();
        batch.Status.Should().Be(PayrollBatchStatus.Exported);
    }

    [Fact]
    public void RejectsDuplicateDeductionInSameBatch()
    {
        var batchId = Guid.NewGuid();
        var batch = PayrollBatch.Create(
            batchId,
            Tenant,
            "PR-2026-08",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            PayrollFrequency.Monthly,
            "ZAR",
            Now).Value;

        var deductionId = Guid.NewGuid();
        var line1 = PayrollBatchLine.Create(
            Guid.NewGuid(),
            Tenant,
            batchId,
            Driver1,
            "EMP001",
            "John Ndlovu",
            deductionId,
            "FINE",
            "Speeding fine",
            500m,
            1000m).Value;

        var line2 = PayrollBatchLine.Create(
            Guid.NewGuid(),
            Tenant,
            batchId,
            Driver1,
            "EMP001",
            "John Ndlovu",
            deductionId,
            "FINE",
            "Speeding fine duplicate",
            500m,
            500m).Value;

        batch.AddLine(line1).IsSuccess.Should().BeTrue();
        batch.AddLine(line2).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void FormatsPastelPayrollBatchCorrectly()
    {
        var batchId = Guid.NewGuid();
        var batch = PayrollBatch.Create(
            batchId,
            Tenant,
            "PR-2026-08",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            PayrollFrequency.Monthly,
            "ZAR",
            Now).Value;

        var line = PayrollBatchLine.Create(
            Guid.NewGuid(),
            Tenant,
            batchId,
            Driver1,
            "EMP001",
            "John Ndlovu",
            Guid.NewGuid(),
            "FINE",
            "Speeding fine",
            500m,
            1000m).Value;

        batch.AddLine(line);

        var bytes = PastelPayrollBatchFormatter.Format(batch);
        var csv = Encoding.UTF8.GetString(bytes);

        csv.Should().Contain("EmployeeCode,TransactionCode,Amount,Period,Reference,Description,BalanceRemaining");
        csv.Should().Contain("EMP001,FINE,500.00,2026/08,");
        csv.Should().Contain("Speeding fine,1000.00");
    }

    [Fact]
    public void FormatsPastelInvoiceBatchCorrectly()
    {
        var invoiceBatchDetail = new InvoiceBatchDetail(
            Guid.NewGuid(),
            "INV-2026-001",
            Guid.NewGuid(),
            "Anglo Platinum",
            Guid.NewGuid(),
            "CONTRACT-001",
            "ZAR",
            "Approved",
            TaxExclusiveAmount: 25000m,
            TaxAmount: 3750m,
            Total: 28750m,
            CreatedAtUtc: Now,
            ApprovedAtUtc: Now,
            ApprovedByUserId: UserId,
            ExportedAtUtc: null,
            ExportedByUserId: null,
            Lines:
            [
                new InvoiceBatchLineModel(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "TRIP-101",
                    34.500m,
                    25000m,
                    "StandardRated",
                    3750m,
                    28750m)
            ],
            Version: "1");

        var bytes = PastelInvoiceBatchFormatter.Format(invoiceBatchDetail);
        var csv = Encoding.UTF8.GetString(bytes);

        csv.Should().Contain("CustomerCode,DocumentDate,OrderNumber,DocumentNumber,Inclusive,LineType,ItemCode,Description,Quantity,UnitPrice,Discount,TaxType");
        csv.Should().Contain("ANGLO PL");
        csv.Should().Contain("CONTRACT-001");
        csv.Should().Contain("INV-2026-001");
        csv.Should().Contain("34.500");
        csv.Should().Contain("01"); // 15% standard rate tax type
    }
}
