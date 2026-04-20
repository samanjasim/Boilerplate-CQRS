using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Api.Tests.Ai.Fakes;

internal sealed class FakeQuestionClassifier : IQuestionClassifier
{
    private readonly QuestionType? _type;

    public FakeQuestionClassifier(QuestionType? type) => _type = type;

    public Task<QuestionType?> ClassifyAsync(string query, CancellationToken ct)
        => Task.FromResult(_type);
}
