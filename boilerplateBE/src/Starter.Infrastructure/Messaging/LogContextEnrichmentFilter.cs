using MassTransit;
using Microsoft.Extensions.Logging;

namespace Starter.Infrastructure.Messaging;

/// <summary>
/// MassTransit consume filter that pushes message identity into the
/// <see cref="ILogger"/> scope (flows into Serilog via its scope enricher) for
/// the duration of <see cref="IConsumer{TMessage}.Consume"/>.
///
/// <para>
/// Without this, log lines emitted by consumers carry the W3C <c>TraceId</c>
/// via <c>Activity.Current</c>, but not MassTransit's own <c>ConversationId</c>
/// or <c>MessageId</c>. Since our
/// <see cref="Persistence.Interceptors.IntegrationEventOutboxInterceptor"/>
/// derives <c>ConversationId</c> from the originating HTTP request's TraceId,
/// surfacing it in consumer logs lets operators grep <i>one</i> correlation
/// token across the whole causal chain:
///   HTTP request → outbox row → consumer 1 → consumer 2 → …
/// </para>
///
/// <para>
/// Scope properties pushed:
/// <list type="bullet">
///   <item><c>ConversationId</c> — shared across an HTTP request's whole event chain</item>
///   <item><c>MessageId</c> — unique per message; use when tracing a single event</item>
///   <item><c>MessageType</c> — short CLR type name of the consumed message</item>
/// </list>
/// Serilog's <c>.Enrich.FromLogContext()</c> plus its logger-provider integration
/// automatically merges <see cref="ILogger.BeginScope{TState}"/> dictionaries
/// into the event as enriched properties, so they appear in every sink output
/// (console, file, OTLP).
/// </para>
///
/// <para>
/// Using <see cref="ILogger.BeginScope{TState}"/> instead of <c>Serilog.Context.LogContext</c>
/// keeps this filter in the <c>Microsoft.Extensions.Logging</c> abstraction —
/// no direct Serilog dependency leaks into Infrastructure.
/// </para>
/// </summary>
public sealed class LogContextEnrichmentFilter<TMessage> : IFilter<ConsumeContext<TMessage>>
    where TMessage : class
{
    private readonly ILogger<LogContextEnrichmentFilter<TMessage>> _logger;

    public LogContextEnrichmentFilter(ILogger<LogContextEnrichmentFilter<TMessage>> logger)
    {
        _logger = logger;
    }

    public async Task Send(
        ConsumeContext<TMessage> context,
        IPipe<ConsumeContext<TMessage>> next)
    {
        var scopeState = new Dictionary<string, object?>
        {
            ["ConversationId"] = context.ConversationId,
            ["MessageId"] = context.MessageId,
            ["MessageType"] = typeof(TMessage).Name,
        };

        using (_logger.BeginScope(scopeState))
        {
            await next.Send(context);
        }
    }

    public void Probe(ProbeContext context) =>
        context.CreateFilterScope("log-context-enrichment");
}
