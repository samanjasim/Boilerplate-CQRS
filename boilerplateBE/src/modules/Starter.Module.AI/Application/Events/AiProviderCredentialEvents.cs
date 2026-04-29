using Starter.Abstractions.Ai;
using Starter.Application.Common.Events;

namespace Starter.Module.AI.Application.Events;

/// <summary>
/// Published after an <c>AiProviderCredential</c> mutation commits to <c>AiDbContext</c>.
/// The audit consumer subscribes to all four shapes and writes the matching
/// <c>AuditLog</c> row in <c>ApplicationDbContext</c>. Decoupling via outbox gives
/// at-least-once delivery + automatic retry on transient failures, replacing the
/// previous direct <c>coreDb.SaveChangesAsync</c> write that had no retry.
///
/// <para>None of these events carry the plaintext or encrypted secret — only the
/// safe <c>KeyPrefix</c> for masked display.</para>
/// </summary>
public sealed record AiProviderCredentialCreatedEvent(
    Guid TenantId,
    Guid CredentialId,
    AiProviderType Provider,
    string KeyPrefix,
    Guid? PerformedBy,
    string? PerformedByEmail,
    DateTime OccurredAt
) : IDomainEvent;

public sealed record AiProviderCredentialRotatedEvent(
    Guid TenantId,
    Guid CredentialId,
    AiProviderType Provider,
    string KeyPrefix,
    Guid? PerformedBy,
    string? PerformedByEmail,
    DateTime OccurredAt
) : IDomainEvent;

public sealed record AiProviderCredentialRevokedEvent(
    Guid TenantId,
    Guid CredentialId,
    AiProviderType Provider,
    string KeyPrefix,
    Guid? PerformedBy,
    string? PerformedByEmail,
    DateTime OccurredAt
) : IDomainEvent;

public sealed record AiProviderCredentialTestedEvent(
    Guid TenantId,
    Guid CredentialId,
    AiProviderType Provider,
    string KeyPrefix,
    Guid? PerformedBy,
    string? PerformedByEmail,
    DateTime OccurredAt
) : IDomainEvent;
