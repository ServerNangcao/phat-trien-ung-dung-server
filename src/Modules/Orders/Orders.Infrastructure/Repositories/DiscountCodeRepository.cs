using Microsoft.EntityFrameworkCore;
using Ticketing.Modules.Orders.Application.Repositories;
using Ticketing.Modules.Orders.Domain.Entities;

namespace Ticketing.Modules.Orders.Infrastructure.Repositories;

public sealed class DiscountCodeRepository : IDiscountCodeRepository
{
    private readonly OrdersDbContext _dbContext;

    public DiscountCodeRepository(OrdersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DiscountCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        // Codes are stored normalized (trimmed, upper-invariant); normalize the lookup too.
        // The entity stays tracked so callers can record a redemption on it.
        var normalized = code.Trim().ToUpperInvariant();
        return await _dbContext.DiscountCodes
            .FirstOrDefaultAsync(d => d.Code == normalized, cancellationToken);
    }

    public async Task AddAsync(DiscountCode discountCode, CancellationToken cancellationToken = default)
    {
        await _dbContext.DiscountCodes.AddAsync(discountCode, cancellationToken);
    }
}
