using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.Invites.Create;

internal sealed class CreateInviteCommandHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateInviteCommand, InviteResponse>
{
    /// <summary>
    /// The code space is 30^6 ≈ 729 million, so a clash is vanishingly unlikely —
    /// but "unlikely" isn't "never" once codes accumulate, and the unique index
    /// would turn one into a 500. A few retries make it a non-event.
    /// </summary>
    private const int MaxCodeAttempts = 5;

    public async Task<Result<InviteResponse>> Handle(
        CreateInviteCommand command,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            command.ChampionshipId,
            ChampionshipRole.TableManager,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<InviteResponse>(caller.Error);
        }

        // An invite cannot hand out a role the issuer could not grant directly.
        if (command.Role >= caller.Value)
        {
            return Result.Failure<InviteResponse>(ChampionshipErrors.CannotActOnEqualOrHigherRole);
        }

        if (command.MaxUses is <= 0)
        {
            return Result.Failure<InviteResponse>(InviteErrors.MaxUsesMustBePositive);
        }

        string? code = await GenerateUnusedCodeAsync(cancellationToken);

        if (code is null)
        {
            return Result.Failure<InviteResponse>(Error.Failure(
                "Invites.CodeGenerationFailed",
                "Could not allocate an invite code. Please try again."));
        }

        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            ChampionshipId = command.ChampionshipId,
            Code = code,
            Role = command.Role,
            CreatedBy = userContext.UserId,
            CreatedAtUtc = dateTimeProvider.UtcNow,
            ExpiresAtUtc = command.ExpiresAtUtc,
            MaxUses = command.MaxUses,
            Uses = 0
        };

        context.Invites.Add(invite);

        await context.SaveChangesAsync(cancellationToken);

        return new InviteResponse
        {
            Id = invite.Id,
            Code = invite.Code,
            Role = invite.Role,
            ExpiresAtUtc = invite.ExpiresAtUtc,
            MaxUses = invite.MaxUses,
            Uses = invite.Uses,
            IsRevoked = invite.IsRevoked
        };
    }

    private async Task<string?> GenerateUnusedCodeAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            string candidate = InviteCode.Generate();

            if (!await context.Invites.AnyAsync(i => i.Code == candidate, cancellationToken))
            {
                return candidate;
            }
        }

        return null;
    }
}
