using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ticketing.Contracts;
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

    public TicketingService(
        TicketingDbContext db,
        QrSigner signer,
        ILogger<TicketingService> logger)
    {
        _db = db;
        _signer = signer;
        _logger = logger;
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
        var rowsAffected = await _db.TicketTypes
            .Where(t => t.Id == ticketTypeId &&
                        t.EventId == eventId &&
                        (t.TotalQuantity - t.ReservedQuantity - t.SoldQuantity) >= quantity &&
                        t.MaxPerOrder >= quantity &&
                        t.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReservedQuantity, t => t.ReservedQuantity + quantity), cancellationToken);

        if (rowsAffected == 0)
        {
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

        if (reservation is null || reservation.Status == ReservationStatus.Released)
        {
            return;
        }

        foreach (var item in reservation.Items)
        {
            await _db.TicketTypes
                .Where(t => t.Id == item.TicketTypeId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReservedQuantity, t => Math.Max(0, t.ReservedQuantity - item.Quantity)), cancellationToken);
        }

        reservation.Release();
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Đã hủy và hoàn trả tồn kho cho lượt giữ vé {ReservationId}", id);
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
        return await _db.Tickets
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
                TicketCode = t.Code,
                TicketTypeName = t.TicketType.Name,
                Status = t.Status,
                QrPayload = t.QrSignature,
                UsedAt = t.UsedAt,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// [TV4 - Đồng]: Lấy danh sách vé của khách hàng ("Vé của tôi").
    /// </summary>
    public async Task<IReadOnlyList<TicketDto>> GetMyTicketsAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Tickets
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
                TicketCode = t.Code,
                TicketTypeName = t.TicketType.Name,
                Status = t.Status,
                QrPayload = t.QrSignature,
                UsedAt = t.UsedAt,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);
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
        var tickets = new List<Ticket>();
        var ticketDtos = new List<TicketDto>();
        var issuedTicketsForEvent = new List<IssuedTicketDto>();

        for (var i = 1; i <= quantity; i++)
        {
            var ticketId = Guid.CreateVersion7();
            var code = $"TK-{ticketId:N}"[..14].ToUpperInvariant();
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

        _logger.LogInformation("Đã phát hành {Quantity} vé cho đơn hàng {OrderId}, khách hàng {Email}",
            quantity, orderId, customerEmail);

        return ticketDtos;
    }
}
