using FluentAssertions;
using LoadIx.Infrastructure.Identity;

namespace LoadIx.UnitTests.Security;

public sealed class AuditSummaryTests
{
    [Fact]
    public void RemovesSecretsAndFileBodiesFromStructuredSummaries()
    {
        var summary = AuditSummary.Serialize(new
        {
            action = "Document.Downloaded",
            password = "do-not-store",
            accessToken = "eyJ-do-not-store",
            body = "private file body",
            content = "private file content",
            storageKey = "tenant/private/file.pdf",
            displayName = "Safe summary",
        });

        summary.Should().NotBeNull();
        summary.Should().Contain("Safe summary");
        summary.Should().NotContain("do-not-store");
        summary.Should().NotContain("private file body");
        summary.Should().NotContain("private file content");
        summary.Should().NotContain("tenant/private/file.pdf");
    }

    [Fact]
    public void RedactsEmbeddedAuthorizationValuesAndBoundsReasonText()
    {
        var metadata = AuditSummary.SanitizeJson(
            "{\"authorization\":\"Bearer secret-token\",\"path\":\"/api/v1/users\"}");
        var reason = AuditSummary.SanitizeText(
            "password=secret-value " + new string('x', 600));

        metadata.Should().NotBeNull();
        metadata.Should().Contain("/api/v1/users");
        metadata.Should().NotContain("secret-token");
        reason.Should().NotContain("secret-value");
        reason!.Length.Should().BeLessThanOrEqualTo(500);
    }
}
