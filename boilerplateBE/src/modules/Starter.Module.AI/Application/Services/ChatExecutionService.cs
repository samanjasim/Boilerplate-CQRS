using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Domain.Entities;
using Starter.Module.AI.Domain.Enums;
using Starter.Module.AI.Domain.Errors;
using Starter.Module.AI.Infrastructure.Persistence;
using Starter.Module.AI.Infrastructure.Providers;
using Starter.Shared.Results;

namespace Starter.Module.AI.Application.Services;

internal sealed class ChatExecutionService(
    AiDbContext context,
    ICurrentUserService currentUser,
    AiProviderFactory providerFactory,
    IQuotaChecker quotaChecker,
    IUsageTracker usageTracker,
    IWebhookPublisher webhookPublisher,
    IConfiguration configuration,
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
        var provider = providerFactory.Create(ResolveProvider(state.Assistant));
        var chatOptions = BuildChatOptions(state.Assistant);

        AiChatCompletion completion;
        try
        {
            completion = await provider.ChatAsync(state.ProviderMessages, chatOptions, ct);
        }
        catch (Exception ex)
        {
            state.Conversation.MarkFailed();
            await context.SaveChangesAsync(CancellationToken.None);
            return Result.Failure<AiChatReplyDto>(AiErrors.ProviderError(ex.Message));
        }

        var assistantMessage = await FinalizeTurnAsync(
            state,
            completion.Content,
            completion.InputTokens,
            completion.OutputTokens,
            ct);

        return Result.Success(new AiChatReplyDto(
            state.Conversation.Id,
            state.UserMessage.ToDto(),
            assistantMessage.ToDto()));
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

        // Reserve an advisory ID so the client can correlate the "start" event with the
        // eventual persisted message. The factory always generates a fresh Guid, so the
        // authoritative ID comes back in the "done" event (done.MessageId).
        var advisoryAssistantMessageId = Guid.NewGuid();

        yield return new ChatStreamEvent("start", new
        {
            ConversationId = state.Conversation.Id,
            UserMessageId = state.UserMessage.Id,
            AssistantMessageId = advisoryAssistantMessageId
        });

        var provider = providerFactory.Create(ResolveProvider(state.Assistant));
        var chatOptions = BuildChatOptions(state.Assistant);

        var contentBuilder = new StringBuilder();
        var finishReason = "stop";
        var streamFailed = false;
        var inputTokens = 0;
        var outputTokens = 0;

        await foreach (var chunkOrError in EnumerateSafelyAsync(
            provider.StreamChatAsync(state.ProviderMessages, chatOptions, ct), ct))
        {
            if (chunkOrError.Error is not null)
            {
                state.Conversation.MarkFailed();
                await context.SaveChangesAsync(CancellationToken.None);

                yield return new ChatStreamEvent("error", new
                {
                    Code = "Ai.ProviderError",
                    Message = chunkOrError.Error
                });
                streamFailed = true;
                yield break;
            }

            var chunk = chunkOrError.Chunk!;

            if (chunk.FinishReason is not null)
                finishReason = chunk.FinishReason;

            if (chunk.ContentDelta is { Length: > 0 } delta)
            {
                contentBuilder.Append(delta);
                yield return new ChatStreamEvent("delta", new { Content = delta });
            }
        }

        if (streamFailed)
            yield break;

        var finalContent = contentBuilder.ToString();

        // Fallback token estimation — providers that support streaming may not emit
        // token counts mid-stream. Use the 4-chars-per-token heuristic.
        var totalInputChars = state.ProviderMessages.Sum(m => m.Content?.Length ?? 0);
        inputTokens = inputTokens > 0 ? inputTokens : EstimateTokens(totalInputChars);
        outputTokens = outputTokens > 0 ? outputTokens : EstimateTokens(finalContent.Length);

        var assistantMessage = await FinalizeTurnAsync(
            state,
            finalContent,
            inputTokens,
            outputTokens,
            ct,
            advisoryAssistantMessageId);

        yield return new ChatStreamEvent("done", new
        {
            MessageId = assistantMessage.Id,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
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
            return Result.Failure<ChatTurnState>(
                new Error("Ai.NotAuthenticated", "You must be signed in to chat.", ErrorType.Unauthorized));

        AiConversation conversation;
        AiAssistant assistant;

        if (conversationId.HasValue)
        {
            // Load existing conversation — returning ConversationNotFound for both missing
            // and foreign ownership avoids leaking existence of other users' data.
            conversation = await context.AiConversations
                .FirstOrDefaultAsync(c => c.Id == conversationId.Value, ct)
                ?? null!;

            if (conversation is null || conversation.UserId != userId)
                return Result.Failure<ChatTurnState>(AiErrors.ConversationNotFound);

            assistant = await context.AiAssistants
                .FirstOrDefaultAsync(a => a.Id == conversation.AssistantId, ct)
                ?? null!;

            if (assistant is null || !assistant.IsActive)
                return Result.Failure<ChatTurnState>(AiErrors.AssistantNotFound);
        }
        else
        {
            // New conversation — assistantId is required
            if (!assistantId.HasValue)
                return Result.Failure<ChatTurnState>(AiErrors.AssistantNotFound);

            assistant = await context.AiAssistants
                .FirstOrDefaultAsync(a => a.Id == assistantId.Value, ct)
                ?? null!;

            if (assistant is null || !assistant.IsActive)
                return Result.Failure<ChatTurnState>(AiErrors.AssistantNotFound);

            conversation = AiConversation.Create(currentUser.TenantId, assistant.Id, userId);
            context.AiConversations.Add(conversation);
        }

        // Quota pre-check acts as can-they-send-at-all gate (not per-message token gate).
        // Full token quota enforcement (with actual token count) happens after the provider
        // call inside FinalizeTurnAsync.
        if (currentUser.TenantId is Guid tenantId)
        {
            var quotaResult = await quotaChecker.CheckAsync(tenantId, AiTokensMetric, 1, ct);
            if (!quotaResult.Allowed)
                return Result.Failure<ChatTurnState>(AiErrors.QuotaExceeded(quotaResult.Limit));
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

        return Result.Success(new ChatTurnState(
            conversation,
            assistant,
            userMsg,
            providerMessages,
            nextOrder + 1  // NextOrder = order for the upcoming assistant reply
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
        CancellationToken ct,
        Guid? presetMessageId = null)
    {
        // The factory always generates a fresh Guid — presetMessageId is advisory only.
        // Clients should use done.MessageId (returned here) as the authoritative message ID.
        _ = presetMessageId;

        var assistantMessage = AiMessage.CreateAssistantMessage(
            state.Conversation.Id,
            content ?? "",
            state.NextOrder,
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

    private AiProviderType ResolveProvider(AiAssistant assistant) =>
        assistant.Provider ?? providerFactory.GetDefaultProviderType();

    private static AiChatOptions BuildChatOptions(AiAssistant assistant) =>
        new(
            Model: assistant.Model ?? "",
            Temperature: assistant.Temperature,
            MaxTokens: assistant.MaxTokens,
            SystemPrompt: assistant.SystemPrompt,
            Tools: null);

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

    // ──────────────────────────────────────────────────────────────
    // Private types
    // ──────────────────────────────────────────────────────────────

    private sealed record ChunkOrError(AiChatChunk? Chunk, string? Error);

    private sealed record ChatTurnState(
        AiConversation Conversation,
        AiAssistant Assistant,
        AiMessage UserMessage,
        List<AiChatMessage> ProviderMessages,
        int NextOrder);
}
