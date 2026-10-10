using System.Reflection;
using System.Runtime.CompilerServices;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Scans the compiled UI assemblies for fire-and-forget async void methods.</summary>
public sealed class UiEventAssemblyTests
{
    /// <summary>Presentation contains no async void methods, including private methods.</summary>
    [Fact]
    public void PresentationAssemblyAsyncVoidMethodsContainsNone()
    {
        Assert.Empty(FindAsyncVoidMethods(Assembly.Load("NvtFwCombiner.Presentation.Avalonia")));
    }

    /// <summary>The distribution launcher contains no async void methods.</summary>
    [Fact]
    public void DistributionLauncherAssemblyAsyncVoidMethodsContainsNone()
    {
        Assert.Empty(FindAsyncVoidMethods(Assembly.Load("NvtFwCombiner.DistributionLauncher")));
    }

    /// <summary>An intentionally bad method in this test assembly proves scanner sensitivity.</summary>
    [Fact]
    public void TestAssemblyAsyncVoidFixtureIsDetected()
    {
        Assert.Contains(FindAsyncVoidMethods(typeof(UiEventAssemblyTests).Assembly),
            method => method.Name == nameof(AsyncVoidFixture));
    }

    private static IEnumerable<MethodInfo> FindAsyncVoidMethods(Assembly assembly)
    {
        return assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.ReturnType == typeof(void)
                && method.IsDefined(typeof(AsyncStateMachineAttribute), inherit: false));
    }

    private static async void AsyncVoidFixture()
    {
        await Task.Yield();
    }
}
