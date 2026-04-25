using FluentAssertions;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Api.Tests.Ai.Retrieval;
using Starter.Module.AI.Application.Services;
using Starter.Abstractions.Ai;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Infrastructure.Observability;
using Xunit;

namespace Starter.Api.Tests.Ai.Observability;

[Collection(ObservabilityTestCollection.Name)]
public sealed class RagRetrievalWebhookTests
{
    private static object? GetProp(object obj, string name) =>
        obj.GetType().GetProperty(name)?.GetValue(obj);

    [Fact]
    public async Task Successful_turn_fires_ai_retrieval_completed_event()
    {
        var publisher = new RecordingWebhookPublisher();
        var fx = new ChatExecutionTestFixture(publisher);
        var docId = fx.SeedTwoRetrievedChunks();
        var assistant = fx.SeedAssistantWithRagScope(AiRagScope.SelectedDocuments, docIds: new[] { docId });
        fx.FakeProvider.ScriptedResponse = "reply";

        await fx.RunOneTurnAsync(assistant, "q");

        publisher.Events.Should().ContainSingle(e => e.EventType == RagWebhookEventNames.Completed);
        var payload = publisher.Events.Single(e => e.EventType == RagWebhookEventNames.Completed).Data;
        payload.Should().NotBeNull();
        ((int)GetProp(payload, "KeptChildren")!).Should().BeGreaterThan(0);
        ((string)GetProp(payload, "DetectedLanguage")!).Should().Be("unknown");
    }

    [Fact]
    public async Task Degraded_turn_fires_ai_retrieval_degraded_event_instead_of_completed()
    {
        var publisher = new RecordingWebhookPublisher();
        var fx = new ChatExecutionTestFixture(publisher);
        var docId = fx.SeedTwoRetrievedChunks();
        fx.OverrideRetrievalContext(
            children: fx.CurrentRetrievalContext.Children,
            truncated: false,
            degradedStages: new[] { "vector-search-0" });

        var assistant = fx.SeedAssistantWithRagScope(AiRagScope.SelectedDocuments, docIds: new[] { docId });
        fx.FakeProvider.ScriptedResponse = "reply";

        await fx.RunOneTurnAsync(assistant, "q");

        publisher.Events.Should().ContainSingle(e => e.EventType == RagWebhookEventNames.Degraded);
        publisher.Events.Should().NotContain(e => e.EventType == RagWebhookEventNames.Completed);
    }

    [Fact]
    public async Task Webhook_publish_timeout_records_stage_outcome()
    {
        using var listener = new TestMeterListener(AiRagMetrics.MeterName);

        var publisher = new RecordingWebhookPublisher { DelayMs = 1000 };
        var fx = new ChatExecutionTestFixture(publisher);
        var docId = fx.SeedTwoRetrievedChunks();
        var assistant = fx.SeedAssistantWithRagScope(AiRagScope.SelectedDocuments, docIds: new[] { docId });
        fx.FakeProvider.ScriptedResponse = "reply";

        await fx.RunOneTurnAsync(assistant, "q");

        listener.Snapshot()
            .Should().Contain(m =>
                m.InstrumentName == "rag.stage.outcome"
                && (string?)m.Tags["rag.stage"] == "webhook-publish"
                && (string?)m.Tags["rag.outcome"] == "timeout");
    }

    [Fact]
    public async Task Retrieval_exception_fires_ai_retrieval_failed_event()
    {
        var publisher = new RecordingWebhookPublisher();
        var fx = new ChatExecutionTestFixture(publisher, throwOnRetrieve: true);
        var assistant = fx.SeedAssistantWithRagScope(AiRagScope.AllTenantDocuments);
        fx.FakeProvider.ScriptedResponse = "reply";

        await fx.RunOneTurnAsync(assistant, "q");

        publisher.Events.Should().ContainSingle(e => e.EventType == RagWebhookEventNames.Failed);
    }

    [Fact]
    public async Task Aggregate_log_line_includes_new_structured_properties()
    {
        var recorder = new RecordingLogger<ChatExecutionService>();
        var publisher = new RecordingWebhookPublisher();

        var fx = new ChatExecutionTestFixture(publisher, chatLogger: recorder);
        var docId = fx.SeedTwoRetrievedChunks();
        var assistant = fx.SeedAssistantWithRagScope(AiRagScope.SelectedDocuments, docIds: new[] { docId });
        fx.FakeProvider.ScriptedResponse = "reply";

        await fx.RunOneTurnAsync(assistant, "q");

        recorder.Entries.Should().Contain(e =>
            e.Message.Contains("RAG retrieval done assistant=") &&
            e.Message.Contains("req=") &&
            e.Message.Contains("siblings=") &&
            e.Message.Contains("stages=") &&
            e.Message.Contains("lang="));
    }

    [Fact]
    public async Task Aggregate_log_req_matches_webhook_payload_request_id()
    {
        var recorder = new RecordingLogger<ChatExecutionService>();
        var publisher = new RecordingWebhookPublisher();

        var fx = new ChatExecutionTestFixture(publisher, chatLogger: recorder);
        var docId = fx.SeedTwoRetrievedChunks();
        var assistant = fx.SeedAssistantWithRagScope(AiRagScope.SelectedDocuments, docIds: new[] { docId });
        fx.FakeProvider.ScriptedResponse = "reply";

        await fx.RunOneTurnAsync(assistant, "q");

        var webhookEvent = publisher.Events.Single(e => e.EventType == RagWebhookEventNames.Completed);
        var webhookRequestId = (Guid)webhookEvent.Data.GetType().GetProperty("RequestId")!.GetValue(webhookEvent.Data)!;

        recorder.Entries.Should().Contain(e => e.Message.Contains($"req={webhookRequestId}"));
    }
}
