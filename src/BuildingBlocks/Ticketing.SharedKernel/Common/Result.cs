namespace Ticketing.SharedKernel;

public readonly record struct Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T value)
    {
        _value = value;
        _error = null;
    }

    private Result(Error error)
    {
        _value = default;
        _error = error;
    }

    public bool IsSuccess => _error is null;
    public bool IsFailure => _error is not null;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot access Value on failure result.");
    public Error Error => IsFailure ? (Error)_error! : throw new InvalidOperationException("Cannot access Error on success result.");

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure)
        => IsSuccess ? onSuccess(Value) : onFailure(Error);
}

public readonly record struct Result
{
    private readonly Error? _error;

    private Result(Error error)
    {
        _error = error;
    }

    public bool IsSuccess => _error is null;
    public bool IsFailure => _error is not null;
    public Error Error => IsFailure ? (Error)_error! : throw new InvalidOperationException("Cannot access Error on success result.");

    public static Result Success() => new();
    public static Result Failure(Error error) => new(error);
}

public readonly record struct Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static Error Validation(string message) => new("ValidationError", message);
    public static Error NotFound(string message) => new("NotFound", message);
    public static Error Conflict(string message) => new("Conflict", message);
    public static Error Unauthorized(string message) => new("Unauthorized", message);
    public static Error Forbidden(string message) => new("Forbidden", message);
    public static Error Internal(string message) => new("InternalError", message);
}