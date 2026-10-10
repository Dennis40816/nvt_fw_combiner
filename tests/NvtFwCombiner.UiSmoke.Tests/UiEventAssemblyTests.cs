using System.Reflection;
using System.Reflection.Emit;
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

    /// <summary>A generated assembly with an async void method proves scanner sensitivity.</summary>
    [Fact]
    public void GeneratedAssemblyAsyncVoidFixtureIsDetected()
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("UiEventAssemblyFixture"), AssemblyBuilderAccess.Run);
        TypeBuilder type = assembly.DefineDynamicModule("UiEventAssemblyFixture")
            .DefineType("Fixture", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);
        MethodBuilder method = type.DefineMethod(
            "AsyncVoidFixture", MethodAttributes.Private | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
        method.GetILGenerator().Emit(OpCodes.Ret);
        method.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(AsyncStateMachineAttribute).GetConstructor([typeof(Type)])!, [typeof(object)]));
        _ = type.CreateType();

        Assert.Contains(FindAsyncVoidMethods(assembly), found => found.Name == "AsyncVoidFixture");
    }

    private static IEnumerable<MethodInfo> FindAsyncVoidMethods(Assembly assembly)
    {
        return assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.ReturnType == typeof(void)
                && method.IsDefined(typeof(AsyncStateMachineAttribute), inherit: false));
    }
}
