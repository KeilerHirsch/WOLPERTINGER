using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Wolpertinger.Presentation.App.Preferences;
using Wolpertinger.Presentation.App.Views;
using Wolpertinger.Presentation.Preferences;
using Wolpertinger.Presentation.State;
using Wolpertinger.Presentation.ViewModels;

namespace Wolpertinger.Presentation.App;

/// <summary>UI-thread lifecycle owner. Only bounded presentation preferences are persisted.</summary>
public sealed class PresentationApplicationController : IDisposable
{
    private readonly IClassicDesktopStyleApplicationLifetime desktop;
    private readonly PresentationStore store;
    private readonly PresentationPreferencesStore preferencesStore;
    private PresentationPreferences preferences = PresentationPreferences.Default;
    private OverlayWindow? overlay;
    private FullscreenHubWindow? hub;
    private DiagnosticsWindow? diagnostics;
    private Task? startup;
    private Task pendingSave = Task.CompletedTask;
    private bool exiting;
    private bool disposed;
    private string? preferenceDiagnostic;

    public PresentationApplicationController(IClassicDesktopStyleApplicationLifetime desktop,
        PresentationStore? store = null, PresentationPreferencesStore? preferencesStore = null)
    {
        this.desktop = desktop;
        this.store = store ?? new PresentationStore();
        this.preferencesStore = preferencesStore ?? new PresentationPreferencesStore();
        this.store.StateChanged += OnStateChanged;
    }

    public Task StartAsync() => startup ??= InitializeAsync();

    private async Task InitializeAsync()
    {
        preferences = await preferencesStore.LoadAsync();
        if (disposed)
            return;

        overlay = new OverlayWindow(preferences);
        hub = new FullscreenHubWindow(preferences);
        diagnostics = new DiagnosticsWindow();
        overlay.Closing += OnSurfaceClosing;
        hub.Closing += OnSurfaceClosing;
        diagnostics.Closing += OnSurfaceClosing;
        RefreshSurfaces();
        if (preferences.OverlayVisible)
            overlay.Show();
        if (preferences.HubVisible)
            hub.Show();
        RefreshSurfaceDiagnostics();
    }

    public async Task ToggleOverlayAsync()
    {
        await StartAsync();
        if (exiting || disposed)
            return;
        if (overlay!.IsVisible)
            overlay.Hide();
        else if (overlay.Attachment.LastDiagnostic is null || overlay.Attachment.TryRecover())
            overlay.Show();
        preferences = preferences with { OverlayVisible = overlay.IsVisible };
        RefreshSurfaceDiagnostics();
        await QueuePreferenceSave();
    }

    public async Task ShowHubAsync()
    {
        await StartAsync();
        if (exiting || disposed)
            return;
        // A hidden failed adapter stays closed until a topology refresh clears its diagnostic.
        if (hub!.Attachment.LastDiagnostic is null || hub.Attachment.TryRecover())
            hub.Show();
        if (hub.IsVisible)
            hub.Activate();
        preferences = preferences with { HubVisible = hub.IsVisible };
        RefreshSurfaceDiagnostics();
        await QueuePreferenceSave();
    }

    public async Task ShowDiagnosticsAsync()
    {
        await StartAsync();
        if (exiting || disposed)
            return;
        RefreshSurfaceDiagnostics();
        diagnostics!.Show();
        diagnostics.Activate();
    }

    public async Task ExitAsync()
    {
        await StartAsync();
        if (exiting || disposed)
            return;
        exiting = true;
        // Keep the chosen visibility for restart; closing windows must not persist false visibility.
        await QueuePreferenceSave();
        Dispose();
        desktop.Shutdown();
    }

    private async void OnSurfaceClosing(object? sender, WindowClosingEventArgs args)
    {
        if (exiting || disposed)
            return;
        args.Cancel = true;
        ((Window)sender!).Hide();
        if (ReferenceEquals(sender, overlay))
            preferences = preferences with { OverlayVisible = false };
        else if (ReferenceEquals(sender, hub))
            preferences = preferences with { HubVisible = false };
        else
            return;
        await QueuePreferenceSave();
    }

    private Task QueuePreferenceSave()
        => pendingSave = SavePreferencesAsync(pendingSave, preferences);

    private async Task SavePreferencesAsync(Task previous, PresentationPreferences value)
    {
        await previous;
        try
        {
            await preferencesStore.SaveAsync(value);
            preferenceDiagnostic = null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            preferenceDiagnostic = "Presentation preferences could not be saved.";
            Trace.TraceWarning("{0} {1}", preferenceDiagnostic, error.Message);
        }
        RefreshSurfaceDiagnostics();
    }

    private void OnStateChanged(object? sender, EventArgs args)
    {
        if (Dispatcher.UIThread.CheckAccess())
            RefreshSurfaces();
        else
            Dispatcher.UIThread.Post(RefreshSurfaces);
    }

    private void RefreshSurfaces()
    {
        if (disposed || overlay is null || hub is null || diagnostics is null)
            return;
        var state = store.State;
        var hasJump = state.Snapshot?.Jump is not null;
        overlay.DataContext = hasJump ? PresentationViewModelFactory.CreateJump(state, preferences.Density) : null;
        hub.DataContext = hasJump ? PresentationViewModelFactory.CreateJump(state, DensityPreset.Expanded) : null;
        diagnostics.DataContext = hasJump ? PresentationViewModelFactory.CreateDiagnostics(state) : null;
        foreach (var window in new Window[] { overlay, hub, diagnostics })
        {
            window.FindControl<StackPanel>("Body")!.IsVisible = hasJump;
            var emptyState = window.FindControl<TextBlock>("EmptyState")!;
            emptyState.IsVisible = !hasJump;
            emptyState.Text = $"{state.Connection} — no validated jump available";
        }
    }

    private void RefreshSurfaceDiagnostics()
    {
        if (disposed || diagnostics is null)
            return;
        diagnostics.FindControl<TextBlock>("SurfaceStatus")!.Text = string.Join(Environment.NewLine,
            new[] { preferenceDiagnostic, overlay?.Attachment.LastDiagnostic, hub?.Attachment.LastDiagnostic }
                .Where(message => message is not null));
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        store.StateChanged -= OnStateChanged;
        overlay?.Attachment.Dispose();
        hub?.Attachment.Dispose();
        foreach (var window in new Window?[] { overlay, hub, diagnostics })
        {
            if (window is null)
                continue;
            window.Closing -= OnSurfaceClosing;
            window.Close();
        }
    }
}
