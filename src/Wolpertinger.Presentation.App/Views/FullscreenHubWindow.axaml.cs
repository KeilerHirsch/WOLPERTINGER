using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Wolpertinger.Presentation.Preferences;
using Wolpertinger.Presentation.Windows;

namespace Wolpertinger.Presentation.App.Views;

public partial class FullscreenHubWindow : Window
{
    public WindowsSurfaceAttachment Attachment { get; }

    public FullscreenHubWindow() : this(PresentationPreferences.Default) { }

    public FullscreenHubWindow(PresentationPreferences preferences)
    {
        InitializeComponent();
        Attachment = WindowsHubAdapter.Attach(this, preferences.PreferredDisplayKey, gameDisplayKey: null);
    }

    private void OnCloseHub(object? sender, RoutedEventArgs args) => Close();

    protected override void OnKeyDown(KeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (args.Key == Key.Escape)
        {
            Close();
            args.Handled = true;
        }
    }
}
