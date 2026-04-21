using System.Text.Json;
using FluentAssertions;
using Starter.Abstractions.Capabilities;
using Starter.Module.Workflow.Infrastructure.Services;
using Xunit;

namespace Starter.Api.Tests.Workflow;

public sealed class FormDataValidatorTests
{
    private readonly IFormDataValidator _sut = new FormDataValidator();

    // Helper: simulate how form data arrives from JSON deserialization — values are JsonElement.
    private static Dictionary<string, object> MakeFormData(Dictionary<string, object> raw)
    {
        var json = JsonSerializer.Serialize(raw);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
    }

    // Helper: build a JsonElement for a single value (simulates condition value from JSON).
    private static JsonElement SerializeToElement<T>(T value)
    {
        var json = JsonSerializer.Serialize(new { v = value });
        return JsonSerializer.Deserialize<JsonElement>(json).GetProperty("v");
    }

    [Fact]
    public void Validate_AllRequiredFieldsPresent_ReturnsNoErrors()
    {
        var fields = new List<FormFieldDefinition>
        {
            new("name", "Name", "text", Required: true),
            new("age", "Age", "number", Required: true),
        };

        var data = MakeFormData(new() { ["name"] = "Alice", ["age"] = 30 });

        var errors = _sut.Validate(fields, data);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MissingRequiredField_ReturnsError()
    {
        var fields = new List<FormFieldDefinition>
        {
            new("name", "Name", "text", Required: true),
        };

        var data = MakeFormData(new() { ["other"] = "value" });

        var errors = _sut.Validate(fields, data);

        errors.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new FormValidationError("name", "'Name' is required."));
    }

    [Fact]
    public void Validate_NumberBelowMin_ReturnsError()
    {
        var fields = new List<FormFieldDefinition>
        {
            new("score", "Score", "number", Required: false, Min: 10),
        };

        var data = MakeFormData(new() { ["score"] = 5 });

        var errors = _sut.Validate(fields, data);

        errors.Should().ContainSingle()
            .Which.FieldName.Should().Be("score");
        errors[0].Message.Should().Contain("at least 10");
    }

    [Fact]
    public void Validate_NumberAboveMax_ReturnsError()
    {
        var fields = new List<FormFieldDefinition>
        {
            new("score", "Score", "number", Required: false, Max: 100),
        };

        var data = MakeFormData(new() { ["score"] = 150 });

        var errors = _sut.Validate(fields, data);

        errors.Should().ContainSingle()
            .Which.FieldName.Should().Be("score");
        errors[0].Message.Should().Contain("at most 100");
    }

    [Fact]
    public void Validate_TextExceedsMaxLength_ReturnsError()
    {
        var fields = new List<FormFieldDefinition>
        {
            new("bio", "Bio", "text", Required: false, MaxLength: 10),
        };

        var data = MakeFormData(new() { ["bio"] = "This is way too long for the field" });

        var errors = _sut.Validate(fields, data);

        errors.Should().ContainSingle()
            .Which.FieldName.Should().Be("bio");
        errors[0].Message.Should().Contain("at most 10 characters");
    }

    [Fact]
    public void Validate_SelectValueNotInOptions_ReturnsError()
    {
        var fields = new List<FormFieldDefinition>
        {
            new("priority", "Priority", "select", Required: false,
                Options: [new("low", "Low"), new("medium", "Medium"), new("high", "High")]),
        };

        var data = MakeFormData(new() { ["priority"] = "critical" });

        var errors = _sut.Validate(fields, data);

        errors.Should().ContainSingle()
            .Which.FieldName.Should().Be("priority");
        errors[0].Message.Should().Contain("invalid selection");
    }

    [Fact]
    public void Validate_CheckboxRequired_FalseValue_ReturnsError()
    {
        var fields = new List<FormFieldDefinition>
        {
            new("agreed", "Terms Agreement", "checkbox", Required: true),
        };

        var data = MakeFormData(new() { ["agreed"] = false });

        var errors = _sut.Validate(fields, data);

        errors.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new FormValidationError("agreed", "'Terms Agreement' is required."));
    }

    [Fact]
    public void Validate_OptionalFieldMissing_NoError()
    {
        var fields = new List<FormFieldDefinition>
        {
            new("notes", "Notes", "textarea", Required: false),
        };

        var data = MakeFormData(new() { ["other"] = "something" });

        var errors = _sut.Validate(fields, data);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NoFormFields_ReturnsNoErrors()
    {
        var errors = _sut.Validate(null, MakeFormData(new() { ["x"] = "y" }));

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_DateField_InvalidFormat_ReturnsError()
    {
        var fields = new List<FormFieldDefinition>
        {
            new("dob", "Date of Birth", "date", Required: false),
        };

        var data = MakeFormData(new() { ["dob"] = "not-a-date" });

        var errors = _sut.Validate(fields, data);

        errors.Should().ContainSingle()
            .Which.FieldName.Should().Be("dob");
        errors[0].Message.Should().Contain("valid date");
    }
}
