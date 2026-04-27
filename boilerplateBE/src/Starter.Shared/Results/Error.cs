namespace Starter.Shared.Results;

public sealed record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
    public static readonly Error NullValue = new("Error.NullValue", "The specified result value is null.", ErrorType.Failure);

    public static Error NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    public static Error Validation(string code, string description) =>
        new(code, description, ErrorType.Validation);

    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);

    public static Error Unauthorized(string description = "Unauthorized access.") =>
        new("Error.Unauthorized", description, ErrorType.Unauthorized);

    public static Error Forbidden(string description = "Access denied.") =>
        new("Error.Forbidden", description, ErrorType.Forbidden);

    public static Error Failure(string code, string description) =>
        new(code, description, ErrorType.Failure);

    public static Error TooManyRequests(string code, string description) =>
        new(code, description, ErrorType.TooManyRequests);

    public override string ToString() => $"{Code}: {Description}";
}
