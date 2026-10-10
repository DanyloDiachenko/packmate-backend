namespace TripItemsService.DTOs;








public record CreateTripItemRequest(
    string Section,
    string Title,
    int Quantity = 1,
    string? Tag = null
);





public record BulkCreateTripItemsRequest(
    List<CreateTripItemRequest> Items
);









public record UpdateTripItemRequest(
    string? Section,
    string? Title,
    int? Quantity,
    bool? IsTaken,
    string? Tag
);











public record TripItemResponse(
    Guid Id,
    Guid TripId,
    string Section,
    string Title,
    int Quantity,
    bool IsTaken,
    string? Tag
);






public record TripSectionGroupResponse(
    string Section,
    List<TripItemResponse> Items
);





public record ErrorResponse(string Message);




public record TripSummaryDto(
    Guid Id,
    bool IsArchived
);