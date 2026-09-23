namespace BCKash.SharedKernel;

/// <summary>Outcome of an Application-layer use case that can fail for expected, user-facing reasons.</summary>
public class Result
{
    public bool Succeeded { get; }
    public string? Error { get; }

    protected Result(bool succeeded, string? error)
    {
        if (succeeded && error is not null)
        {
            throw new InvalidOperationException("A successful result cannot carry an error message.");
        }

        if (!succeeded && error is null)
        {
            throw new InvalidOperationException("A failed result must carry an error message.");
        }

        Succeeded = succeeded;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, null);
    public static Result<T> Failure<T>(string error) => new(default, false, error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool succeeded, string? error) : base(succeeded, error)
    {
        _value = value;
    }

    /// <summary>Throws if the result failed — only read this after checking <see cref="Result.Succeeded"/>.</summary>
    public T Value => Succeeded
        ? _value!
        : throw new InvalidOperationException($"Cannot access the value of a failed result: {Error}");
}
