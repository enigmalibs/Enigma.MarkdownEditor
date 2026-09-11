using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests;

/// <summary>
/// Bootstrap smoke tests: they prove the xUnit v3 harness runs under the Microsoft Testing Platform
/// and that <c>Enigma.MarkdownEditor.Core</c> is referenced and loadable from the suite — plus what
/// can be said about the identity this type reads off the running assembly.
/// </summary>
/// <remarks>
/// The version properties are asserted for shape rather than for value: what they report is whatever
/// the csproj said when this build was made, and pinning a literal here would mean editing a test on
/// every release. The parsing they are built on is covered exactly, in
/// <see cref="ProductVersionTests"/>.
/// </remarks>
public sealed class AppInfoTests
{
    [Fact]
    public void DisplayName_IsTheProductName()
        => Assert.Equal("Enigma Markdown Editor", AppInfo.DisplayName);

    [Fact]
    public void AppDataFolderName_MatchesTheCoreAssemblyPrefix()
    {
        string? coreAssemblyName = typeof(AppInfo).Assembly.GetName().Name;

        Assert.Equal($"{AppInfo.AppDataFolderName}.Core", coreAssemblyName);
    }

    [Fact]
    public void Version_IsTheOneTheCsprojStamped()
    {
        // Read from Core's own assembly, so it is the <Version> property both csprojs carry rather
        // than the test runner's — which is what an entry-assembly lookup would have answered.
        System.Version? assemblyVersion = typeof(AppInfo).Assembly.GetName().Version;

        Assert.NotNull(assemblyVersion);
        Assert.StartsWith(assemblyVersion.ToString(3), AppInfo.Version, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Version_IsNeverEmpty()
        => Assert.NotEmpty(AppInfo.Version);

    [Fact]
    public void BuildSha_IsEitherAbsentOrShort()
    {
        // Null on a build from a source drop, seven characters on one cut from the repository. The
        // About dialog hides its line in the first case, which is why an empty string is not allowed
        // to be a third answer.
        Assert.True(AppInfo.BuildSha is null || AppInfo.BuildSha.Length is > 0 and <= 7);
    }

    [Fact]
    public void Copyright_IsTheNoticeDirectoryBuildPropsSets()
        => Assert.Contains("Josué Clément", AppInfo.Copyright, System.StringComparison.Ordinal);
}
