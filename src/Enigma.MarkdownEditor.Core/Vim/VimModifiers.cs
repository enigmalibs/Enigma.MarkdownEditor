using System;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// The modifier keys held down with a <see cref="VimKey"/>.
/// </summary>
/// <remarks>
/// <see cref="Shift"/> is recorded but never consulted for a character key: the character itself
/// already carries it — <c>G</c> arrives as <c>'G'</c>, not as <c>'g'</c> plus a flag — which is what
/// keeps the engine right on a keyboard layout where <c>$</c> is not where a US layout puts it.
/// </remarks>
[Flags]
public enum VimModifiers
{
    /// <summary>
    /// No modifier.
    /// </summary>
    None = 0,

    /// <summary>
    /// Shift.
    /// </summary>
    Shift = 1,

    /// <summary>
    /// Control.
    /// </summary>
    Control = 2,

    /// <summary>
    /// Alt.
    /// </summary>
    Alt = 4,
}
