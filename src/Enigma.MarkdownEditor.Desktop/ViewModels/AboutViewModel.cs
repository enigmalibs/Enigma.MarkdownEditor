using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.MarkdownEditor.Core;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// What the About dialog says: which product this is, which build of it is running, and what it is
/// built out of.
/// </summary>
/// <remarks>
/// Read-only, and every property is get-only: nothing here changes while the dialog is open. It
/// derives from <see cref="ObservableObject"/> anyway, so that it is the same shape as every other
/// ViewModel in this folder and stays bindable if a later phase gives it something that does move.
/// </remarks>
public sealed class AboutViewModel : ObservableObject
{
    /// <summary>
    /// What the application redistributes, and under what terms.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every entry was read from the package it names — the <c>&lt;license&gt;</c> element of its
    /// <c>.nuspec</c>, or the licence file the package ships — rather than recalled. This is the
    /// attribution the MIT, BSD and OFL terms all ask for, and the list is the whole of it.
    /// </para>
    /// <para>
    /// One row per thing a reader would recognise, not one per assembly: the four Enigma libraries
    /// share a row because they share an author and a licence, and TextMateSharp covers the grammars
    /// package it brings with it.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlyList<CreditEntry> DefaultCredits =
    [
        new CreditEntry("Avalonia", "MIT"),
        new CreditEntry("AvaloniaEdit", "MIT"),
        new CreditEntry("TextMateSharp", "MIT"),
        new CreditEntry("Markdig", "BSD-2-Clause"),
        new CreditEntry("NLog", "BSD-3-Clause"),
        new CreditEntry("CommunityToolkit.Mvvm", "MIT"),
        new CreditEntry("Enigma libraries", "MIT"),
        new CreditEntry("Phosphor Icons", "MIT"),
        new CreditEntry("Inter", "SIL Open Font License 1.1"),
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutViewModel"/> class over the running build.
    /// </summary>
    public AboutViewModel()
        : this(AppInfo.Version, AppInfo.BuildSha, AppInfo.Copyright, DefaultCredits)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutViewModel"/> class over given values.
    /// </summary>
    /// <param name="version">The version to show.</param>
    /// <param name="buildSha">The short revision to show, or <see langword="null"/> to show no build line.</param>
    /// <param name="copyright">The copyright notice, or an empty string to show no copyright line.</param>
    /// <param name="credits">The attribution list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="version"/>, <paramref name="copyright"/> or <paramref name="credits"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The values are arguments rather than read from <see cref="AppInfo"/> here, because
    /// <see cref="AppInfo"/> reports the assembly the test host is running and a test cannot vary it.
    /// This is the seam that lets the missing-revision case be asserted at all.
    /// </remarks>
    public AboutViewModel(string version, string? buildSha, string copyright, IReadOnlyList<CreditEntry> credits)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(copyright);
        ArgumentNullException.ThrowIfNull(credits);

        Version = version;
        BuildSha = buildSha;
        Copyright = copyright;
        Credits = credits;
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

    /// <summary>
    /// Gets what this application is built out of, and under what licence.
    /// </summary>
    public IReadOnlyList<CreditEntry> Credits { get; }
}
