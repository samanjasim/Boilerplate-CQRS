using System.Runtime.CompilerServices;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Observability;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Module.AI.Infrastructure.Retrieval;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Services;

internal sealed class ChatExecutionService(
    AiDbContext context,
    ICurrentUserService currentUser,
    IAiProviderFactory providerFactory,
    IQuotaChecker quotaChecker,
    IUsageTracker usageTracker,
    IWebhookPublisher webhookPublisher,
    IAiToolRegistry toolRegistry,
    IRagRetrievalService retrievalService,
    ISender sender,
    IConfiguration configuration,
    IResourceAccessService access,
    ILogger<ChatExecutionService> logger) : IChatExecutionService
{
    private const string AiTokensMetric = "ai_tokens";
    private const int MaxTitleLength = 80;

    // ──────────────────────────────────────────────────────────────
    // Public: non-streaming turn
    // ──────────────────────────────────────────────────────────────

    public async Task<Result<AiChatReplyDto>> ExecuteAsync(
        Guid? conversationId,
        Guid? assistantId,
        string userMessage,
        CancellationToken ct = default)
    {
        var stateResult = await PrepareTurnAsync(conversationId, assistantId, userMessage, ct);
        if (stateResult.IsFailure)
            return Result.Failure<AiChatReplyDto>(stateResult.Error);

        var state = stateResult.Value;
        var retrieved = await RetrieveContextSafelyAsync(state.Assistant, userMessage, state.ProviderMessages, ct);
        var effectiveSystemPrompt = ResolveSystemPrompt(state.Assistant, retrieved);

        var provider = providerFactory.Create(ResolveProvider(state.Assistant));
        var chatOptions = BuildChatOptions(state.Assistant, effectiveSystemPrompt, state.Tools.ProviderTools);

        var messages = new List<AiChatMessage>(state.ProviderMessages);
        var totalInput = 0;
        var totalOutput = 0;
        var stepBudget = Math.Clamp(state.Assistant.MaxAgentSteps, 1, 20);
        var nextOrder = state.NextOrder;

        try
        {
            for (var step = 0; step < stepBudget; step++)
            {
                var completion = await provider.ChatAsync(messages, chatOptions, ct);
                totalInput += completion.InputTokens;
                totalOutput += completion.OutputTokens;

                if (completion.ToolCalls is null || completion.ToolCalls.Count == 0)
                {
                    var citations = CitationParser.Parse(completion.Content, retrieved.Children);
                    var finalMessage = await FinalizeTurnAsync(
                        state, completion.Content, totalInput, totalOutput, nextOrder, citations, ct);
                    return Result.Success(new AiChatReplyDto(
                        state.Conversation.Id,
                        state.UserMessage.ToDto(),
                        finalMessage.ToDto()));
                }

                var toolCallsJson = System.Text.Json.JsonSerializer.Serialize(
                    completion.ToolCalls, SerializerOptions);

                var assistantCallMsg = AiMessage.CreateAssistantMessage(
                    state.Conversation.Id,
                    completion.Content ?? "",
                    nextOrder++,
                    completion.InputTokens,
                    completion.OutputTokens,
                    toolCalls: toolCallsJson);
                context.AiMessages.Add(assistantCallMsg);
                messages.Add(new AiChatMessage(
                    "assistant", completion.Content, ToolCalls: completion.ToolCalls));

                foreach (var call in completion.ToolCalls)
                {
                    var dispatch = await DispatchToolAsync(call, state.Tools, ct);

                    var toolResultMsg = AiMessage.CreateToolResultMessage(
                        state.Conversation.Id, call.Id, dispatch.Json, nextOrder++);
                    context.AiMessages.Add(toolResultMsg);
                    messages.Add(new AiChatMessage("tool", dispatch.Json, ToolCallId: call.Id));
                }

                await context.SaveChangesAsync(ct);
            }

            var hitLimitMsg = await FinalizeTurnAsync(
                state,
                "I couldn't fully complete the task within my step budget. Please narrow the request.",
                totalInput, totalOutput, nextOrder, [], ct);
            return Result.Success(new AiChatReplyDto(
                state.Conversation.Id,
                state.UserMessage.ToDto(),
                hitLimitMsg.ToDto()));
        }
        catch (Exception ex)
        {
            await FailTurnAsync(state);
            return Result.Failure<AiChatReplyDto>(AiErrors.ProviderError(ex.Message));
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Public: streaming turn
    // ──────────────────────────────────────────────────────────────

    public async IAsyncEnumerable<ChatStreamEvent> ExecuteStreamAsync(
        Guid? conversationId,
        Guid? assistantId,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var stateResult = await PrepareTurnAsync(conversationId, assistantId, userMessage, ct);
        if (stateResult.IsFailure)
        {
            yield return new ChatStreamEvent("error", new
            {
                Code = stateResult.Error.Code,
                Message = stateResult.Error.Description
            });
            yield break;
        }

        var state = stateResult.Value;

        yield return new ChatStreamEvent("start", new
        {
            ConversationId = state.Conversation.Id,
            UserMessageId = state.UserMessage.Id
        });

        var retrieved = await RetrieveContextSafelyAsync(state.Assistant, userMessage, state.ProviderMessages, ct);
        var effectiveSystemPrompt = ResolveSystemPrompt(state.Assistant, retrieved);

        var provider = providerFactory.Create(ResolveProvider(state.Assistant));
        var chatOptions = BuildChatOptions(state.Assistant, effectiveSystemPrompt, state.Tools.ProviderTools);

        var messages = new List<AiChatMessage>(state.ProviderMessages);
        var totalInput = 0;
        var totalOutput = 0;
        var stepBudget = Math.Clamp(state.Assistant.MaxAgentSteps, 1, 20);
        var nextOrder = state.NextOrder;

        var finalContentBuilder = new StringBuilder();
        var finishReason = "stop";
        var priorPromptChars = 0;

        for (var step = 0; step < stepBudget; step++)
        {
            // Count only the chars added to the prompt since the previous round so the
            // fallback estimate grows linearly with conversation size instead of O(N^2).
            var currentPromptChars = messages.Sum(m => m.Content?.Length ?? 0);
            var newPromptChars = currentPromptChars - priorPromptChars;
            priorPromptChars = currentPromptChars;

            var roundContent = new StringBuilder();
            var toolCallBuilders = new Dictionary<string, ToolCallBuilder>(StringComparer.Ordinal);
            int? roundInput = null;
            int? roundOutput = null;
            string? roundFinish = null;

            await foreach (var chunkOrError in EnumerateSafelyAsync(
                provider.StreamChatAsync(messages, chatOptions, ct), ct))
            {
                if (chunkOrError.Error is not null)
                {
                    await FailTurnAsync(state);
                    yield return new ChatStreamEvent("error", new
                    {
                        Code = "Ai.ProviderError",
                        Message = chunkOrError.Error
                    });
                    yield break;
                }

                var chunk = chunkOrError.Chunk!;

                if (chunk.FinishReason is not null) roundFinish = chunk.FinishReason;
                if (chunk.InputTokens is int ci && ci > 0) roundInput = ci;
                if (chunk.OutputTokens is int co && co > 0) roundOutput = co;

                if (chunk.ContentDelta is { Length: > 0 } delta)
                {
                    roundContent.Append(delta);
                    yield return new ChatStreamEvent("delta", new { Content = delta });
                }

                if (chunk.ToolCallDelta is { } tc)
                {
                    if (!toolCallBuilders.TryGetValue(tc.Id, out var builder))
                    {
                        builder = new ToolCallBuilder(tc.Id, tc.Name);
                        toolCallBuilders[tc.Id] = builder;
                    }
                    builder.AppendArguments(tc.ArgumentsJson);
                }
            }

            totalInput += roundInput ?? EstimateTokens(newPromptChars);
            totalOutput += roundOutput ?? EstimateTokens(roundContent.Length);
            if (roundFinish is not null) finishReason = roundFinish;

            if (toolCallBuilders.Count == 0)
            {
                finalContentBuilder.Append(roundContent);
                break;
            }

            var assembledCalls = toolCallBuilders.Values.Select(b => b.Build()).ToList();
            var toolCallsJson = System.Text.Json.JsonSerializer.Serialize(
                assembledCalls, SerializerOptions);

            var assistantCallMsg = AiMessage.CreateAssistantMessage(
                state.Conversation.Id,
                roundContent.ToString(),
                nextOrder++,
                roundInput ?? 0,
                roundOutput ?? 0,
                toolCalls: toolCallsJson);
            context.AiMessages.Add(assistantCallMsg);
            messages.Add(new AiChatMessage(
                "assistant",
                roundContent.Length == 0 ? null : roundContent.ToString(),
                ToolCalls: assembledCalls));

            foreach (var call in assembledCalls)
            {
                yield return new ChatStreamEvent("tool_call", new
                {
                    CallId = call.Id,
                    Name = call.Name,
                    ArgumentsJson = call.ArgumentsJson
                });

                var dispatch = await DispatchToolAsync(call, state.Tools, ct);

                var toolResultMsg = AiMessage.CreateToolResultMessage(
                    state.Conversation.Id, call.Id, dispatch.Json, nextOrder++);
                context.AiMessages.Add(toolResultMsg);
                messages.Add(new AiChatMessage("tool", dispatch.Json, ToolCallId: call.Id));

                yield return new ChatStreamEvent("tool_result", new
                {
                    CallId = call.Id,
                    IsError = dispatch.IsError,
                    Content = dispatch.Json
                });
            }

            await context.SaveChangesAsync(ct);
        }

        var finalContent = finalContentBuilder.ToString();
        var citations = CitationParser.Parse(finalContent, retrieved.Children);

        if (citations.Count > 0)
        {
            yield return new ChatStreamEvent("citations", new
            {
                Items = citations.Select(c => new
                {
                    Marker = c.Marker,
                    ChunkId = c.ChunkId,
                    DocumentId = c.DocumentId,
                    DocumentName = c.DocumentName,
                    SectionTitle = c.SectionTitle,
                    PageNumber = c.PageNumber,
                    Score = c.Score
                }).ToList()
            });
        }

        var assistantMessage = await FinalizeTurnAsync(
            state, finalContent, totalInput, totalOutput, nextOrder, citations, ct);

        yield return new ChatStreamEvent("done", new
        {
            MessageId = assistantMessage.Id,
            InputTokens = totalInput,
            OutputTokens = totalOutput,
            FinishReason = finishReason
        });
    }

    // ──────────────────────────────────────────────────────────────
    // Private: shared turn preparation
    // ──────────────────────────────────────────────────────────────

    private async Task<Result<ChatTurnState>> PrepareTurnAsync(
        Guid? conversationId,
        Guid? assistantId,
        string userMessage,
        CancellationToken ct)
    {
        // Auth guard — must be signed in to chat
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<ChatTurnState>(AiErrors.NotAuthenticated);

        AiConversation? conversation;
        AiAssistant? assistant;

        if (conversationId.HasValue)
        {
            // Load existing conversation — returning ConversationNotFound for both missing
            // and foreign ownership avoids leaking existence of other users' data.
            conversation = await context.AiConversations
                .FirstOrDefaultAsync(c => c.Id == conversationId.Value, ct);

            if (conversation is null || conversation.UserId != userId)
                return Result.Failure<ChatTurnState>(AiErrors.ConversationNotFound);

            assistant = await context.AiAssistants
                .FirstOrDefaultAsync(a => a.Id == conversation.AssistantId, ct);

            if (assistant is null || !assistant.IsActive)
                return Result.Failure<ChatTurnState>(AiErrors.AssistantNotFound);
        }
        else
        {
            // New conversation — assistantId is required
            if (!assistantId.HasValue)
                return Result.Failure<ChatTurnState>(AiErrors.AssistantNotFound);

            assistant = await context.AiAssistants
                .FirstOrDefaultAsync(a => a.Id == assistantId.Value, ct);

            if (assistant is null || !assistant.IsActive)
                return Result.Failure<ChatTurnState>(AiErrors.AssistantNotFound);

            conversation = AiConversation.Create(currentUser.TenantId, assistant.Id, userId);
            context.AiConversations.Add(conversation);
        }

        // ACL gate — TenantWide assistants are open to any member of the same tenant
        // (or platform admins whose TenantId is null). Ownership, explicit grants, and
        // admin bypass pass through CanAccessAsync. Not-found masks existence to avoid leaking.
        var tenantWideBypass = assistant.Visibility == ResourceVisibility.TenantWide
            && (currentUser.TenantId == null || currentUser.TenantId == assistant.TenantId);
        var canAccess = tenantWideBypass || await access.CanAccessAsync(
            currentUser, ResourceTypes.AiAssistant, assistant.Id, AccessLevel.Viewer, ct);
        if (!canAccess)
            return Result.Failure<ChatTurnState>(AiErrors.AssistantNotFound);

        // Pre-flight quota gate: increments by 1 to block tenants already at their limit.
        // Concurrent requests can pass simultaneously before any real usage lands;
        // over-consumption is bounded by (concurrent-requests × 1) tokens until IncrementAsync
        // in FinalizeTurnAsync catches up. Acceptable for v1 — revisit if this becomes material.
        if (currentUser.TenantId is Guid tenantId)
        {
            var quotaResult = await quotaChecker.CheckAsync(tenantId, AiTokensMetric, 1, ct);
            if (!quotaResult.Allowed)
            {
                // Fire-and-forget webhook so ops/billing can react (e.g. notify the tenant admin).
                // Failure here must not shadow the quota error the user is about to see.
                try
                {
                    await webhookPublisher.PublishAsync(
                        "ai.quota.exceeded",
                        tenantId,
                        new
                        {
                            TenantId = tenantId,
                            UserId = userId,
                            Metric = AiTokensMetric,
                            Limit = quotaResult.Limit,
                            Current = quotaResult.Current
                        },
                        ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to publish ai.quota.exceeded webhook for tenant {TenantId}.", tenantId);
                }

                return Result.Failure<ChatTurnState>(AiErrors.QuotaExceeded(quotaResult.Limit));
            }
        }

        // Load prior messages ordered by Order to feed to the provider
        var priorMessages = await context.AiMessages
            .Where(m => m.ConversationId == conversation.Id)
            .OrderBy(m => m.Order)
            .ToListAsync(ct);

        var nextOrder = priorMessages.Count == 0 ? 0 : priorMessages[^1].Order + 1;

        var trimmed = userMessage.Trim();
        var userMsg = AiMessage.CreateUserMessage(conversation.Id, trimmed, nextOrder);
        context.AiMessages.Add(userMsg);

        // Build provider message list — skip System-role messages (they're injected via
        // AiChatOptions.SystemPrompt instead), map roles to provider string literals
        var providerMessages = new List<AiChatMessage>();

        foreach (var m in priorMessages)
        {
            var role = m.Role switch
            {
                MessageRole.User => "user",
                MessageRole.Assistant => "assistant",
                MessageRole.ToolResult => "tool",
                _ => null  // skip System and any unknown roles
            };

            if (role is null) continue;

            providerMessages.Add(new AiChatMessage(role, m.Content));
        }

        // Append the current user message last
        providerMessages.Add(new AiChatMessage("user", trimmed));

        var toolResolution = await toolRegistry.ResolveForAssistantAsync(assistant, ct);

        return Result.Success(new ChatTurnState(
            conversation,
            assistant,
            userMsg,
            providerMessages,
            nextOrder + 1,  // NextOrder = order for the upcoming assistant reply
            toolResolution
        ));
    }

    // ──────────────────────────────────────────────────────────────
    // Private: shared post-provider finalization
    // ──────────────────────────────────────────────────────────────

    private async Task<AiMessage> FinalizeTurnAsync(
        ChatTurnState state,
        string? content,
        int inputTokens,
        int outputTokens,
        int order,
        IReadOnlyList<AiMessageCitation> citations,
        CancellationToken ct)
    {
        var assistantMessage = citations.Count > 0
            ? AiMessage.CreateAssistantMessageWithCitations(
                state.Conversation.Id,
                content ?? "",
                order,
                citations,
                inputTokens,
                outputTokens)
            : AiMessage.CreateAssistantMessage(
                state.Conversation.Id,
                content ?? "",
                order,
                inputTokens,
                outputTokens);

        context.AiMessages.Add(assistantMessage);

        state.Conversation.AddMessageStats(inputTokens, outputTokens);

        // Auto-title on first assistant reply — truncate user message to MaxTitleLength chars
        if (state.Conversation.Title is null
            && state.UserMessage.Content is { Length: > 0 } text)
        {
            var title = text.Length > MaxTitleLength
                ? text[..MaxTitleLength] + "…"
                : text;
            state.Conversation.SetTitle(title);
        }

        var resolvedProvider = ResolveProvider(state.Assistant);
        var model = state.Assistant.Model
            ?? providerFactory.GetDefaultProviderType().ToString();

        var estimatedCost = EstimateCost(resolvedProvider, inputTokens, outputTokens);

        var usageLog = AiUsageLog.Create(
            tenantId: currentUser.TenantId,
            userId: currentUser.UserId!.Value,   // safe — PrepareTurnAsync already guarded
            provider: resolvedProvider,
            model: model,
            inputTokens: inputTokens,
            outputTokens: outputTokens,
            estimatedCost: estimatedCost,
            requestType: AiRequestType.Chat,
            conversationId: state.Conversation.Id);

        context.AiUsageLogs.Add(usageLog);

        await context.SaveChangesAsync(ct);

        // Increment quota + usage tracker after a successful save.
        // Failure here is non-fatal — we've already persisted the turn.
        var totalTokens = inputTokens + outputTokens;
        if (currentUser.TenantId is Guid tenantId && totalTokens > 0)
        {
            try
            {
                await quotaChecker.IncrementAsync(tenantId, AiTokensMetric, totalTokens, ct);
                await usageTracker.IncrementAsync(tenantId, AiTokensMetric, totalTokens, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to increment AI token quota/usage for tenant {TenantId}. " +
                    "The chat turn was persisted successfully.", tenantId);
            }
        }

        try
        {
            await webhookPublisher.PublishAsync(
                "ai.chat.completed",
                currentUser.TenantId,
                new
                {
                    ConversationId = state.Conversation.Id,
                    UserId = currentUser.UserId!.Value,
                    AssistantId = state.Assistant.Id,
                    MessageCount = state.Conversation.MessageCount,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens
                },
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to publish ai.chat.completed webhook for conversation {ConversationId}.",
                state.Conversation.Id);
        }

        return assistantMessage;
    }

    // ──────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Cleans up after a failed turn. Detaches the pending user message so it is never
    /// flushed as an orphan row. For newly created conversations (never saved to DB) the
    /// conversation is also detached — nothing is persisted and MarkFailed is skipped.
    /// For pre-existing conversations the Failed status is saved so clients can see the
    /// conversation reached a terminal state.
    /// </summary>
    private async Task FailTurnAsync(ChatTurnState state)
    {
        var wasNew = context.Entry(state.Conversation).State == EntityState.Added;
        context.Entry(state.UserMessage).State = EntityState.Detached;
        if (wasNew)
        {
            context.Entry(state.Conversation).State = EntityState.Detached;
            return; // nothing to persist — new conversation never made it to DB
        }
        state.Conversation.MarkFailed();

        // Bound the save on a 5-second timeout so a hung DB can't leave the request
        // pinned forever. Using a non-cancellable token here is intentional — the caller's
        // token may already be cancelled (mid-stream client disconnect) and we still want
        // to best-effort persist the Failed status so the conversation has a terminal state.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await context.SaveChangesAsync(cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to persist Failed status for conversation {ConversationId}.",
                state.Conversation.Id);
        }
    }

    private AiProviderType ResolveProvider(AiAssistant assistant) =>
        assistant.Provider ?? providerFactory.GetDefaultProviderType();

    private static AiChatOptions BuildChatOptions(
        AiAssistant assistant,
        string systemPrompt,
        IReadOnlyList<AiToolDefinitionDto> tools) =>
        new(
            Model: assistant.Model ?? "",
            Temperature: assistant.Temperature,
            MaxTokens: assistant.MaxTokens,
            SystemPrompt: systemPrompt,
            Tools: tools.Count == 0 ? null : tools);

    private async Task<RetrievedContext> RetrieveContextSafelyAsync(
        AiAssistant assistant, string userMessage, IReadOnlyList<AiChatMessage> providerMessages, CancellationToken ct)
    {
        if (assistant.RagScope == AiRagScope.None || string.IsNullOrWhiteSpace(userMessage))
            return RetrievedContext.Empty;

        try
        {
            var history = BuildRagHistory(providerMessages);
            var retrieved = await retrievalService.RetrieveForTurnAsync(assistant, userMessage, history, ct);
            AiRagMetrics.ContextTokens.Record(retrieved.TotalTokens);
            if (retrieved.TruncatedByBudget)
            {
                AiRagMetrics.ContextTruncated.Add(
                    1, new KeyValuePair<string, object?>("rag.reason", "budget"));
            }
            foreach (var stage in retrieved.DegradedStages)
            {
                AiRagMetrics.DegradedStages.Add(
                    1, new KeyValuePair<string, object?>("rag.stage", stage));
            }
            var requestId = Guid.NewGuid();

            var eventName = retrieved.DegradedStages.Count > 0
                ? RagWebhookEventNames.Degraded
                : RagWebhookEventNames.Completed;

            var payload = new
            {
                RequestId = requestId,
                AssistantId = assistant.Id,
                TenantId = currentUser.TenantId,
                KeptChildren = retrieved.Children.Count,
                KeptParents = retrieved.Parents.Count,
                SiblingsCount = retrieved.Siblings.Count,
                FusedCandidates = retrieved.FusedCandidates,
                TotalTokens = retrieved.TotalTokens,
                Truncated = retrieved.TruncatedByBudget,
                DegradedStages = retrieved.DegradedStages.Count == 0
                    ? Array.Empty<string>()
                    : retrieved.DegradedStages.ToArray(),
                DetectedLanguage = retrieved.DetectedLanguage,
                Stages = Array.Empty<object>()
            };

            await PublishRagLifecycleAsync(eventName, currentUser.TenantId, payload, ct);

            var degradedSummary = retrieved.DegradedStages.Count == 0
                ? "none"
                : string.Join(",", retrieved.DegradedStages);

            logger.LogInformation(
                "RAG retrieval done assistant={AssistantId} req={RequestId} children={Children} parents={Parents} siblings={Siblings} tokens={Tokens} truncated={Truncated} stages={StagesSummary} degraded={DegradedStages} lang={DetectedLang}",
                assistant.Id,
                requestId,
                retrieved.Children.Count,
                retrieved.Parents.Count,
                retrieved.Siblings.Count,
                retrieved.TotalTokens,
                retrieved.TruncatedByBudget,
                "all",
                degradedSummary,
                retrieved.DetectedLanguage);

            return retrieved;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RAG retrieval failed for assistant {AssistantId}; proceeding without context.",
                assistant.Id);

            var failedPayload = new
            {
                RequestId = Guid.NewGuid(),
                AssistantId = assistant.Id,
                TenantId = currentUser.TenantId,
                Error = ex.Message
            };
            await PublishRagLifecycleAsync(RagWebhookEventNames.Failed, currentUser.TenantId, failedPayload, ct);

            return RetrievedContext.Empty;
        }
    }

    private async Task PublishRagLifecycleAsync(string eventType, Guid? tenantId, object payload, CancellationToken ct)
    {
        var publishSw = System.Diagnostics.Stopwatch.StartNew();
        var publishOutcome = RagStageOutcome.Success;
        try
        {
            using var publishCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            publishCts.CancelAfter(500);
            await webhookPublisher.PublishAsync(eventType, tenantId, payload, publishCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            publishOutcome = RagStageOutcome.Timeout;
            logger.LogWarning("RAG webhook publish timed out for event {EventType}", eventType);
        }
        catch (Exception ex)
        {
            publishOutcome = RagStageOutcome.Error;
            logger.LogWarning(ex, "RAG webhook publish failed for event {EventType}", eventType);
        }
        finally
        {
            publishSw.Stop();
            AiRagMetrics.StageDuration.Record(
                publishSw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("rag.stage", "webhook-publish"));
            AiRagMetrics.StageOutcome.Add(
                1,
                new KeyValuePair<string, object?>("rag.stage", "webhook-publish"),
                new KeyValuePair<string, object?>("rag.outcome", publishOutcome));
        }
    }

    private static string ResolveSystemPrompt(AiAssistant assistant, RetrievedContext retrieved) =>
        retrieved.IsEmpty
            ? assistant.SystemPrompt
            : ContextPromptBuilder.Build(assistant.SystemPrompt, retrieved);

    /// <summary>
    /// Converts the provider message list into a history slice for the RAG retrieval service.
    /// The last entry is the current turn's user message — it is excluded so the resolver
    /// only sees prior conversation turns.
    /// </summary>
    private static IReadOnlyList<RagHistoryMessage> BuildRagHistory(IReadOnlyList<AiChatMessage> providerMessages)
    {
        // providerMessages' last entry is the current turn's user message — drop it
        // so the resolver only sees prior conversation.
        if (providerMessages.Count <= 1) return Array.Empty<RagHistoryMessage>();

        var result = new List<RagHistoryMessage>(providerMessages.Count - 1);
        for (var i = 0; i < providerMessages.Count - 1; i++)
        {
            var m = providerMessages[i];
            if (m.Role != "user" && m.Role != "assistant") continue;
            if (string.IsNullOrWhiteSpace(m.Content)) continue;
            result.Add(new RagHistoryMessage(m.Role, m.Content));
        }
        return result;
    }

    /// <summary>
    /// 4 chars per token is a widely used rough heuristic (GPT tokenizer averages ~3.5-4).
    /// </summary>
    private static int EstimateTokens(int charCount) => Math.Max(1, charCount / 4);

    private decimal EstimateCost(AiProviderType provider, int inputTokens, int outputTokens)
    {
        var section = configuration.GetSection($"AI:Providers:{provider}");
        var inRate = section.GetValue<decimal?>("CostPerInputToken") ?? 0m;
        var outRate = section.GetValue<decimal?>("CostPerOutputToken") ?? 0m;
        return inputTokens * inRate + outputTokens * outRate;
    }

    /// <summary>
    /// Wraps MoveNextAsync in a try/catch so that a provider exception mid-stream
    /// is surfaced as a ChunkOrError rather than propagating through the caller's
    /// yield state machine (which would suppress the "done" event).
    /// OperationCanceledException is re-thrown — callers should stop on cancellation.
    /// C# does not allow yield inside catch; errors are staged in a local and yielded after.
    /// </summary>
    private static async IAsyncEnumerable<ChunkOrError> EnumerateSafelyAsync(
        IAsyncEnumerable<AiChatChunk> source,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var enumerator = source.GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                bool hasNext;
                AiChatChunk? current = null;
                string? iterationError = null;

                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                    if (hasNext)
                        current = enumerator.Current;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    hasNext = false;
                    iterationError = ex.Message;
                }

                // Yield error outside the catch to satisfy the C# compiler
                if (iterationError is not null)
                {
                    yield return new ChunkOrError(null, iterationError);
                    yield break;
                }

                if (!hasNext) break;

                yield return new ChunkOrError(current!, null);
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    private async Task<ToolDispatchResult> DispatchToolAsync(
        AiToolCall call,
        ToolResolutionResult tools,
        CancellationToken ct)
    {
        if (!tools.DefinitionsByName.TryGetValue(call.Name, out var def))
            return Failure(AiErrors.ToolNotFound);

        if (!currentUser.HasPermission(def.RequiredPermission))
            return Failure(AiErrors.ToolPermissionDenied(call.Name));

        object? command;
        try
        {
            command = System.Text.Json.JsonSerializer.Deserialize(
                call.ArgumentsJson,
                def.CommandType,
                SerializerOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deserialize args for tool {Tool}.", call.Name);
            return Failure(AiErrors.ToolArgumentsInvalid(call.Name, ex.Message));
        }

        if (command is null)
            return Failure(AiErrors.ToolArgumentsInvalid(call.Name, "Deserialized arguments were null."));

        object? rawResult;
        try
        {
            rawResult = await sender.Send(command, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tool {Tool} threw during dispatch.", call.Name);
            return Failure(AiErrors.ToolExecutionFailed(call.Name, ex.Message));
        }

        // Commands that return Result / Result<T> surface failure through Error rather than throwing.
        if (rawResult is Result r)
        {
            if (r.IsFailure)
                return Failure(r.Error);

            var resultType = rawResult.GetType();
            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var value = resultType.GetProperty("Value")!.GetValue(rawResult);
                return Success(value);
            }

            return Success(null);
        }

        return Success(rawResult);

        static ToolDispatchResult Success(object? value) => new(
            System.Text.Json.JsonSerializer.Serialize(new { ok = true, value }, SerializerOptions),
            IsError: false);

        static ToolDispatchResult Failure(Error error) => new(
            System.Text.Json.JsonSerializer.Serialize(
                new { ok = false, error = new { code = error.Code, message = error.Description } },
                SerializerOptions),
            IsError: true);
    }

    private static readonly System.Text.Json.JsonSerializerOptions SerializerOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

    // ──────────────────────────────────────────────────────────────
    // Private types
    // ──────────────────────────────────────────────────────────────

    private sealed record ChunkOrError(AiChatChunk? Chunk, string? Error);

    private sealed record ToolDispatchResult(string Json, bool IsError);

    private sealed class ToolCallBuilder(string id, string name)
    {
        private readonly StringBuilder _args = new();

        public string Id { get; } = id;
        public string Name { get; } = name;

        public void AppendArguments(string fragment)
        {
            if (!string.IsNullOrEmpty(fragment)) _args.Append(fragment);
        }

        public AiToolCall Build()
        {
            var json = _args.Length == 0 ? "{}" : _args.ToString();
            return new AiToolCall(Id, Name, json);
        }
    }

    private sealed record ChatTurnState(
        AiConversation Conversation,
        AiAssistant Assistant,
        AiMessage UserMessage,
        List<AiChatMessage> ProviderMessages,
        int NextOrder,
        ToolResolutionResult Tools);
}
