using System.Diagnostics;
using System.Text.Json;
using Wolpertinger.Presentation.Preferences;

namespace Wolpertinger.Presentation.App.Preferences;

public sealed class PresentationPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        RespectRequiredConstructorParameters = true
    };
    private readonly string path;
    private readonly SemaphoreSlim saveGate = new(1, 1);

    public static PresentationPreferences Default => PresentationPreferences.Default;

    public PresentationPreferencesStore(string? path = null)
    {
        this.path = Path.GetFullPath(path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WOLPERTINGER", "presentation.json"));
    }

    public async Task<PresentationPreferences> LoadAsync()
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous);
            var preferences = await JsonSerializer.DeserializeAsync<PresentationPreferences>(stream, JsonOptions);
            return preferences is not null && Enum.IsDefined(preferences.Dock) && Enum.IsDefined(preferences.Density)
                ? preferences : Default;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.TraceWarning("Presentation preferences unavailable; using defaults: {0}", error.Message);
            return Default;
        }
    }

    public async Task SaveAsync(PresentationPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        await saveGate.WaitAsync();
        var temporaryPath = path + ".tmp";
        var temporaryCreated = false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous))
            {
                temporaryCreated = true;
                await JsonSerializer.SerializeAsync(stream, preferences, JsonOptions);
                await stream.FlushAsync();
                stream.Flush(flushToDisk: true);
            }

            // Both paths share a directory/volume; never delete the destination before replacing it.
            if (File.Exists(path))
                File.Replace(temporaryPath, path, destinationBackupFileName: null);
            else
                File.Move(temporaryPath, path);
        }
        finally
        {
            if (temporaryCreated)
            {
                try { File.Delete(temporaryPath); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    Trace.TraceWarning("Could not remove temporary presentation preferences: {0}", error.Message);
                }
            }
            saveGate.Release();
        }
    }
}
