using System.Globalization;
using FluentAssertions;
using LoadIx.Domain.Common;
using LoadIx.Domain.Documents;

namespace LoadIx.UnitTests.Domain;

public sealed class OperationalDocumentTests
{
    [Fact]
    public void RejectsDeletionAfterOwningTripIsFinalised()
    {
        var document = CreateDocument();

        var result = document.Delete(
            Guid.NewGuid(),
            DateTimeOffset.Parse(
                "2026-07-29T10:00:00Z",
                CultureInfo.InvariantCulture),
            ownerIsFinalised: true);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("document.owner_finalised");
        document.Status.Should().Be(DocumentStatus.Available);
    }

    [Fact]
    public void RecordsReasonedDocumentTombstoneBeforeFinalisation()
    {
        var document = CreateDocument();
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.Parse(
            "2026-07-29T10:00:00Z",
            CultureInfo.InvariantCulture);

        document.Delete(actor, now, ownerIsFinalised: false).IsSuccess
            .Should().BeTrue();

        document.Status.Should().Be(DocumentStatus.Deleted);
        document.DeletedByUserId.Should().Be(actor);
        document.DeletedAtUtc.Should().Be(now);
    }

    private static OperationalDocument CreateDocument() =>
        OperationalDocument.Create(
            Guid.NewGuid(),
            TenantId.New(),
            DocumentOwnerType.Trip,
            Guid.NewGuid(),
            DocumentType.ProofOfDelivery,
            "pod.pdf",
            "application/pdf",
            100,
            new string('A', 64),
            "tenant/documents/document/original.pdf",
            null,
            Guid.NewGuid(),
            DateTimeOffset.Parse(
                "2026-07-29T09:00:00Z",
                CultureInfo.InvariantCulture),
            "Synthetic POD").Value;
}
