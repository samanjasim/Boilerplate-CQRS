using FluentValidation;

namespace Starter.Application.Features.Files.Commands.UpdateFileMetadata;

public sealed class UpdateFileMetadataCommandValidator : AbstractValidator<UpdateFileMetadataCommand>
{
    public UpdateFileMetadataCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => x.Description is not null);

        RuleFor(x => x.Tags)
            .Must(tags => tags!.Length <= 10)
            .WithMessage("Maximum 10 tags allowed.")
            .When(x => x.Tags is not null);

        RuleForEach(x => x.Tags)
            .MaximumLength(100)
            .When(x => x.Tags is not null);
    }
}
