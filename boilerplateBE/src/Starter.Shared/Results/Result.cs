namespace Starter.Shared.Results;

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Cannot have error with success result.");

        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Cannot have no error with failure result.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }
    public ValidationErrors? ValidationErrors { get; protected init; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    public static Result ValidationFailure(ValidationErrors validationErrors) =>
        new(false, Error.Validation("Validation", "One or more validation errors occurred."))
        {
            ValidationErrors = validationErrors
        };

    public static Result<TValue> ValidationFailure<TValue>(ValidationErrors validationErrors) =>
        new(default, false, Error.Validation("Validation", "One or more validation errors occurred."))
        {
            ValidationErrors = validationErrors
        };

    public static Result Create(bool condition, Error error) =>
        condition ? Success() : Failure(error);

    public static Result<TValue> Create<TValue>(TValue? value, Error error) =>
        value is not null ? Success(value) : Failure<TValue>(error);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access value of failed result.");

    public static Result<TValue> Success(TValue value) => new(value, true, Error.None);
    public static new Result<TValue> Failure(Error error) => new(default, false, error);

    public static implicit operator Result<TValue>(TValue? value) =>
        value is not null ? Success(value) : Failure<TValue>(Error.NullValue);

    public Result<TResult> Map<TResult>(Func<TValue, TResult> mapper)
    {
        return IsSuccess
            ? Result.Success(mapper(Value))
            : Result.Failure<TResult>(Error);
    }

    public async Task<Result<TResult>> MapAsync<TResult>(Func<TValue, Task<TResult>> mapper)
    {
        return IsSuccess
            ? Result.Success(await mapper(Value))
            : Result.Failure<TResult>(Error);
    }

    public Result<TResult> Bind<TResult>(Func<TValue, Result<TResult>> binder)
    {
        return IsSuccess ? binder(Value) : Result.Failure<TResult>(Error);
    }

    public async Task<Result<TResult>> BindAsync<TResult>(Func<TValue, Task<Result<TResult>>> binder)
    {
        return IsSuccess ? await binder(Value) : Result.Failure<TResult>(Error);
    }

    public TValue GetValueOrDefault(TValue defaultValue = default!) =>
        IsSuccess ? Value : defaultValue;

    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(Error);
}
