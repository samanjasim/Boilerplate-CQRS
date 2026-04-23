using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Eval.Errors;

public static class EvalErrors
{
    public static readonly Error FixtureNotFound = Error.NotFound(
        "Ai.Eval.FixtureNotFound", "The requested eval fixture does not exist.");

    public static readonly Error FixtureInvalid = Error.Validation(
        "Ai.Eval.FixtureInvalid", "The eval fixture JSON is invalid or malformed.");

    public static readonly Error BaselineMissing = Error.Failure(
        "Ai.Eval.BaselineMissing", "Baseline snapshot file is missing.");

    public static readonly Error DatasetLanguageMismatch = Error.Validation(
        "Ai.Eval.DatasetLanguageMismatch", "Dataset language must be 'en' or 'ar'.");

    public static readonly Error AssistantNotFound = Error.NotFound(
        "Ai.Eval.AssistantNotFound", "Assistant not found or not accessible.");

    public static readonly Error JudgeModelUnavailable = Error.Failure(
        "Ai.Eval.JudgeModelUnavailable", "The configured faithfulness judge model is unavailable.");
}
