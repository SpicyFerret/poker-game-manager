using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Championships;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.Members.GetMembers;

internal sealed class GetMembersQueryHandler(
    IApplicationDbContext context,
    IChampionshipContext championshipContext)
    : IQueryHandler<GetMembersQuery, IReadOnlyList<MemberResponse>>
{
    public async Task<Result<IReadOnlyList<MemberResponse>>> Handle(
        GetMembersQuery query,
        CancellationToken cancellationToken)
    {
        Result<ChampionshipRole> role = await championshipContext.RequireRoleAsync(
            query.ChampionshipId,
            ChampionshipRole.Player,
            cancellationToken);

        if (role.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MemberResponse>>(role.Error);
        }

        List<MemberResponse> members = await context.ChampionshipMembers
            .Where(m => m.ChampionshipId == query.ChampionshipId)
            .Select(m => new MemberResponse
            {
                UserId = m.UserId,
                DisplayName = m.User.DisplayName,
                Role = m.Role,
                JoinedAtUtc = m.JoinedAtUtc,
                HasPaymentHandle = m.User.PaymentHandle != null
            })
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.DisplayName)
            .ToListAsync(cancellationToken);

        return members;
    }
}
