namespace LoadIx.Domain.Common;

public class Result
{
    protected Result(bool isSuccess, DomainError? error)
    {
        if (isSuccess == (error is not null))
        {
            throw new ArgumentException(
                "A successful result cannot have an error and a failed result must have one.",
                nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public DomainError? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(DomainError error) => new(false, error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(true, null)
    {
        _value = value;
    }

    private Result(DomainError error) : base(false, error)
    {
    }

    public T Value =>
        IsSuccess
            ? _value!
            : throw new InvalidOperationException(
                $"A failed result has no value. Error: {Error?.Code}");

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(DomainError error) => new(error);
}
