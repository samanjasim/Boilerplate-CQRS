using FluentValidation;
using Starter.Domain.ImportExport.Enums;

namespace Starter.Application.Features.ImportExport.Commands.StartImport;

public sealed class StartImportCommandValidator : AbstractValidator<StartImportCommand>
{
    public StartImportCommandValidator()
    {
        RuleFor(x => x.FileId)
            .NotEmpty().WithMessage("File ID is required.");

        RuleFor(x => x.EntityType)
            .NotEmpty().WithMessage("Entity type is required.");

        RuleFor(x => x.ConflictMode)
            .IsInEnum().WithMessage("Conflict mode must be a valid value.");
    }
}
