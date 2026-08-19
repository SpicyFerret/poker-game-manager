using Application.Abstractions.Authorization;
using Domain.Championships;
using SharedKernel;

namespace Application.UnitTests.Abstractions;

/// <summary>
/// Stands in for the real <see cref="IChampionshipContext"/>, which lives in
/// Infrastructure and reads the membership table. Applies the same minimum-role
/// rule, so a handler test still exercises its own authorization branch.
/// </summary>
public sealed class FakeChampionshipContext(ChampionshipRole? role) : IChampionshipContext
{
    public Task<ChampionshipRole?> GetRoleAsync(Guid championshipId, CancellationToken cancellationToken) =>
        Task.FromResult(role);

    public Task<Result<ChampionshipRole>> RequireRoleAsync(
        Guid championshipId,
        ChampionshipRole minimum,
        CancellationToken cancellationToken)
    {
        if (role is null)
        {
            return Task.FromResult(Result.Failure<ChampionshipRole>(ChampionshipErrors.NotAMember));
        }

        return Task.FromResult(role < minimum
            ? Result.Failure<ChampionshipRole>(ChampionshipErrors.InsufficientRole(minimum))
            : Result.Success(role.Value));
    }
}
