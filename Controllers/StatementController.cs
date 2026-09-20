using IntelligentDocAnalyzer.Helpers;
using IntelligentDocAnalyzer.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentDocAnalyzer.Controllers;

[ApiController]
[Route("[controller]")]
public class StatementController : ControllerBase
{
    private readonly StatementProcessor _processor;
    private readonly FinancialAnalyzer _analyzer;

    public StatementController(StatementProcessor processor, FinancialAnalyzer analyzer)
    {
        _processor = processor;
        _analyzer = analyzer;
    }

    [HttpPost("analyze-file")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AnalyzeFile(IFormFile? file)
    {
        if (file == null)
        {
            return BadRequest(new { error = "No file uploaded." });
        }

        if (!file.HasAllowedExtension())
        {
            return BadRequest(new { error = "Unsupported file type." });
        }

        try
        {
            var (transactions, modelUsed) = await _processor.ProcessAsync(file);

            var analysis = _analyzer.Analyze(transactions);
            analysis.RawModelUsed = modelUsed;

            return Ok(analysis);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new { error = "Azure credentials are invalid or not configured." });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Document processing failed.", detail = ex.Message });
        }
    }


    // POST /Statement/analyze-url
    [HttpPost("analyze-url")]
    [Consumes("application/json")]
    public async Task<IActionResult> AnalyzeUrl([FromBody] AnalyzeUrlRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileUrl))
        {
            return BadRequest(new { error = "fileUrl is required." });
        }

        if (!Uri.TryCreate(request.FileUrl, UriKind.Absolute, out var uri))
        {
            return BadRequest(new { error = "Invalid fileUrl provided." });
        }

        try
        {
            var (transactions, modelUsed) = await _processor.ProcessFromUrlAsync(uri);

            var analysis = _analyzer.Analyze(transactions);
            analysis.RawModelUsed = modelUsed;

            return Ok(analysis);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode( StatusCodes.Status401Unauthorized, new { error = "Azure credentials are invalid or not configured." });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,  new { error = "Document processing failed.", detail = ex.Message });
        }
    }
}


public class AnalyzeUrlRequest
{
    public string FileUrl { get; set; } = string.Empty;
}
