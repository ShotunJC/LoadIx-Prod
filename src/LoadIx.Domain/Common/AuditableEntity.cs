namespace LoadIx.Domain.Common;

public abstract class AuditableEntity<TId> : Entity<TId>, IAuditableEntity
    where TId : notnull
{
    protected AuditableEntity(TId id) : base(id)
    {
    }

    protected AuditableEntity()
    {
    }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public DateTimeOffset ModifiedAtUtc { get; private set; }

    public Guid? ModifiedByUserId { get; private set; }

    public byte[] RowVersion { get; private set; } = [];
}
