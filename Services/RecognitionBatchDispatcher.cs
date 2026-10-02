using Hangfire;
using IntelligentDocAnalyzer.DbContext;
using IntelligentDocAnalyzer.Models;
using MongoDB.Driver;

namespace IntelligentDocAnalyzer.Services;

// Replaces the always-listening BackgroundService. This only runs when triggered
// (see HangfireSetup for the recurring safety net, and wire an on-submit trigger
// from wherever a job is created) instead of polling MongoDB in a tight loop.
//
// Each run claims up to BatchSize queued jobs and hands each one to Hangfire with
// a staggered delay, so the 30s spacing between Azure calls is enforced by
// Hangfire's own scheduler rather than a manual Task.Delay inside a long-running
// loop.
public class RecognitionBatchDispatcher
{
    private const int BatchSize = 10;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(45);
    private readonly MongoDbContext _dbContext;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<RecognitionBatchDispatcher> _logger;

    public RecognitionBatchDispatcher(MongoDbContext dbContext, IBackgroundJobClient backgroundJobClient,
        ILogger<RecognitionBatchDispatcher> logger)
    {
        _dbContext = dbContext;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public async Task DispatchNextBatchAsync(CancellationToken cancellationToken)
    {
        var claimed = await ClaimNextBatchAsync(cancellationToken);
        if (claimed.Count == 0)
            return;

        for (int i = 0; i < claimed.Count; i++)
        {
            var job = claimed[i];
            var delay = TimeSpan.FromSeconds(Interval.TotalSeconds * i);

            _backgroundJobClient.Schedule<RecognitionJobProcessor>(p => p.ProcessAsync(job.Id, CancellationToken.None), delay);
        }

        _logger.LogInformation(
            "Dispatched {Count} recognition job(s), spaced {IntervalSeconds}s apart.",
            claimed.Count, Interval.TotalSeconds);
    }

    // Atomically claims up to BatchSize still-Queued jobs, oldest first, so two
    // dispatch triggers firing around the same time can never grab the same job:
    // FindOneAndUpdate only matches a document while it's still Status == Queued,
    // and Mongo guarantees that check-and-set is atomic per document.
    private async Task<List<RecognitionJob>> ClaimNextBatchAsync(CancellationToken cancellationToken)
    {
        var filter = Builders<RecognitionJob>.Filter.Eq(x => x.Status, JobStatus.Queued);
        var sort = Builders<RecognitionJob>.Sort.Ascending(x => x.SubmittedAtUtc);
        var update = Builders<RecognitionJob>.Update.Set(x => x.Status, JobStatus.Scheduled);
        var options = new FindOneAndUpdateOptions<RecognitionJob>
        {
            Sort = sort,
            ReturnDocument = ReturnDocument.After
        };

        var claimed = new List<RecognitionJob>();

        for (int i = 0; i < BatchSize; i++)
        {
            var job = await _dbContext.RecognitionJobs
                .FindOneAndUpdateAsync(filter, update, options, cancellationToken);

            if (job is null)
                break;

            claimed.Add(job);
        }

        return claimed;
    }
}