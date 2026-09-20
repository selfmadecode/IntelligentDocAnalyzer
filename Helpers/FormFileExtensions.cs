namespace IntelligentDocAnalyzer.Helpers;

public static class FormFileExtensions
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".png",
            ".jpg",
            ".jpeg",
            ".tiff"
        };

    public static bool HasAllowedExtension(this IFormFile file)
    {
        if (file == null)
            return false;

        var extension = Path.GetExtension(file.FileName);

        return AllowedExtensions.Contains(extension);
    }
}
