namespace Backend.Models;

public sealed class ArchitectureAnalysisResponse
{
    public required string Analysis { get; init; }
    public required List<string> Violations { get; init; }
    public required List<string> Suggestions { get; init; }
    public required List<string> Patterns { get; init; }
    public double Confidence { get; init; }
    public string? RefactoredCode { get; init; }
    public string? ArchitectureDiagram { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
