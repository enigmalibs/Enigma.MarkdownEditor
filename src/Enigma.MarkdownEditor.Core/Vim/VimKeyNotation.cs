using System;
using System.Collections.Generic;
using System.Text;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Keys as vim writes them: a character as itself, a named key in angle brackets, and
/// <c>&lt;lt&gt;</c> for a <c>&lt;</c> that is a character rather than the start of a name.
/// </summary>
/// <remarks>
/// <para>
/// One notation, one implementation, three readers. A status message names a dead sequence with it
/// (<c>Not a command: d&lt;Esc&gt;</c>); a macro register holds the keys it recorded in it, so that
/// <c>"ap</c> pastes something a person can read and edit and <c>@a</c> can read it back; and the test
/// harness spells its keys in it, so a case reads the way a vim user would type it.
/// </para>
/// <para>
/// The names are vim's own — <c>&lt;Esc&gt;</c> <c>&lt;CR&gt;</c> <c>&lt;BS&gt;</c> <c>&lt;Tab&gt;</c>
/// <c>&lt;Home&gt;</c> <c>&lt;End&gt;</c> <c>&lt;Left&gt;</c> <c>&lt;Right&gt;</c> <c>&lt;Up&gt;</c>
/// <c>&lt;Down&gt;</c> <c>&lt;PageUp&gt;</c> <c>&lt;PageDown&gt;</c> <c>&lt;Space&gt;</c>
/// <c>&lt;Bar&gt;</c> <c>&lt;lt&gt;</c>, with <c>C-</c>,
/// <c>A-</c> (or <c>M-</c>) and <c>S-</c> for the modifiers — plus <c>&lt;Other&gt;</c>, which is not
/// vim's, for the one key this engine names without giving it a meaning. Reading is lenient the way
/// vim's is: a <c>&lt;</c> that does not open a name the notation knows is a literal <c>&lt;</c>, so
/// text that was never meant as keys — a yanked line — still reads back character by character.
/// </para>
/// </remarks>
public static class VimKeyNotation
{
    private const string LessThan = "lt";

    // Vim's spellings, and its alternatives for the ones it has several of.
    private static readonly (string Spelling, VimSpecialKey Key)[] Names =
    [
        ("Esc", VimSpecialKey.Escape),
        ("CR", VimSpecialKey.Enter),
        ("Enter", VimSpecialKey.Enter),
        ("Return", VimSpecialKey.Enter),
        ("BS", VimSpecialKey.Backspace),
        ("Tab", VimSpecialKey.Tab),
        ("Home", VimSpecialKey.Home),
        ("End", VimSpecialKey.End),
        ("Left", VimSpecialKey.Left),
        ("Right", VimSpecialKey.Right),
        ("Up", VimSpecialKey.Up),
        ("Down", VimSpecialKey.Down),
        ("PageUp", VimSpecialKey.PageUp),
        ("PageDown", VimSpecialKey.PageDown),
        ("Other", VimSpecialKey.Other),
    ];

    /// <summary>
    /// Writes one key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The key in vim's notation.</returns>
    public static string Format(VimKey key)
    {
        string name = key.IsCharacter ? key.Character.ToString() : NameOf(key.Special);
        VimModifiers modifiers = key.Modifiers;

        // Shift is already in a character — 'G' arrives as 'G' — so it is written only on a named key,
        // where <S-Tab> is the only way to say it.
        if (key.IsCharacter)
        {
            modifiers &= ~VimModifiers.Shift;
        }

        if ((modifiers == VimModifiers.None) && key.IsCharacter)
        {
            return key.Character == '<' ? $"<{LessThan}>" : name;
        }

        StringBuilder builder = new("<");

        if ((modifiers & VimModifiers.Alt) != VimModifiers.None)
        {
            builder.Append("A-");
        }

        if ((modifiers & VimModifiers.Control) != VimModifiers.None)
        {
            builder.Append("C-");
        }

        if ((modifiers & VimModifiers.Shift) != VimModifiers.None)
        {
            builder.Append("S-");
        }

        // A modified '<' is written as its name too: <C-<> would leave the reader guessing where the
        // key ends.
        return builder.Append(key.IsCharacter && (key.Character == '<') ? LessThan : name).Append('>').ToString();
    }

    /// <summary>
    /// Writes a run of keys, one after the other.
    /// </summary>
    /// <param name="keys">The keys, in order.</param>
    /// <returns>The keys in vim's notation, with nothing between them.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="keys"/> is <see langword="null"/>.</exception>
    public static string Format(IEnumerable<VimKey> keys)
    {
        if (keys is null)
        {
            throw new ArgumentNullException(nameof(keys));
        }

        StringBuilder builder = new();

        foreach (VimKey key in keys)
        {
            builder.Append(Format(key));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Reads keys back out of the notation.
    /// </summary>
    /// <param name="text">The keys as written.</param>
    /// <returns>The keys, in order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Every character is a key except a <c>&lt;</c> that opens a name the notation knows, which is
    /// that key. A <c>&lt;</c> that opens nothing — <c>&lt;Foo&gt;</c>, <c>&lt;x&gt;</c>, a <c>&lt;</c>
    /// with no <c>&gt;</c> after it — is a <c>&lt;</c>, exactly as it is in a vim mapping, which is what
    /// lets a yanked line be run as keys without every comparison in it becoming a mystery key.
    /// </remarks>
    public static IReadOnlyList<VimKey> Parse(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        List<VimKey> keys = [];

        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];

            if (character == '<')
            {
                int close = text.IndexOf('>', index + 1);

                if ((close > index) && (Named(text.AsSpan((index + 1)..close)) is { } named))
                {
                    keys.Add(named);
                    index = close;

                    continue;
                }
            }

            keys.Add(VimKey.Typed(character));
        }

        return keys;
    }

    private static string NameOf(VimSpecialKey key)
        => key switch
        {
            VimSpecialKey.Escape => "Esc",
            VimSpecialKey.Enter => "CR",
            VimSpecialKey.Backspace => "BS",
            VimSpecialKey.Tab => "Tab",
            VimSpecialKey.Home => "Home",
            VimSpecialKey.End => "End",
            VimSpecialKey.Left => "Left",
            VimSpecialKey.Right => "Right",
            VimSpecialKey.Up => "Up",
            VimSpecialKey.Down => "Down",
            VimSpecialKey.PageUp => "PageUp",
            VimSpecialKey.PageDown => "PageDown",
            _ => "Other",
        };

    /// <summary>
    /// Reads what is between the angle brackets.
    /// </summary>
    /// <returns>The key, or <see langword="null"/> when the name is not one the notation knows.</returns>
    private static VimKey? Named(ReadOnlySpan<char> name)
    {
        VimModifiers modifiers = VimModifiers.None;

        // The modifiers come first, in any order and any case, each one letter and a dash: <C-S-Left>.
        while ((name.Length > 2) && (name[1] == '-') && (ModifierFor(name[0]) is { } modifier))
        {
            modifiers |= modifier;
            name = name[2..];
        }

        if (name.Equals(LessThan, StringComparison.OrdinalIgnoreCase))
        {
            return VimKey.Typed('<', modifiers);
        }

        if (name.Equals("Space", StringComparison.OrdinalIgnoreCase))
        {
            return VimKey.Typed(' ', modifiers);
        }

        if (name.Equals("Bar", StringComparison.OrdinalIgnoreCase))
        {
            return VimKey.Typed('|', modifiers);
        }

        if (SpecialFor(name) is { } special)
        {
            return VimKey.Named(special, modifiers);
        }

        // A single character behind a modifier is that character held with it: <C-r>. On its own,
        // <x> names nothing, and the '<' that opened it is a character.
        if ((name.Length == 1) && (modifiers != VimModifiers.None))
        {
            return VimKey.Typed(name[0], modifiers);
        }

        return null;
    }

    private static VimModifiers? ModifierFor(char letter)
        => char.ToUpperInvariant(letter) switch
        {
            'C' => VimModifiers.Control,
            'S' => VimModifiers.Shift,
            'A' or 'M' => VimModifiers.Alt,
            _ => null,
        };

    private static VimSpecialKey? SpecialFor(ReadOnlySpan<char> name)
    {
        foreach ((string spelling, VimSpecialKey key) in Names)
        {
            if (name.Equals(spelling, StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        return null;
    }
}
