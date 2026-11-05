using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ticketing.Internal.Infrastructure;

namespace Ticketing.Internal.Application;

/// <summary>
/// Background worker tự động quét và nhả vé khi lượt giữ vé 10 phút hết hạn (FR-11).
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
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TicketingDbContext>();

                var now = DateTime.UtcNow;
                var expiredReservations = await db.Reservations
                    .Where(x => (x.Status == "Active" || x.Status == "PaymentPending") && x.ExpiresAtUtc <= now)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                if (expiredReservations.Count == 0)
                {
                    continue;
                }

                await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);

                foreach (var reservation in expiredReservations)
                {
                    reservation.Status = "Expired";

                    var ticketType = await db.TicketTypes
                        .FirstOrDefaultAsync(x => x.Id == reservation.TicketTypeId, stoppingToken);

                    if (ticketType is not null)
                    {
                        ticketType.QuantityAvailable += reservation.Quantity;
                        ticketType.Version++;
                    }
                }

                await db.SaveChangesAsync(stoppingToken);
                await tx.CommitAsync(stoppingToken);

                _logger.LogInformation("Đã hoàn trả {Count} lượt giữ vé hết hạn vào kho.", expiredReservations.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong ReservationExpiryWorker khi nhả vé.");
            }
        }
    }
}

