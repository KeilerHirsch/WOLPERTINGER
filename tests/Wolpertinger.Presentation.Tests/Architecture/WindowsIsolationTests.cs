using System.Reflection;
using System.Runtime.InteropServices;
using Wolpertinger.Presentation.Contracts;
using Wolpertinger.Presentation.State;
using Wolpertinger.Presentation.Windows;

namespace Wolpertinger.Presentation.Tests.Architecture;

public sealed class WindowsIsolationTests
{
    [Theory]
    [InlineData(typeof(PresentationSnapshot))]
    [InlineData(typeof(PresentationStore))]
    public void NeutralAssembliesHaveNoNativeImportsOrPlatformDependencies(Type marker)
    {
        Assert.Empty(Imports(marker.Assembly));
        Assert.DoesNotContain(marker.Assembly.GetReferencedAssemblies(), reference =>
            reference.Name == "Wolpertinger.Presentation.Windows" ||
            reference.Name!.StartsWith("Avalonia", StringComparison.Ordinal) ||
            reference.Name.StartsWith("Microsoft.Win32", StringComparison.Ordinal));
    }

    [Fact]
    public void AllNativeDeclarationsAreInWindowsNativeMethods()
    {
        var imports = Imports(typeof(WindowsOverlayAdapter).Assembly).ToArray();
        Assert.NotEmpty(imports);
        Assert.All(imports, method => Assert.Equal(
            "Wolpertinger.Presentation.Windows.Interop.WindowsNativeMethods", method.DeclaringType!.FullName));
    }

    private static IEnumerable<MethodInfo> Imports(Assembly assembly) => assembly.GetTypes()
        .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        .Where(method => method.GetCustomAttribute<DllImportAttribute>() is not null);
}
