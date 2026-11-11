using FluentValidation;

namespace Events.Internal.Features.Events.CreateEvent;

internal sealed class CreateEventValidator : AbstractValidator<CreateEventRequest>
{
    public CreateEventValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.VenueId).NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.Description)
            .MaximumLength(5000)
            .When(x => x.Description is not null);

        RuleFor(x => x.StartsAt)
            .NotEmpty()
            .GreaterThan(DateTime.UtcNow).WithMessage("StartsAt phải là thời điểm trong tương lai.");

        RuleFor(x => x.EndsAt)
            .NotEmpty()
            .GreaterThan(x => x.StartsAt).WithMessage("EndsAt phải sau StartsAt.");
    }
}
