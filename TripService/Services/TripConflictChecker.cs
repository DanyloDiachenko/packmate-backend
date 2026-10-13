using Microsoft.EntityFrameworkCore;
using TripService.Data;

namespace TripService.Services;

public interface ITripConflictChecker
{
    Task<bool> HasOverlapAsync(
        Guid userId,
        DateOnly departDate,
        DateOnly returnDate,
        Guid? excludeId = null,
        CancellationToken ct = default);
}

public class TripConflictChecker : ITripConflictChecker
{
    private readonly TripDbContext _db;

    public TripConflictChecker(TripDbContext db) => _db = db;

    public Task<bool> HasOverlapAsync(
        Guid userId,
        DateOnly departDate,
        DateOnly returnDate,
        Guid? excludeId = null,
        CancellationToken ct = default) =>
        _db.Trips.AnyAsync(t =>
            t.UserId == userId &&
            (!excludeId.HasValue || t.Id != excludeId.Value) &&
            ((departDate < t.ReturnDate && returnDate > t.DepartDate) ||
             (departDate == returnDate && t.DepartDate == t.ReturnDate && departDate == t.DepartDate)),
            ct);
}
