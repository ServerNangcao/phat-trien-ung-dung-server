using Ticketing.Modules.Orders.Domain.Entities;

namespace Ticketing.Modules.Orders.Application.Repositories;

/// <summary>
/// Repository port for discount codes (FR-16). Lives in the Application layer so the
/// domain never depends on persistence.
/// </summary>
public interface IDiscountCodeRepository
{
    /// <summary>Look up a code by its normalized value; codes are stored trimmed and upper-invariant.</summary>
    Task<DiscountCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task AddAsync(DiscountCode discountCode, CancellationToken cancellationToken = default);
}
