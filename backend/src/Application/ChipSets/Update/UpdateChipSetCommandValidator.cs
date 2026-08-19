using FluentValidation;

namespace Application.ChipSets.Update;

internal sealed class UpdateChipSetCommandValidator : AbstractValidator<UpdateChipSetCommand>
{
    public UpdateChipSetCommandValidator()
    {
        RuleFor(c => c.ChampionshipId).NotEmpty();
        RuleFor(c => c.ChipSetId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(80);
        RuleFor(c => c.Denominations).NotEmpty();
        RuleForEach(c => c.Denominations).SetValidator(new ChipDenominationModelValidator());
    }
}
