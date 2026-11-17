using Events.Internal.Domain;
using Events.Internal.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Ticketing.SharedKernel;

namespace Events.Internal.Features.Events.CreateEvent;

internal sealed class CreateEventHandler(
    EventsDbContext db,
    IValidator<CreateEventRequest> validator)
{
    public async Task<Result<Guid>> HandleAsync(
        Guid organizerId,
        CreateEventRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Validation<Guid>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        // Kiểm tra Category tồn tại
        var categoryExists = await db.Categories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
            return Result.NotFound<Guid>("Category không tồn tại.");

        // Kiểm tra Venue tồn tại
        var venueExists = await db.Venues
            .AnyAsync(v => v.Id == request.VenueId, cancellationToken);
        if (!venueExists)
            return Result.NotFound<Guid>("Venue không tồn tại.");

        // Sinh slug từ title + timestamp (đảm bảo unique)
        var slug = GenerateSlug(request.Title);

        // Kiểm tra slug unique
        var slugExists = await db.Events
            .AnyAsync(e => e.Slug == slug, cancellationToken);
        if (slugExists)
            slug = $"{slug}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        var @event = Event.Create(
            organizerId,
            request.CategoryId,
            request.VenueId,
            request.Title,
            slug,
            request.Description,
            request.StartsAt,
            request.EndsAt);

        db.Events.Add(@event);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(@event.Id);
    }

    private static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant();
        // Normalize Vietnamese characters to ASCII (basic mapping)
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[àáạảãâầấậẩẫăằắặẳẵ]", "a");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[èéẹẻẽêềếệểễ]", "e");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[ìíịỉĩ]", "i");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[òóọỏõôồốộổỗơờớợởỡ]", "o");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[ùúụủũưừứựửữ]", "u");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[ỳýỵỷỹ]", "y");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[đ]", "d");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
        slug = slug.Trim('-');
        return slug;
    }
}
