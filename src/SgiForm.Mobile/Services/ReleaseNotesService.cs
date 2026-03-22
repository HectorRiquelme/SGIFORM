using System.Text.Json;

namespace SgiForm.Mobile.Services;

public class ReleaseNotesService
{
    public async Task<ReleaseNotesDocument> GetAsync()
    {
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync("release-notes.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var data = JsonSerializer.Deserialize<ReleaseNotesDocument>(json, JsonOptions);
            return data ?? ReleaseNotesDocument.Empty;
        }
        catch
        {
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
