namespace Ticketing.SharedKernel;

/// <summary>
/// Triển khai mặc định trả về thời gian thực của hệ thống.
/// Đăng ký Singleton trong DI: services.AddSingleton&lt;IClock, SystemClock&gt;();
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

