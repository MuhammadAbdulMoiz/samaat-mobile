using System.Xml.Linq;

namespace Samaat.Mobile.Tests;

/// <summary>
/// The phone app's layering, read from each project's references, in the style of the desktop's ArchitectureTests.
/// The shared projects (external/samaat.io) are checked there; this file covers the four phone projects and how they
/// may use the shared ones. A new project or reference has to be added to <see cref="Allowed"/> on purpose.
/// </summary>
public sealed class ArchitectureTests
{
    private const string Core = "Samaat.Core";
    private const string Transport = "Samaat.Transport";
    private const string Handoff = "Samaat.Handoff";
    private const string Ui = "Samaat.UI";
    private const string Simulator = "Samaat.Simulator";
    private const string Drivers = "Samaat.Drivers";
    private const string Standard = "Samaat.Protocols.Standard";

    private const string Mobile = "Samaat.Mobile";
    private const string Preview = "Samaat.Mobile.Preview";
    private const string PlatformAndroid = "Samaat.Platform.Android";
    private const string Android = "Samaat.Android";

    /// <summary>Which projects each phone project may reference. Anything not listed is a layering violation.</summary>
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        // The phone UI and logic: shared projects only, never a vendor protocol, a driver list or a platform.
        // The simulator is a shared feature module (the desktop app references it too) and backs the tests and preview.
        [Mobile] = [Core, Ui, Handoff, Simulator],

        // Android's implementations of Core, Transport and Handoff contracts, plus the standard Battery Service.
        [PlatformAndroid] = [Core, Transport, Handoff, Standard],

        // Composition roots: the only places that pick drivers and a platform.
        [Android] = [Mobile, PlatformAndroid, Drivers],
        [Preview] = [Mobile],
    };

    /// <summary>The only projects that build an executable and wire the app together.</summary>
    private static readonly HashSet<string> CompositionRoots = [Android, Preview];

    private static readonly DirectoryInfo Root = FindRoot();

    private static DirectoryInfo FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "Samaat.Mobile.slnx")))
        {
            dir = dir.Parent ?? throw new InvalidOperationException("Samaat.Mobile.slnx not found above the test output");
        }

        return dir;
    }

    private static string SharedSrc => Path.Combine(Root.FullName, "external", "samaat.io", "src");

    private static IEnumerable<string> PhoneProjects() =>
        Directory.EnumerateFiles(Path.Combine(Root.FullName, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(p => Path.GetFileNameWithoutExtension(p))
            .Order(StringComparer.Ordinal);

    private static string PathOf(string project)
    {
        var phone = Path.Combine(Root.FullName, "src", project, $"{project}.csproj");
        return File.Exists(phone) ? phone : Path.Combine(SharedSrc, project, $"{project}.csproj");
    }

    private static XDocument Load(string project) => XDocument.Load(PathOf(project));

    private static IReadOnlyList<string> ProjectReferences(string project) =>
        Load(project).Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension(r.Attribute("Include")!.Value.Replace('\\', '/')))
            .ToList();

    private static bool IsAndroidOnly(string project) =>
        Load(project).Descendants("TargetFramework").Any(t => t.Value.Contains("-android", StringComparison.Ordinal));

    private static bool IsExecutable(string project) =>
        Load(project).Descendants("OutputType").Any(t => t.Value is "Exe" or "WinExe");

    /// <summary>Every project a phone project builds, directly or through another project, shared ones included.</summary>
    private static IReadOnlySet<string> Closure(IEnumerable<string> start)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(start);
        while (queue.TryDequeue(out var project))
        {
            if (seen.Add(project))
            {
                foreach (var reference in ProjectReferences(project))
                {
                    queue.Enqueue(reference);
                }
            }
        }

        return seen;
    }

    public static TheoryData<string> Projects()
    {
        var data = new TheoryData<string>();
        foreach (var project in PhoneProjects())
        {
            data.Add(project);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void Project_HasALayeringRule(string project)
    {
        Assert.True(Allowed.ContainsKey(project),
            $"{project} has no entry in ArchitectureTests.Allowed. Decide which layers it may depend on and add it there.");
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void Project_ReferencesOnlyWhatItsLayerAllows(string project)
    {
        Assert.True(Allowed.TryGetValue(project, out var allowed), project);

        var violations = ProjectReferences(project).Except(allowed).ToList();

        Assert.True(violations.Count == 0, $"{project} must not reference {string.Join(", ", violations)}");
    }

    [Fact]
    public void ThePhoneUi_ReferencesOnlySharedProjects()
    {
        var shared = Directory.EnumerateFiles(SharedSrc, "*.csproj", SearchOption.AllDirectories)
            .Select(p => Path.GetFileNameWithoutExtension(p)).ToHashSet(StringComparer.Ordinal);

        var notShared = ProjectReferences(Mobile).Where(r => !shared.Contains(r)).ToList();

        Assert.Empty(notShared);
        Assert.False(IsAndroidOnly(Mobile), "Samaat.Mobile must stay platform-neutral so it renders headless");
    }

    [Fact]
    public void OnlyTheCompositionRoots_AreExecutables_OrPickDriversAndPlatforms()
    {
        foreach (var project in PhoneProjects())
        {
            var isRoot = CompositionRoots.Contains(project);
            Assert.True(isRoot || !IsExecutable(project), $"{project} builds an executable but isn't a composition root");

            var references = ProjectReferences(project);
            Assert.True(isRoot || !references.Contains(Drivers), $"{project} picks drivers; only a composition root may");
            Assert.True(project == Android || !references.Contains(PlatformAndroid), $"{project} references the Android platform layer");
            Assert.DoesNotContain(references, r => CompositionRoots.Contains(r));
        }
    }

    [Fact]
    public void ThePlatformLayer_ImplementsContracts_AndDependsOnNoPhoneProject()
    {
        var phone = PhoneProjects().ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(ProjectReferences(PlatformAndroid), phone.Contains);
        Assert.Contains(Core, ProjectReferences(PlatformAndroid));
        Assert.Contains(Handoff, ProjectReferences(PlatformAndroid));
    }

    [Fact]
    public void EveryProjectTheAndroidSolutionBuilds_IsInIt()
    {
        var solution = File.ReadAllText(Path.Combine(Root.FullName, "Samaat.Mobile.Android.slnx"));

        var missing = Closure(PhoneProjects()).Where(p => !solution.Contains($"/{p}/{p}.csproj", StringComparison.Ordinal)).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryProjectThatBuildsWithoutAndroid_IsInTheMainSolution()
    {
        var solution = File.ReadAllText(Path.Combine(Root.FullName, "Samaat.Mobile.slnx"));
        var neutral = PhoneProjects().Where(p => !IsAndroidOnly(p));

        var missing = Closure(neutral).Where(p => !solution.Contains($"/{p}/{p}.csproj", StringComparison.Ordinal)).ToList();

        Assert.Empty(missing);
        Assert.DoesNotContain(PhoneProjects().Where(IsAndroidOnly), p => solution.Contains($"/{p}/{p}.csproj", StringComparison.Ordinal));
    }

    /// <summary>The phone used to keep a fourth copy of the driver list (Jieli and Fast Pair only, so no Bose).</summary>
    [Fact]
    public void NoPhoneProject_WritesItsOwnDriverList()
    {
        var handWritten = Directory.EnumerateFiles(Path.Combine(Root.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f) is var code && (code.Contains("new DriverRegistry(", StringComparison.Ordinal)
                || code.Contains("Driver());", StringComparison.Ordinal)))
            .Select(f => Path.GetRelativePath(Root.FullName, f))
            .ToList();

        Assert.True(handWritten.Count == 0,
            $"Build the registry with Samaat.Drivers.BuiltInDrivers.CreateRegistry instead: {string.Join(", ", handWritten)}");
        Assert.Contains("BuiltInDrivers.CreateRegistry(", File.ReadAllText(Path.Combine(Root.FullName, "src", Android, "AndroidHost.cs")));
    }

    [Fact]
    public void TheRules_DescribeOnlyProjectsThatExist()
    {
        Assert.Empty(Allowed.Keys.Except(PhoneProjects()));
    }
}
