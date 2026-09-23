namespace IntelligentDocAnalyzer.Models;

public class RecognitionJob
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public DateTime SubmittedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? ModelUsed { get; set; }
    public List<Transaction>? Result { get; set; }
    public string? ErrorMessage { get; set; }

    // Held only until the worker processes the job, then cleared so completed
    // jobs don't keep the original file bytes sitting in memory.
    public BinaryData? Content { get; set; }
}
