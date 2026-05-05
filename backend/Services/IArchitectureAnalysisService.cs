using Backend.Models;

namespace Backend.Services;

public interface IArchitectureAnalysisService
{
    Task<ArchitectureAnalysisResponse> AnalyzeAsync(ArchitectureAnalysisRequest request, CancellationToken cancellationToken = default);
}
