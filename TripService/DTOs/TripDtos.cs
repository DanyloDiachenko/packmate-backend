using System;
using TripService.Entities;

namespace TripService.DTOs;

/// <summary>
/// Request payload to create a new trip.
/// </summary>
/// <param name="Destination">Trip destination information including city and country.</param>
/// <param name="DepartDate">Departure date in YYYY-MM-DD format.</param>
/// <param name="ReturnDate">Return date in YYYY-MM-DD format.</param>
/// <param name="TripType">Type of trip (e.g. leisure, business, adventure, camping).</param>
public record CreateTripRequest(
    Destination Destination,
    DateOnly DepartDate,
    DateOnly ReturnDate,
    string TripType
);

/// <summary>
/// Request payload to update an existing trip.
/// </summary>
/// <param name="Destination">Optional updated destination information.</param>
/// <param name="DepartDate">Optional updated departure date in YYYY-MM-DD format.</param>
/// <param name="ReturnDate">Optional updated return date in YYYY-MM-DD format.</param>
/// <param name="TripType">Optional updated type of trip.</param>
public record UpdateTripRequest(
    Destination Destination,
    DateOnly DepartDate,
    DateOnly ReturnDate,
    string TripType
);

/// <summary>
/// Trip details returned for created or queried trips.
/// </summary>
/// <param name="Id">Unique identifier of the trip.</param>
/// <param name="UserId">Identifier of the user who owns this trip.</param>
/// <param name="Destination">Destination details.</param>
/// <param name="DepartDate">Departure date.</param>
/// <param name="ReturnDate">Return date.</param>
/// <param name="TripType">Trip type classification.</param>
/// <param name="CreatedAt">Timestamp when trip was created in UTC.</param>
public record TripResponse(
    Guid Id,
    Guid UserId,
    Destination Destination,
    DateOnly DepartDate,
    DateOnly ReturnDate,
    string TripType,
    DateTime CreatedAt
);

/// <summary>
/// Standard error response containing a descriptive error message.
/// </summary>
/// <param name="Message">Descriptive error message.</param>
public record ErrorResponse(string Message);