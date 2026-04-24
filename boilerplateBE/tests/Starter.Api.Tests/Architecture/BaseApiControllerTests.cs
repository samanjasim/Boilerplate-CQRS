using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Starter.Abstractions.Web;
using Starter.Shared.Models;
using Xunit;

namespace Starter.Api.Tests.Architecture;

public sealed class BaseApiControllerValidateRouteIdTests
{
    private sealed class TestController(ISender mediator) : BaseApiController(mediator)
    {
        public IActionResult? CallValidateRouteId(Guid routeId, Guid bodyId) =>
            ValidateRouteId(routeId, bodyId);
    }

    private readonly TestController _controller =
        new(new Mock<ISender>().Object);

    [Fact]
    public void ValidateRouteId_Returns_Null_When_Ids_Match()
    {
        var id = Guid.NewGuid();
        _controller.CallValidateRouteId(id, id).Should().BeNull();
    }

    [Fact]
    public void ValidateRouteId_Returns_BadRequest_With_Envelope_When_Ids_Differ()
    {
        var result = _controller.CallValidateRouteId(Guid.NewGuid(), Guid.NewGuid());

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value.Should().BeOfType<ApiResponse>()
            .Which.Success.Should().BeFalse();
    }
}
