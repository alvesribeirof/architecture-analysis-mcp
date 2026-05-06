using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

[ApiController]
[Route("api/architecture")]
[EnableRateLimiting("analysis")]
public sealed class ArchitectureAnalysisController(
    IArchitectureAnalysisService analysisService,
    ILogger<ArchitectureAnalysisController> logger,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(ArchitectureAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Analyze(
        [FromBody] ArchitectureAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SourceCode))
        {
            return BadRequest(new { message = "sourceCode is required." });
        }

        if (string.IsNullOrWhiteSpace(request.LlmModel))
        {
            return BadRequest(new { message = "llmModel is required." });
        }

        try
        {
            var response = await analysisService.AnalyzeAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Architecture analysis failed for file {FilePath}", request.FilePath);

            // Em produção, não expõe detalhes internos da exceção ao cliente.
            var detail = environment.IsDevelopment() ? ex.Message : "An internal error occurred.";
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Failed to analyze architecture.",
                detail
            });
        }
    }
}
