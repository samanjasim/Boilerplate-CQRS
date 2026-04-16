using FluentAssertions;
using Starter.Module.Communication.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Communication.Infrastructure;

public sealed class StubbleTemplateEngineTests
{
    private readonly StubbleTemplateEngine _sut = new();

    [Fact]
    public void Render_SimpleVariable_Substitutes()
    {
        var result = _sut.Render("Hi {{userName}}!", new() { ["userName"] = "Saman" });

        result.Should().Be("Hi Saman!");
    }

    [Fact]
    public void Render_MissingVariable_RendersEmptyString()
    {
        // Mustache behaviour: missing vars render as empty, not error. This is a safety
        // property we rely on — a misspelled variable name in a template should not
        // throw at runtime.
        var result = _sut.Render("Hi {{userName}}, code {{missing}}!", new() { ["userName"] = "Saman" });

        result.Should().Be("Hi Saman, code !");
    }

    [Fact]
    public void Render_SectionBlock_RendersOnlyWhenTruthy()
    {
        const string template = "Order placed.{{#trackingUrl}} Track at {{trackingUrl}}.{{/trackingUrl}}";

        var withUrl = _sut.Render(template, new() { ["trackingUrl"] = "https://track.example/123" });
        var withoutUrl = _sut.Render(template, new Dictionary<string, object>());

        withUrl.Should().Be("Order placed. Track at https://track.example/123.");
        withoutUrl.Should().Be("Order placed.");
    }

    [Fact]
    public void Render_InvertedSection_RendersOnlyWhenFalsy()
    {
        const string template = "{{^tracking}}No tracking available.{{/tracking}}";

        var withoutTracking = _sut.Render(template, new Dictionary<string, object>());
        var withTracking = _sut.Render(template, new() { ["tracking"] = "present" });

        withoutTracking.Should().Be("No tracking available.");
        withTracking.Should().Be("");
    }

    [Fact]
    public void Render_ListSection_IteratesCollection()
    {
        const string template = "Items:{{#items}} - {{name}}:{{price}}{{/items}}";

        var result = _sut.Render(template, new()
        {
            ["items"] = new List<Dictionary<string, object>>
            {
                new() { ["name"] = "Widget", ["price"] = 9 },
                new() { ["name"] = "Gadget", ["price"] = 12 },
            },
        });

        result.Should().Be("Items: - Widget:9 - Gadget:12");
    }

    [Fact]
    public void Validate_WellFormedTemplate_ReturnsTrue()
    {
        var ok = _sut.Validate("Hi {{userName}} {{#section}}x{{/section}}", out var errors);

        ok.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_UnclosedSection_ReturnsFalseWithError()
    {
        var ok = _sut.Validate("Hi {{#section}}unterminated", out var errors);

        ok.Should().BeFalse();
        errors.Should().NotBeEmpty();
    }
}
