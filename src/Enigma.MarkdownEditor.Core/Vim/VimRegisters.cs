using System.Collections.Generic;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// The registers: the unnamed one every yank and delete fills, the twenty-six a name in front of a
/// command picks, the yank register <c>0</c>, and the black hole <c>_</c>.
/// </summary>
/// <remarks>
/// <para>
/// Vim's rules, the ones a markdown editor has a use for. The unnamed register <c>"</c> is what
/// <c>p</c> puts without a name and what every yank, delete and change fills — a named one included,
/// so <c>"ayy</c> then <c>p</c> puts the line too. <c>0</c> is filled by a yank alone, which is what
/// keeps the text you meant to put safe from the delete you did on the way. A capital appends to its
/// lowercase register rather than replacing it. And <c>_</c> takes everything and keeps nothing, so
/// <c>"_dd</c> deletes without disturbing what a later <c>p</c> was going to put.
/// </para>
/// <para>
/// A macro is a register too, holding keys rather than text, and nothing here knows the difference:
/// <c>"ap</c> pastes the keys and <c>@a</c> runs a yanked line. The store is the engine's and outlives
/// the document, as vim's registers outlive the buffer they were filled from.
/// </para>
/// </remarks>
public sealed class VimRegisters
{
    /// <summary>
    /// The unnamed register, when a command has to say which one it means.
    /// </summary>
    public const char UnnamedName = '"';

    /// <summary>
    /// The register a yank fills on its own.
    /// </summary>
    public const char YankName = '0';

    /// <summary>
    /// The register that keeps nothing.
    /// </summary>
    public const char BlackHoleName = '_';

    private readonly Dictionary<char, VimRegister> _named = [];

    /// <summary>
    /// Gets what the last delete, change or yank took out, or <see langword="null"/> if nothing has.
    /// </summary>
    /// <remarks>
    /// Nullable rather than empty because a <c>dd</c> on an empty line puts an empty <em>line</em>
    /// here, which <c>p</c> must put back, and "nothing has been yanked yet" has to be a different
    /// answer from that.
    /// </remarks>
    public VimRegister? Unnamed { get; private set; }

    /// <summary>
    /// Whether a character names a register a command can be prefixed with.
    /// </summary>
    /// <param name="name">The character after the <c>"</c>.</param>
    /// <returns><see langword="true"/> for <c>a</c>–<c>z</c>, <c>A</c>–<c>Z</c>, <c>0</c>, <c>_</c> and <c>"</c>.</returns>
    public static bool IsName(char name)
        => char.IsAsciiLetter(name) || (name is YankName or BlackHoleName or UnnamedName);

    /// <summary>
    /// Reads a register.
    /// </summary>
    /// <param name="name">
    /// Which one: a letter in either case (a capital reads its lowercase register), <c>0</c>, <c>"</c>
    /// or <c>'\0'</c> for the unnamed one, <c>_</c> for the black hole.
    /// </param>
    /// <returns>Its content, or <see langword="null"/> when it holds nothing — always, for <c>_</c>.</returns>
    public VimRegister? Get(char name)
    {
        if (name is '\0' or UnnamedName)
        {
            return Unnamed;
        }

        return _named.TryGetValue(char.ToLowerInvariant(name), out VimRegister register) ? register : null;
    }

    /// <summary>
    /// Records what a yank, delete or change took out.
    /// </summary>
    /// <param name="name">The register the command named, or <c>'\0'</c> when it named none.</param>
    /// <param name="value">What it took.</param>
    /// <param name="yank">Whether it was a yank, which alone fills <c>0</c>.</param>
    /// <param name="newLine">The buffer's line terminator, for an append that has to join lines.</param>
    /// <remarks>
    /// The unnamed register follows every fill but the black hole's, so that <c>p</c> after a named
    /// yank puts the same text a named put would. <c>0</c> is filled by an unnamed yank only: a yank
    /// into <c>a</c> is already somewhere safe.
    /// </remarks>
    internal void Fill(char name, VimRegister value, bool yank, string newLine)
    {
        switch (name)
        {
            case BlackHoleName:
                return;

            case '\0':
            case UnnamedName:
                Unnamed = value;

                if (yank)
                {
                    _named[YankName] = value;
                }

                return;

            default:
                Unnamed = char.IsAsciiLetterUpper(name)
                    ? Append(char.ToLowerInvariant(name), value, newLine)
                    : (_named[name] = value);

                return;
        }
    }

    /// <summary>
    /// Puts the unnamed register back as it was, for the <c>P</c> over a selection that promises to
    /// leave it alone after the delete inside it has filled it.
    /// </summary>
    internal void SetUnnamed(VimRegister? value) => Unnamed = value;

    /// <summary>
    /// Stores the keys a recording collected.
    /// </summary>
    /// <param name="name">The register the recording was into; a capital appends to its lowercase register.</param>
    /// <param name="keys">The keys, in <see cref="VimKeyNotation"/>.</param>
    /// <remarks>
    /// Characterwise, so that <c>"ap</c> pastes the keys inline, and an append adds to the end of what
    /// is there without a line break — which is what typing the extra keys after the first recording
    /// would have given. The unnamed register is left alone: a recording is not a yank.
    /// </remarks>
    internal void Record(char name, string keys)
    {
        char target = char.ToLowerInvariant(name);

        if (char.IsAsciiLetterUpper(name) && _named.TryGetValue(target, out VimRegister existing))
        {
            _named[target] = existing with { Text = existing.Text + keys };

            return;
        }

        _named[target] = VimRegister.Characters(keys);
    }

    /// <summary>
    /// Appends to a register, with vim's rule for the kind: lines onto anything, or anything onto
    /// lines, make lines; characters onto characters stay characters.
    /// </summary>
    private VimRegister Append(char target, VimRegister value, string newLine)
    {
        if (!_named.TryGetValue(target, out VimRegister existing))
        {
            return _named[target] = value;
        }

        bool characters = (existing.Kind == VimRegisterKind.Characterwise) && (value.Kind == VimRegisterKind.Characterwise);

        return _named[target] = characters
            ? VimRegister.Characters(existing.Text + value.Text)
            : VimRegister.Lines(existing.Text + newLine + value.Text);
    }
}
