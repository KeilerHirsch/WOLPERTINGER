using System.Reflection;
using System.Runtime.InteropServices;
using Wolpertinger.Presentation.Contracts;
using Wolpertinger.Presentation.State;

namespace Wolpertinger.Presentation.Tests.Architecture;

public sealed class NeutralAssemblyBoundaryTests
{
    [Fact]
    public void PresentationAssemblyHasNoEdgeWindowsOrNativeDependency()
    {
        var assembly = typeof(PresentationStore).Assembly;
        var references = assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, x => x.Name == "Wolpertinger.Edge");
        Assert.DoesNotContain(references, x => x.Name == "Wolpertinger.Presentation.Windows");
        Assert.DoesNotContain(references, x => x.Name?.StartsWith("Avalonia", StringComparison.Ordinal) == true);

        var pinvokes = assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            .Where(m => m.GetCustomAttribute<DllImportAttribute>() is not null);
        Assert.Empty(pinvokes);
    }

    [Fact]
    public void ContractsAssemblyHasNoEdgeWindowsOrAvaloniaDependency()
    {
        var references = typeof(PresentationSnapshot).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, x => x.Name == "Wolpertinger.Edge");
        Assert.DoesNotContain(references, x => x.Name == "Wolpertinger.Presentation.Windows");
        Assert.DoesNotContain(references, x => x.Name?.StartsWith("Avalonia", StringComparison.Ordinal) == true);
    }
}
