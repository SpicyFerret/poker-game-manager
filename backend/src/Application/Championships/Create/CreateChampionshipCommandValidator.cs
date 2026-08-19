using FluentValidation;

namespace Application.Championships.Create;

internal sealed class CreateChampionshipCommandValidator : AbstractValidator<CreateChampionshipCommand>
{
    public CreateChampionshipCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(80);
        RuleFor(c => c.Description).MaximumLength(500);

        RuleFor(c => c.DefaultBuyIn).GreaterThan(0);
        RuleFor(c => c.DefaultRebuy).GreaterThanOrEqualTo(0);

        // Zero would make every stack worth nothing and every settlement zero.
        RuleFor(c => c.MoneyPerUnit).GreaterThan(0);

        RuleForEach(c => c.PointsByPosition)
            .GreaterThanOrEqualTo(0)
            .When(c => c.PointsByPosition is not null);
    }
}
