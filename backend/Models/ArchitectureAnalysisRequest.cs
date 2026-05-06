using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public sealed class ArchitectureAnalysisRequest
{
    [Required]
    [MaxLength(500_000, ErrorMessage = "sourceCode must not exceed 500.000 characters.")]
    public required string SourceCode { get; init; }

    // Opcional: pode ser "<source_code>" quando o código é enviado diretamente pelo cliente MCP.
    [MaxLength(2_000, ErrorMessage = "filePath must not exceed 2.000 characters.")]
    public string? FilePath { get; init; }

    [Required]
    [MaxLength(200, ErrorMessage = "llmModel must not exceed 200 characters.")]
    public required string LlmModel { get; init; }

    [MaxLength(5_000, ErrorMessage = "additionalContext must not exceed 5.000 characters.")]
    public string? AdditionalContext { get; init; }

    public string[]? CustomRules { get; init; }

    public bool GenerateRefactoring { get; init; }
}
