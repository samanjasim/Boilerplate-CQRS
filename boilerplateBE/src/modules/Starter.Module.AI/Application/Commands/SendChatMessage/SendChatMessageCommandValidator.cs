using FluentValidation;

namespace Starter.Module.AI.Application.Commands.SendChatMessage;

public sealed class SendChatMessageCommandValidator : AbstractValidator<SendChatMessageCommand>
{
    public SendChatMessageCommandValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message content is required.")
            .MaximumLength(16_000).WithMessage("Message is too long (max 16,000 characters).");

        RuleFor(x => x)
            .Must(x => x.ConversationId.HasValue || x.AssistantId.HasValue)
            .WithMessage("Either ConversationId (to continue a conversation) or AssistantId (to start a new one) is required.");
    }
}
