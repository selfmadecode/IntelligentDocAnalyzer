using Grpc.Core;
using IntelligentDocAnalyzer.DbContext;
using IntelligentDocAnalyzer.Grpc;
using MongoDB.Driver;

namespace IntelligentDocAnalyzer.Services;

public class RecognitionGrpcService : Recognition.RecognitionBase
{
    private readonly RecognitionSubmissionService _submissionService;
    private readonly StatementProcessor _processor;
    private readonly FinancialAnalyzer _analyzer;
    private readonly MongoDbContext _dbContext;

    public RecognitionGrpcService(RecognitionSubmissionService submissionService, StatementProcessor processor, FinancialAnalyzer analyzer, MongoDbContext dbContext)
    {
        _submissionService = submissionService;
        _processor = processor;
        _analyzer = analyzer;
        _dbContext = dbContext;
    }

    public override async Task<SubmitFileResponse> SubmitFile(SubmitFileRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "file_name and file_content are required."));
        }

        var job = await _submissionService.SubmitAsync(
            request.FileName,
            request.FileContent.ToByteArray(),
            context.CancellationToken);

        return new SubmitFileResponse
        {
            JobId = job.Id.ToString(),
            Status = job.Status.ToString()
        };
    }

    public override async Task<GetResultResponse> GetResult(GetResultRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.JobId, out var jobId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "job_id is not a valid GUID."));
        }

        var job = await _dbContext.RecognitionJobs
            .Find(x => x.Id == jobId)
            .FirstOrDefaultAsync(context.CancellationToken);

        if (job is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Job {request.JobId} was not found."));
        }

        var response = new GetResultResponse
        {
            JobId = job.Id.ToString(),
            Status = job.Status.ToString(),
            FileName = job.FileName,
            ModelUsed = job.ModelUsed ?? string.Empty,
            ErrorMessage = job.ErrorMessage ?? string.Empty
        };

        if (job.Status == JobStatus.Completed && job.Result != null)
        {
            response.ResultJson = System.Text.Json.JsonSerializer.Serialize(job.Result);
        }

        return response;
    }

    public override async Task<AnalyzeUrlResponse> AnalyzeUrl(Grpc.AnalyzeUrlRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.FileUrl))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "fileUrl is required."));
        }

        if (!Uri.TryCreate(request.FileUrl, UriKind.Absolute, out var uri))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid fileUrl provided."));
        }

        try
        {
            var (transactions, modelUsed) = await _processor.ProcessFromUrlAsync(uri);

            var analysis = _analyzer.Analyze(transactions);
            analysis.RawModelUsed = modelUsed;

            return new AnalyzeUrlResponse
            {
                ResultJson = System.Text.Json.JsonSerializer.Serialize(analysis)
            };
        }
        catch (InvalidOperationException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Azure credentials are invalid or not configured."));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"Document processing failed: {ex.Message}"));
        }
    }
}
