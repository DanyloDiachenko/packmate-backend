namespace PresetService.DTOs;

public record PresetItemDto(
    Guid? Id,
    string Section,
    string Title,
    int Quantity = 1,
    string? Tag = null
);

public record CreatePresetRequest(
    string Name,
    string? Description,
    Dictionary<string, List<PresetItemDto>>? Categories
);

public record UpdatePresetRequest(
    string Name,
    string? Description,
    Dictionary<string, List<PresetItemDto>>? Categories
);

public record PresetResponse(
    Guid Id,
    Guid UserId,
    string Name,
    string? Description,
    Dictionary<string, List<PresetItemDto>> Categories,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record PresetSummaryResponse(
    Guid Id,
    Guid UserId,
    string Name,
    string? Description,
    int TotalItemsCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record ErrorResponse(string Message);