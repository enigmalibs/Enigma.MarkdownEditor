using System.IO;
using Enigma.MarkdownEditor.Core;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests;

/// <summary>
/// The identity <c>packaging/windows/buildmsi.ps1</c> gives the Windows installer: the upgrade code, the
/// product name and the executable the shortcuts start.
/// </summary>
/// <remarks>
/// Nothing in the build reads that script, so a typo in any of the three would only show once an MSI is
/// installed — as a second product beside the first, a wrong name in <i>Apps &amp; features</i>, or
/// shortcuts to a file that does not exist. The script derives the install folder, the shortcuts, the
/// publish folder and the executable from these three lines, so these are the lines pinned.
/// </remarks>
public sealed class WindowsInstallerIdentityTests
{
    /// <summary>
    /// The upgrade code every MSI of the application has carried since 1.0.0 — the one in each profile
    /// under <c>msiProfiles/</c>. Every later MSI must keep it: one with another code installs beside the
    /// old version instead of replacing it.
    /// </summary>
    private const string UpgradeCode = "a7b550b9-1338-458f-bf2b-66dcf1d25380";

    [Fact]
    public void TheInstallerKeepsTheUpgradeCodeOfTheInstalledReleases()
        => Assert.Contains($"$UpgradeCode = '{UpgradeCode}'", ReadScript());

    [Fact]
    public void TheInstallerNamesTheProductAsTheApplicationDoes()
        => Assert.Contains($"$ProductName = '{AppInfo.DisplayName}'", ReadScript());

    [Fact]
    public void TheInstallerPackagesAndStartsTheExecutableOfTheAssembly()
        => Assert.Contains($"$Assembly = '{typeof(App).Assembly.GetName().Name}'", ReadScript());

    private static string[] ReadScript()
        => File.ReadAllLines(RepositoryRoot.File("packaging", "windows", "buildmsi.ps1"));
}
