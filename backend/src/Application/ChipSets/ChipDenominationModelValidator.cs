using FluentValidation;

namespace Application.ChipSets;

internal sealed class ChipDenominationModelValidator : AbstractValidator<ChipDenominationModel>
{
    public ChipDenominationModelValidator()
    {
        RuleFor(d => d.FaceValue).GreaterThan(0);

        // The effective value is what all the arithmetic uses, so a zero would
        // make a chip worth nothing while still occupying stock.
        RuleFor(d => d.EffectiveValue).GreaterThan(0);

        RuleFor(d => d.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(d => d.Colour).MaximumLength(30);
    }
}
