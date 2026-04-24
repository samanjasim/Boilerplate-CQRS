namespace Starter.Module.AI.Application.Services.Retrieval;

public interface IQuestionClassifier
{
    Task<QuestionType?> ClassifyAsync(Guid tenantId, string query, CancellationToken ct);
}
