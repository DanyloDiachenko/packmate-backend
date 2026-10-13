using Microsoft.EntityFrameworkCore;
using TripService.Data;
using TripService.Entities;

namespace TripService.Services;

public interface ITripArchiveService
{
    Task AutoArchiveExpiredAsync(Guid userId, CancellationToken ct = default);
    Task<bool> AutoArchiveIfExpiredAsync(Trip trip, CancellationToken ct = default);
}

public class TripArchiveService : ITripArchiveService
{
    private readonly TripDbContext _db;

    public TripArchiveService(TripDbContext db) => _db = db;

    public Task AutoArchiveExpiredAsync(Guid userId, CancellationToken ct = default)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        return _db.Trips
            .Where(t => t.UserId == userId && !t.IsArchived && t.ReturnDate <= cutoff)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsArchived, true), ct);
    }

    public async Task<bool> AutoArchiveIfExpiredAsync(Trip trip, CancellationToken ct = default)
    {
        if (trip.IsArchived) return false;
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        if (trip.ReturnDate > cutoff) return false;

        trip.IsArchived = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
