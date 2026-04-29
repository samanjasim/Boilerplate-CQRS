using FluentValidation;

namespace Starter.Module.AI.Application.Commands.Settings.ProviderCredentials.RotateProviderCredential;

public sealed class RotateProviderCredentialCommandValidator : AbstractValidator<RotateProviderCredentialCommand>
{
    public RotateProviderCredentialCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Secret).NotEmpty();
    }
}
