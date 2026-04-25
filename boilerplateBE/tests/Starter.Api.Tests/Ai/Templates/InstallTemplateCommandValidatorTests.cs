using FluentValidation.TestHelper;
using Starter.Module.AI.Application.Commands.InstallTemplate;
using Xunit;

namespace Starter.Api.Tests.Ai.Templates;

public class InstallTemplateCommandValidatorTests
{
    private readonly InstallTemplateCommandValidator _v = new();

    [Fact]
    public void Empty_slug_fails()
    {
        var result = _v.TestValidate(new InstallTemplateCommand(""));
        result.ShouldHaveValidationErrorFor(c => c.TemplateSlug);
    }

    [Fact]
    public void Whitespace_slug_fails()
    {
        var result = _v.TestValidate(new InstallTemplateCommand("   "));
        result.ShouldHaveValidationErrorFor(c => c.TemplateSlug);
    }

    [Fact]
    public void Slug_over_128_chars_fails()
    {
        var result = _v.TestValidate(new InstallTemplateCommand(new string('a', 129)));
        result.ShouldHaveValidationErrorFor(c => c.TemplateSlug);
    }

    [Fact]
    public void Valid_slug_passes()
    {
        var result = _v.TestValidate(new InstallTemplateCommand("support_assistant_anthropic"));
        result.ShouldNotHaveValidationErrorFor(c => c.TemplateSlug);
    }

    [Fact]
    public void Valid_slug_with_target_tenant_passes()
    {
        var result = _v.TestValidate(
            new InstallTemplateCommand("support_assistant_anthropic", TargetTenantId: Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
