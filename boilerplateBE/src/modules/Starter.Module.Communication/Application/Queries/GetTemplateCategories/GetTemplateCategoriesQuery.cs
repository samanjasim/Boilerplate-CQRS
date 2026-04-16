using MediatR;
using Starter.Shared.Results;

namespace Starter.Module.Communication.Application.Queries.GetTemplateCategories;

public sealed record GetTemplateCategoriesQuery : IRequest<Result<List<string>>>;
