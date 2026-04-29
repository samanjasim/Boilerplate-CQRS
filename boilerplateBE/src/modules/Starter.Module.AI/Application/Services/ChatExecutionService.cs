using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Access;
using Starter.Application.Common.Access.Contracts;
using Starter.Application.Common.Interfaces;
using Starter.Domain.Common.Access.Enums;
using Starter.Module.AI.Application.DTOs;
using Starter.Module.AI.Application.Services.Personas;
using Starter.Module.AI.Application.Services.Retrieval;
using Starter.Module.AI.Application.Services.Runtime;
using Starter.Module.AI.Application.Services.Settings;
using Starter.Module.AI.Domain.Entities;
using Starter.Abstractions.Ai;
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
    IAiAgentRuntimeFactory agentRuntimeFactory,
    IConfiguration configuration,
    IResourceAccessService access,
    IPersonaResolver personaResolver,
    ISafetyPresetClauseProvider safetyClauses,
    IPersonaContextAccessor personaContextAccessor,
    IAiProviderCredentialResolver providerCredentialResolver,
    IAiModelDefaultResolver modelDefaults,
    IAiBrandPromptResolver brandPromptResolver,
    ILogger<ChatExecutionService> logger) : IChatExecutionService
{
    private const string AiTokensMetric = "ai_tokens";
    private const int MaxTitleLength = 80;
    private const string StepBudgetExceededMessage =
        "I couldn't fully complete the task within my step budget. Please narrow the request.";

    // ──────────────────────────────────────────────────────────────
    // Public: non-streaming turn
    // ──────────────────────────────────────────────────────────────

    public async Task<Result<AiChatReplyDto>> ExecuteAsync(
        Guid? conversationId,
        Guid? assistantId,
        string userMessage,
        Guid? personaId = null,
        CancellationToken ct = default)
    {
        var stateResult = await PrepareTurnAsync(conversationId, assistantId, userMessage, personaId, ct);
        if (stateResult.IsFailure)
            return Result.Failure<AiChatReplyDto>(stateResult.Error);

        var state = stateResult.Value;
        var retrieved = await RetrieveContextSafelyAsync(
            state.Assistant, userMessage, state.ProviderMessages, ct);
        var effectiveSystemPrompt = await ResolveEffectiveSystemPromptAsync(state.Assistant, retrieved, state.Persona, ct);

        var modelResult = await modelDefaults.ResolveAsync(
            state.Assistant.TenantId,
            state.Assistant.ExecutionMode == AssistantExecutionMode.Agent
                ? AiAgentClass.ToolAgent
                : AiAgentClass.Chat,
            state.Assistant.Provider,
            state.Assistant.Model,
            state.Assistant.Temperature,
            state.Assistant.MaxTokens,
            ct);
        if (modelResult.IsFailure)
            return Result.Failure<AiChatReplyDto>(modelResult.Error);

        var modelConfig = modelResult.Value;
        var provider = modelConfig.Provider;
        var credentialResult = await providerCredentialResolver.ResolveAsync(
            state.Assistant.TenantId,
            provider,
            ct);
        if (credentialResult.IsFailure)
            return Result.Failure<AiChatReplyDto>(credentialResult.Error);

        var credential = credentialResult.Value;
        var stepBudget = Math.Clamp(state.Assistant.MaxAgentSteps, 1, 20);
        var ctx = new AgentRunContext(
            Messages: state.ProviderMessages,
            SystemPrompt: effectiveSystemPrompt,
            ModelConfig: new AgentModelConfig(
                Provider: modelConfig.Provider,
                Model: modelConfig.Model,
                Temperature: modelConfig.Temperature,
                MaxTokens: modelConfig.MaxTokens),
            Tools: state.Tools,
            MaxSteps: stepBudget,
            LoopBreak: LoopBreakPolicy.Default,
            Streaming: false,
            Persona: state.Persona,
            AssistantId: state.Assistant.Id,
            TenantId: state.Assistant.TenantId,
            CallerUserId: currentUser.UserId,
            CallerHasPermission: currentUser.HasPermission,
            // Plan 5d-2: thread display name + conversation id so the runtime can install
            // ICurrentAgentRunContextAccessor for the agent dispatcher (pending approvals).
            AssistantName: state.Assistant.Name,
            ConversationId: state.Conversation.Id,
            ProviderApiKey: credential.Secret,
            ProviderCredentialSource: credential.Source,
            ProviderCredentialId: credential.ProviderCredentialId);

        var sink = new ChatAgentRunSink(context, state.Conversation.Id, state.NextOrder, streamWriter: null);

        AgentRunResult runResult;
        try
        {
            var runtime = agentRuntimeFactory.Create(provider);
            runResult = await runtime.RunAsync(ctx, sink, ct);
        }
        catch (Exception ex)
        {
            await FailTurnAsync(state);
            return Result.Failure<AiChatReplyDto>(AiErrors.ProviderError(ex.Message));
        }

        if (runResult.Status == AgentRunStatus.ProviderError)
        {
            await FailTurnAsync(state);
            return Result.Failure<AiChatReplyDto>(AiErrors.ProviderError(runResult.TerminationReason ?? "provider error"));
        }

        if (runResult.Status == AgentRunStatus.Cancelled)
        {
            await FailTurnAsync(state);
            ct.ThrowIfCancellationRequested();
            // If ct wasn't cancelled but the runtime returned Cancelled (shouldn't happen),
            // surface as provider error for consistent API shape.
            return Result.Failure<AiChatReplyDto>(AiErrors.ProviderError("cancelled"));
        }

        // ──────────────────────────────────────────────────────────────
        // Plan 5d-2: moderation outcomes from the outermost runtime decorator. Each branch
        // has a distinct persistence shape — keep the order: InputBlocked first (no LLM
        // ran, NO usage log), OutputBlocked next (LLM ran, full FinalizeTurnAsync),
        // AwaitingApproval (partial run, conversation stays Active),
        // ModerationProviderUnavailable (real outage, surface 5xx; do not pollute history).
        // These run BEFORE CostCap/RateLimit because the moderation decorator is wired
        // outermost — once it short-circuits, the inner decorators never observe the run.
        // ──────────────────────────────────────────────────────────────
        if (runResult.Status == AgentRunStatus.InputBlocked)
        {
            // No tokens used. M2 acid invariant: no AiUsageLog row may exist after this path.
            // We still persist the user message + a refusal as the assistant message so
            // chat history is honest about what happened.
            var refusalContent = runResult.FinalContent ?? "Request blocked by content moderation.";
            var assistantMsg = AiMessage.CreateAssistantMessage(
                state.Conversation.Id, refusalContent, sink.NextOrder, 0, 0);
            context.AiMessages.Add(assistantMsg);
            state.Conversation.AddMessageStats(0, 0);
            PersistModerationEvents(runResult.ModerationEvents);
            await context.SaveChangesAsync(ct);

            return Result.Success(new AiChatReplyDto(
                state.Conversation.Id,
                state.UserMessage.ToDto(),
                assistantMsg.ToDto(),
                PersonaSlug: state.Persona?.Slug)
            {
                Status = "blocked"
            });
        }

        if (runResult.Status == AgentRunStatus.OutputBlocked)
        {
            // Tokens WERE consumed (the LLM ran before the output was blocked). Full
            // FinalizeTurnAsync to persist the usage log alongside the refusal message —
            // moderation events stage onto the change tracker first so they flush atomically.
            var refusalContent = runResult.FinalContent ?? "Response blocked by content moderation.";
            PersistModerationEvents(runResult.ModerationEvents);
            var refusalMessage = await FinalizeTurnAsync(
                state, ctx, refusalContent,
                (int)runResult.TotalInputTokens, (int)runResult.TotalOutputTokens,
                sink.NextOrder, Array.Empty<AiMessageCitation>(), ct);

            return Result.Success(new AiChatReplyDto(
                state.Conversation.Id,
                state.UserMessage.ToDto(),
                refusalMessage.ToDto(),
                PersonaSlug: state.Persona?.Slug)
            {
                Status = "blocked"
            });
        }

        if (runResult.Status == AgentRunStatus.AwaitingApproval)
        {
            // Partial run: persist the user msg + a placeholder "awaiting approval"
            // assistant message so chat history shows the pause point. Conversation stays
            // Active (NOT Failed) because the next /approve call will resume the turn.
            // Usage log records tokens that were consumed during the partial run.
            var approvalId = ExtractApprovalId(runResult);
            var approval = approvalId is { } id
                ? await context.AiPendingApprovals.FirstOrDefaultAsync(p => p.Id == id, ct)
                : null;

            var pauseMsgContent = $"Awaiting approval for action: {approval?.ToolName ?? "unknown"}";
            var pauseMsg = AiMessage.CreateAssistantMessage(
                state.Conversation.Id,
                pauseMsgContent,
                sink.NextOrder,
                (int)runResult.TotalInputTokens,
                (int)runResult.TotalOutputTokens);
            context.AiMessages.Add(pauseMsg);
            state.Conversation.AddMessageStats(
                (int)runResult.TotalInputTokens, (int)runResult.TotalOutputTokens);

            if (runResult.TotalInputTokens > 0 || runResult.TotalOutputTokens > 0)
            {
                var usage = await CreateUsageLogAsync(state, ctx,
                    (int)runResult.TotalInputTokens, (int)runResult.TotalOutputTokens, ct);
                context.AiUsageLogs.Add(usage);
            }

            AiAgentMetrics.PendingApprovals.Add(1,
                new KeyValuePair<string, object?>("ai.approval.outcome", "created"));

            await context.SaveChangesAsync(ct);

            return Result.Success(new AiChatReplyDto(
                state.Conversation.Id,
                state.UserMessage.ToDto(),
                pauseMsg.ToDto(),
                PersonaSlug: state.Persona?.Slug)
            {
                Status = "awaiting_approval",
                ApprovalId = approval?.Id,
                ExpiresAt = approval?.ExpiresAt,
                ToolName = approval?.ToolName,
                ApprovalReason = approval?.ReasonHint
            });
        }

        if (runResult.Status == AgentRunStatus.ModerationProviderUnavailable)
        {
            // Real moderator outage (FailClosed kicked in). Detach pending entities so we
            // don't pollute history with a "service down" message; surface a 5xx-class
            // error so clients retry.
            await FailTurnAsync(state);
            return Result.Failure<AiChatReplyDto>(AiModerationErrors.ProviderUnavailable);
        }

        // Plan 5d-1: surface cost-cap and rate-limit refusals as proper errors so the
        // controller maps them to HTTP 429 (via ErrorType.TooManyRequests). Without this
        // the run silently completes with empty content + 0-token AiUsageLog + 200 OK.
        if (runResult.Status == AgentRunStatus.CostCapExceeded)
        {
            await FailTurnAsync(state);
            return Result.Failure<AiChatReplyDto>(Error.TooManyRequests(
                "AiAgent.CostCapExceeded",
                runResult.TerminationReason ?? "Cost cap exceeded."));
        }

        if (runResult.Status == AgentRunStatus.RateLimitExceeded)
        {
            await FailTurnAsync(state);
            return Result.Failure<AiChatReplyDto>(Error.TooManyRequests(
                "AiAgent.RateLimitExceeded",
                runResult.TerminationReason ?? "Rate limit exceeded."));
        }

        var finalContent = runResult.Status switch
        {
            AgentRunStatus.Completed => runResult.FinalContent ?? "",
            AgentRunStatus.MaxStepsExceeded => StepBudgetExceededMessage,
            AgentRunStatus.LoopBreak => StepBudgetExceededMessage,
            _ => ""
        };

        var citations = CitationParser.Parse(finalContent, retrieved.Children);
        var finalOrder = sink.NextOrder;
        // Stage moderation events (input + output Allowed/Redacted from the decorator) onto
        // the change tracker BEFORE FinalizeTurnAsync so they flush in the same SaveChanges.
        // Without this, LogAllOutcomes-emitted events on the happy path are silently dropped.
        PersistModerationEvents(runResult.ModerationEvents);
        var finalMessage = await FinalizeTurnAsync(
            state, ctx, finalContent,
            (int)runResult.TotalInputTokens, (int)runResult.TotalOutputTokens,
            finalOrder, citations, ct);

        return Result.Success(new AiChatReplyDto(
            state.Conversation.Id,
            state.UserMessage.ToDto(),
            finalMessage.ToDto(),
            PersonaSlug: state.Persona?.Slug));
    }

    // ──────────────────────────────────────────────────────────────
    // Public: streaming turn
    // ──────────────────────────────────────────────────────────────

    public async IAsyncEnumerable<ChatStreamEvent> ExecuteStreamAsync(
        Guid? conversationId,
        Guid? assistantId,
        string userMessage,
        Guid? personaId = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var stateResult = await PrepareTurnAsync(conversationId, assistantId, userMessage, personaId, ct);
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
            UserMessageId = state.UserMessage.Id,
            PersonaSlug = state.Persona?.Slug
        });

        var retrieved = await RetrieveContextSafelyAsync(
            state.Assistant, userMessage, state.ProviderMessages, ct);
        var effectiveSystemPrompt = await ResolveEffectiveSystemPromptAsync(state.Assistant, retrieved, state.Persona, ct);

        var modelResult = await modelDefaults.ResolveAsync(
            state.Assistant.TenantId,
            state.Assistant.ExecutionMode == AssistantExecutionMode.Agent
                ? AiAgentClass.ToolAgent
                : AiAgentClass.Chat,
            state.Assistant.Provider,
            state.Assistant.Model,
            state.Assistant.Temperature,
            state.Assistant.MaxTokens,
            ct);
        if (modelResult.IsFailure)
        {
            yield return new ChatStreamEvent("error", new
            {
                Code = modelResult.Error.Code,
                Message = modelResult.Error.Description
            });
            yield break;
        }

        var modelConfig = modelResult.Value;
        var provider = modelConfig.Provider;
        var credentialResult = await providerCredentialResolver.ResolveAsync(
            state.Assistant.TenantId,
            provider,
            ct);
        if (credentialResult.IsFailure)
        {
            yield return new ChatStreamEvent("error", new
            {
                Code = credentialResult.Error.Code,
                Message = credentialResult.Error.Description
            });
            yield break;
        }

        var credential = credentialResult.Value;
        var stepBudget = Math.Clamp(state.Assistant.MaxAgentSteps, 1, 20);
        var ctx = new AgentRunContext(
            Messages: state.ProviderMessages,
            SystemPrompt: effectiveSystemPrompt,
            ModelConfig: new AgentModelConfig(
                Provider: modelConfig.Provider,
                Model: modelConfig.Model,
                Temperature: modelConfig.Temperature,
                MaxTokens: modelConfig.MaxTokens),
            Tools: state.Tools,
            MaxSteps: stepBudget,
            LoopBreak: LoopBreakPolicy.Default,
            Streaming: true,
            AssistantId: state.Assistant.Id,
            TenantId: state.Assistant.TenantId,
            CallerUserId: currentUser.UserId,
            CallerHasPermission: currentUser.HasPermission,
            Persona: state.Persona,
            // Plan 5d-2: thread display name + conversation id so the runtime can install
            // ICurrentAgentRunContextAccessor for the agent dispatcher (pending approvals).
            AssistantName: state.Assistant.Name,
            ConversationId: state.Conversation.Id,
            ProviderApiKey: credential.Secret,
            ProviderCredentialSource: credential.Source,
            ProviderCredentialId: credential.ProviderCredentialId);

        var channel = Channel.CreateUnbounded<ChatStreamEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
        var sink = new ChatAgentRunSink(context, state.Conversation.Id, state.NextOrder, channel.Writer);

        AgentRunResult? runResult = null;
        Exception? runException = null;

        var runTask = Task.Run(async () =>
        {
            try
            {
                var runtime = agentRuntimeFactory.Create(provider);
                runResult = await runtime.RunAsync(ctx, sink, ct);
            }
            catch (Exception ex)
            {
                runException = ex;
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, ct);

        await foreach (var frame in channel.Reader.ReadAllAsync(ct))
            yield return frame;

        await runTask;

        if (runException is not null)
        {
            await FailTurnAsync(state);
            yield return new ChatStreamEvent("error", new
            {
                Code = "Ai.ProviderError",
                Message = runException.Message
            });
            yield break;
        }

        if (runResult is null)
            yield break;

        // Defensive: the runtime returns Cancelled only when ct was observed; ReadAllAsync(ct)
        // typically throws OCE first, so this branch rarely executes. Retained to cover a
        // future runtime that could return Cancelled for non-ct reasons.
        if (runResult.Status == AgentRunStatus.Cancelled)
        {
            await FailTurnAsync(state);
            ct.ThrowIfCancellationRequested();
            yield return new ChatStreamEvent("error", new
            {
                Code = "Ai.ProviderError",
                Message = "cancelled"
            });
            yield break;
        }

        if (runResult.Status == AgentRunStatus.ProviderError)
        {
            await FailTurnAsync(state);
            yield return new ChatStreamEvent("error", new
            {
                Code = "Ai.ProviderError",
                Message = runResult.TerminationReason ?? "provider error"
            });
            yield break;
        }

        // ──────────────────────────────────────────────────────────────
        // Plan 5d-2: streaming SSE frames for moderation outcomes. Emit the marker frame
        // (moderation_blocked / awaiting_approval / error) BEFORE the terminal "done"
        // frame so the client can branch on the marker and ignore "done" details for
        // refused turns. ProviderUnavailable surfaces an "error" frame and skips "done".
        // ──────────────────────────────────────────────────────────────
        if (runResult.Status == AgentRunStatus.InputBlocked
            || runResult.Status == AgentRunStatus.OutputBlocked)
        {
            yield return new ChatStreamEvent("moderation_blocked", new
            {
                Stage = runResult.Status == AgentRunStatus.InputBlocked ? "input" : "output",
                Reason = runResult.TerminationReason
            });

            // Persistence semantics differ between input vs output blocked: input blocked
            // never invoked the LLM (no usage log row). Output blocked did consume tokens
            // (full FinalizeTurnAsync writes the usage log). Mirror the non-streaming path.
            string refusalContent;
            Guid messageId;
            if (runResult.Status == AgentRunStatus.InputBlocked)
            {
                refusalContent = runResult.FinalContent ?? "Request blocked by content moderation.";
                var assistantMsg = AiMessage.CreateAssistantMessage(
                    state.Conversation.Id, refusalContent, sink.NextOrder, 0, 0);
                context.AiMessages.Add(assistantMsg);
                state.Conversation.AddMessageStats(0, 0);
                PersistModerationEvents(runResult.ModerationEvents);
                await context.SaveChangesAsync(ct);
                messageId = assistantMsg.Id;
            }
            else
            {
                refusalContent = runResult.FinalContent ?? "Response blocked by content moderation.";
                PersistModerationEvents(runResult.ModerationEvents);
                var assistantMsg = await FinalizeTurnAsync(
                    state, ctx, refusalContent,
                    (int)runResult.TotalInputTokens, (int)runResult.TotalOutputTokens,
                    sink.NextOrder, Array.Empty<AiMessageCitation>(), ct);
                messageId = assistantMsg.Id;
            }

            yield return new ChatStreamEvent("done", new
            {
                MessageId = messageId,
                InputTokens = (int)runResult.TotalInputTokens,
                OutputTokens = (int)runResult.TotalOutputTokens,
                FinishReason = runResult.Status.ToString()
            });
            yield break;
        }

        if (runResult.Status == AgentRunStatus.AwaitingApproval)
        {
            var approvalId = ExtractApprovalId(runResult);
            var approval = approvalId is { } id
                ? await context.AiPendingApprovals.FirstOrDefaultAsync(p => p.Id == id, ct)
                : null;

            // Persist the same "awaiting approval" placeholder + optional usage log as the
            // non-streaming branch so chat history is consistent across both transports.
            var pauseMsgContent = $"Awaiting approval for action: {approval?.ToolName ?? "unknown"}";
            var pauseMsg = AiMessage.CreateAssistantMessage(
                state.Conversation.Id,
                pauseMsgContent,
                sink.NextOrder,
                (int)runResult.TotalInputTokens,
                (int)runResult.TotalOutputTokens);
            context.AiMessages.Add(pauseMsg);
            state.Conversation.AddMessageStats(
                (int)runResult.TotalInputTokens, (int)runResult.TotalOutputTokens);

            if (runResult.TotalInputTokens > 0 || runResult.TotalOutputTokens > 0)
            {
                var usage = await CreateUsageLogAsync(state, ctx,
                    (int)runResult.TotalInputTokens, (int)runResult.TotalOutputTokens, ct);
                context.AiUsageLogs.Add(usage);
            }

            AiAgentMetrics.PendingApprovals.Add(1,
                new KeyValuePair<string, object?>("ai.approval.outcome", "created"));

            await context.SaveChangesAsync(ct);

            yield return new ChatStreamEvent("awaiting_approval", new
            {
                ApprovalId = approval?.Id,
                ExpiresAt = approval?.ExpiresAt,
                ToolName = approval?.ToolName,
                Reason = approval?.ReasonHint
            });
            yield return new ChatStreamEvent("done", new
            {
                MessageId = pauseMsg.Id,
                InputTokens = (int)runResult.TotalInputTokens,
                OutputTokens = (int)runResult.TotalOutputTokens,
                FinishReason = "awaiting_approval"
            });
            yield break;
        }

        if (runResult.Status == AgentRunStatus.ModerationProviderUnavailable)
        {
            await FailTurnAsync(state);
            yield return new ChatStreamEvent("error", new
            {
                Code = AiModerationErrors.ProviderUnavailable.Code,
                Message = "Content moderation provider is unavailable; please retry."
            });
            yield break;
        }

        // Plan 5d-1: emit explicit error frames for cap/rate-limit refusals so the
        // streaming client surfaces the correct error and doesn't get a phantom done event.
        if (runResult.Status == AgentRunStatus.CostCapExceeded)
        {
            await FailTurnAsync(state);
            yield return new ChatStreamEvent("error", new
            {
                Code = "AiAgent.CostCapExceeded",
                Message = runResult.TerminationReason ?? "Cost cap exceeded."
            });
            yield break;
        }

        if (runResult.Status == AgentRunStatus.RateLimitExceeded)
        {
            await FailTurnAsync(state);
            yield return new ChatStreamEvent("error", new
            {
                Code = "AiAgent.RateLimitExceeded",
                Message = runResult.TerminationReason ?? "Rate limit exceeded."
            });
            yield break;
        }

        var finalContent = runResult.Status switch
        {
            AgentRunStatus.Completed => runResult.FinalContent ?? "",
            AgentRunStatus.MaxStepsExceeded => StepBudgetExceededMessage,
            AgentRunStatus.LoopBreak => StepBudgetExceededMessage,
            _ => ""
        };

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

        var finalOrder = sink.NextOrder;
        // Stage moderation events from the decorator (LogAllOutcomes Allowed/Redacted) BEFORE
        // FinalizeTurnAsync so they flush atomically with the assistant message + usage log.
        PersistModerationEvents(runResult.ModerationEvents);
        var assistantMessage = await FinalizeTurnAsync(
            state, ctx, finalContent,
            (int)runResult.TotalInputTokens, (int)runResult.TotalOutputTokens,
            finalOrder, citations, ct);

        yield return new ChatStreamEvent("done", new
        {
            MessageId = assistantMessage.Id,
            InputTokens = (int)runResult.TotalInputTokens,
            OutputTokens = (int)runResult.TotalOutputTokens,
            FinishReason = runResult.Status == AgentRunStatus.Completed ? "stop" : runResult.Status.ToString()
        });
    }

    // ──────────────────────────────────────────────────────────────
    // Private: shared turn preparation
    // ──────────────────────────────────────────────────────────────

    private async Task<Result<ChatTurnState>> PrepareTurnAsync(
        Guid? conversationId,
        Guid? assistantId,
        string userMessage,
        Guid? personaId,
        CancellationToken ct)
    {
        // Auth guard — must be signed in to chat
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<ChatTurnState>(AiErrors.NotAuthenticated);

        // Persona resolution (Plan 5b). Guarded by feature flag Ai:Personas:Enabled (default true).
        PersonaContext? persona = null;
        var personasEnabled = configuration.GetValue<bool?>("AI:Personas:Enabled") ?? true;
        if (personasEnabled)
        {
            var personaResult = await personaResolver.ResolveAsync(personaId, ct);
            if (personaResult.IsFailure)
                return Result.Failure<ChatTurnState>(personaResult.Error);
            persona = personaResult.Value;
            personaContextAccessor.Set(persona);
        }

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

        // Persona visibility filter (Plan 5b). Applied after ACL so we don't leak assistant
        // existence to callers who lack any access at all.
        if (persona is not null &&
            !assistant.IsVisibleToPersona(persona.Slug, persona.PermittedAgentSlugs))
            return Result.Failure<ChatTurnState>(AiErrors.AssistantNotPermittedForPersona);

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
            toolResolution,
            persona
        ));
    }

    // ──────────────────────────────────────────────────────────────
    // Private: shared post-provider finalization
    // ──────────────────────────────────────────────────────────────

    private async Task<AiMessage> FinalizeTurnAsync(
        ChatTurnState state,
        AgentRunContext ctx,
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

        // Persona observability (Plan 5b).
        if (state.Persona is { } p)
        {
            AiAgentMetrics.RunsByPersona.Add(1,
                new KeyValuePair<string, object?>("persona_slug", p.Slug),
                new KeyValuePair<string, object?>("audience", p.Audience.ToString()),
                new KeyValuePair<string, object?>("safety", p.Safety.ToString()));

            System.Diagnostics.Activity.Current?.SetTag("ai.persona.slug", p.Slug);
            System.Diagnostics.Activity.Current?.SetTag("ai.persona.audience", p.Audience.ToString());
            System.Diagnostics.Activity.Current?.SetTag("ai.persona.safety", p.Safety.ToString());
        }

        // Auto-title on first assistant reply — truncate user message to MaxTitleLength chars
        if (state.Conversation.Title is null
            && state.UserMessage.Content is { Length: > 0 } text)
        {
            var title = text.Length > MaxTitleLength
                ? text[..MaxTitleLength] + "…"
                : text;
            state.Conversation.SetTitle(title);
        }

        var usageLog = await CreateUsageLogAsync(state, ctx, inputTokens, outputTokens, ct);
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

    /// <summary>
    /// Plan 5d-2: shared helper used by both <see cref="FinalizeTurnAsync"/> and the
    /// <c>AwaitingApproval</c> branch so a partial run can record the tokens it actually
    /// consumed without duplicating the model + cost + agent-principal lookup.
    /// </summary>
    private async Task<AiUsageLog> CreateUsageLogAsync(
        ChatTurnState state,
        AgentRunContext ctx,
        int inputTokens,
        int outputTokens,
        CancellationToken ct)
    {
        var resolvedProvider = ctx.ModelConfig.Provider;
        var model = string.IsNullOrWhiteSpace(ctx.ModelConfig.Model)
            ? providerFactory.GetDefaultProviderType().ToString()
            : ctx.ModelConfig.Model;
        var estimatedCost = EstimateCost(resolvedProvider, inputTokens, outputTokens);

        // Plan 5d-1: stamp the usage log with the assistant + paired agent principal so
        // GetAgentUsage queries and AiCostReconciliationJob can attribute spend to the
        // right agent. Lookup is one-shot per turn (small) and the FK is nullable to
        // preserve compatibility with non-agent code paths.
        Guid? agentPrincipalId = null;
        if (state.Assistant.Id != Guid.Empty)
        {
            agentPrincipalId = await context.AiAgentPrincipals
                .Where(p => p.AiAssistantId == state.Assistant.Id)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);
        }

        return AiUsageLog.Create(
            tenantId: currentUser.TenantId,
            userId: currentUser.UserId!.Value,   // safe — PrepareTurnAsync already guarded
            provider: resolvedProvider,
            model: model,
            inputTokens: inputTokens,
            outputTokens: outputTokens,
            estimatedCost: estimatedCost,
            requestType: AiRequestType.Chat,
            conversationId: state.Conversation.Id,
            aiAssistantId: state.Assistant.Id,
            agentPrincipalId: agentPrincipalId,
            providerCredentialSource: ctx.ProviderCredentialSource,
            providerCredentialId: ctx.ProviderCredentialId);
    }

    /// <summary>
    /// Plan 5d-2: stages moderation events emitted by the moderation runtime decorator
    /// onto the shared change tracker so they flush atomically alongside the user/assistant
    /// messages and (where applicable) the usage log in the next SaveChangesAsync.
    /// </summary>
    private void PersistModerationEvents(IReadOnlyList<AiModerationEvent>? events)
    {
        if (events is null || events.Count == 0) return;
        context.AiModerationEvents.AddRange(events);
    }

    /// <summary>
    /// Plan 5d-2: pulls the approval ID out of the synthetic tool-result JSON written by
    /// <c>AgentToolDispatcher</c> when a [DangerousAction] tool created an approval. The
    /// JSON shape is <c>{"ok":false,"error":{"code":"AiAgent.AwaitingApproval","approvalId":"..."}}</c>.
    /// Returns null if no tool invocation in the last step carries that envelope.
    /// </summary>
    private static Guid? ExtractApprovalId(AgentRunResult result)
    {
        var lastStep = result.Steps.LastOrDefault();
        if (lastStep is null) return null;
        var lastInvocation = lastStep.ToolInvocations.LastOrDefault();
        if (lastInvocation is null || string.IsNullOrWhiteSpace(lastInvocation.ResultJson))
            return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(lastInvocation.ResultJson);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("approvalId", out var idEl) &&
                Guid.TryParse(idEl.GetString(), out var id))
            {
                return id;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Best-effort — a malformed envelope just means we don't surface the approval ID.
        }
        return null;
    }

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

    private string ResolveSystemPrompt(AiAssistant assistant, RetrievedContext retrieved, PersonaContext? persona)
    {
        var basePrompt = retrieved.IsEmpty
            ? assistant.SystemPrompt
            : ContextPromptBuilder.Build(assistant.SystemPrompt, retrieved);

        if (persona is null) return basePrompt;

        var clause = safetyClauses.GetClause(persona.Safety, persona.Audience, CultureInfo.CurrentUICulture);
        if (string.IsNullOrEmpty(clause)) return basePrompt;

        return clause + "\n\n" + basePrompt;
    }

    private async Task<string> ResolveEffectiveSystemPromptAsync(
        AiAssistant assistant,
        RetrievedContext retrieved,
        PersonaContext? persona,
        CancellationToken ct)
    {
        var baseSystemPrompt = ResolveSystemPrompt(assistant, retrieved, persona);
        var brandClause = await brandPromptResolver.ResolveClauseAsync(assistant.TenantId, ct);
        return string.IsNullOrWhiteSpace(brandClause)
            ? baseSystemPrompt
            : brandClause + "\n\n" + baseSystemPrompt;
    }

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

    private decimal EstimateCost(AiProviderType provider, int inputTokens, int outputTokens)
    {
        var section = configuration.GetSection($"AI:Providers:{provider}");
        var inRate = section.GetValue<decimal?>("CostPerInputToken") ?? 0m;
        var outRate = section.GetValue<decimal?>("CostPerOutputToken") ?? 0m;
        return inputTokens * inRate + outputTokens * outRate;
    }

    // ──────────────────────────────────────────────────────────────
    // Private types
    // ──────────────────────────────────────────────────────────────

    private sealed record ChatTurnState(
        AiConversation Conversation,
        AiAssistant Assistant,
        AiMessage UserMessage,
        List<AiChatMessage> ProviderMessages,
        int NextOrder,
        ToolResolutionResult Tools,
        PersonaContext? Persona);
}
