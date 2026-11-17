using Microsoft.EntityFrameworkCore;
using Ticketing.Contracts;
using Ticketing.Internal.Domain;
using Ticketing.Internal.Infrastructure;

namespace Ticketing.Internal.Api;

internal sealed class TicketingModuleApi(TicketingDbContext db) : ITicketingModuleApi
{
    public async Task<RedeemTicketResult> RedeemTicketAsync(
        string qrPayload,
        Guid eventId,
        string staffId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await db.Tickets
            .Include(t => t.TicketType)
            .FirstOrDefaultAsync(t => t.Code == qrPayload && t.EventId == eventId, cancellationToken);

        if (ticket is null)
            return RedeemTicketResult.Invalid("Mã vé không tồn tại trong sự kiện này.");

        if (ticket.Status == TicketStatus.Used)
            return RedeemTicketResult.AlreadyUsed(ticket.Code, ticket.UsedAt ?? DateTime.UtcNow);

        if (ticket.Status != TicketStatus.Valid)
            return RedeemTicketResult.Invalid($"Vé không thể sử dụng. Trạng thái hiện tại: {ticket.Status}");

        var staffGuid = Guid.TryParse(staffId, out var parsedGuid) ? parsedGuid : Guid.Empty;
        ticket.MarkUsed(staffGuid);
        await db.SaveChangesAsync(cancellationToken);

        return RedeemTicketResult.Valid(ticket.Id, ticket.Code, ticket.TicketType?.Name ?? "Vé");
    }

    public async Task<IReadOnlyList<TicketDto>> GetTicketsByOrderIdsAsync(
        IEnumerable<Guid> orderIds,
        CancellationToken cancellationToken = default)
    {
        var idList = orderIds.Distinct().ToList();
        if (idList.Count == 0)
            return [];

        var tickets = await db.Tickets
            .Where(t => idList.Contains(t.OrderId))
            .Select(t => new TicketDto
            {
                Id = t.Id,
                OrderId = t.OrderId,
                TicketTypeId = t.TicketTypeId,
                TicketCode = t.Code,
                Status = t.Status,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return tickets;
    }

    public async Task<int> GetTotalIssuedTicketsCountAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        return await db.Tickets
            .CountAsync(t => t.EventId == eventId, cancellationToken);
    }

    public async Task<IReadOnlyList<TicketTypeSummaryDto>> GetTicketTypesByEventIdAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var ticketTypes = await db.TicketTypes
            .Where(tt => tt.EventId == eventId)
            .OrderBy(tt => tt.Price)
            .Select(tt => new TicketTypeSummaryDto(
                tt.Id,
                tt.EventId,
                tt.Name,
                tt.Price,
                tt.TotalQuantity,
                tt.SoldQuantity,
                tt.AvailableQuantity))
            .ToListAsync(cancellationToken);

        return ticketTypes;
    }
}
