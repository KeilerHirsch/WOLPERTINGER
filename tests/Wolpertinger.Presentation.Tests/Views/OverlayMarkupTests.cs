using System.Xml.Linq;

namespace Wolpertinger.Presentation.Tests.Views;

public sealed class OverlayMarkupTests
{
    [Fact]
    public void PassiveOverlayKeepsAThinGlanceableBody()
    {
        var root = RepoRoot();
        var path = Path.Combine(root, "src", "Wolpertinger.Presentation.App", "Views", "OverlayWindow.axaml");
        var document = XDocument.Load(path);
        XNamespace a = "https://github.com/avaloniaui";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var body = document.Descendants(a + "StackPanel")
            .Single(element => (string?)element.Attribute(x + "Name") == "Body");

        Assert.InRange(body.Elements().Count(), 1, 4);
        Assert.Empty(document.Descendants(a + "Button"));
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "WOLPERTINGER.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("WOLPERTINGER repository root not found.");
    }
}
