namespace LoadIx.Domain.Common;

public readonly record struct TenantId
{
    private TenantId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static TenantId New() => new(Guid.NewGuid());

    public static TenantId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A tenant identifier cannot be empty.", nameof(value));
        }

        return new TenantId(value);
    }

    public override string ToString() => Value.ToString();
}
