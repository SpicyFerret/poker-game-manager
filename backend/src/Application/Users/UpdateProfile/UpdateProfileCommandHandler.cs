using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.UpdateProfile;

internal sealed class UpdateProfileCommandHandler(IApplicationDbContext context, IUserContext userContext)
    : ICommandHandler<UpdateProfileCommand>
{
    public async Task<Result> Handle(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users
            .SingleOrDefaultAsync(u => u.Id == userContext.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(userContext.UserId));
        }

        user.DisplayName = command.DisplayName.Trim();
        user.PaymentType = command.PaymentType;
        user.PaymentHandle = command.PaymentHandle?.Trim();

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
