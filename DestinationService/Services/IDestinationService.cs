using DestinationService.DTOs;

namespace DestinationService.Services;

public interface IDestinationService
{
    
    
    
    Task<IReadOnlyList<DestinationDto>> SearchDestinationsAsync(string? query, int limit = 20, CancellationToken ct = default);
}
