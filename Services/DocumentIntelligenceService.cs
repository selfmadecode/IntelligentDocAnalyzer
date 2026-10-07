using Azure;
using Azure.AI.DocumentIntelligence;

namespace IntelligentDocAnalyzer.Services;

public class DocumentIntelligenceService
{
    private readonly DocumentIntelligenceClient _client;

    public DocumentIntelligenceService(IConfiguration configuration)
    {
        var endpoint = configuration["AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT"]
            ?? Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT");

        var key = configuration["AZURE_DOCUMENT_INTELLIGENCE_KEY"]
            ?? Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INTELLIGENCE_KEY");
       
        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException("Azure Document Intelligence endpoint and key must be configured in environment variables 'AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT' and 'AZURE_DOCUMENT_INTELLIGENCE_KEY'.");
        }

        var credential = new AzureKeyCredential(key);
        _client = new DocumentIntelligenceClient(new Uri(endpoint), credential);
    }

    public async Task<AnalyzeResult> AnalyzeDocumentAsync(string modelId, BinaryData content)
    {
        if (content == null)
            throw new ArgumentNullException(nameof(content));

        Operation<AnalyzeResult> operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, modelId, content);

        return operation.Value;
    }

    public async Task<AnalyzeResult> AnalyzeDocumentAsync(string modelId, Uri uriSource)
    {
        if (uriSource == null)
            throw new ArgumentNullException(nameof(uriSource));

        Operation<AnalyzeResult> operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, modelId, uriSource);
        
        return operation.Value;
    }

    public async Task<AnalyzeResult> AnalyzeDocumentAsync(string modelId, Stream content)
    {
        if (content == null)
            throw new ArgumentNullException(nameof(content));

        if (content.CanSeek)
            content.Position = 0;

        BinaryData data = await BinaryData.FromStreamAsync(content);
        return await AnalyzeDocumentAsync(modelId, data);
    }
}
