using IntelligentDocAnalyzer.Dto;
using IntelligentDocAnalyzer.Models;

namespace IntelligentDocAnalyzer.Interfaces;

public interface IRecognitionJobState
{
    Task<RecognitionJob> CreateState(FileDTO fileDTO, CancellationToken cancellationToken = default);
    Task<RecognitionJob> TryGet(Guid id);
}
