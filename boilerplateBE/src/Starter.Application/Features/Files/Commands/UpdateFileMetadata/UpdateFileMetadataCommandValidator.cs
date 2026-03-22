using FluentValidation;

namespace Starter.Application.Features.Files.Commands.UpdateFileMetadata;

public sealed class UpdateFileMetadataCommandValidator : AbstractValidator<UpdateFileMetadataCommand>
{
    public UpdateFileMetadataCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x.Tags)
            .Must(tags => tags == null || tags.Length <= 10)
            .WithMessage("Maximum 10 tags allowed.")
            .Must(tags => tags == null || tags.All(t => t.Length <= 100))
            .WithMessage("Each tag must not exceed 100 characters.");
    }
}
