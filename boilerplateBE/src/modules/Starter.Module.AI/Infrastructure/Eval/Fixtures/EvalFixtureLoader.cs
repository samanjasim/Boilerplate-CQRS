using System.Text.Json;
using System.Text.Json.Serialization;
using Starter.Module.AI.Application.Eval.Contracts;
using Starter.Module.AI.Application.Eval.Errors;
using Starter.Shared.Results;

namespace Starter.Module.AI.Infrastructure.Eval.Fixtures;

public static class EvalFixtureLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public static Result<EvalDataset> LoadFromString(string json)
    {
        RawFixture? raw;
        try { raw = JsonSerializer.Deserialize<RawFixture>(json, Options); }
        catch (JsonException) { return Result.Failure<EvalDataset>(EvalErrors.FixtureInvalid); }

        if (raw is null) return Result.Failure<EvalDataset>(EvalErrors.FixtureInvalid);
        if (raw.Language is not ("en" or "ar"))
            return Result.Failure<EvalDataset>(EvalErrors.DatasetLanguageMismatch);

        var docs = raw.Documents ?? new List<RawDoc>();
        var questions = raw.Questions ?? new List<RawQuestion>();

        var docIds = new HashSet<Guid>();
        foreach (var d in docs)
            if (!docIds.Add(d.Id)) return Result.Failure<EvalDataset>(EvalErrors.FixtureInvalid);

        foreach (var q in questions)
            foreach (var id in q.RelevantDocumentIds ?? new List<Guid>())
                if (!docIds.Contains(id))
                    return Result.Failure<EvalDataset>(EvalErrors.FixtureInvalid);

        var dataset = new EvalDataset(
            Name: raw.Name ?? "unknown",
            Language: raw.Language,
            Description: raw.Description,
            Documents: docs.Select(d => new EvalDocument(
                d.Id, d.FileName, d.Content, d.Language ?? raw.Language)).ToList(),
            Questions: questions.Select(q => new EvalQuestion(
                Id: q.Id,
                Query: q.Query,
                RelevantDocumentIds: q.RelevantDocumentIds ?? new List<Guid>(),
                RelevantChunkIds: q.RelevantChunkIds,
                ExpectedAnswerSnippet: q.ExpectedAnswerSnippet,
                Tags: q.Tags ?? new List<string>())).ToList());

        return Result.Success(dataset);
    }

    public static Result<EvalDataset> LoadFromFile(string path)
        => !File.Exists(path)
            ? Result.Failure<EvalDataset>(EvalErrors.FixtureNotFound)
            : LoadFromString(File.ReadAllText(path));

    private sealed class RawFixture
    {
        public string? Name { get; set; }
        public string Language { get; set; } = "";
        public string? Description { get; set; }
        public List<RawDoc>? Documents { get; set; }
        public List<RawQuestion>? Questions { get; set; }
    }

    private sealed class RawDoc
    {
        public Guid Id { get; set; }
        [JsonPropertyName("file_name")] public string FileName { get; set; } = "";
        public string? Language { get; set; }
        public string Content { get; set; } = "";
    }

    private sealed class RawQuestion
    {
        public string Id { get; set; } = "";
        public string Query { get; set; } = "";
        [JsonPropertyName("relevant_document_ids")] public List<Guid>? RelevantDocumentIds { get; set; }
        [JsonPropertyName("relevant_chunk_ids")] public List<Guid>? RelevantChunkIds { get; set; }
        [JsonPropertyName("expected_answer_snippet")] public string? ExpectedAnswerSnippet { get; set; }
        public List<string>? Tags { get; set; }
    }
}
