using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ticketing.Contracts;
using Ticketing.Internal.Domain;
using Ticketing.Internal.Infrastructure;

namespace Ticketing.Internal.Application;

internal sealed class TicketingService : ITicketingModuleApi
{
    private readonly TicketingDbContext _db;
    private readonly QrSigner _signer;
    private readonly ILogger<TicketingService> _logger;

    public TicketingService(
        TicketingDbContext db,
        QrSigner signer,
        ILogger<TicketingService> logger)
    {
        _db = db;
        _signer = signer;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TicketTypeDto>> GetTypesAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        return await _db.TicketTypes
            .AsNoTracking()
            .Where(x => x.EventId == eventId)
            .OrderBy(x => x.Price)
            .Select(x => new TicketTypeDto(
                x.Id,
                x.EventId,
                x.Name,
                x.Price,
                x.Capacity,
                x.QuantityAvailable,
                x.MaxPerOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReservationDto> ReserveAsync(
        Guid customerId,
        Guid eventId,
        Guid ticketTypeId,
        int quantity,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (quantity < 1 || quantity > 20)
        {
            throw new ArgumentException("Số lượng vé mỗi đơn phải từ 1 đến 20.", nameof(quantity));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency-Key không được để trống.", nameof(idempotencyKey));
        }

        // 1. Kiểm tra idempotency
        var existing = await _db.Reservations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CustomerId == customerId && x.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existing is not null)
        {
            if (existing.EventId != eventId || existing.TicketTypeId != ticketTypeId || existing.Quantity != quantity)
            {
                throw new InvalidOperationException("Idempotency-Key đã được sử dụng với thông tin khác.");
            }

            return MapReservation(existing);
        }

        // 2. Trừ kho nguyên tử chống bán vượt vé (FR-11, FR-12)
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var ticketType = await _db.TicketTypes
            .FirstOrDefaultAsync(x => x.Id == ticketTypeId && x.EventId == eventId, cancellationToken);

        if (ticketType is null)
        {
            throw new InvalidOperationException("Hạng vé không tồn tại.");
        }

        if (ticketType.QuantityAvailable < quantity || ticketType.MaxPerOrder < quantity)
        {
            throw new InvalidOperationException("Không đủ vé khả dụng hoặc vượt quá giới hạn đặt.");
        }

        ticketType.QuantityAvailable -= quantity;
        ticketType.Version++;

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            CustomerId = customerId,
            Quantity = quantity,
            UnitPrice = ticketType.Price,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            Status = "Active",
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        };

        _db.Reservations.Add(reservation);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "Tranh chấp khi giữ vé cho ticket type {TicketTypeId}", ticketTypeId);

            var retry = await _db.Reservations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CustomerId == customerId && x.IdempotencyKey == idempotencyKey, cancellationToken);

            if (retry is not null)
            {
                return MapReservation(retry);
            }

            throw new InvalidOperationException("Không thể hoàn tất giữ vé do xung đột dữ liệu. Vui lòng thử lại.");
        }

        return MapReservation(reservation);
    }

    public async Task<ReservationDto?> GetReservationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await _db.Reservations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return item is null ? null : MapReservation(item);
    }

    public async Task MarkPaymentPendingAsync(
        Guid id,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _db.Reservations
            .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId, cancellationToken);

        if (reservation is null || reservation.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Lượt giữ vé không tồn tại hoặc đã hết hạn.");
        }

        reservation.Status = "PaymentPending";
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(
        Guid id,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var reservation = await _db.Reservations
            .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId && (x.Status == "Active" || x.Status == "PaymentPending"), cancellationToken);

        if (reservation is null)
        {
            return;
        }

        reservation.Status = "Cancelled";

        var ticketType = await _db.TicketTypes
            .FirstOrDefaultAsync(x => x.Id == reservation.TicketTypeId, cancellationToken);

        if (ticketType is not null)
        {
            ticketType.QuantityAvailable += reservation.Quantity;
            ticketType.Version++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<RedeemTicketResult> RedeemTicketAsync(
        string qrPayload,
        Guid eventId,
        string staffId,
        CancellationToken cancellationToken = default)
    {
        // 1. Xác thực tính hợp lệ của chữ ký QR HMAC-SHA256 (không lộ PII)
        if (!_signer.TryVerify(qrPayload, eventId, out var ticketId))
        {
            return RedeemTicketResult.Invalid("Mã QR không hợp lệ hoặc không thuộc sự kiện này.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(x => x.Id == ticketId && x.EventId == eventId, cancellationToken);

        if (ticket is null)
        {
            return RedeemTicketResult.Invalid("Không tìm thấy thông tin vé.");
        }

        // 2. Chặn vé đã sử dụng (chống quay vòng vé)
        if (ticket.Status == "Used")
        {
            return RedeemTicketResult.AlreadyUsed(ticket.TicketCode, ticket.UsedAtUtc ?? DateTime.UtcNow);
        }

        if (ticket.Status != "Valid")
        {
            return RedeemTicketResult.Invalid($"Vé ở trạng thái không hợp lệ: {ticket.Status}");
        }

        // 3. Đổi trạng thái vé sang 'Used' nguyên tử
        ticket.Status = "Used";
        ticket.UsedAtUtc = DateTime.UtcNow;
        ticket.UsedByStaffId = staffId;

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return RedeemTicketResult.Valid(ticket.Id, ticket.TicketCode, ticket.TicketTypeName);
    }

    public async Task<IReadOnlyList<TicketDto>> GetTicketsByOrderIdsAsync(
        IEnumerable<Guid> orderIds,
        CancellationToken cancellationToken = default)
    {
        var ids = orderIds.ToList();
        return await _db.Tickets
            .AsNoTracking()
            .Where(x => ids.Contains(x.OrderId))
            .OrderBy(x => x.Sequence)
            .Select(x => MapTicket(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TicketDto>> GetMyTicketsAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Tickets
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Sequence)
            .Select(x => MapTicket(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetTotalIssuedTicketsCountAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Tickets
            .AsNoTracking()
            .CountAsync(x => x.EventId == eventId && x.Status != "Cancelled", cancellationToken);
    }

    public async Task<IReadOnlyList<TicketDto>> IssueTicketsForOrderAsync(
        Guid orderId,
        Guid eventId,
        Guid customerId,
        string customerEmail,
        string customerName,
        string eventTitle,
        Guid ticketTypeId,
        string ticketTypeName,
        decimal unitPrice,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var existingTickets = await _db.Tickets
            .Where(x => x.OrderId == orderId)
            .ToListAsync(cancellationToken);

        if (existingTickets.Count > 0)
        {
            return existingTickets.Select(MapTicket).ToList();
        }

        var tickets = new List<Ticket>();
        var issuedDtoList = new List<IssuedTicketDto>();

        for (var i = 0; i < quantity; i++)
        {
            var ticketId = Guid.NewGuid();
            var qr = _signer.Sign(ticketId, eventId);
            var code = $"TK-{ticketId.ToString("N")[..8].ToUpperInvariant()}";

            var ticket = new Ticket
            {
                Id = ticketId,
                EventId = eventId,
                OrderId = orderId,
                CustomerId = customerId,
                TicketTypeId = ticketTypeId,
                Sequence = i + 1,
                TicketCode = code,
                EventTitle = eventTitle,
                TicketTypeName = ticketTypeName,
                Status = "Valid",
                QrPayload = qr,
                CreatedAt = DateTime.UtcNow
            };

            tickets.Add(ticket);
            issuedDtoList.Add(new IssuedTicketDto
            {
                TicketId = ticketId,
                TicketCode = code,
                TicketTypeName = ticketTypeName,
                Price = unitPrice,
                QrPayload = qr
            });
        }

        _db.Tickets.AddRange(tickets);

        // Đẩy TicketIssuedIntegrationEvent vào Outbox để Notifications gửi email vé (Điểm 2, FR-18)
        _db.AddOutboxMessage(new TicketIssuedIntegrationEvent
        {
            OrderId = orderId,
            TargetEventId = eventId,
            EventTitle = eventTitle,
            CustomerEmail = customerEmail,
            CustomerName = customerName,
            Tickets = issuedDtoList
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return tickets.Select(MapTicket).ToList();
    }

    private static ReservationDto MapReservation(Reservation x) =>
        new(x.Id, x.EventId, x.TicketTypeId, x.CustomerId, x.Quantity, x.UnitPrice, x.ExpiresAtUtc, x.Status);

    private static TicketDto MapTicket(Ticket x) =>
        new()
        {
            Id = x.Id,
            EventId = x.EventId,
            OrderId = x.OrderId,
            TicketTypeId = x.TicketTypeId,
            CustomerId = x.CustomerId,
            TicketCode = x.TicketCode,
            EventTitle = x.EventTitle,
            TicketTypeName = x.TicketTypeName,
            Status = x.Status,
            QrPayload = x.QrPayload,
            UsedAt = x.UsedAtUtc,
            CreatedAt = x.CreatedAt
        };
}

