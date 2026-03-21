using Starter.Shared.Results;
using FluentValidation;
using MediatR;

namespace Starter.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next();

        var validationErrors = ValidationErrors.FromErrors(
            failures.Select(f => new ValidationError(f.PropertyName, f.ErrorMessage)));

        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var resultType = typeof(TResponse).GetGenericArguments()[0];

            var failureMethod = typeof(Result)
                .GetMethod(nameof(Result.ValidationFailure), 1, [typeof(ValidationErrors)])!
                .MakeGenericMethod(resultType);

            return (TResponse)failureMethod.Invoke(null, [validationErrors])!;
        }

        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(object)Result.ValidationFailure(validationErrors);
        }

        throw new ValidationException(failures);
    }
}
