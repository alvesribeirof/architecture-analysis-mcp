using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Backend.Models;

namespace Backend.Services;

public sealed class OpenRouterArchitectureAnalysisService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<OpenRouterArchitectureAnalysisService> logger) : IArchitectureAnalysisService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private const string OpenRouterEndpoint = "https://openrouter.ai/api/v1/chat/completions";

    public async Task<ArchitectureAnalysisResponse> AnalyzeAsync(
        ArchitectureAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["OpenRouter:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenRouter API key is not configured. Set OpenRouter:ApiKey.");
        }

        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(request);

        var schemaProperties = new Dictionary<string, object>
        {
            ["analysis"] = new { type = "string" },
            ["violations"] = new { type = "array", items = new { type = "string" } },
            ["suggestions"] = new { type = "array", items = new { type = "string" } },
            ["patterns"] = new { type = "array", items = new { type = "string" } },
            ["confidence"] = new { type = "number" }
        };

        var requiredFields = new List<string> { "analysis", "violations", "suggestions", "patterns", "confidence" };

        if (request.GenerateRefactoring)
        {
            schemaProperties["refactoredCode"] = new { type = "string" };
            schemaProperties["architectureDiagram"] = new { type = "string" };
            requiredFields.Add("refactoredCode");
            requiredFields.Add("architectureDiagram");
        }

        var openRouterPayload = new
        {
            model = request.LlmModel,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.2,
            max_tokens = 2500, // Increased tokens for code/diagrams
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "architecture_analysis",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        additionalProperties = false,
                        properties = schemaProperties,
                        required = requiredFields
                    }
                }
            }
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, OpenRouterEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(openRouterPayload), Encoding.UTF8, "application/json")
        };

        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Headers.Add("HTTP-Referer", configuration["OpenRouter:Referer"] ?? "http://localhost");
        message.Headers.Add("X-Title", configuration["OpenRouter:Title"] ?? "Architecture Analysis MCP Backend");

        using var response = await httpClient.SendAsync(message, cancellationToken);

        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("OpenRouter request failed ({StatusCode}): {Body}", response.StatusCode, rawResponse);
            throw new HttpRequestException($"OpenRouter request failed with {(int)response.StatusCode}: {rawResponse}");
        }

        var completion = JsonSerializer.Deserialize<OpenRouterCompletionResponse>(rawResponse, JsonOptions)
            ?? throw new InvalidOperationException("OpenRouter returned an empty response.");

        var content = completion.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("OpenRouter returned no content in completion.");
        }

        var parsed = JsonSerializer.Deserialize<ArchitectureAnalysisResponse>(content, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse architecture analysis content.");

        return parsed.WithMetadata(request.LlmModel);
    }

    private static string BuildSystemPrompt()
    {
        return """
Você é um Arquiteto de Software Sênior especializado em qualidade arquitetural.
Analise o código focando em:
1) Violação de princípios SOLID
2) Cheiros de projeto (acoplamento alto, baixa coesão, classes utilitárias inchadas)
3) Sugestões de Design Patterns aplicáveis com justificativa objetiva
4) Feedback acionável e imediato para o desenvolvedor
5) Geração de Diagramas Mermaid (se solicitado)
6) Refatoração do Código (se solicitado)

Responda EXCLUSIVAMENTE no formato JSON válido.
Se for solicitado para gerar refatoração, preencha os campos `refactoredCode` com o novo código e `architectureDiagram` com o diagrama Mermaid da nova arquitetura usando sintaxe ```mermaid ... ```.

Regras:
- Não inclua markdown fora das strings do JSON.
- O JSON deve ser validado estritamente.
- Confidence deve variar entre 0 e 1.
""";
    }

    private static string BuildUserPrompt(ArchitectureAnalysisRequest request)
    {
        var rulesText = request.CustomRules != null && request.CustomRules.Any() 
            ? $"\nRegras customizadas do time (MANDATÓRIO SEGUIR):\n- {string.Join("\n- ", request.CustomRules)}" 
            : "";

        var refactoringText = request.GenerateRefactoring 
            ? "\n[IMPORTANTE] Gere a refatoração do código no campo 'refactoredCode' e um diagrama de classes Mermaid refletindo a refatoração no campo 'architectureDiagram'."
            : "";

        return $"""
Arquivo: {request.FilePath}
Modelo solicitado: {request.LlmModel}
Contexto adicional: {request.AdditionalContext ?? "(não informado)"}{rulesText}{refactoringText}

Código-fonte:
{request.SourceCode}
""";
    }

    private sealed class OpenRouterCompletionResponse
    {
        public List<Choice>? Choices { get; init; }

        public sealed class Choice
        {
            public Message? Message { get; init; }
        }

        public sealed class Message
        {
            public string? Content { get; init; }
        }
    }
}

internal static class ArchitectureAnalysisResponseExtensions
{
    public static ArchitectureAnalysisResponse WithMetadata(this ArchitectureAnalysisResponse input, string model)
    {
        var metadata = input.Metadata ?? new Dictionary<string, object>();
        metadata["provider"] = "OpenRouter";
        metadata["model"] = model;
        metadata["generatedAtUtc"] = DateTime.UtcNow.ToString("O");

        return new ArchitectureAnalysisResponse
        {
            Analysis = input.Analysis,
            Violations = input.Violations,
            Suggestions = input.Suggestions,
            Patterns = input.Patterns,
            Confidence = input.Confidence,
            RefactoredCode = input.RefactoredCode,
            ArchitectureDiagram = input.ArchitectureDiagram,
            Metadata = metadata
        };
    }
}
