using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.MarkdownEditor.Core;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// What the About dialog says: which product this is, and which build of it is running.
/// </summary>
/// <remarks>
/// Read-only, and every property is get-only: nothing here changes while the dialog is open. It
/// derives from <see cref="ObservableObject"/> anyway, so that it is the same shape as every other
/// ViewModel in this folder and stays bindable if a later phase gives it something that does move.
/// </remarks>
public sealed class AboutViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AboutViewModel"/> class over the running build.
    /// </summary>
    public AboutViewModel()
        : this(AppInfo.Version, AppInfo.BuildSha, AppInfo.Copyright)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutViewModel"/> class over given values.
    /// </summary>
    /// <param name="version">The version to show.</param>
    /// <param name="buildSha">The short revision to show, or <see langword="null"/> to show no build line.</param>
    /// <param name="copyright">The copyright notice, or an empty string to show no copyright line.</param>
    /// <exception cref="ArgumentNullException"><paramref name="version"/> or <paramref name="copyright"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The values are arguments rather than read from <see cref="AppInfo"/> here, because
    /// <see cref="AppInfo"/> reports the assembly the test host is running and a test cannot vary it.
    /// This is the seam that lets the missing-revision case be asserted at all.
    /// </remarks>
    public AboutViewModel(string version, string? buildSha, string copyright)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(copyright);

        Version = version;
        BuildSha = buildSha;
        Copyright = copyright;
    }

    /// <summary>
    /// Gets the product name, which is the same in every build.
    /// </summary>
    public string DisplayName => AppInfo.DisplayName;

    /// <summary>
    /// Gets the version of the running build.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets the short commit this build was cut from, or <see langword="null"/> if it recorded none.
    /// </summary>
    public string? BuildSha { get; }

    /// <summary>
    /// Gets a value indicating whether there is a revision to show.
    /// </summary>
    /// <remarks>
    /// Bound by the view rather than a null-check converter: a build with no revision must show no
    /// build line at all, not an empty one under the version.
    /// </remarks>
    public bool HasBuildSha => !string.IsNullOrEmpty(BuildSha);

    /// <summary>
    /// Gets the copyright notice, or an empty string if the assembly carries none.
    /// </summary>
    public string Copyright { get; }

    /// <summary>
    /// Gets a value indicating whether there is a copyright notice to show.
    /// </summary>
    public bool HasCopyright => Copyright.Length > 0;
}
