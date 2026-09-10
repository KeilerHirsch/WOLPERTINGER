using System.Text.Json;
using Wolpertinger.Presentation.App.Preferences;
using Wolpertinger.Presentation.Preferences;
using Wolpertinger.Presentation.ViewModels;

namespace Wolpertinger.Presentation.Tests.Preferences;

public sealed class PresentationPreferencesStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Wolpertinger-preferences", Guid.NewGuid().ToString("N"));
    private string PreferencesPath => Path.Combine(directory, "presentation.json");

    [Fact]
    public async Task MissingFileUsesExistingNeutralDefaults()
    {
        Assert.Equal(PresentationPreferences.Default, PresentationPreferencesStore.Default);
        Assert.Equal(PresentationPreferences.Default, await new PresentationPreferencesStore(PreferencesPath).LoadAsync());
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task CreatesDirectoryAndRoundTripsEveryPreferenceExactly()
    {
        var expected = new PresentationPreferences(false, true, DockPreset.Left, DensityPreset.Compact, "DISPLAY-2/opaque-ä");
        await new PresentationPreferencesStore(PreferencesPath).SaveAsync(expected);

        Assert.Equal(expected, await new PresentationPreferencesStore(PreferencesPath).LoadAsync());
        Assert.False(File.Exists(PreferencesPath + ".tmp"));
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(PreferencesPath));
        Assert.Equal(new[] { "Density", "Dock", "HubVisible", "OverlayVisible", "PreferredDisplayKey" },
            json.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray());
    }

    [Fact]
    public async Task ReplacesExistingFileAndRoundTripsNullDisplayKey()
    {
        var store = new PresentationPreferencesStore(PreferencesPath);
        await store.SaveAsync(new(true, true, DockPreset.Top, DensityPreset.Expanded, "DISPLAY-2"));
        var expected = new PresentationPreferences(false, false, DockPreset.Bottom, DensityPreset.Standard, null);

        await store.SaveAsync(expected);

        Assert.Equal(expected, await new PresentationPreferencesStore(PreferencesPath).LoadAsync());
        Assert.False(File.Exists(PreferencesPath + ".tmp"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"OverlayVisible\":false,\"HubVisible\":true,\"Dock\":1,\"Density\":1}")]
    [InlineData("{\"OverlayVisible\":true,\"HubVisible\":true,\"Dock\":0,\"Density\":1,\"PreferredDisplayKey\":null}")]
    [InlineData("{\"OverlayVisible\":true,\"HubVisible\":true,\"Dock\":255,\"Density\":1,\"PreferredDisplayKey\":null}")]
    [InlineData("{\"OverlayVisible\":false,\"HubVisible\":true,\"Dock\":1,\"Density\":0,\"PreferredDisplayKey\":null}")]
    [InlineData("{\"OverlayVisible\":false,\"HubVisible\":true,\"Dock\":1,\"Density\":255,\"PreferredDisplayKey\":null}")]
    [InlineData("{\"OverlayVisible\":false,\"HubVisible\":true,\"Dock\":1,\"Density\":1,\"PreferredDisplayKey\":42}")]
    public async Task InvalidFileFallsBackAsAWholeWithoutBlockingStartup(string content)
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(PreferencesPath, content);

        Assert.Equal(PresentationPreferencesStore.Default, await new PresentationPreferencesStore(PreferencesPath).LoadAsync());
        Assert.Equal(content, await File.ReadAllTextAsync(PreferencesPath));
    }

    [Fact]
    public async Task DirectoryInPlaceOfFileDoesNotBlockStartup()
    {
        Directory.CreateDirectory(PreferencesPath);
        Assert.Equal(PresentationPreferencesStore.Default, await new PresentationPreferencesStore(PreferencesPath).LoadAsync());
    }

    [Fact]
    public async Task LockedFileDoesNotBlockStartup()
    {
        Directory.CreateDirectory(directory);
        await using var locked = new FileStream(PreferencesPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(PresentationPreferencesStore.Default, await new PresentationPreferencesStore(PreferencesPath).LoadAsync());
    }

    [Fact]
    public async Task FailedReplacementPreservesPreviousPreferencesAndReportsFailure()
    {
        var store = new PresentationPreferencesStore(PreferencesPath);
        await store.SaveAsync(PresentationPreferencesStore.Default);
        await using (var locked = new FileStream(PreferencesPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => store.SaveAsync(new(false, true, DockPreset.Left, DensityPreset.Compact, "other")));
        }

        Assert.Equal(PresentationPreferencesStore.Default, await store.LoadAsync());
        Assert.False(File.Exists(PreferencesPath + ".tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
