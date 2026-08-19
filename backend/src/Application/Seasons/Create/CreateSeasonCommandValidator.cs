using FluentValidation;

namespace Application.Seasons.Create;

internal sealed class CreateSeasonCommandValidator : AbstractValidator<CreateSeasonCommand>
{
    public CreateSeasonCommandValidator()
    {
        RuleFor(c => c.ChampionshipId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(60);
    }
}
