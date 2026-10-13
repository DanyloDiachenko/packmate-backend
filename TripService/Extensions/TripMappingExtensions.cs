using TripService.DTOs;
using TripService.Entities;

namespace TripService.Extensions;

public static class TripMappingExtensions
{
    public static TripResponse ToResponse(this Trip trip) =>
        new(trip.Id, trip.UserId, trip.Destination, trip.DepartDate, trip.ReturnDate,
            trip.Tags, trip.IsArchived, trip.CreatedAt);
}
