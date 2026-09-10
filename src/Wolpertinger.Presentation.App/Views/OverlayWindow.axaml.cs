using Avalonia.Controls;
using Wolpertinger.Presentation.Preferences;
using Wolpertinger.Presentation.Windows;

namespace Wolpertinger.Presentation.App.Views;

public partial class OverlayWindow : Window
{
    public WindowsSurfaceAttachment Attachment { get; }

    public OverlayWindow() : this(PresentationPreferences.Default) { }

    public OverlayWindow(PresentationPreferences preferences)
    {
        InitializeComponent();
        Attachment = WindowsOverlayAdapter.Attach(this, preferences.Dock, preferences.Density,
            preferences.PreferredDisplayKey, gameDisplayKey: null);
    }
}
