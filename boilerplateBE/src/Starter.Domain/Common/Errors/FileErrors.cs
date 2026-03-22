using Starter.Shared.Results;

namespace Starter.Domain.Common.Errors;

public static class FileErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("File.NotFound", $"File with ID '{id}' was not found.");

    public static Error AccessDenied() =>
        Error.Forbidden("You do not have access to this file.");
}
