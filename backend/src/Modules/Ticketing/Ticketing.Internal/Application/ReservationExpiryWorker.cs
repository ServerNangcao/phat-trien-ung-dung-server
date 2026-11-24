using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ticketing.Internal.Domain;
using Ticketing.Internal.Infrastructure;

namespace Ticketing.Internal.Application;

/// <summary>
/// [TV4 - Đồng]: Trả vé hết hạn (Periodic Reservation Expiry Worker).
/// Chạy nền định kỳ quét các đơn giữ chỗ đã quá hạn 10 phút để tự động hoàn trả số lượng vé về kho sẵn có.
/// </summary>
internal sealed class ReservationExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReservationExpiryWorker> _logger;

    public ReservationExpiryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ReservationExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ExpireReservationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình quét vé giữ chỗ quá hạn 10 phút");
            }
        }
    }

    private async Task ExpireReservationsAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketingDbContext>();
        var now = DateTime.UtcNow;

        // Tìm các lượt giữ chỗ đang Active hoặc PaymentPending nhưng đã quá thời hạn 10 phút (ExpiresAt <= now)
        var expiredReservations = await db.Reservations
            .Include(r => r.Items)
            .Where(r => (r.Status == ReservationStatus.Active || r.Status == ReservationStatus.PaymentPending) && r.ExpiresAt <= now)
            .OrderBy(r => r.ExpiresAt)
            .Take(100)
            .ToListAsync(ct);

        if (expiredReservations.Count == 0)
        {
            return;
        }

        var releasedCount = 0;
        foreach (var reservation in expiredReservations)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var rowsUpdated = await db.Reservations
                    .Where(r => r.Id == reservation.Id && (r.Status == ReservationStatus.Active || r.Status == ReservationStatus.PaymentPending))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(r => r.Status, ReservationStatus.Released)
                        .SetProperty(r => r.UpdatedAt, DateTime.UtcNow), ct);

                if (rowsUpdated == 1)
                {
                    foreach (var item in reservation.Items)
                    {
                        await db.TicketTypes
                            .Where(t => t.Id == item.TicketTypeId)
                            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReservedQuantity, t => Math.Max(0, t.ReservedQuantity - item.Quantity)), ct);
                    }

                    await tx.CommitAsync(ct);
                    releasedCount++;
                }
                else
                {
                    await tx.RollbackAsync(ct);
                }
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogWarning(ex, "Lỗi khi xử lý hết hạn cho reservation {ReservationId}", reservation.Id);
            }
        }

        if (releasedCount > 0)
        {
            _logger.LogInformation("[TV4 - Đồng]: Đã hoàn trả tồn kho cho {Count} lượt giữ chỗ quá hạn 10 phút.", releasedCount);
        }
    }
}
