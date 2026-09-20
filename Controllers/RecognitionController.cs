using IntelligentDocAnalyzer.Interfaces;
using IntelligentDocAnalyzer.Models;
using IntelligentDocAnalyzer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace IntelligentDocAnalyzer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RecognitionController : ControllerBase
{
    private readonly IJobStore _jobStore;
    private readonly IBackgroundTaskQueue _queue;
    private readonly StatementProcessor _processor;
    private readonly FinancialAnalyzer _analyzer;

    public RecognitionController(IJobStore jobStore, IBackgroundTaskQueue queue, StatementProcessor processor, FinancialAnalyzer analyzer)
    {
        _jobStore = jobStore;
        _queue = queue;
        _processor = processor;
        _analyzer = analyzer;
    }

    // POST api/recognition
    // multipart/form-data with a single file field named "file". A client submitting
    // a set of images sends one request per image — each call returns its own job id
    // ("sent through different packages" in the assignment's wording).
    [HttpPost]
    [RequestSizeLimit(25_000_000)]
    [HttpPost("analyze-file")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<RecognitionJobResponse>> Submit(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        await using var stream = file.OpenReadStream();
        var content = await BinaryData.FromStreamAsync(stream, cancellationToken);

        var job = _jobStore.Create(file.FileName, content);
        await _queue.EnqueueAsync(job.Id, cancellationToken);

        var response = new RecognitionJobResponse(job);
        return AcceptedAtAction(nameof(GetResult), new { id = job.Id }, response);
    }

    // GET api/recognition/{id}
    // Replies with the current state, and the parsed result once it's ready.
    [HttpGet("{id:guid}")]
    public ActionResult<RecognitionJobResponse> GetResult(Guid id)
    {
        if (!_jobStore.TryGet(id, out var job) || job == null)
        {
            return NotFound();
        }

        return Ok(new RecognitionJobResponse(job));
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
            return StatusCode(StatusCodes.Status401Unauthorized, new { error = "Azure credentials are invalid or not configured." });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Document processing failed.", detail = ex.Message });
        }
    }
}
