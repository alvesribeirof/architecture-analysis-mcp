using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/architecture")]
public sealed class ArchitectureAnalysisController(
    IArchitectureAnalysisService analysisService,
    ILogger<ArchitectureAnalysisController> logger) : ControllerBase
{
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(ArchitectureAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Analyze(
        [FromBody] ArchitectureAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SourceCode))
        {
            return BadRequest(new { message = "sourceCode is required." });
        }

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return BadRequest(new { message = "filePath is required." });
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
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Failed to analyze architecture.",
                detail = ex.Message
            });
        }
    }
}
