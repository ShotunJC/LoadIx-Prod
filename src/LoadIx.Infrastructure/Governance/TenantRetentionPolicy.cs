using LoadIx.Domain.Common;

namespace LoadIx.Infrastructure.Governance;

public sealed class TenantRetentionPolicy : AuditableEntity<Guid>, ITenantOwned
{
    private TenantRetentionPolicy(
        Guid id,
        TenantId tenantId,
        int operationalDataRetentionDays,
        int auditRetentionDays,
        int documentMetadataRetentionDays,
        bool legalHold,
        string? legalHoldReason)
        : base(id)
    {
        TenantId = tenantId;
        OperationalDataRetentionDays = operationalDataRetentionDays;
        AuditRetentionDays = auditRetentionDays;
        DocumentMetadataRetentionDays = documentMetadataRetentionDays;
        LegalHold = legalHold;
        LegalHoldReason = legalHoldReason?.Trim();
    }

    private TenantRetentionPolicy()
    {
    }

    public TenantId TenantId { get; private set; }

    public int OperationalDataRetentionDays { get; private set; }

    public int AuditRetentionDays { get; private set; }

    public int DocumentMetadataRetentionDays { get; private set; }

    public bool LegalHold { get; private set; }

    public string? LegalHoldReason { get; private set; }

    public static TenantRetentionPolicy Create(
        Guid id,
        TenantId tenantId,
        int operationalDataRetentionDays,
        int auditRetentionDays,
        int documentMetadataRetentionDays,
        bool legalHold,
        string? legalHoldReason) =>
        new(
            id,
            tenantId,
            operationalDataRetentionDays,
            auditRetentionDays,
            documentMetadataRetentionDays,
            legalHold,
            legalHoldReason);

    public void Update(
        int operationalDataRetentionDays,
        int auditRetentionDays,
        int documentMetadataRetentionDays,
        bool legalHold,
        string? legalHoldReason)
    {
        OperationalDataRetentionDays = operationalDataRetentionDays;
        AuditRetentionDays = auditRetentionDays;
        DocumentMetadataRetentionDays = documentMetadataRetentionDays;
        LegalHold = legalHold;
        LegalHoldReason = legalHoldReason?.Trim();
    }
}
