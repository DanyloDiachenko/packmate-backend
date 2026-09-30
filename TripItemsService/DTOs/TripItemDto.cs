namespace TripItemsService.DTOs;

/// <summary>
/// Request payload to add a single item to a trip packing list.
/// </summary>
/// <param name="Section">Category section (e.g. Clothes, Documents, Toiletries, Tech).</param>
/// <param name="Title">Title or name of the item.</param>
/// <param name="Quantity">Quantity of the item (defaults to 1).</param>
/// <param name="Tag">Optional descriptive tag (e.g. Essential, Weather, Optional).</param>
public record CreateTripItemRequest(
    string Section,
    string Title,
    int Quantity = 1,
    string? Tag = null
);

/// <summary>
/// Request payload to bulk add multiple items to a trip packing list.
/// </summary>
/// <param name="Items">List of items to add to the trip.</param>
public record BulkCreateTripItemsRequest(
    List<CreateTripItemRequest> Items
);

/// <summary>
/// Request payload to update an existing packing item.
/// </summary>
/// <param name="Section">Optional updated section category.</param>
/// <param name="Title">Optional updated title or name.</param>
/// <param name="Quantity">Optional updated quantity.</param>
/// <param name="IsTaken">Optional flag indicating whether item has been packed/taken.</param>
/// <param name="Tag">Optional updated tag.</param>
public record UpdateTripItemRequest(
    string? Section,
    string? Title,
    int? Quantity,
    bool? IsTaken,
    string? Tag
);

/// <summary>
/// Packing item response details.
/// </summary>
/// <param name="Id">Unique identifier of the trip item.</param>
/// <param name="TripId">Identifier of the associated trip.</param>
/// <param name="Section">Category section of the item.</param>
/// <param name="Title">Title or name of the item.</param>
/// <param name="Quantity">Quantity of the item.</param>
/// <param name="IsTaken">Flag indicating whether the item is marked as packed/taken.</param>
/// <param name="Tag">Optional descriptive tag.</param>
public record TripItemResponse(
    Guid Id,
    Guid TripId,
    string Section,
    string Title,
    int Quantity,
    bool IsTaken,
    string? Tag
);

/// <summary>
/// Group of packing items categorized by section.
/// </summary>
/// <param name="Section">Category section name.</param>
/// <param name="Items">List of items belonging to this section.</param>
public record TripSectionGroupResponse(
    string Section,
    List<TripItemResponse> Items
);

/// <summary>
/// Standard error response containing a descriptive error message.
/// </summary>
/// <param name="Message">Descriptive error message.</param>
public record ErrorResponse(string Message);