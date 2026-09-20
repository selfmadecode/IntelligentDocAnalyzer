namespace IntelligentDocAnalyzer.Helpers;

public static class StreamExtensions
{
    public static async Task<MemoryStream> ToMemoryStreamAsync(this IFormFile file, CancellationToken cancellationToken = default)
    {
        var ms = new MemoryStream();

        await file.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;

        return ms;
    }
}
