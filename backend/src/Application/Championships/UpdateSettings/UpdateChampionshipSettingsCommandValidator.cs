using FluentValidation;

namespace Application.Championships.UpdateSettings;

internal sealed class UpdateChampionshipSettingsCommandValidator
    : AbstractValidator<UpdateChampionshipSettingsCommand>
{
    public UpdateChampionshipSettingsCommandValidator()
    {
        RuleFor(c => c.ChampionshipId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(80);
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.DefaultBuyIn).GreaterThan(0);
        RuleFor(c => c.DefaultRebuy).GreaterThanOrEqualTo(0);
        RuleFor(c => c.MoneyPerUnit).GreaterThan(0);

        RuleFor(c => c.PointsByPosition).NotEmpty();
        RuleForEach(c => c.PointsByPosition).GreaterThanOrEqualTo(0);
    }
}
