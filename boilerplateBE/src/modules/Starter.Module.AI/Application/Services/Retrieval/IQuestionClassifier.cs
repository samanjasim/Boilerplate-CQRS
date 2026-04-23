namespace Starter.Module.AI.Application.Services.Retrieval;

public interface IQuestionClassifier
{
    Task<QuestionType?> ClassifyAsync(string query, CancellationToken ct);
}
