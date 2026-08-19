using FluentValidation;

namespace Application.Users.UpdateProfile;

internal sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(c => c.DisplayName).NotEmpty().MaximumLength(40);

        RuleFor(c => c.PaymentHandle)
            .NotEmpty()
            .MaximumLength(200)
            .When(c => c.PaymentType is not null)
            .WithMessage("A payment handle is required when a payment type is set.");

        RuleFor(c => c.PaymentType)
            .NotNull()
            .When(c => !string.IsNullOrWhiteSpace(c.PaymentHandle))
            .WithMessage("A payment type is required when a payment handle is set.");
    }
}
