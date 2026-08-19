using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.Invites.GetInvites;

internal sealed class GetInvitesQueryHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : IQueryHandler<GetInvitesQuery, IReadOnlyList<InviteResponse>>
{
    public async Task<Result<IReadOnlyList<InviteResponse>>> Handle(
        GetInvitesQuery query,
        CancellationToken cancellationToken)
    {
        // Codes are credentials — anyone who can read one can hand out membership,
        // so a plain Player has no business listing them.
        Result<ChampionshipRole> caller = await championshipContext.RequireRoleAsync(
            query.ChampionshipId,
            ChampionshipRole.TableManager,
            cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<InviteResponse>>(caller.Error);
        }

        List<InviteResponse> invites = await context.Invites
            .Where(i => i.ChampionshipId == query.ChampionshipId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new InviteResponse
            {
                Id = i.Id,
                Code = i.Code,
                Role = i.Role,
                ExpiresAtUtc = i.ExpiresAtUtc,
                MaxUses = i.MaxUses,
                Uses = i.Uses,
                IsRevoked = i.IsRevoked
            })
            .ToListAsync(cancellationToken);

        return invites;
    }
}
