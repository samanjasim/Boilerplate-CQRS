using FluentAssertions;
using Starter.Module.AI.Infrastructure.Eval.Fixtures;
using Xunit;

namespace Starter.Api.Tests.Ai.Eval;

public sealed class EvalFixtureLoaderTests
{
    [Fact]
    public void LoadFromString_ValidFixture_Parses()
    {
        const string json = """
        {
          "name": "test-en-v1",
          "language": "en",
          "description": "test",
          "documents": [
            {
              "id": "11111111-1111-4111-8111-111111111111",
              "file_name": "a.md",
              "language": "en",
              "content": "hello"
            }
          ],
          "questions": [
            {
              "id": "q1",
              "query": "hi?",
              "relevant_document_ids": ["11111111-1111-4111-8111-111111111111"],
              "relevant_chunk_ids": null,
              "expected_answer_snippet": "hello",
              "tags": ["factual"]
            }
          ]
        }
        """;

        var result = EvalFixtureLoader.LoadFromString(json);
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("test-en-v1");
        result.Value.Language.Should().Be("en");
        result.Value.Documents.Should().HaveCount(1);
        result.Value.Questions.Should().HaveCount(1);
        result.Value.Questions[0].Tags.Should().ContainSingle(t => t == "factual");
    }

    [Fact]
    public void LoadFromString_MalformedJson_ReturnsFixtureInvalid()
    {
        var result = EvalFixtureLoader.LoadFromString("{ not json");
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ai.Eval.FixtureInvalid");
    }

    [Fact]
    public void LoadFromString_UnsupportedLanguage_ReturnsLanguageMismatch()
    {
        const string json = """
        { "name":"x","language":"fr","documents":[],"questions":[] }
        """;
        var result = EvalFixtureLoader.LoadFromString(json);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ai.Eval.DatasetLanguageMismatch");
    }

    [Fact]
    public void LoadFromString_DuplicateDocumentIds_ReturnsFixtureInvalid()
    {
        const string json = """
        {
          "name": "t", "language": "en", "documents": [
            {"id":"11111111-1111-4111-8111-111111111111","file_name":"a","language":"en","content":"x"},
            {"id":"11111111-1111-4111-8111-111111111111","file_name":"b","language":"en","content":"y"}
          ], "questions": []
        }
        """;
        var result = EvalFixtureLoader.LoadFromString(json);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ai.Eval.FixtureInvalid");
    }

    [Fact]
    public void LoadFromString_QuestionReferencesUnknownDocId_ReturnsFixtureInvalid()
    {
        const string json = """
        {
          "name": "t", "language": "en",
          "documents": [
            {"id":"11111111-1111-4111-8111-111111111111","file_name":"a","language":"en","content":"x"}
          ],
          "questions": [
            {"id":"q1","query":"?","relevant_document_ids":["22222222-2222-4222-8222-222222222222"],
             "relevant_chunk_ids":null,"expected_answer_snippet":null,"tags":[]}
          ]
        }
        """;
        var result = EvalFixtureLoader.LoadFromString(json);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ai.Eval.FixtureInvalid");
    }

    [Fact]
    public void LoadFromFile_SeedEnFixture_Parses()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Ai", "Eval", "fixtures", "rag-eval-dataset-en.json");
        var result = EvalFixtureLoader.LoadFromFile(path);
        result.IsSuccess.Should().BeTrue();
        result.Value.Questions.Should().HaveCountGreaterOrEqualTo(15);
        result.Value.Questions.Should().Contain(q => q.Tags.Contains("factual"));
        result.Value.Questions.Should().Contain(q => q.Tags.Contains("multi-doc"));
        result.Value.Questions.Should().Contain(q => q.Tags.Contains("negation"));
        result.Value.Questions.Should().Contain(q => q.Tags.Contains("comparative"));
    }

    [Fact]
    public void LoadFromFile_SeedArFixture_Parses()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Ai", "Eval", "fixtures", "rag-eval-dataset-ar.json");
        var result = EvalFixtureLoader.LoadFromFile(path);
        result.IsSuccess.Should().BeTrue();
        result.Value.Language.Should().Be("ar");
        result.Value.Questions.Should().HaveCountGreaterOrEqualTo(15);
    }
}
