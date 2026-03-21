using Starter.Shared.Results;

namespace Starter.Shared.Models;

public class ApiResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string>? Errors { get; init; }
    public IDictionary<string, string[]>? ValidationErrors { get; init; }

    public static ApiResponse Ok(string? message = null) =>
        new() { Success = true, Message = message };

    public static ApiResponse Fail(string message) =>
        new() { Success = false, Message = message };

    public static ApiResponse Fail(IEnumerable<string> errors) =>
        new() { Success = false, Errors = errors.ToList() };

    public static ApiResponse Fail(ValidationErrors validationErrors) =>
        new() { Success = false, ValidationErrors = validationErrors.ToDictionary() };

    public static ApiResponse FromResult(Result result)
    {
        if (result.IsSuccess)
            return Ok();

        if (result.ValidationErrors is not null)
            return Fail(result.ValidationErrors);

        return Fail(result.Error.Description);
    }
}

public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public new static ApiResponse<T> Fail(string message) =>
        new() { Success = false, Message = message };

    public new static ApiResponse<T> Fail(IEnumerable<string> errors) =>
        new() { Success = false, Errors = errors.ToList() };

    public new static ApiResponse<T> Fail(ValidationErrors validationErrors) =>
        new() { Success = false, ValidationErrors = validationErrors.ToDictionary() };

    public static ApiResponse<T> FromResult(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        if (result.ValidationErrors is not null)
            return Fail(result.ValidationErrors);

        return Fail(result.Error.Description);
    }
}
