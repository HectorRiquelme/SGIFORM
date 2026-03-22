using System.Text.Json;

namespace SanitasField.Web.Services;

public class ReleaseNotesService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ReleaseNotesService> _logger;

    public ReleaseNotesService(IWebHostEnvironment env, ILogger<ReleaseNotesService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<ReleaseNotesDocument> GetAsync()
    {
        var filePath = Path.Combine(_env.ContentRootPath, "release-notes.json");
        if (!File.Exists(filePath))
            return ReleaseNotesDocument.Empty;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var data = JsonSerializer.Deserialize<ReleaseNotesDocument>(json, JsonOptions);
            return data ?? ReleaseNotesDocument.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leyendo release-notes.json");
            return ReleaseNotesDocument.Empty;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

public record ReleaseNotesDocument(string CurrentVersion, List<ReleaseNoteItem> Notes)
{
    public static ReleaseNotesDocument Empty => new("0.0.0", new List<ReleaseNoteItem>());
}

public record ReleaseNoteItem(string Version, string Date, string Title, List<string> Changes);
