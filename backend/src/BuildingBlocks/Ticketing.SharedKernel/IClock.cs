namespace Ticketing.SharedKernel;

/// <summary>
/// Trừu tượng hóa thời gian hệ thống để hỗ trợ kiểm thử (test).
/// Thay vì gọi DateTime.UtcNow trực tiếp, inject IClock vào service
/// và dùng IClock.UtcNow — khi viết test có thể mock thời gian tùy ý.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

