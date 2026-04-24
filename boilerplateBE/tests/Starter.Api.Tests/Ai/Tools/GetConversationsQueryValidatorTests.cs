using FluentAssertions;
using Starter.Module.AI.Application.Queries.GetConversations;
using Xunit;

namespace Starter.Api.Tests.Ai.Tools;

public sealed class GetConversationsQueryValidatorTests
{
    private readonly GetConversationsQueryValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_PageNumber_Fails(int pageNumber)
    {
        var result = _validator.Validate(new GetConversationsQuery(PageNumber: pageNumber));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetConversationsQuery.PageNumber));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    [InlineData(1000)]
    public void Invalid_PageSize_Fails(int pageSize)
    {
        var result = _validator.Validate(new GetConversationsQuery(PageSize: pageSize));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetConversationsQuery.PageSize));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 50)]
    [InlineData(10, 20)]
    public void Valid_Ranges_Pass(int pageNumber, int pageSize)
    {
        var result = _validator.Validate(new GetConversationsQuery(PageNumber: pageNumber, PageSize: pageSize));

        result.IsValid.Should().BeTrue();
    }
}
