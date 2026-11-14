using Events.Internal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Ticketing.SharedKernel;

namespace Events.Internal.Features.Events.ManageCategory;

// ─── Category CRUD ───────────────────────────────────────────────────────────

internal sealed record CreateCategoryRequest(string Name, string Slug);

internal sealed record CategoryResponse(Guid Id, string Name, string Slug);

internal sealed class CategoryHandler(EventsDbContext db)
{
    public async Task<Result<Guid>> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Validation<Guid>("Tên category không được để trống.");

        if (string.IsNullOrWhiteSpace(request.Slug))
            return Result.Validation<Guid>("Slug không được để trống.");

        var slugExists = await db.Categories
            .AnyAsync(c => c.Slug == request.Slug, cancellationToken);

        if (slugExists)
            return Result.Conflict<Guid>($"Slug '{request.Slug}' đã tồn tại.");

        var category = Domain.Category.Create(request.Name, request.Slug);
        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(category.Id);
    }

    public async Task<Result<IReadOnlyList<CategoryResponse>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var categories = await db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponse(c.Id, c.Name, c.Slug))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CategoryResponse>>(categories);
    }
}
