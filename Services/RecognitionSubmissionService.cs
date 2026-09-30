using Hangfire;
using IntelligentDocAnalyzer.DbContext;
using IntelligentDocAnalyzer.Models;

namespace IntelligentDocAnalyzer.Services;

// The single place a file becomes a queued RecognitionJob. Both the REST
// controller and the gRPC service should call this — that's what
// guarantees a file submitted over gRPC goes through Azure exactly the
// same way one submitted over REST does (same GridFS upload, same Mongo
// document, same Hangfire trigger).
//
// If your existing RecognitionController builds the job differently,
// point it at this method too so both entry points share one code path.
public class RecognitionSubmissionService
{
    private readonly MongoDbContext _dbContext;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public RecognitionSubmissionService(MongoDbContext dbContext, IBackgroundJobClient backgroundJobClient)
    {
        _dbContext = dbContext;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<RecognitionJob> SubmitAsync(string fileName, byte[] fileContent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("fileName is required.", nameof(fileName));
        if (fileContent is null || fileContent.Length == 0)
            throw new ArgumentException("fileContent is empty.", nameof(fileContent));

        var fileId = await _dbContext.GridFS.UploadFromBytesAsync(fileName, fileContent, cancellationToken: cancellationToken);

        var job = new RecognitionJob
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            Status = JobStatus.Queued,
            SubmittedAtUtc = DateTime.UtcNow,
            FileId = fileId
        };

        await _dbContext.RecognitionJobs.InsertOneAsync(job, cancellationToken: cancellationToken);

        // Trigger the batch dispatcher instead of waiting for the 1-minute
        // recurring safety net — see RecognitionBatchDispatcher.
        _backgroundJobClient.Enqueue<RecognitionBatchDispatcher>(d => d.DispatchNextBatchAsync(CancellationToken.None));

        return job;
    }
}

