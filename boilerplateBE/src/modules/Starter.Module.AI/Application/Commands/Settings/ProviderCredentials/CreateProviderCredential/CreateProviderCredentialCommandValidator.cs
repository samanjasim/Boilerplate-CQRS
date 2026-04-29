using FluentValidation;

namespace Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.CreateProviderCredential;

public sealed class CreateProviderCredentialCommandValidator : AbstractValidator<CreateProviderCredentialCommand>
{
    public CreateProviderCredentialCommandValidator()
    {
        RuleFor(x => x.Provider).IsInEnum();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Secret).NotEmpty();
    }
}
