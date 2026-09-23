namespace IntelligentDocAnalyzer.Models;

public class RecognitionJobResponse
{
    public Guid Id { get; }
    public string Status { get; }
    public string FileName { get; }
    public DateTime SubmittedAtUtc { get; }
    public DateTime? CompletedAtUtc { get; }
    public string? ModelUsed { get; }
    //public List<Transaction>? Result { get; }
    public StatementAnalysisResult? Result { get; set; }
    public string? ErrorMessage { get; }

    public RecognitionJobResponse(RecognitionJob job)
    {
        Id = job.Id;
        Status = job.Status.ToString();
        FileName = job.FileName;
        SubmittedAtUtc = job.SubmittedAtUtc;
        CompletedAtUtc = job.CompletedAtUtc;
        ModelUsed = job.ModelUsed;
        Result = job.Status == JobStatus.Completed ? job.Result : null;
        ErrorMessage = job.ErrorMessage;
    }
}
