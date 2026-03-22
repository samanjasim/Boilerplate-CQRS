using FluentValidation;

namespace Starter.Application.Features.Files.Commands.UploadFile;

public sealed class UploadFileCommandValidator : AbstractValidator<UploadFileCommand>
{
    private const long MaxFileSize = 50 * 1024 * 1024; // 50MB

    public UploadFileCommandValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.")
            .MaximumLength(500).WithMessage("File name must not exceed 500 characters.")
            .Must(name => !name.Contains("..") && !name.Contains('/') && !name.Contains('\\'))
            .WithMessage("File name contains invalid characters.");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required.")
            .MaximumLength(200);

        RuleFor(x => x.Size)
            .GreaterThan(0).WithMessage("File must not be empty.")
            .LessThanOrEqualTo(MaxFileSize).WithMessage("File size must not exceed 50MB.");
    }
}
