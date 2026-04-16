using Starter.Shared.Results;

namespace Starter.Module.Communication.Domain.Errors;

public static class CommunicationErrors
{
    public static readonly Error ChannelConfigNotFound = Error.NotFound(
        "Communication.ChannelConfigNotFound",
        "The specified channel configuration was not found.");

    public static readonly Error IntegrationConfigNotFound = Error.NotFound(
        "Communication.IntegrationConfigNotFound",
        "The specified integration configuration was not found.");

    public static readonly Error TemplateNotFound = Error.NotFound(
        "Communication.TemplateNotFound",
        "The specified message template was not found.");

    public static readonly Error TemplateOverrideNotFound = Error.NotFound(
        "Communication.TemplateOverrideNotFound",
        "The specified template override was not found.");

    public static readonly Error TriggerRuleNotFound = Error.NotFound(
        "Communication.TriggerRuleNotFound",
        "The specified trigger rule was not found.");

    public static readonly Error DeliveryLogNotFound = Error.NotFound(
        "Communication.DeliveryLogNotFound",
        "The specified delivery log entry was not found.");

    public static readonly Error ChannelNotConfigured = Error.Validation(
        "Communication.ChannelNotConfigured",
        "No channel configuration found for the specified channel.");

    public static readonly Error TemplateRenderFailed = Error.Failure(
        "Communication.TemplateRenderFailed",
        "Failed to render the message template.");

    public static readonly Error InvalidCredentials = Error.Validation(
        "Communication.InvalidCredentials",
        "The provided channel credentials are not valid.");

    public static readonly Error TestConnectionFailed = Error.Failure(
        "Communication.TestConnectionFailed",
        "Failed to test the channel connection.");

    public static readonly Error DuplicateChannelConfig = Error.Conflict(
        "Communication.DuplicateChannelConfig",
        "A configuration for this channel and provider already exists.");

    public static readonly Error DuplicateTemplateOverride = Error.Conflict(
        "Communication.DuplicateTemplateOverride",
        "A template override already exists for this template.");

    public static readonly Error TenantRequired = Error.Validation(
        "Communication.TenantRequired",
        "A tenant context is required for this operation.");

    public static readonly Error InvalidChannelProviderCombination = Error.Validation(
        "Communication.InvalidChannelProviderCombination",
        "The specified channel and provider combination is not supported.");

    public static readonly Error EventNotRegistered = Error.Validation(
        "Communication.EventNotRegistered",
        "The specified event is not registered.");

    public static readonly Error RequiredNotificationNotFound = Error.NotFound(
        "Communication.RequiredNotificationNotFound",
        "The specified required notification was not found.");

    public static readonly Error DuplicateRequiredNotification = Error.Conflict(
        "Communication.DuplicateRequiredNotification",
        "A required notification for this category and channel already exists.");

    public static Error QuotaExceeded(string channel, long limit) =>
        Error.Validation("Communication.QuotaExceeded",
            $"You have reached the maximum number of {channel} messages allowed ({limit}).");
}
