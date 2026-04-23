using System.Text.Json;
using Starter.Module.AI.Application.Eval.Errors;
using Starter.Shared.Results;

namespace Starter.Module.AI.Infrastructure.Eval.Baseline;

public static class BaselineLoader
{
    public static Result<BaselineSnapshot> Load(string path)
    {
        if (!File.Exists(path)) return Result.Failure<BaselineSnapshot>(EvalErrors.BaselineMissing);
        var json = File.ReadAllText(path);
        var snapshot = JsonSerializer.Deserialize<BaselineSnapshot>(
            json, BaselineJson.Options);
        return snapshot is null
            ? Result.Failure<BaselineSnapshot>(EvalErrors.BaselineMissing)
            : Result.Success(snapshot);
    }
}

internal static class BaselineJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };
}
