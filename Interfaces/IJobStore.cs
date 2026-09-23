using IntelligentDocAnalyzer.Models;

namespace IntelligentDocAnalyzer.Interfaces;

public interface IJobStore
{
    RecognitionJob Create(string fileName, BinaryData content);
    bool TryGet(Guid id, out RecognitionJob? job);
    void Update(RecognitionJob job);
}
