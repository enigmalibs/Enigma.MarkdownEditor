namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// One keystroke as the engine sees it: a character, or a named key, plus whatever was held down.
/// </summary>
/// <param name="Character">
/// The character that was typed, or <c>'\0'</c> when <paramref name="Special"/> says otherwise.
/// </param>
/// <param name="Special">The named key, or <see cref="VimSpecialKey.None"/> for a character.</param>
/// <param name="Modifiers">What was held down with it.</param>
/// <remarks>
/// A character rather than a key code, deliberately. Which physical key produces <c>$</c> or <c>0</c>
/// depends on the keyboard layout, so an engine that matched key codes would be a vim that only works
/// on a US keyboard. The editor gets the character from the platform's own text input, which is
/// already layout- and dead-key-correct, and hands it here.
/// </remarks>
public readonly record struct VimKey(char Character, VimSpecialKey Special, VimModifiers Modifiers)
{
    /// <summary>
    /// A character keystroke.
    /// </summary>
    /// <param name="character">The character that was typed.</param>
    /// <param name="modifiers">What was held down with it.</param>
    /// <returns>The keystroke.</returns>
    public static VimKey Typed(char character, VimModifiers modifiers = VimModifiers.None)
        => new(character, VimSpecialKey.None, modifiers);

    /// <summary>
    /// A named keystroke that types nothing.
    /// </summary>
    /// <param name="key">Which key it was.</param>
    /// <param name="modifiers">What was held down with it.</param>
    /// <returns>The keystroke.</returns>
    public static VimKey Named(VimSpecialKey key, VimModifiers modifiers = VimModifiers.None)
        => new('\0', key, modifiers);

    /// <summary>
    /// Gets a value indicating whether this keystroke carries a character.
    /// </summary>
    public bool IsCharacter => Special == VimSpecialKey.None;

    /// <summary>
    /// Gets a value indicating whether Control or Alt was held down.
    /// </summary>
    /// <remarks>
    /// The test for "this belongs to the application, not to vim". Normal mode swallows a bare key
    /// whatever it means, but has to let Ctrl+S and Ctrl+W through to the window's own shortcuts.
    /// </remarks>
    public bool HasCommandModifier => (Modifiers & (VimModifiers.Control | VimModifiers.Alt)) != VimModifiers.None;

    /// <summary>
    /// Renders the keystroke the way vim writes it, for a status message or a macro register.
    /// </summary>
    /// <returns>The character, or the named key in angle brackets — <see cref="VimKeyNotation"/>.</returns>
    public override string ToString() => VimKeyNotation.Format(this);
}
