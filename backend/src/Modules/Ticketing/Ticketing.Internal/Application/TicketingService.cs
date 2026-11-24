using Events.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ticketing.Contracts;
using Ticketing.Infrastructure.Core;
using Ticketing.Internal.Domain;
using Ticketing.Internal.Infrastructure;

namespace Ticketing.Internal.Application;

/// <summary>
/// [TV4 - Đồng]: Service xử lý nghiệp vụ bán vé, giữ chỗ và phát hành vé điện tử.
/// </summary>
internal sealed class TicketingService : ITicketingModuleApi
{
    private readonly TicketingDbContext _db;
    private readonly QrSigner _signer;
    private readonly ILogger<TicketingService> _logger;
    private readonly IEventsModuleApi? _eventsApi;

    public TicketingService(
        TicketingDbContext db,
        QrSigner signer,
        ILogger<TicketingService> logger,
        IEventsModuleApi? eventsApi = null)
    {
        _db = db;
        _signer = signer;
        _logger = logger;
        _eventsApi = eventsApi;
    }

    /// <summary>
    /// [TV4 - Đồng]: Lấy danh sách hạng vé theo sự kiện.
    /// </summary>
    public async Task<IReadOnlyList<TicketTypeDto>> GetTypesAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        return await _db.TicketTypes
            .AsNoTracking()
            .Where(t => t.EventId == eventId && t.IsActive)
            .OrderBy(t => t.Price)
            .Select(t => new TicketTypeDto(
                t.Id,
                t.EventId,
                t.Name,
                t.Price,
                t.TotalQuantity,
                Math.Max(0, t.TotalQuantity - t.ReservedQuantity - t.SoldQuantity),
                t.MaxPerOrder))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// [TV4 - Đồng]: Trừ kho nguyên tử & Giữ chỗ 10 phút.
    /// Đảm bảo tính nhất quán cao (high concurrency), không bao giờ bị bán âm vé (oversell).
    /// Hỗ trợ chống trùng lặp (Idempotency) qua Idempotency-Key.
    /// </summary>
    public async Task<ReservationDto> ReserveAsync(
        Guid customerId,
        Guid eventId,
        Guid ticketTypeId,
        int quantity,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (quantity is < 1 or > 20)
        {
            throw new InvalidOperationException("Số vé mỗi đơn phải từ 1 đến 20.");
        }

        // 1. Chống trùng lặp (Idempotency) - Kiểm tra nếu yêu cầu đã tồn tại
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.Reservations
                .Include(r => r.Items)
                .ThenInclude(i => i.TicketType)
                .FirstOrDefaultAsync(r => r.CustomerId == customerId && r.IdempotencyKey == idempotencyKey, cancellationToken);

            if (existing is not null)
            {
                var firstItem = existing.Items.FirstOrDefault();
                return new ReservationDto(
                    existing.Id,
                    existing.EventId,
                    firstItem?.TicketTypeId ?? ticketTypeId,
                    existing.CustomerId,
                    firstItem?.Quantity ?? quantity,
                    firstItem?.TicketType?.Price ?? 0,
                    existing.ExpiresAt,
                    existing.Status);
            }
        }

        // 2. Trừ kho nguyên tử (Atomic Update) - Đảm bảo chỉ trừ khi còn đủ vé sẵn có
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var rowsAffected = await _db.TicketTypes
                .Where(t => t.Id == ticketTypeId &&
                            t.EventId == eventId &&
                            (t.TotalQuantity - t.ReservedQuantity - t.SoldQuantity) >= quantity &&
                            t.MaxPerOrder >= quantity &&
                            t.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReservedQuantity, t => t.ReservedQuantity + quantity), cancellationToken);

            if (rowsAffected == 0)
            {
                await tx.RollbackAsync(cancellationToken);
                throw new InvalidOperationException("Không đủ vé hoặc vé đã hết.");
            }

            var ticketType = await _db.TicketTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == ticketTypeId, cancellationToken);

            var price = ticketType?.Price ?? 0;

            // 3. Thiết lập TTL 10 phút cho lượt giữ vé (Reservation 10p)
            var expiresAt = DateTime.UtcNow.AddMinutes(10);
            var reservation = Reservation.Create(customerId, eventId, expiresAt, idempotencyKey);
            reservation.AddItem(ticketTypeId, quantity);

            _db.Reservations.Add(reservation);
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            _logger.LogInformation("Đã giữ thành công {Quantity} vé loại {TypeId} cho khách {CustomerId}, hết hạn lúc {ExpiresAt}",
                quantity, ticketTypeId, customerId, expiresAt);

            return new ReservationDto(
                reservation.Id,
                reservation.EventId,
                ticketTypeId,
                reservation.CustomerId,
                quantity,
                price,
                reservation.ExpiresAt,
                reservation.Status);
        }
        catch (DbUpdateException ex) when (BaseDbContext.IsUniqueConstraintViolation(ex) && !string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await tx.RollbackAsync(cancellationToken);

            var existingReservation = await _db.Reservations
                .Include(r => r.Items)
                .ThenInclude(i => i.TicketType)
                .FirstOrDefaultAsync(r => r.CustomerId == customerId && r.IdempotencyKey == idempotencyKey, cancellationToken);

            if (existingReservation is not null)
            {
                var existingItem = existingReservation.Items.FirstOrDefault();
                return new ReservationDto(
                    existingReservation.Id,
                    existingReservation.EventId,
                    existingItem?.TicketTypeId ?? ticketTypeId,
                    existingReservation.CustomerId,
                    existingItem?.Quantity ?? quantity,
                    existingItem?.TicketType?.Price ?? 0,
                    existingReservation.ExpiresAt,
                    existingReservation.Status);
            }

            throw;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// [TV4 - Đồng]: Lấy chi tiết thông tin lượt giữ vé.
    /// </summary>
    public async Task<ReservationDto?> GetReservationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _db.Reservations
            .Include(r => r.Items)
            .ThenInclude(i => i.TicketType)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (reservation is null)
        {
            return null;
        }

        var item = reservation.Items.FirstOrDefault();
        return new ReservationDto(
            reservation.Id,
            reservation.EventId,
            item?.TicketTypeId ?? Guid.Empty,
            reservation.CustomerId,
            item?.Quantity ?? 0,
            item?.TicketType?.Price ?? 0,
            reservation.ExpiresAt,
            reservation.Status);
    }

    /// <summary>
    /// [TV4 - Đồng]: Đánh dấu lượt giữ vé đang chờ thanh toán.
    /// </summary>
    public async Task MarkPaymentPendingAsync(
        Guid id,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _db.Reservations
            .FirstOrDefaultAsync(r => r.Id == id && r.CustomerId == customerId, cancellationToken);

        if (reservation is null)
        {
            throw new InvalidOperationException("Không tìm thấy thông tin lượt giữ vé.");
        }

        reservation.MarkPaymentPending();
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// [TV4 - Đồng]: Hủy lượt giữ vé và hoàn trả lại số lượng về kho sẵn có.
    /// </summary>
    public async Task ReleaseAsync(
        Guid id,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var reservation = await _db.Reservations
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id && r.CustomerId == customerId, cancellationToken);

        if (reservation is null || reservation.Status == ReservationStatus.Confirmed || reservation.Status == ReservationStatus.Released)
        {
            return;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var rowsUpdated = await _db.Reservations
                .Where(r => r.Id == id && r.CustomerId == customerId && (r.Status == ReservationStatus.Active || r.Status == ReservationStatus.PaymentPending))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, ReservationStatus.Released)
                    .SetProperty(r => r.UpdatedAt, DateTime.UtcNow), cancellationToken);

            if (rowsUpdated == 1)
            {
                foreach (var item in reservation.Items)
                {
                    await _db.TicketTypes
                        .Where(t => t.Id == item.TicketTypeId)
                        .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReservedQuantity, t => Math.Max(0, t.ReservedQuantity - item.Quantity)), cancellationToken);
                }

                await tx.CommitAsync(cancellationToken);
                _logger.LogInformation("Đã hủy và hoàn trả tồn kho cho lượt giữ vé {ReservationId}", id);
            }
            else
            {
                await tx.RollbackAsync(cancellationToken);
            }
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// [TV4 - Đồng]: Soát vé nguyên tử tại cổng: Kiểm tra chữ ký HMAC và đổi trạng thái vé sang 'Used'.
    /// Chống vé giả mạo và chống quét lại vé đã qua cổng (chống quay vòng vé).
    /// </summary>
    public async Task<RedeemTicketResult> RedeemTicketAsync(
        string qrPayload,
        Guid eventId,
        string staffId,
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra tính toàn vẹn và chữ ký số HMAC-SHA256
        if (!_signer.TryVerify(qrPayload, eventId, out var ticketId))
        {
            return RedeemTicketResult.Invalid("Mã QR không hợp lệ, bị giả mạo hoặc không thuộc sự kiện này.");
        }

        Guid? staffGuid = Guid.TryParse(staffId, out var parsedGuid) ? parsedGuid : null;

        // 2. Cập nhật nguyên tử trạng thái sang 'Used' CHỈ KHI trạng thái đang là 'Valid'
        var rowsUpdated = await _db.Tickets
            .Where(t => t.Id == ticketId && t.EventId == eventId && t.Status == TicketStatus.Valid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TicketStatus.Used)
                .SetProperty(t => t.UsedAt, DateTime.UtcNow)
                .SetProperty(t => t.UsedByStaffId, staffGuid)
                .SetProperty(t => t.UpdatedAt, DateTime.UtcNow), cancellationToken);

        var ticket = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.TicketType)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.EventId == eventId, cancellationToken);

        if (ticket is null)
        {
            return RedeemTicketResult.Invalid("Không tìm thấy thông tin vé trong hệ thống.");
        }

        var ticketTypeName = ticket.TicketType?.Name ?? "Vé";

        if (rowsUpdated > 0)
        {
            _logger.LogInformation("Check-in thành công vé {TicketId} ({Code}) bởi nhân viên {StaffId}",
                ticket.Id, ticket.Code, staffId);
            return RedeemTicketResult.Valid(ticket.Id, ticket.Code, ticketTypeName);
        }

        // 3. Nếu không cập nhật được và trạng thái đã là 'Used' -> Báo vé đã sử dụng (chống quay vòng vé)
        if (ticket.Status == TicketStatus.Used)
        {
            _logger.LogWarning("Phát hiện vé đã qua cổng trước đó: {TicketId} ({Code}), used at {UsedAt}",
                ticket.Id, ticket.Code, ticket.UsedAt);
            return RedeemTicketResult.AlreadyUsed(ticket.Code, ticket.UsedAt ?? DateTime.UtcNow);
        }

        return RedeemTicketResult.Invalid($"Trạng thái vé không hợp lệ: {ticket.Status}");
    }

    /// <summary>
    /// [TV4 - Đồng]: Lấy danh sách vé theo OrderIds.
    /// </summary>
    public async Task<IReadOnlyList<TicketDto>> GetTicketsByOrderIdsAsync(
        IEnumerable<Guid> orderIds,
        CancellationToken cancellationToken = default)
    {
        var orderIdList = orderIds.ToList();
        var tickets = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.TicketType)
            .Where(t => orderIdList.Contains(t.OrderId))
            .OrderBy(t => t.Sequence)
            .Select(t => new TicketDto
            {
                Id = t.Id,
                EventId = t.EventId,
                OrderId = t.OrderId,
                TicketTypeId = t.TicketTypeId,
                CustomerId = t.CustomerId,
                CustomerEmail = t.CustomerEmail,
                TicketCode = t.Code,
                TicketTypeName = t.TicketType.Name,
                Status = t.Status,
                QrPayload = t.QrSignature,
                UsedAt = t.UsedAt,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return await EnrichEventTitlesAsync(tickets, cancellationToken);
    }

    /// <summary>
    /// [TV4 - Đồng]: Lấy danh sách vé của khách hàng ("Vé của tôi").
    /// </summary>
    public async Task<IReadOnlyList<TicketDto>> GetMyTicketsAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var tickets = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.TicketType)
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TicketDto
            {
                Id = t.Id,
                EventId = t.EventId,
                OrderId = t.OrderId,
                TicketTypeId = t.TicketTypeId,
                CustomerId = t.CustomerId,
                CustomerEmail = t.CustomerEmail,
                TicketCode = t.Code,
                TicketTypeName = t.TicketType.Name,
                Status = t.Status,
                QrPayload = t.QrSignature,
                UsedAt = t.UsedAt,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return await EnrichEventTitlesAsync(tickets, cancellationToken);
    }

    private async Task<IReadOnlyList<TicketDto>> EnrichEventTitlesAsync(
        List<TicketDto> tickets,
        CancellationToken cancellationToken)
    {
        if (_eventsApi is null || tickets.Count == 0)
        {
            return tickets;
        }

        var eventIds = tickets.Select(t => t.EventId).Distinct();
        var eventsById = await _eventsApi.GetEventsByIdsAsync(eventIds, cancellationToken);

        return tickets
            .Select(t => eventsById.TryGetValue(t.EventId, out var ev) && !string.IsNullOrWhiteSpace(ev.Title)
                ? t with { EventTitle = ev.Title }
                : t)
            .ToList();
    }

    /// <summary>
    /// [TV4 - Đồng]: Lấy tổng số lượng vé đã phát hành của sự kiện.
    /// </summary>
    public async Task<int> GetTotalIssuedTicketsCountAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Tickets
            .AsNoTracking()
            .CountAsync(t => t.EventId == eventId && t.Status != TicketStatus.Cancelled, cancellationToken);
    }

    /// <summary>
    /// [TV4 - Đồng]: Phát hành vé điện tử có mã QR ký số HMAC sau khi đơn hàng thanh toán thành công (FR-17).
    /// </summary>
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
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var tickets = new List<Ticket>();
            var ticketDtos = new List<TicketDto>();
            var issuedTicketsForEvent = new List<IssuedTicketDto>();

            for (var i = 1; i <= quantity; i++)
            {
                var ticketId = Guid.CreateVersion7();
                var code = $"TK-{ticketId:N}".ToUpperInvariant();
                var qrSignature = _signer.Sign(ticketId, eventId);

                var ticket = Ticket.Issue(
                    orderId,
                    ticketTypeId,
                    eventId,
                    customerId,
                    i,
                    code,
                    qrSignature,
                    customerEmail,
                    ticketId);

                tickets.Add(ticket);

                ticketDtos.Add(new TicketDto
                {
                    Id = ticket.Id,
                    EventId = ticket.EventId,
                    OrderId = ticket.OrderId,
                    TicketTypeId = ticket.TicketTypeId,
                    CustomerId = ticket.CustomerId,
                    CustomerEmail = customerEmail,
                    TicketCode = ticket.Code,
                    TicketTypeName = ticketTypeName,
                    EventTitle = eventTitle,
                    Status = ticket.Status,
                    QrPayload = qrSignature,
                    CreatedAt = ticket.CreatedAt
                });

                issuedTicketsForEvent.Add(new IssuedTicketDto
                {
                    TicketId = ticket.Id,
                    TicketCode = code,
                    TicketTypeName = ticketTypeName,
                    Price = unitPrice,
                    QrPayload = qrSignature
                });
            }

            _db.Tickets.AddRange(tickets);

            // Chuyển số lượng từ ReservedQuantity sang SoldQuantity
            await _db.TicketTypes
                .Where(t => t.Id == ticketTypeId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.ReservedQuantity, t => Math.Max(0, t.ReservedQuantity - quantity))
                    .SetProperty(t => t.SoldQuantity, t => t.SoldQuantity + quantity), cancellationToken);

            // Bắn Integration Event TicketIssued vào Outbox để Notifications gửi email vé kèm mã QR
            _db.AddOutboxMessage(new TicketIssuedIntegrationEvent
            {
                OrderId = orderId,
                TargetEventId = eventId,
                EventTitle = eventTitle,
                CustomerEmail = customerEmail,
                CustomerName = customerName,
                Tickets = issuedTicketsForEvent
            });

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            _logger.LogInformation("Đã phát hành {Quantity} vé cho đơn hàng {OrderId}, khách hàng {Email}",
                quantity, orderId, customerEmail);

            return ticketDtos;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Lấy danh sách thông tin các hạng vé của sự kiện (tên, giá, tổng số lượng, đã bán).
    /// </summary>
    public async Task<IReadOnlyList<TicketTypeSummaryDto>> GetTicketTypesByEventIdAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var ticketTypes = await _db.TicketTypes
            .AsNoTracking()
            .Where(tt => tt.EventId == eventId)
            .OrderBy(tt => tt.Price)
            .Select(tt => new TicketTypeSummaryDto(
                tt.Id,
                tt.EventId,
                tt.Name,
                tt.Price,
                tt.TotalQuantity,
                tt.SoldQuantity,
                Math.Max(0, tt.TotalQuantity - tt.ReservedQuantity - tt.SoldQuantity)))
            .ToListAsync(cancellationToken);

        return ticketTypes;
    }
}
