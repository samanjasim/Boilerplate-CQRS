using FluentValidation;
using Microsoft.Extensions.Options;
using Starter.Module.AI.Infrastructure.Settings;

namespace Starter.Module.AI.Application.Commands.UploadDocument;

public sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain",
        "text/markdown",
        "text/csv",
        "application/csv",
    };

    public UploadDocumentCommandValidator(IOptions<AiRagSettings> ragOptions)
    {
        var maxBytes = ragOptions.Value.MaxUploadBytes;

        RuleFor(c => c.File)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(f => f.Length <= maxBytes)
                .WithMessage($"File exceeds the {maxBytes / (1024 * 1024)} MB upload limit.")
            .Must(f => AllowedContentTypes.Contains(f.ContentType ?? ""))
                .WithMessage("Content type is not supported for knowledge base ingestion.");

        RuleFor(c => c.Name)
            .MaximumLength(200)
            .When(c => !string.IsNullOrWhiteSpace(c.Name));
    }
}
