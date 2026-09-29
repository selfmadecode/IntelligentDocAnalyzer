using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace IntelligentDocAnalyzer.Models;

public class RecognitionJob
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public DateTime SubmittedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? ModelUsed { get; set; }
    public StatementAnalysisResult Result { get; set; } = new StatementAnalysisResult();
    public string? ErrorMessage { get; set; }
    public ObjectId? FileId { get; set; }

}
