using CheckIn.Contracts;
using CheckIn.Internal.Domain;
using CheckIn.Internal.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CheckIn.Internal.Api;

internal sealed class CheckInModuleApi(CheckInDbContext db) : ICheckInModuleApi
{
    public async Task<EventCheckInSummaryDto> GetEventCheckInSummaryAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var validLogsQuery = db.CheckInLogs
            .Where(x => x.EventId == eventId && x.Result == CheckInResult.Valid);

        var totalCheckedIn = await validLogsQuery.CountAsync(cancellationToken);
        var lastScanned = totalCheckedIn > 0
            ? await validLogsQuery.MaxAsync(x => (DateTime?)x.ScannedAt, cancellationToken)
            : null;

        return new EventCheckInSummaryDto(
            EventId: eventId,
            TotalCheckedIn: totalCheckedIn,
            LastCheckInAt: lastScanned);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetCheckedInCountsByEventIdsAsync(
        IEnumerable<Guid> eventIds,
        CancellationToken cancellationToken = default)
    {
        var idList = eventIds.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, int>();

        var counts = await db.CheckInLogs
            .Where(x => idList.Contains(x.EventId) && x.Result == CheckInResult.Valid)
            .GroupBy(x => x.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count, cancellationToken);

        return counts;
    }
}
