using LoadIx.Domain.Common;

namespace LoadIx.Domain.Finance;

public sealed class CostEntry : Entity<Guid>, ITenantOwned
{
    private CostEntry()
    {
        Description = string.Empty;
        CurrencyCode = string.Empty;
    }

    private CostEntry(
        Guid id,
        TenantId tenantId,
        Guid tripFinancialsId,
        CostEntryType type,
        decimal amount,
        string currencyCode,
        string description,
        Guid actorUserId,
        DateTimeOffset occurredAtUtc,
        bool isAdjustment)
        : base(id)
    {
        TenantId = tenantId;
        TripFinancialsId = tripFinancialsId;
        Type = type;
        Amount = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        Description = description.Trim();
        ActorUserId = actorUserId;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        IsAdjustment = isAdjustment;
    }

    public TenantId TenantId { get; private set; }
    public Guid TripFinancialsId { get; private set; }
    public CostEntryType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; }
    public string Description { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public bool IsAdjustment { get; private set; }

    public static Result<CostEntry> Create(
        Guid id,
        TenantId tenantId,
        Guid tripFinancialsId,
        CostEntryType type,
        decimal amount,
        string currencyCode,
        string description,
        Guid actorUserId,
        DateTimeOffset occurredAtUtc,
        bool isAdjustment = false)
    {
        if (id == Guid.Empty || tenantId.IsEmpty || tripFinancialsId == Guid.Empty
            || actorUserId == Guid.Empty || occurredAtUtc.Offset != TimeSpan.Zero)
        {
            return Result<CostEntry>.Failure(new DomainError(
                "finance.cost_context_invalid",
                "Cost entries require tenant, trip financials, actor, and UTC timestamp."));
        }

        if (!Enum.IsDefined(type) || amount <= 0)
        {
            return Result<CostEntry>.Failure(new DomainError(
                "finance.cost_invalid",
                "Cost type must be supported and amount must be positive."));
        }

        var money = Money.Create(amount, currencyCode);
        var error = money.IsFailure
            ? money.Error
            : DomainValidation.Required(
                description,
                "finance.cost_description_required",
                500);
        return error is null
            ? Result<CostEntry>.Success(new CostEntry(
                id,
                tenantId,
                tripFinancialsId,
                type,
                amount,
                money.Value.CurrencyCode,
                description,
                actorUserId,
                occurredAtUtc,
                isAdjustment))
            : Result<CostEntry>.Failure(error);
    }
}
