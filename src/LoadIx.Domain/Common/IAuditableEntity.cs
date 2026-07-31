namespace LoadIx.Domain.Common;

public interface IAuditableEntity
{
    DateTimeOffset CreatedAtUtc { get; }

    Guid? CreatedByUserId { get; }

    DateTimeOffset ModifiedAtUtc { get; }

    Guid? ModifiedByUserId { get; }

    byte[] RowVersion { get; }
}
