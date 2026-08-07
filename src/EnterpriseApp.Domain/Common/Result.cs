namespace EnterpriseApp.Domain.Common;

/// <summary>
/// Railway-oriented result type. Avoids exceptions for expected business failures.
/// Usage: return Result.Success(entity) or Result.Failure("Not found")
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Success result cannot have an error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Failure result must have an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error    { get; }

    public static Result Success()              => new(true, Error.None);
    public static Result Failure(Error error)   => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error)  => new(default, false, error);
}

public sealed class Result<TValue>(TValue? value, bool isSuccess, Error error) : Result(isSuccess, error)
{
    private readonly TValue? _value = value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access value of a failure result.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}

public record Error(string Code, string Description)
{
    public static readonly Error None     = new(string.Empty, string.Empty);
    public static readonly Error NotFound = new("General.NotFound", "The resource was not found.");
    public static readonly Error Unauthorized = new("General.Unauthorized", "Unauthorized.");
    public static readonly Error Conflict = new("General.Conflict", "A conflict occurred.");

    public static Error Validation(string field, string msg) => new($"Validation.{field}", msg);
    public static Error NotFoundFor(string entity, object id) =>
        new($"{entity}.NotFound", $"{entity} with id '{id}' was not found.");
}
