using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Championships.GetMine;

internal sealed class GetMyChampionshipsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext)
    : IQueryHandler<GetMyChampionshipsQuery, IReadOnlyList<ChampionshipSummaryResponse>>
{
    public async Task<Result<IReadOnlyList<ChampionshipSummaryResponse>>> Handle(
        GetMyChampionshipsQuery query,
        CancellationToken cancellationToken)
    {
        List<ChampionshipSummaryResponse> championships = await context.ChampionshipMembers
            .Where(m => m.UserId == userContext.UserId)
            .Join(
                context.Championships,
                member => member.ChampionshipId,
                championship => championship.Id,
                (member, championship) => new ChampionshipSummaryResponse
                {
                    Id = championship.Id,
                    Name = championship.Name,
                    Description = championship.Description,
                    Role = member.Role,
                    MemberCount = context.ChampionshipMembers
                        .Count(other => other.ChampionshipId == championship.Id)
                })
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return championships;
    }
}
