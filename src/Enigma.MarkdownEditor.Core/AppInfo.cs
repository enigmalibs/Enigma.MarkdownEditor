using System.Reflection;

namespace Enigma.MarkdownEditor.Core;

/// <summary>
/// Product identity shared by every layer of the application.
/// </summary>
public static class AppInfo
{
    /// <summary>
    /// The name shown to the user — window title, dialogs, about box.
    /// </summary>
    public const string DisplayName = "Enigma Markdown Editor";

    /// <summary>
    /// Folder name used under the per-user application-data root for settings, logs and session
    /// state. Kept identical to the assembly-name prefix so the on-disk layout matches the product.
    /// </summary>
    public const string AppDataFolderName = "Enigma.MarkdownEditor";

    /// <summary>
    /// The product version and the revision it was built from, read once from this assembly.
    /// </summary>
    /// <remarks>
    /// From <b>this</b> assembly rather than from <c>Assembly.GetEntryAssembly()</c>: Core and Desktop
    /// carry one <c>&lt;Version&gt;</c> between them, so either answers the same thing — and under a
    /// test host the entry assembly is the runner, which answers the runner's version or nothing at
    /// all.
    /// </remarks>
    private static readonly ProductVersion Product = ProductVersion.From(
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        FallbackVersion());

    /// <summary>
    /// The product version, as the csproj's <c>&lt;Version&gt;</c> states it. Never empty.
    /// </summary>
    public static string Version => Product.Version;

    /// <summary>
    /// The short commit this build was cut from, or <see langword="null"/> when it recorded none —
    /// a build from a source drop rather than from the repository. A surface showing it hides the
    /// line rather than showing an empty one.
    /// </summary>
    public static string? BuildSha => Product.BuildSha;

    /// <summary>
    /// The copyright notice, from the assembly attribute <c>Directory.Build.props</c> populates.
    /// Empty when the assembly carries none, so that a surface showing it can hide the line.
    /// </summary>
    public static string Copyright { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright?.Trim() ?? string.Empty;

    /// <summary>
    /// The plain assembly version, for when the informational one says nothing usable.
    /// </summary>
    /// <returns>The version in <c>X.Y.Z</c> form, or <see langword="null"/> if there is none.</returns>
    /// <remarks>
    /// Trimmed to three components: the assembly version is always four (<c>1.0.0.0</c>), and the
    /// fourth is a zero the csproj never wrote and the user has no use for.
    /// </remarks>
    private static string? FallbackVersion()
    {
        System.Version? version = typeof(AppInfo).Assembly.GetName().Version;

        return version is null ? null : version.ToString(version.Build >= 0 ? 3 : 2);
    }
}
