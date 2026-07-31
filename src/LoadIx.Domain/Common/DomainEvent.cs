namespace LoadIx.Domain.Common;

public abstract record DomainEvent
{
    protected DomainEvent(DateTimeOffset occurredAtUtc)
    {
        if (occurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Domain event timestamps must be UTC.",
                nameof(occurredAtUtc));
        }

        EventId = Guid.NewGuid();
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }
}
