using FluentValidation;
using Starter.Module.ImportExport.Domain.Enums;

namespace Starter.Module.ImportExport.Application.Commands.StartImport;

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
