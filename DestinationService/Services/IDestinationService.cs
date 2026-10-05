using DestinationService.DTOs;

namespace DestinationService.Services;

public interface IDestinationService
{
    /// <summary>
    /// Searches destinations by city or country name, or returns popular destinations if query is empty.
    /// </summary>
    Task<IReadOnlyList<DestinationDto>> SearchDestinationsAsync(string? query, int limit = 20, CancellationToken ct = default);
}
