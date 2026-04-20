using System.Reflection;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Starter.Module.AI.Application.Queries.SearchKnowledgeBase;
using Starter.Module.AI.Constants;
using Starter.Module.AI.Controllers;
using Starter.Shared.Models;
using Starter.Shared.Results;
using Xunit;

namespace Starter.Api.Tests.Ai.Retrieval;

public sealed class AiSearchControllerTests
{
    [Fact]
    public async Task Search_Forwards_Query_To_Mediator_And_Returns_Ok_On_Success()
    {
        var sender = new Mock<ISender>();
        var dto = new SearchKnowledgeBaseResultDto([], 0, false, []);
        sender.Setup(s => s.Send(
                It.IsAny<SearchKnowledgeBaseQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var controller = new AiSearchController(sender.Object);
        var query = new SearchKnowledgeBaseQuery("hello", null, 5, null, true);

        var result = await controller.Search(query, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ApiResponse<SearchKnowledgeBaseResultDto>>();
        sender.Verify(
            s => s.Send(query, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Search_Endpoint_Is_Protected_By_SearchKnowledgeBase_Policy()
    {
        var action = typeof(AiSearchController).GetMethod(nameof(AiSearchController.Search))!;
        var authorize = action.GetCustomAttribute<AuthorizeAttribute>();

        authorize.Should().NotBeNull();
        authorize!.Policy.Should().Be(AiPermissions.SearchKnowledgeBase);
    }

    [Fact]
    public void Controller_Has_Expected_Route()
    {
        var routes = typeof(AiSearchController)
            .GetCustomAttributes<RouteAttribute>(inherit: false)
            .Select(r => r.Template)
            .ToArray();

        routes.Should().Contain("api/v{version:apiVersion}/ai/search");
    }

    [Fact]
    public void Search_Is_HttpPost()
    {
        var action = typeof(AiSearchController).GetMethod(nameof(AiSearchController.Search))!;
        action.GetCustomAttribute<HttpPostAttribute>().Should().NotBeNull();
    }
}
