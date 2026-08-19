using FluentValidation;

namespace Application.ChipSets.Create;

internal sealed class CreateChipSetCommandValidator : AbstractValidator<CreateChipSetCommand>
{
    public CreateChipSetCommandValidator()
    {
        RuleFor(c => c.ChampionshipId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(80);
        RuleFor(c => c.Denominations).NotEmpty();
        RuleForEach(c => c.Denominations).SetValidator(new ChipDenominationModelValidator());
    }
}
