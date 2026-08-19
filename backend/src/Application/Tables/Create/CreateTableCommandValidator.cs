using FluentValidation;

namespace Application.Tables.Create;

internal sealed class CreateTableCommandValidator : AbstractValidator<CreateTableCommand>
{
    public CreateTableCommandValidator()
    {
        RuleFor(c => c.ChampionshipId).NotEmpty();
        RuleFor(c => c.ChipSetId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(80);
        RuleFor(c => c.BuyIn).GreaterThan(0).When(c => c.BuyIn is not null);
        RuleFor(c => c.Rebuy).GreaterThanOrEqualTo(0).When(c => c.Rebuy is not null);
        RuleFor(c => c.SmallChipReserve).GreaterThanOrEqualTo(0);
    }
}
