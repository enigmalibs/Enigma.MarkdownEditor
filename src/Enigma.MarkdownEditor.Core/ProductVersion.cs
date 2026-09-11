using System;

namespace Enigma.MarkdownEditor.Core;

/// <summary>
/// The product version as the application states it: the version the user is told, and the source
/// revision the build was cut from, if the build recorded one.
/// </summary>
/// <param name="Version">The version, never <see langword="null"/> and never empty.</param>
/// <param name="BuildSha">
/// The short source revision, or <see langword="null"/> when the build stamped none.
/// </param>
/// <remarks>
/// Separate from <see cref="AppInfo"/> because the parsing is the part worth testing: the properties
/// on <see cref="AppInfo"/> read the running assembly, which a test cannot vary, while
/// <see cref="From(string?, string?)"/> is a pure function over the two strings that assembly holds.
/// </remarks>
public readonly record struct ProductVersion(string Version, string? BuildSha)
{
    /// <summary>
    /// What <see cref="From(string?, string?)"/> reports when neither the informational version nor
    /// the fallback says anything usable. A version is always shown, so there has to be one.
    /// </summary>
    public const string Unknown = "0.0.0";

    /// <summary>
    /// How many characters of a source revision are kept — the length git itself abbreviates to.
    /// </summary>
    private const int ShaLength = 7;

    /// <summary>
    /// Reads an <c>AssemblyInformationalVersion</c> value.
    /// </summary>
    /// <param name="informationalVersion">
    /// The attribute's value — <c>X.Y.Z</c>, or <c>X.Y.Z+&lt;sha&gt;</c> when the build recorded a
    /// source revision. Anything the SDK did not stamp is treated as absent rather than rejected.
    /// </param>
    /// <param name="fallbackVersion">
    /// What to report when <paramref name="informationalVersion"/> says nothing usable — the plain
    /// assembly version, which is always present. May itself be <see langword="null"/>.
    /// </param>
    /// <returns>The version to show, and the short revision if there was one.</returns>
    /// <remarks>
    /// <para>
    /// The version is everything before the first <c>+</c>, taken verbatim rather than re-parsed:
    /// a pre-release version such as <c>1.1.0-rc.1</c> is the product's version exactly as the csproj
    /// states it, and running it through <see cref="System.Version"/> would only lose the suffix.
    /// </para>
    /// <para>
    /// A value is unusable only when there is nothing before the <c>+</c> to show — it is null, blank,
    /// or begins with the separator. That is the whole of "malformed" here, and it falls back rather
    /// than throwing: a missing attribute must not be the reason an About box cannot open.
    /// </para>
    /// </remarks>
    public static ProductVersion From(string? informationalVersion, string? fallbackVersion)
    {
        string candidate = informationalVersion?.Trim() ?? string.Empty;

        if (candidate.Length > 0)
        {
            int separator = candidate.IndexOf('+', StringComparison.Ordinal);
            string version = separator < 0 ? candidate : candidate[..separator];

            if (version.Length > 0)
            {
                return new ProductVersion(version, Shorten(separator < 0 ? null : candidate[(separator + 1)..]));
            }
        }

        string fallback = fallbackVersion?.Trim() ?? string.Empty;

        return new ProductVersion(fallback.Length > 0 ? fallback : Unknown, null);
    }

    /// <summary>
    /// Abbreviates a source revision to <see cref="ShaLength"/> characters.
    /// </summary>
    /// <param name="revision">The revision the build stamped, if any.</param>
    /// <returns>The abbreviated revision, or <see langword="null"/> when there was nothing to abbreviate.</returns>
    /// <remarks>
    /// A revision shorter than <see cref="ShaLength"/> is kept whole: it is not this method's business
    /// to decide that a build stamped something too short to be a commit hash.
    /// </remarks>
    private static string? Shorten(string? revision)
    {
        string trimmed = revision?.Trim() ?? string.Empty;

        return trimmed.Length switch
        {
            0 => null,
            <= ShaLength => trimmed,
            _ => trimmed[..ShaLength],
        };
    }
}
