using System;
using System.Text.Json.Serialization;
using TripService.Entities;

namespace TripService.DTOs;








public record CreateTripRequest(
    Destination Destination,
    DateOnly DepartDate,
    DateOnly ReturnDate,
    List<string> Tags
);









public record UpdateTripRequest(
    Destination? Destination,
    DateOnly DepartDate,
    DateOnly ReturnDate,
    List<string>? Tags,
    bool? IsArchived = null
);





public record SetTripArchivedRequest(bool IsArchived);












public record TripResponse(
    Guid Id,
    Guid UserId,
    Destination Destination,
    DateOnly DepartDate,
    DateOnly ReturnDate,
    List<string> Tags,
    bool IsArchived,
    DateTime CreatedAt
);






public record PaginatedTripsResponse(
    [property: JsonPropertyName("totalItems")] int TotalItems,
    [property: JsonPropertyName("trips")] IReadOnlyList<TripResponse> Trips
);





public record ErrorResponse(string Message);