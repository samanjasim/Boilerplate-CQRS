using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Starter.Application.Common.Interfaces;
using Starter.Module.Communication.Domain.Entities;
using Starter.Module.Communication.Domain.Enums;

namespace Starter.Module.Communication.Infrastructure.Persistence.Seed;

/// <summary>
/// Manual-QA demo data for the Phase 5b communication cluster: realistic varied rows
/// for each demo tenant so every visual variant on every redesigned page has at least
/// one example. Strictly gated behind <c>DatabaseSettings:SeedDemoCommunicationData</c>
/// — production startups never hit this code.
///
/// Idempotent per tenant: skipped if the tenant already has any DeliveryLog row.
/// Reflection is used to override the private timestamps on entities (CreatedAt,
/// LastTestedAt) so the seed can exercise the age-tinted last-tested chip thresholds
/// (fresh / today / week / older) and span the 7-day delivery hero window.
/// </summary>
internal static class DemoCommunicationSeed
{
    private const string DemoCredentialsJson = "{\"placeholder\":\"demo-data-only\"}";

    public static async Task SeedAsync(
        CommunicationDbContext context,
        IApplicationDbContext appDb,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var demoSlugs = new[] { "acme", "globex", "initech" };
        var demoTenants = await appDb.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Slug != null && demoSlugs.Contains(t.Slug))
            .Select(t => new { t.Id, Slug = t.Slug!, t.Name })
            .ToListAsync(cancellationToken);

        if (demoTenants.Count == 0)
        {
            logger.LogInformation("DemoCommunicationSeed skipped: no demo tenants found");
            return;
        }

        var systemTemplates = await context.MessageTemplates
            .IgnoreQueryFilters()
            .Where(t => t.IsSystem)
            .Select(t => new { t.Id, t.Name, t.Category, t.DefaultChannel })
            .ToListAsync(cancellationToken);

        var totalChannels = 0;
        var totalIntegrations = 0;
        var totalRules = 0;
        var totalLogs = 0;

        foreach (var tenant in demoTenants)
        {
            var alreadySeeded = await context.DeliveryLogs
                .IgnoreQueryFilters()
                .AnyAsync(d => d.TenantId == tenant.Id, cancellationToken);
            if (alreadySeeded) continue;

            var channels = SeedChannels(context, tenant.Id, tenant.Slug);
            var integrations = SeedIntegrations(context, tenant.Id, tenant.Slug);
            await context.SaveChangesAsync(cancellationToken);
            totalChannels += channels;
            totalIntegrations += integrations;

            var rules = SeedTriggerRules(context, tenant.Id, systemTemplates
                .Select(s => (s.Id, s.Name))
                .ToList());
            await context.SaveChangesAsync(cancellationToken);
            totalRules += rules;

            var logs = SeedDeliveryLogs(context, tenant.Id, tenant.Slug, systemTemplates
                .Select(s => (s.Id, s.Name, s.DefaultChannel))
                .ToList());
            await context.SaveChangesAsync(cancellationToken);
            totalLogs += logs;
        }

        if (totalChannels + totalIntegrations + totalRules + totalLogs == 0) return;

        logger.LogInformation(
            "DemoCommunicationSeed seeded {Channels} channels, {Integrations} integrations, " +
            "{Rules} trigger rules, {Logs} delivery logs across {Tenants} tenant(s)",
            totalChannels, totalIntegrations, totalRules, totalLogs, demoTenants.Count);
    }

    private static int SeedChannels(CommunicationDbContext context, Guid tenantId, string slug)
    {
        var rows = new (NotificationChannel Channel, ChannelProvider Provider, string Name, bool IsDefault, bool Active, bool ErrorState, TimeSpan? TestedAgo)[]
        {
            // Email — Active SMTP marked default, tested 30 minutes ago (fresh tone)
            (NotificationChannel.Email, ChannelProvider.Smtp,     $"{slug.ToUpperInvariant()} Mailer",      true,  true,  false, TimeSpan.FromMinutes(30)),
            // Email — Inactive SendGrid, tested 2 days ago (week tone, amber)
            (NotificationChannel.Email, ChannelProvider.SendGrid, $"{slug} marketing pipe",                false, false, false, TimeSpan.FromDays(2)),
            // Email — Errored SES (bad creds), tested 6 hours ago (today tone)
            (NotificationChannel.Email, ChannelProvider.Ses,      $"{slug} bulk transactional",            false, false, true,  TimeSpan.FromHours(6)),
            // Sms — Active Twilio, tested 4 days ago (week tone)
            (NotificationChannel.Sms,   ChannelProvider.Twilio,   $"{slug} SMS sender",                    false, true,  false, TimeSpan.FromDays(4)),
            // Push — Active FCM, tested 9 days ago (older tone)
            (NotificationChannel.Push,  ChannelProvider.Fcm,      $"{slug} mobile push (Android)",         false, true,  false, TimeSpan.FromDays(9)),
            // Push — Inactive APNS, never tested
            (NotificationChannel.Push,  ChannelProvider.Apns,     $"{slug} mobile push (iOS)",             false, false, false, null),
            // WhatsApp — Errored, tested 3 days ago
            (NotificationChannel.WhatsApp, ChannelProvider.MetaWhatsApp, $"{slug} WhatsApp business",      false, false, true,  TimeSpan.FromDays(3)),
            // InApp — Active Ably, tested 12 minutes ago (fresh tone)
            (NotificationChannel.InApp, ChannelProvider.Ably,     $"{slug} realtime feed",                 false, true,  false, TimeSpan.FromMinutes(12)),
        };

        var seeded = 0;
        foreach (var r in rows)
        {
            var cfg = ChannelConfig.Create(tenantId, r.Channel, r.Provider, r.Name, DemoCredentialsJson, r.IsDefault);
            if (!r.Active) cfg.Deactivate();
            if (r.ErrorState) cfg.RecordTestResult(false, "Demo: simulated provider auth failure");
            else if (r.TestedAgo.HasValue) cfg.RecordTestResult(true, "Demo: ok");

            if (r.TestedAgo.HasValue)
                SetPrivateValue(cfg, nameof(ChannelConfig.LastTestedAt), DateTime.UtcNow - r.TestedAgo.Value);
            context.ChannelConfigs.Add(cfg);
            seeded++;
        }
        return seeded;
    }

    private static int SeedIntegrations(CommunicationDbContext context, Guid tenantId, string slug)
    {
        var rows = new (IntegrationType Type, string Name, bool Active, bool ErrorState, TimeSpan? TestedAgo)[]
        {
            (IntegrationType.Slack,          $"#{slug}-engineering",          true,  false, TimeSpan.FromMinutes(45)),
            (IntegrationType.Slack,          $"#{slug}-alerts",               false, false, TimeSpan.FromHours(20)),
            (IntegrationType.Telegram,       $"{slug} ops bot",               true,  false, TimeSpan.FromDays(1)),
            (IntegrationType.Discord,        $"{slug} community",             false, true,  TimeSpan.FromDays(5)),
            (IntegrationType.MicrosoftTeams, $"{slug} leadership channel",    true,  false, null),
        };

        var seeded = 0;
        foreach (var r in rows)
        {
            var cfg = IntegrationConfig.Create(tenantId, r.Type, r.Name, DemoCredentialsJson);
            if (!r.Active) cfg.Deactivate();
            if (r.ErrorState) cfg.RecordTestResult(false, "Demo: simulated webhook 401");
            else if (r.TestedAgo.HasValue) cfg.RecordTestResult(true, "Demo: ok");

            if (r.TestedAgo.HasValue)
                SetPrivateValue(cfg, nameof(IntegrationConfig.LastTestedAt), DateTime.UtcNow - r.TestedAgo.Value);
            context.IntegrationConfigs.Add(cfg);
            seeded++;
        }
        return seeded;
    }

    private static int SeedTriggerRules(
        CommunicationDbContext context,
        Guid tenantId,
        List<(Guid Id, string Name)> templates)
    {
        if (templates.Count == 0) return 0;

        // Pick stable per-tenant template references — fall through cleanly on any
        // template the registrar has shipped.
        Guid TemplateIdFor(string contains) =>
            templates.FirstOrDefault(t => t.Name.Contains(contains, StringComparison.OrdinalIgnoreCase)).Id is var id && id != Guid.Empty
                ? id
                : templates[0].Id;

        var rows = new (string Name, string Event, Guid TemplateId, string Channels, int DelaySeconds, bool Active)[]
        {
            ("Welcome new user",         "user.invited",             TemplateIdFor("invite"),    "[\"InApp\",\"Email\"]",                       0,    true),
            ("Password reset alert",     "auth.password_reset",      TemplateIdFor("password"),  "[\"Email\"]",                                  0,    true),
            ("Payment failed escalate",  "billing.payment_failed",   TemplateIdFor("payment"),   "[\"Email\",\"Sms\",\"Slack\"]",               60,   true),
            ("Quarterly newsletter",     "marketing.newsletter",     templates[0].Id,            "[\"Email\"]",                                  3600, false),
            ("Critical job failure",     "job.failed",               templates[^1].Id,           "[\"Slack\",\"Email\"]",                       0,    true),
        };

        var seeded = 0;
        foreach (var r in rows)
        {
            var rule = TriggerRule.Create(tenantId, r.Name, r.Event, r.TemplateId,
                recipientMode: "Tenant", channelSequenceJson: r.Channels, delaySeconds: r.DelaySeconds);
            if (!r.Active) rule.Deactivate();
            context.TriggerRules.Add(rule);
            seeded++;
        }
        return seeded;
    }

    private static int SeedDeliveryLogs(
        CommunicationDbContext context,
        Guid tenantId,
        string slug,
        List<(Guid Id, string Name, NotificationChannel DefaultChannel)> templates)
    {
        if (templates.Count == 0) return 0;

        // Distribution chosen so the 4-card hero (Delivered / Failed / Pending / Bounced)
        // shows all four with realistic ratios (~70% delivered, ~10% failed, ~15% pending,
        // ~5% bounced). Spread across 7 days so the "Last 7 days" window captures everything.
        var distribution = new (DeliveryStatus Status, int Count)[]
        {
            (DeliveryStatus.Delivered, 18),
            (DeliveryStatus.Failed,    3),
            (DeliveryStatus.Pending,   2),
            (DeliveryStatus.Queued,    1),
            (DeliveryStatus.Sending,   1),
            (DeliveryStatus.Bounced,   2),
        };

        var rng = new Random(unchecked((int)((uint)tenantId.GetHashCode() ^ 0x5b5b5b5b)));
        var subjects = new[]
        {
            "Your weekly digest", "Action required: confirm your address",
            "Welcome to the platform", "Payment receipt #INV-2026-",
            "Two new comments on your task", "Heads up: trial ends Friday",
            "Approval requested", "Password reset requested",
        };
        var recipients = new[]
        {
            $"alice@{slug}.com", $"bob@{slug}.com", $"carol@{slug}.com",
            $"dan@{slug}.com",   $"eve@{slug}.com", $"frank@{slug}.com",
        };

        var seeded = 0;
        foreach (var (status, count) in distribution)
        {
            for (var i = 0; i < count; i++)
            {
                var template = templates[rng.Next(templates.Count)];
                var hoursAgo = rng.Next(1, 7 * 24);
                var createdAt = DateTime.UtcNow.AddHours(-hoursAgo);

                var log = DeliveryLog.Create(
                    tenantId,
                    recipientUserId: null,
                    recipientAddress: recipients[rng.Next(recipients.Length)],
                    messageTemplateId: template.Id,
                    templateName: template.Name,
                    channel: template.DefaultChannel,
                    integrationType: null,
                    subject: subjects[rng.Next(subjects.Length)] + (i + 1),
                    bodyPreview: "Demo body preview — generated by DemoCommunicationSeed.",
                    variablesJson: null);

                ApplyStatus(log, status);
                SetPrivateValue(log, nameof(DeliveryLog.CreatedAt), createdAt);

                context.DeliveryLogs.Add(log);
                seeded++;
            }
        }
        return seeded;
    }

    private static void ApplyStatus(DeliveryLog log, DeliveryStatus status)
    {
        switch (status)
        {
            case DeliveryStatus.Delivered:
                log.MarkQueued();
                log.MarkSending(ChannelProvider.Smtp);
                log.MarkDelivered(providerMessageId: $"msg-{Guid.NewGuid():N}", durationMs: 250);
                break;
            case DeliveryStatus.Failed:
                log.MarkQueued();
                log.MarkSending(ChannelProvider.Smtp);
                log.MarkFailed("Demo: 5xx upstream error");
                break;
            case DeliveryStatus.Bounced:
                log.MarkQueued();
                log.MarkSending(ChannelProvider.Smtp);
                log.MarkBounced("Demo: address rejected by provider");
                break;
            case DeliveryStatus.Queued:
                log.MarkQueued();
                break;
            case DeliveryStatus.Sending:
                log.MarkQueued();
                log.MarkSending(ChannelProvider.Smtp);
                break;
            // Pending: leave at the factory default.
        }
    }

    /// <summary>
    /// Reflectively writes a private setter / backing field. Used only by demo seeding so
    /// production code paths remain encapsulated. Walks the type chain to find inherited
    /// properties (e.g. <c>BaseEntity.CreatedAt</c>).
    /// </summary>
    private static void SetPrivateValue(object target, string propertyName, object? value)
    {
        var type = target.GetType();
        while (type is not null)
        {
            var prop = type.GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop is { CanWrite: true })
            {
                prop.SetValue(target, value);
                return;
            }
            // Fallback: hidden auto-property backing field (`<Name>k__BackingField`).
            var field = type.GetField($"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field is not null)
            {
                field.SetValue(target, value);
                return;
            }
            type = type.BaseType;
        }
    }
}
