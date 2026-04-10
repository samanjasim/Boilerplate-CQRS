using MediatR;

namespace Starter.Api.Controllers;

public abstract class BaseApiController(ISender mediator) : Starter.Abstractions.Web.BaseApiController(mediator);
