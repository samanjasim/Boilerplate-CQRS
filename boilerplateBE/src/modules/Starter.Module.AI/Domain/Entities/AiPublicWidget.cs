using Starter.Domain.Common;
using Starter.Module.AI.Domain.Enums;

namespace Starter.Module.AI.Domain.Entities;

public sealed class AiPublicWidget : AggregateRoot, ITenantEntity
{
    private List<string> _allowedOrigins = new();

    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public AiPublicWidgetStatus Status { get; private set; }

    public IReadOnlyList<string> AllowedOrigins
    {
        get => _allowedOrigins;
        private set => _allowedOrigins = value?.ToList() ?? new();
    }

    public Guid? DefaultAssistantId { get; private set; }
    public string DefaultPersonaSlug { get; private set; } = AiPersona.AnonymousSlug;
    public int? MonthlyTokenCap { get; private set; }
    public int? DailyTokenCap { get; private set; }
    public int? RequestsPerMinute { get; private set; }
    public string? MetadataJson { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    private AiPublicWidget() { }

    private AiPublicWidget(
        Guid tenantId,
        string name,
        IEnumerable<string> allowedOrigins,
        Guid? defaultAssistantId,
        string defaultPersonaSlug,
        int? monthlyTokenCap,
        int? dailyTokenCap,
        int? requestsPerMinute,
        Guid? createdByUserId) : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        Name = name.Trim();
        Status = AiPublicWidgetStatus.Active;
        _allowedOrigins = NormalizeOrigins(allowedOrigins);
        DefaultAssistantId = defaultAssistantId;
        DefaultPersonaSlug = NormalizePersonaSlug(defaultPersonaSlug);
        MonthlyTokenCap = monthlyTokenCap;
        DailyTokenCap = dailyTokenCap;
        RequestsPerMinute = requestsPerMinute;
        CreatedByUserId = createdByUserId;
    }

    public static AiPublicWidget Create(
        Guid tenantId,
        string name,
        IEnumerable<string> allowedOrigins,
        Guid? defaultAssistantId,
        string defaultPersonaSlug,
        int? monthlyTokenCap,
        int? dailyTokenCap,
        int? requestsPerMinute,
        Guid? createdByUserId)
    {
        ValidateQuotas(monthlyTokenCap, dailyTokenCap, requestsPerMinute);

        return new AiPublicWidget(
            tenantId,
            name,
            allowedOrigins,
            defaultAssistantId,
            defaultPersonaSlug,
            monthlyTokenCap,
            dailyTokenCap,
            requestsPerMinute,
            createdByUserId);
    }

    public void Update(
        string name,
        IEnumerable<string> allowedOrigins,
        Guid? defaultAssistantId,
        string defaultPersonaSlug,
        int? monthlyTokenCap,
        int? dailyTokenCap,
        int? requestsPerMinute,
        string? metadataJson)
    {
        ValidateQuotas(monthlyTokenCap, dailyTokenCap, requestsPerMinute);

        Name = name.Trim();
        _allowedOrigins = NormalizeOrigins(allowedOrigins);
        DefaultAssistantId = defaultAssistantId;
        DefaultPersonaSlug = NormalizePersonaSlug(defaultPersonaSlug);
        MonthlyTokenCap = monthlyTokenCap;
        DailyTokenCap = dailyTokenCap;
        RequestsPerMinute = requestsPerMinute;
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson;
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetStatus(AiPublicWidgetStatus status)
    {
        Status = status;
        ModifiedAt = DateTime.UtcNow;
    }

    private static void ValidateQuotas(int? monthlyTokenCap, int? dailyTokenCap, int? requestsPerMinute)
    {
        if (monthlyTokenCap is < 0) throw new ArgumentOutOfRangeException(nameof(monthlyTokenCap));
        if (dailyTokenCap is < 0) throw new ArgumentOutOfRangeException(nameof(dailyTokenCap));
        if (requestsPerMinute is < 0) throw new ArgumentOutOfRangeException(nameof(requestsPerMinute));
    }

    private static string NormalizePersonaSlug(string defaultPersonaSlug) =>
        string.IsNullOrWhiteSpace(defaultPersonaSlug)
            ? AiPersona.AnonymousSlug
            : defaultPersonaSlug.Trim().ToLowerInvariant();

    private static List<string> NormalizeOrigins(IEnumerable<string> origins) =>
        origins
            .Select(NormalizeOrigin)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(o => o, StringComparer.Ordinal)
            .ToList();

    private static string NormalizeOrigin(string origin)
    {
        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri))
            throw new ArgumentException($"Invalid origin '{origin}'.", nameof(origin));

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            throw new ArgumentException($"Origin '{origin}' must use http or https.", nameof(origin));

        return uri.IsDefaultPort
            ? $"{uri.Scheme}://{uri.Host.ToLowerInvariant()}"
            : $"{uri.Scheme}://{uri.Host.ToLowerInvariant()}:{uri.Port}";
    }
}
