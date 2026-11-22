using Ticketing.SharedKernel;

namespace Events.Internal.Domain;

/// <summary>
/// Mô hình thống kê số liệu tổng hợp của sự kiện (doanh thu, vé bán, lượt check-in).
/// Lưu trữ snapshot phục vụ truy vấn Organizer Analytics độ trễ thấp p95 <= 500ms.
/// </summary>
internal sealed class EventStatistics : BaseEntity<Guid>
{
    /// <summary>
    /// ID của sự kiện.
    /// </summary>
    public Guid EventId { get; private set; }

    /// <summary>
    /// Tổng doanh thu thực tế (VND).
    /// </summary>
    public decimal TotalRevenue { get; private set; }

    /// <summary>
    /// Tổng số lượng vé đã bán thực tế.
    /// </summary>
    public int TotalTicketsSold { get; private set; }

    /// <summary>
    /// Tổng số lượng người tham dự đã quét vé check-in thành công.
    /// </summary>
    public int TotalCheckedIn { get; private set; }

    /// <summary>
    /// Chuỗi JSON lưu chi tiết phân bố vé bán theo từng hạng vé (VIP, Standard...).
    /// </summary>
    public string? TiersBreakdownJson { get; private set; }

    /// <summary>
    /// Thời điểm cập nhật số liệu thống kê gần nhất.
    /// </summary>
    public DateTime LastCalculatedAt { get; private set; }

    private EventStatistics() { }

    public static EventStatistics Create(
        Guid eventId,
        decimal totalRevenue = 0,
        int totalTicketsSold = 0,
        int totalCheckedIn = 0,
        string? tiersBreakdownJson = null) => new()
    {
        Id = Guid.CreateVersion7(),
        EventId = eventId,
        TotalRevenue = totalRevenue,
        TotalTicketsSold = totalTicketsSold,
        TotalCheckedIn = totalCheckedIn,
        TiersBreakdownJson = tiersBreakdownJson,
        LastCalculatedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };

    public void Update(
        decimal totalRevenue,
        int totalTicketsSold,
        int totalCheckedIn,
        string? tiersBreakdownJson = null)
    {
        TotalRevenue = totalRevenue;
        TotalTicketsSold = totalTicketsSold;
        TotalCheckedIn = totalCheckedIn;
        if (tiersBreakdownJson is not null)
        {
            TiersBreakdownJson = tiersBreakdownJson;
        }
        LastCalculatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
