using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// A made-up font list, and a real <see cref="FontCatalog"/> over it.
/// </summary>
/// <remarks>
/// <para>
/// Not a fake catalog: <see cref="FontCatalog"/> takes the font list as an argument precisely so that
/// tests can be the ones supplying it, and its resolving and fallback rules are what the consumers
/// under test are being asserted through. A stub implementing <see cref="IFontCatalog"/> by hand would
/// assert a second copy of those rules instead.
/// </para>
/// <para>
/// What has to be faked is the list itself. The real one comes from
/// <see cref="FontCatalog.ForCurrentSystem"/>, which is neither the same on the machine running the
/// suite as on the next one, nor meaningful on the headless platform — whose font manager reports a
/// single synthetic family.
/// </para>
/// </remarks>
internal static class TestFonts
{
    /// <summary>
    /// The family reported as the interface font, and as such the last-resort editor font.
    /// </summary>
    public const string UiFamily = "Fake Sans";

    /// <summary>
    /// The fixed-width families on the imaginary machine.
    /// </summary>
    /// <remarks>
    /// Deliberately none of the names on <see cref="FontCatalog.PreferredMonospaceFamilies"/>, so a
    /// test that means "the preferred list decided this" cannot pass by accident.
    /// </remarks>
    public static readonly string[] Monospace = ["Fake Mono", "Another Mono"];

    /// <summary>
    /// The proportional families on the imaginary machine.
    /// </summary>
    public static readonly string[] Proportional = [UiFamily, "Fake Serif"];

    /// <summary>
    /// A family name no test list contains.
    /// </summary>
    public const string NotInstalled = "Nowhere Mono";

    /// <summary>
    /// The catalog every test gets unless it needs a different machine.
    /// </summary>
    /// <returns>A catalog over <see cref="Monospace"/> and <see cref="Proportional"/>.</returns>
    public static FontCatalog Catalog()
        => new([.. Monospace, .. Proportional], Monospace, UiFamily);
}
