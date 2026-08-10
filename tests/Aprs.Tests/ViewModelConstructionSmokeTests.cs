using System.Reflection;
using Aprs.Desktop.ViewModels;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Headless construction smoke test for the UI view models. It does not render pixels (that needs the
/// click-through, or a future Avalonia.Headless harness), but it catches the "throws on open because a
/// dependency was null / the constructor does I/O" class automatically — so those never reach a tester.
///
/// It reflects over every <c>public static … CreateDesignTime()</c> factory in the ViewModels assembly (the
/// factory the XAML previewer and the feature windows rely on to construct a VM), invokes each, and asserts
/// it returns non-null without throwing. Reflection-driven so a newly-added view model is covered
/// automatically, with no per-VM maintenance.
/// </summary>
public sealed class ViewModelConstructionSmokeTests
{
    // Any CreateDesignTime factory that genuinely requires a live Avalonia application context (Dispatcher /
    // Application.Current) rather than being purely constructible headless goes here, with a reason. Keep
    // this list empty unless a real dependency forces it — the whole point is to construct everything.
    private static readonly HashSet<string> RequiresAvaloniaContext = new();

    public static IEnumerable<object[]> DesignTimeFactories()
    {
        var assembly = typeof(MainWindowViewModel).Assembly;
        foreach (var type in assembly.GetTypes())
        {
            if (type.Namespace != "Aprs.Desktop.ViewModels") continue;
            var factory = type.GetMethod("CreateDesignTime",
                BindingFlags.Public | BindingFlags.Static, binder: null, types: Type.EmptyTypes, modifiers: null);
            if (factory is null) continue;
            if (RequiresAvaloniaContext.Contains(type.Name)) continue;
            yield return new object[] { type.Name };
        }
    }

    [Theory]
    [MemberData(nameof(DesignTimeFactories))]
    public void CreateDesignTime_ConstructsWithoutThrowing(string viewModelTypeName)
    {
        var assembly = typeof(MainWindowViewModel).Assembly;
        var type = assembly.GetType($"Aprs.Desktop.ViewModels.{viewModelTypeName}", throwOnError: true)!;
        var factory = type.GetMethod("CreateDesignTime",
            BindingFlags.Public | BindingFlags.Static, binder: null, types: Type.EmptyTypes, modifiers: null)!;

        object? vm;
        try
        {
            vm = factory.Invoke(null, null);
        }
        catch (TargetInvocationException ex)
        {
            throw new Xunit.Sdk.XunitException(
                $"{viewModelTypeName}.CreateDesignTime() threw {ex.InnerException?.GetType().Name}: {ex.InnerException?.Message}");
        }

        Assert.NotNull(vm);
    }

    /// <summary>
    /// Guards that the five previously-orphaned features specifically stay constructible (they are the newest
    /// windows and the most likely to regress a dependency). Belt-and-suspenders over the reflection sweep.
    /// </summary>
    [Theory]
    [InlineData(nameof(GeofenceEditorViewModel))]
    [InlineData(nameof(SimulationViewModel))]
    [InlineData(nameof(TrainingModeViewModel))]
    [InlineData(nameof(DirewolfProfileViewModel))]
    [InlineData(nameof(FileHooksViewModel))]
    public void FormerlyOrphanedFeature_HasDesignTimeFactory(string viewModelTypeName)
    {
        var type = typeof(MainWindowViewModel).Assembly
            .GetType($"Aprs.Desktop.ViewModels.{viewModelTypeName}", throwOnError: true)!;
        var factory = type.GetMethod("CreateDesignTime",
            BindingFlags.Public | BindingFlags.Static, binder: null, types: Type.EmptyTypes, modifiers: null);
        Assert.NotNull(factory); // if this fails, the reflection sweep above would silently skip this VM
    }
}
