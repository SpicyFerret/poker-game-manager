using Application.Abstractions.Messaging;
using Application.Users.UpdateProfile;
using Domain.Users;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class UpdateProfile : IEndpoint
{
    public sealed record Request(string DisplayName, PaymentHandleType? PaymentType, string? PaymentHandle);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/me/profile", async (
            Request request,
            ICommandHandler<UpdateProfileCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateProfileCommand(
                request.DisplayName,
                request.PaymentType,
                request.PaymentHandle);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .RequireAuthorization();
    }
}
