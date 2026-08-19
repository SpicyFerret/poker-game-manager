using FluentValidation;

namespace Application.Championships.Members.Add;

internal sealed class AddMemberCommandValidator : AbstractValidator<AddMemberCommand>
{
    public AddMemberCommandValidator()
    {
        RuleFor(c => c.ChampionshipId).NotEmpty();
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
    }
}
