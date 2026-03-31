using Starter.Application.Common.Interfaces;
using Starter.Domain.Identity.Errors;
using Starter.Domain.Identity.ValueObjects;
using Starter.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Starter.Application.Features.Users.Commands.UpdateUser;

internal sealed class UpdateUserCommandHandler(
    IApplicationDbContext context) : IRequestHandler<UpdateUserCommand, Result>
{
    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
            return Result.Failure(UserErrors.NotFound(request.Id));

        var newEmail = Email.Create(request.Email);
        var fullName = FullName.Create(request.FirstName, request.LastName);
        var phoneNumber = !string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? PhoneNumber.Create(request.PhoneNumber)
            : null;

        // Check email uniqueness if changed
        if (newEmail != user.Email)
        {
            var emailExists = await context.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Email.Value == newEmail.Value && u.Id != request.Id, cancellationToken);

            if (emailExists)
                return Result.Failure(UserErrors.EmailAlreadyExists(request.Email));
        }

        user.UpdateProfile(fullName, phoneNumber, newEmail);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
