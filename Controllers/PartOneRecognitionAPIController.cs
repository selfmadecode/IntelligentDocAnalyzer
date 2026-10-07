using IntelligentDocAnalyzer.Dto;
using IntelligentDocAnalyzer.Models;
using IntelligentDocAnalyzer.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentDocAnalyzer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PartOneRecognitionAPIController : ControllerBase
{
    private readonly StatementProcessor _statementProcessor;
    private readonly FinancialAnalyzer _analyzer;

    public PartOneRecognitionAPIController(StatementProcessor statementProcessor, FinancialAnalyzer analyzer)
    {
        _statementProcessor = statementProcessor;
        _analyzer = analyzer;
    }

    [HttpPost]
    [RequestSizeLimit(25_000_000)]
    [HttpPost("analyze-file")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<StatementAnalysisResult>> Submit([FromForm] FileDTO dto, CancellationToken cancellationToken)
    {
        var file = dto.File;
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        using var stream = file.OpenReadStream();
        var binaryData = await BinaryData.FromStreamAsync(stream, cancellationToken);

        var (transactions, modelUsed) = await _statementProcessor.ProcessAsync(binaryData);
        var analysisResult = _analyzer.Analyze(transactions);


        return Ok(analysisResult);
    }
}
