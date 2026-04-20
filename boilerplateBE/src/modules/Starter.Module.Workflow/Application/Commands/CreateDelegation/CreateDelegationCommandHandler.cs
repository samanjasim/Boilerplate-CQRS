using MediatR;
using Microsoft.Extensions.Logging;
using Starter.Abstractions.Capabilities;
using Starter.Application.Common.Interfaces;
using Starter.Module.Workflow.Domain.Entities;
using Starter.Module.Workflow.Infrastructure.Persistence;
using Starter.Shared.Results;

namespace Starter.Module.Workflow.Application.Commands.CreateDelegation;

internal sealed class CreateDelegationCommandHandler(
    WorkflowDbContext dbContext,
    ICurrentUserService currentUser,
    IMessageDispatcher messageDispatcher,
    ILogger<CreateDelegationCommandHandler> logger) : IRequestHandler<CreateDelegationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateDelegationCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!.Value;

        // Validate dates
        if (request.StartDate >= request.EndDate)
            return Result.Failure<Guid>(Error.Validation(
                "Delegation.InvalidDates", "Start date must be before end date."));

        if (request.EndDate <= DateTime.UtcNow)
            return Result.Failure<Guid>(Error.Validation(
                "Delegation.ExpiredDates", "End date must be in the future."));

        if (request.ToUserId == userId)
            return Result.Failure<Guid>(Error.Validation(
                "Delegation.SelfDelegation", "Cannot delegate to yourself."));

        var rule = DelegationRule.Create(
            currentUser.TenantId,
            userId,
            request.ToUserId,
            request.StartDate,
            request.EndDate);

        dbContext.DelegationRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Notify the delegate
        try
        {
            var variables = new Dictionary<string, object>
            {
                ["startDate"] = request.StartDate.ToString("yyyy-MM-dd"),
                ["endDate"] = request.EndDate.ToString("yyyy-MM-dd"),
            };

            await messageDispatcher.SendAsync(
                "workflow.delegation-created",
                request.ToUserId,
                variables,
                currentUser.TenantId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to send delegation notification to user {UserId}", request.ToUserId);
        }

        logger.LogInformation(
            "Delegation created: {FromUserId} -> {ToUserId} ({StartDate} to {EndDate})",
            userId, request.ToUserId, request.StartDate, request.EndDate);

        return Result.Success(rule.Id);
    }
}
