namespace Backend.Models;

public sealed class ArchitectureAnalysisRequest
{
    public required string SourceCode { get; init; }
    public required string FilePath { get; init; }
    public required string LlmModel { get; init; }
    public string? AdditionalContext { get; init; }
}
