using System;
using TripService.Entities;

namespace TripService.DTOs;

public record CreateTripRequest(
    Destination Destination,
    DateOnly DepartDate,
    DateOnly ReturnDate,
    string TripType
);

public record UpdateTripRequest(
    Destination Destination,
    DateOnly DepartDate,
    DateOnly ReturnDate,
    string TripType
);

public record TripResponse(
    Guid Id,
    Guid UserId,
    Destination Destination,
    DateOnly DepartDate,
    DateOnly ReturnDate,
    string TripType,
    DateTime CreatedAt
);