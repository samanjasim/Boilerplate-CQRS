using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Starter.Module.AI.Application.Commands.UploadDocument;
using Starter.Module.AI.Infrastructure.Settings;
using Xunit;

namespace Starter.Api.Tests.Ai.Validation;

public sealed class UploadDocumentCommandValidatorTests
{
    private readonly UploadDocumentCommandValidator _validator =
        new(Options.Create(new AiRagSettings { MaxUploadBytes = 10 * 1024 * 1024 }));

    [Fact]
    public void Fails_When_File_Is_Null_Without_NullReferenceException()
    {
        var act = () => _validator.Validate(new UploadDocumentCommand(File: null!, Name: null));

        act.Should().NotThrow();
        var result = _validator.Validate(new UploadDocumentCommand(File: null!, Name: null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(UploadDocumentCommand.File));
    }

    [Fact]
    public void Fails_When_Content_Type_Is_Not_Allowed()
    {
        var file = MakeFile(length: 1024, contentType: "application/zip");

        var result = _validator.Validate(new UploadDocumentCommand(file, Name: null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Content type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Fails_When_File_Exceeds_Max_Size()
    {
        var file = MakeFile(length: 11 * 1024 * 1024, contentType: "application/pdf");

        var result = _validator.Validate(new UploadDocumentCommand(file, Name: null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("upload limit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Passes_For_Allowed_Content_Type_Within_Size_Limit()
    {
        var file = MakeFile(length: 1024, contentType: "application/pdf");

        var result = _validator.Validate(new UploadDocumentCommand(file, Name: "doc"));

        result.IsValid.Should().BeTrue();
    }

    private static IFormFile MakeFile(long length, string contentType)
    {
        var mock = new Mock<IFormFile>();
        mock.SetupGet(f => f.Length).Returns(length);
        mock.SetupGet(f => f.ContentType).Returns(contentType);
        mock.SetupGet(f => f.FileName).Returns("upload.bin");
        return mock.Object;
    }
}
