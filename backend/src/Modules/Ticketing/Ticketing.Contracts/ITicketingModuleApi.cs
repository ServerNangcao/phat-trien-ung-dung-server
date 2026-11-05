using Ticketing.SharedKernel;

namespace Ticketing.Contracts;

/// <summary>
/// Trạng thái kết quả check-in vé.
/// </summary>
public enum RedeemTicketStatus
{
    Valid,
    AlreadyUsed,
    Invalid
}

/// <summary>
/// Kết quả check-in trả về từ Ticketing module (Điểm 6).
/// </summary>
public sealed record RedeemTicketResult
{
    public RedeemTicketStatus Status { get; init; }
    public Guid? TicketId { get; init; }
    public string? TicketCode { get; init; }
    public string? TicketTypeName { get; init; }
    public DateTime? UsedAt { get; init; }
    public string? Message { get; init; }

    public static RedeemTicketResult Valid(Guid ticketId, string ticketCode, string ticketTypeName) =>
        new()
        {
            Status = RedeemTicketStatus.Valid,
            TicketId = ticketId,
            TicketCode = ticketCode,
            TicketTypeName = ticketTypeName,
            Message = "Vé hợp lệ"
        };

    public static RedeemTicketResult AlreadyUsed(string ticketCode, DateTime usedAt) =>
        new()
        {
            Status = RedeemTicketStatus.AlreadyUsed,
            TicketCode = ticketCode,
            UsedAt = usedAt,
            Message = $"Vé đã được sử dụng lúc {usedAt:HH:mm:ss dd/MM/yyyy}"
        };

    public static RedeemTicketResult Invalid(string message) =>
        new()
        {
            Status = RedeemTicketStatus.Invalid,
            Message = message
        };
}

/// <summary>
/// DTO thông tin vé dùng trong đơn hàng và quản lý vé cá nhân.
/// </summary>
public sealed record TicketDto
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public Guid OrderId { get; init; }
    public Guid TicketTypeId { get; init; }
    public Guid CustomerId { get; init; }
    public string TicketCode { get; init; } = string.Empty;
    public string EventTitle { get; init; } = string.Empty;
    public string TicketTypeName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string QrPayload { get; init; } = string.Empty;
    public DateTime? UsedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record TicketTypeDto(
    Guid Id,
    Guid EventId,
    string Name,
    decimal Price,
    int Capacity,
    int QuantityAvailable,
    int MaxPerOrder);

public sealed record ReservationDto(
    Guid Id,
    Guid EventId,
    Guid TicketTypeId,
    Guid CustomerId,
    int Quantity,
    decimal UnitPrice,
    DateTime ExpiresAtUtc,
    string Status);

/// <summary>
/// API công khai của module Ticketing.
/// Các module khác gọi qua interface này (in-process).
/// </summary>
public interface ITicketingModuleApi
{
    /// <summary>
    /// Lấy danh sách hạng vé theo sự kiện.
    /// </summary>
    Task<IReadOnlyList<TicketTypeDto>> GetTypesAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo lượt giữ vé tạm thời 10 phút và trừ tồn kho nguyên tử (FR-11, FR-12).
    /// </summary>
    Task<ReservationDto> ReserveAsync(
        Guid customerId,
        Guid eventId,
        Guid ticketTypeId,
        int quantity,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy thông tin lượt giữ vé.
    /// </summary>
    Task<ReservationDto?> GetReservationAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đánh dấu lượt giữ vé đang chờ thanh toán.
    /// </summary>
    Task MarkPaymentPendingAsync(
        Guid id,
        Guid customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hủy lượt giữ vé và hoàn trả tồn kho.
    /// </summary>
    Task ReleaseAsync(
        Guid id,
        Guid customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soát vé nguyên tử tại cổng: Kiểm tra chữ ký HMAC và đổi trạng thái vé sang 'Used' (Điểm 6, FR-20, FR-21).
    /// </summary>
    Task<RedeemTicketResult> RedeemTicketAsync(
        string qrPayload,
        Guid eventId,
        string staffId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách vé theo danh sách OrderId (phục vụ đối soát & danh sách người tham dự).
    /// </summary>
    Task<IReadOnlyList<TicketDto>> GetTicketsByOrderIdsAsync(
        IEnumerable<Guid> orderIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách vé của khách hàng ("Vé của tôi").
    /// </summary>
    Task<IReadOnlyList<TicketDto>> GetMyTicketsAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy tổng số lượng vé đã phát hành của sự kiện (bộ đếm FR-21).
    /// </summary>
    Task<int> GetTotalIssuedTicketsCountAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phát hành vé điện tử có mã QR ký số HMAC sau khi đơn hàng thanh toán thành công (FR-17).
    /// </summary>
    Task<IReadOnlyList<TicketDto>> IssueTicketsForOrderAsync(
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
        CancellationToken cancellationToken = default);
}
