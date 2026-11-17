using Events.Internal.Domain;
using Events.Internal.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Ticketing.SharedKernel;

namespace Events.Internal.Features.Events.UpdateEvent;

internal sealed record UpdateEventRequest(
    Guid? CategoryId,
    Guid? VenueId,
    string? Title,
    string? Description,
    DateTime? StartsAt,
    DateTime? EndsAt);

internal sealed class UpdateEventValidator : AbstractValidator<UpdateEventRequest>
{
    public UpdateEventValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(500)
            .When(x => x.Title is not null);

        RuleFor(x => x.Description)
            .MaximumLength(5000)
            .When(x => x.Description is not null);

        RuleFor(x => x)
            .Must(x => x.StartsAt is null || x.EndsAt is null || x.EndsAt > x.StartsAt)
            .WithMessage("EndsAt phải sau StartsAt.");
    }
}

internal sealed class UpdateEventHandler(
    EventsDbContext db,
    IValidator<UpdateEventRequest> validator)
{
    public async Task<Result> HandleAsync(
        Guid eventId,
        Guid organizerId,
        UpdateEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Validation(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var @event = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);
        if (@event is null)
            return Result.NotFound("Sự kiện không tồn tại.");

        // Chỉ Organizer sở hữu mới được sửa
        if (@event.OrganizerId != organizerId)
            return Result.Forbidden("Bạn không có quyền chỉnh sửa sự kiện này.");

        // Chỉ sửa được khi ở Draft hoặc Rejected
        if (@event.Status != EventStatus.Draft && @event.Status != EventStatus.Rejected)
            return Result.Conflict("Chỉ có thể chỉnh sửa sự kiện ở trạng thái Draft hoặc Rejected.");

        if (request.CategoryId.HasValue)
        {
            var categoryExists = await db.Categories.AnyAsync(c => c.Id == request.CategoryId.Value, cancellationToken);
            if (!categoryExists)
                return Result.NotFound("Category không tồn tại.");
        }

        if (request.VenueId.HasValue)
        {
            var venueExists = await db.Venues.AnyAsync(v => v.Id == request.VenueId.Value, cancellationToken);
            if (!venueExists)
                return Result.NotFound("Venue không tồn tại.");
        }

        @event.Update(
            request.CategoryId,
            request.VenueId,
            request.Title,
            request.Description,
            request.StartsAt,
            request.EndsAt);

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
