using IntelligentDocAnalyzer.DbContext;
using IntelligentDocAnalyzer.Models;
using MongoDB.Driver;

namespace IntelligentDocAnalyzer.Services;

public class RecognitionJobProcessor
{
    private readonly StatementProcessor _statementProcessor;
    private readonly FinancialAnalyzer _analyzer;
    private readonly MongoDbContext _dbContext;
    private readonly ILogger<RecognitionJobProcessor> _logger;

    public RecognitionJobProcessor(
        StatementProcessor statementProcessor,
        FinancialAnalyzer analyzer,
        MongoDbContext dbContext,
        ILogger<RecognitionJobProcessor> logger)
    {
        _statementProcessor = statementProcessor;
        _analyzer = analyzer;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _dbContext.RecognitionJobs
            .Find(x => x.Id == jobId)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            _logger.LogWarning("Job {JobId} no longer exists; skipping.", jobId);
            return;
        }

        await MarkAsProcessingAsync(job, cancellationToken);

        try
        {
            var content = await DownloadFileAsync(job, cancellationToken);

            var (transactions, modelUsed) = await _statementProcessor.ProcessAsync(content);

            var analysisResult = _analyzer.Analyze(transactions);

            await CompleteJobAsync(job, analysisResult, modelUsed, cancellationToken);
        }
        catch (Exception ex)
        {
            await FailJobAsync(job, ex, cancellationToken);
        }
    }

    private async Task<BinaryData> DownloadFileAsync(RecognitionJob job, CancellationToken cancellationToken)
    {
        if (job.FileId is null)
        {
            throw new InvalidOperationException($"Job {job.Id} does not have a FileId.");
        }

        await using var stream = await _dbContext.GridFS.OpenDownloadStreamAsync(job.FileId.Value, cancellationToken: cancellationToken);
        return await BinaryData.FromStreamAsync(stream, cancellationToken);
    }

    private async Task MarkAsProcessingAsync(RecognitionJob job, CancellationToken cancellationToken)
    {
        job.Status = JobStatus.Processing;

        var update = Builders<RecognitionJob>.Update.Set(x => x.Status, JobStatus.Processing);
        await _dbContext.RecognitionJobs.UpdateOneAsync(x => x.Id == job.Id, update, cancellationToken: cancellationToken);
    }

    private async Task CompleteJobAsync(RecognitionJob job, StatementAnalysisResult result, string modelUsed, CancellationToken cancellationToken)
    {
        job.Status = JobStatus.Completed;
        job.Result = result;
        job.ModelUsed = modelUsed;
        job.CompletedAtUtc = DateTime.UtcNow;

        var update = Builders<RecognitionJob>.Update
            .Set(x => x.Status, job.Status)
            .Set(x => x.Result, job.Result)
            .Set(x => x.ModelUsed, job.ModelUsed)
            .Set(x => x.CompletedAtUtc, job.CompletedAtUtc);

        await _dbContext.RecognitionJobs.UpdateOneAsync(x => x.Id == job.Id, update, cancellationToken: cancellationToken);
    }

    private async Task FailJobAsync(RecognitionJob job, Exception exception, CancellationToken cancellationToken)
    {
        job.Status = JobStatus.Failed;
        job.ErrorMessage = exception.Message;
        job.CompletedAtUtc = DateTime.UtcNow;

        _logger.LogError(exception, "Recognition failed for job {JobId}", job.Id);

        var update = Builders<RecognitionJob>.Update
            .Set(x => x.Status, job.Status)
            .Set(x => x.ErrorMessage, job.ErrorMessage)
            .Set(x => x.CompletedAtUtc, job.CompletedAtUtc);

        await _dbContext.RecognitionJobs.UpdateOneAsync(x => x.Id == job.Id, update, cancellationToken: cancellationToken);
    }
}
