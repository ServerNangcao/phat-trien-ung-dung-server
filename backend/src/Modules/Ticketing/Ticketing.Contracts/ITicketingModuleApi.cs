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
/// DTO thông tin vé cơ bản dùng khi Orders truy vấn danh sách người tham dự (Điểm 5).
/// </summary>
public sealed record TicketDto
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public Guid TicketTypeId { get; init; }
    public string TicketCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// API công khai của module Ticketing.
/// Các module khác gọi qua interface này (in-process).
/// </summary>
public interface ITicketingModuleApi
{
    /// <summary>
    /// Soát vé nguyên tử tại cổng: Kiểm tra chữ ký HMAC và đổi trạng thái vé sang 'Used' (Điểm 6).
    /// </summary>
    Task<RedeemTicketResult> RedeemTicketAsync(
        string qrPayload,
        Guid eventId,
        string staffId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách vé theo danh sách OrderId có phân trang (Điểm 5: Danh sách người tham dự).
    /// </summary>
    Task<IReadOnlyList<TicketDto>> GetTicketsByOrderIdsAsync(
        IEnumerable<Guid> orderIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy tổng số lượng vé đã phát hành của sự kiện (phục vụ bộ đếm FR-21).
    /// </summary>
    Task<int> GetTotalIssuedTicketsCountAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách thông tin các hạng vé của sự kiện (tên, giá, tổng số lượng, đã bán).
    /// </summary>
    Task<IReadOnlyList<TicketTypeSummaryDto>> GetTicketTypesByEventIdAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);
}

public sealed record TicketTypeSummaryDto(
    Guid Id,
    Guid EventId,
    string Name,
    decimal Price,
    int TotalQuantity,
    int SoldQuantity,
    int AvailableQuantity);
