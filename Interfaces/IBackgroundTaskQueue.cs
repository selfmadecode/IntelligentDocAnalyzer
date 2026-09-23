namespace IntelligentDocAnalyzer.Interfaces;

public interface IBackgroundTaskQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}
