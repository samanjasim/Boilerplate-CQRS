using Starter.Module.AI.Application.Services.Retrieval;

namespace Starter.Api.Tests.Ai.Retrieval;

internal sealed class NoOpQuestionClassifier : IQuestionClassifier
{
    public Task<QuestionType?> ClassifyAsync(string query, CancellationToken ct)
        => Task.FromResult<QuestionType?>(null);
}
