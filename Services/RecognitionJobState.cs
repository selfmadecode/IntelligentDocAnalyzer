using IntelligentDocAnalyzer.DbContext;
using IntelligentDocAnalyzer.Dto;
using IntelligentDocAnalyzer.Interfaces;
using IntelligentDocAnalyzer.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace IntelligentDocAnalyzer.Services;

public class RecognitionJobState : IRecognitionJobState
{
    private readonly MongoDbContext _mongo;
    public RecognitionJobState(MongoDbContext mongo)
    {
        _mongo = mongo;
    }

    public async Task<RecognitionJob> CreateState(FileDTO fileDTO, CancellationToken cancellationToken = default)
    {
        var job = CreateJob(fileDTO.File);

        job.FileId = await UploadFile(job, fileDTO.File, cancellationToken);
        await SaveJob(job,cancellationToken);

        return job;
    }

    public async Task<RecognitionJob> TryGet(Guid id)
    {
        return await _mongo.RecognitionJobs.Find(j => j.Id == id).FirstOrDefaultAsync();
    }

    private static RecognitionJob CreateJob(IFormFile file)
    {
        return new RecognitionJob
        {
            Id = Guid.NewGuid(),
            FileName = file.FileName,
            Status = JobStatus.Queued,
            SubmittedAtUtc = DateTime.UtcNow
        };
    }

    private async Task<ObjectId> UploadFile(RecognitionJob job, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();

        var options = new GridFSUploadOptions
        {
            Metadata = new BsonDocument
            {
                { "contentType", file.ContentType },
                { "fileName", file.FileName },
                { "jobId", job.Id.ToString() }
            }
        };

        return await _mongo.GridFS.UploadFromStreamAsync(
            file.FileName,
            stream,
            options,
            cancellationToken);
    }

    private async Task SaveJob(RecognitionJob job, CancellationToken cancellationToken)
    {
        await _mongo.RecognitionJobs.InsertOneAsync(job, cancellationToken: cancellationToken);
    }
}
