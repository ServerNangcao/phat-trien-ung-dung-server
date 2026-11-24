using Microsoft.EntityFrameworkCore;
using Ticketing.Infrastructure.Core.Inbox;
using Ticketing.Infrastructure.Core.Outbox;

namespace Ticketing.Infrastructure.Core;

/// <summary>
/// BaseDbContext cấu hình sẵn schema riêng, bảng migration history riêng,
/// và bảng OutboxMessages + InboxMessages cho mỗi module.
/// Mỗi module kế thừa class này và chỉ định schema name.
/// </summary>
public abstract class BaseDbContext : DbContext
{
    private readonly string _schema;

    protected BaseDbContext(DbContextOptions options, string schema) : base(options)
    {
        _schema = schema;
    }

    /// <summary>
    /// Schema name của module — dùng cho migration history table.
    /// </summary>
    public string Schema => _schema;

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(_schema);

        // Áp dụng cấu hình Outbox/Inbox (bảng nằm trong schema của module)
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
    }

    /// <summary>
    /// Thêm một IntegrationEvent vào Outbox trong cùng transaction hiện tại.
    /// EventType lưu tên class ngắn (không có namespace) để tránh lỗi khi đổi namespace.
    /// </summary>
    public void AddOutboxMessage(Ticketing.SharedKernel.IntegrationEvent integrationEvent)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = integrationEvent.GetType().Name,
            EventId = integrationEvent.EventId,
            Payload = System.Text.Json.JsonSerializer.Serialize(
                integrationEvent,
                integrationEvent.GetType()),
            CreatedAt = DateTime.UtcNow,
        };

        OutboxMessages.Add(message);
    }

    /// <summary>
    /// Kiểm tra và ghi Inbox — trả về true nếu event chưa xử lý (ghi thành công),
    /// false nếu đã xử lý rồi (unique constraint violation).
    /// Phải gọi trong cùng transaction với logic nghiệp vụ.
    /// </summary>
    public async Task<bool> TryAddInboxMessageAsync(
        Guid eventId,
        string handler,
        CancellationToken cancellationToken = default)
    {
        var exists = await InboxMessages
            .AnyAsync(x => x.EventId == eventId && x.Handler == handler, cancellationToken);

        if (exists)
        {
            return false;
        }

        InboxMessages.Add(new InboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Handler = handler,
            ProcessedAt = DateTime.UtcNow,
        });

        return true;
    }

    /// <summary>
    /// Kiểm tra ngoại lệ DbUpdateException có phải do vi phạm ràng buộc Unique hay không.
    /// Hỗ trợ mã lỗi 23505 trên PostgreSQL và chuỗi lỗi "UNIQUE constraint failed" trên SQLite.
    /// </summary>
    public static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is Npgsql.PostgresException pgEx)
        {
            return pgEx.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation;
        }

        var innerType = ex.InnerException?.GetType().Name;
        if (innerType == "SqliteException")
        {
            var message = ex.InnerException?.Message ?? string.Empty;
            return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
