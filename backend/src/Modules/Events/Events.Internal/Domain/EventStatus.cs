namespace Events.Internal.Domain;

/// <summary>
/// Trạng thái sự kiện — lưu dạng text trong DB với CHECK constraint.
/// Dùng constants thay vì enum để tránh lỗi khi serialize/deserialize.
/// </summary>
internal static class EventStatus
{
    public const string Draft = nameof(Draft);
    public const string PendingApproval = nameof(PendingApproval);
    public const string OnSale = nameof(OnSale);
    public const string Ended = nameof(Ended);
    public const string Rejected = nameof(Rejected);
}
