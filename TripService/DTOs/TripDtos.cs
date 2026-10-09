using System;
using System.Text.Json.Serialization;
using TripService.Entities;

namespace TripService.DTOs;

/// <summary>
/// Request payload to create a new trip.
/// </summary>
/// <param name="Destination">Trip destination information including city and country.</param>
/// <param name="DepartDate">Departure date in YYYY-MM-DD format.</param>
/// <param name="ReturnDate">Return date in YYYY-MM-DD format.</param>
/// <param name="Tags">Array of tags or categories for the trip (e.g. leisure, beach, skiing).</param>
public record CreateTripRequest(
    Destination Destination,
    DateOnly DepartDate,
    DateOnly ReturnDate,
    List<string> Tags
);

/// <summary>
/// Request payload to update an existing trip.
/// </summary>
/// <param name="Destination">Optional updated destination information.</param>
/// <param name="DepartDate">Optional updated departure date in YYYY-MM-DD format.</param>
/// <param name="ReturnDate">Optional updated return date in YYYY-MM-DD format.</param>
/// <param name="Tags">Optional updated array of tags for the trip.</param>
/// <param name="IsArchived">Optional updated archive status for the trip.</param>
public record UpdateTripRequest(
    Destination? Destination,
    DateOnly DepartDate,
    DateOnly ReturnDate,
    List<string>? Tags,
    bool? IsArchived = null
);

/// <summary>
/// Request payload to set or toggle the archived state of a trip.
/// </summary>
/// <param name="IsArchived">Whether the trip is archived.</param>
public record SetTripArchivedRequest(bool IsArchived);

/// <summary>
/// Trip details returned for created or queried trips.
/// </summary>
/// <param name="Id">Unique identifier of the trip.</param>
/// <param name="UserId">Identifier of the user who owns this trip.</param>
/// <param name="Destination">Destination details.</param>
/// <param name="DepartDate">Departure date.</param>
/// <param name="ReturnDate">Return date.</param>
/// <param name="Tags">Array of tags associated with the trip.</param>
/// <param name="IsArchived">Whether the trip is archived.</param>
/// <param name="CreatedAt">Timestamp when trip was created in UTC.</param>
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

/// <summary>
/// Paginated trips response containing the total count and the list of trips.
/// </summary>
/// <param name="TotalItems">Total count of trips belonging to the authenticated user.</param>
/// <param name="Trips">List of trips for the current page.</param>
public record PaginatedTripsResponse(
    [property: JsonPropertyName("totalItems")] int TotalItems,
    [property: JsonPropertyName("trips")] IReadOnlyList<TripResponse> Trips
);

/// <summary>
/// Standard error response containing a descriptive error message.
/// </summary>
/// <param name="Message">Descriptive error message.</param>
public record ErrorResponse(string Message);