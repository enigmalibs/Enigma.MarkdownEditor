using System.Collections.Generic;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// What fonts this machine has, which of them are monospace, and which one the editor should use when
/// the setting does not name a family that is actually here.
/// </summary>
/// <remarks>
/// The half of the font story that <c>Core</c> cannot tell. <c>AppSettings.EditorFontFamily</c> holds
/// a family name or nothing at all, and answering "nothing at all" — or "a family from the machine
/// this settings file came from" — needs the installed fonts, which only the UI framework knows
/// about.
/// </remarks>
public interface IFontCatalog
{
    /// <summary>
    /// Gets every installed family, sorted by name.
    /// </summary>
    IReadOnlyList<string> AllFamilies { get; }

    /// <summary>
    /// Gets the installed families whose characters are all the same width, sorted by name.
    /// </summary>
    /// <remarks>
    /// The list the editor's font picker offers: a proportional font in a text editor puts the caret
    /// somewhere different on every line, and the pane shows line numbers beside code.
    /// </remarks>
    IReadOnlyList<string> MonospaceFamilies { get; }

    /// <summary>
    /// Gets the family the editor falls back to when the setting names none — the best monospace font
    /// this machine has.
    /// </summary>
    string DefaultMonospaceFamily { get; }

    /// <summary>
    /// Gets the family the rest of the interface is drawn in, used to render a row for a font that
    /// cannot be shown in itself.
    /// </summary>
    string SystemDefaultFamily { get; }

    /// <summary>
    /// Says whether <paramref name="family"/> is installed here.
    /// </summary>
    /// <param name="family">A family name, possibly empty or <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> when a family of that name is installed; otherwise
    /// <see langword="false"/>. Case is ignored, and an empty or <see langword="null"/> name is not
    /// available — it is the absence of a choice rather than a family.
    /// </returns>
    bool IsAvailable(string? family);

    /// <summary>
    /// Turns the stored <c>EditorFontFamily</c> into a family the editor can actually render with.
    /// </summary>
    /// <param name="storedFamily">What the settings file holds. May be empty or <see langword="null"/>.</param>
    /// <returns>
    /// <paramref name="storedFamily"/> when it names an installed family; otherwise
    /// <see cref="DefaultMonospaceFamily"/>.
    /// </returns>
    /// <remarks>
    /// Resolving rather than correcting: the stored value is left exactly as it is, so a settings file
    /// carried between two machines keeps naming the font the other one has instead of being rewritten
    /// to whatever this one happens to offer.
    /// </remarks>
    string ResolveEditorFamily(string? storedFamily);
}
