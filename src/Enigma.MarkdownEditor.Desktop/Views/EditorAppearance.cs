using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using TextMateInstallation = AvaloniaEdit.TextMate.TextMate.Installation;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// How one <see cref="TextEditor"/> is painted: TextMate token colours for the markdown it holds, and
/// the chrome around them — plain text, line numbers, caret, selection and the current line.
/// </summary>
/// <remarks>
/// <para>
/// The two halves come from two palettes on purpose. Tokens come from a TextMate theme, because that
/// is where a grammar's scopes are given colours and nothing in the Enigma palette has an opinion
/// about a markdown heading or a C# keyword. Everything else comes from the Enigma brushes, so the
/// selection and the current line match the panes either side of the editor instead of putting a
/// second design language in the middle of the window.
/// </para>
/// <para>
/// AvaloniaEdit.TextMate 12 applies none of a theme's editor colours itself — its installation colours
/// tokens and leaves the rest to be read out of the theme by hand — so the chrome below is not
/// overriding anything. It is the only thing that sets those brushes at all.
/// </para>
/// </remarks>
public sealed class EditorAppearance : IDisposable
{
    /// <summary>
    /// The extension whose grammar every tab is highlighted with. The pane is not a general-purpose
    /// text editor, and a fenced block takes its language from inside the markdown grammar rather
    /// than from the name of the file around it.
    /// </summary>
    public const string MarkdownExtension = ".md";

    private const string ForegroundKey = "EnigmaForegroundBrush";
    private const string LineNumbersKey = "EnigmaForegroundTertiaryBrush";
    private const string SelectionKey = "EnigmaSelectionBrush";
    private const string CurrentLineKey = "EnigmaHoverBrush";

    // Fill only: the line the caret is on is a hint, not a box drawn round the text.
    private static readonly Pen NoBorder = new(Brushes.Transparent, 0);

    private readonly TextEditor _editor;
    private readonly RegistryOptions _registry;
    private readonly TextMateInstallation _installation;

    private ThemeName _themeApplied;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="EditorAppearance"/> class: installs TextMate on
    /// <paramref name="editor"/>, gives it the markdown grammar, and paints it for
    /// <paramref name="variant"/>.
    /// </summary>
    /// <param name="editor">The editor to highlight. Its document may be swapped afterwards.</param>
    /// <param name="variant">The variant the window is showing right now.</param>
    /// <param name="failureHandler">
    /// Told about anything tokenizing throws. Tokenizing runs on a background thread, so without a
    /// handler a grammar that fails takes the process down rather than a colour with it.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public EditorAppearance(TextEditor editor, ThemeVariant variant, Action<Exception> failureHandler)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        ArgumentNullException.ThrowIfNull(failureHandler);

        // The variant is handed to the registry rather than corrected afterwards: the installation
        // applies the registry's default theme as it is built, and starting on the wrong one would
        // paint the editor dark for a frame in a light window.
        _themeApplied = ThemeNameFor(variant);

        _registry = new RegistryOptions(_themeApplied);
        _installation = _editor.InstallTextMate(_registry, initCurrentDocument: true, failureHandler);
        _installation.SetGrammar(_registry.GetScopeByExtension(MarkdownExtension));

        ApplyChrome(variant);
    }

    /// <summary>
    /// Maps a resolved theme variant onto the TextMate theme that expresses it.
    /// </summary>
    /// <param name="variant">The variant the editor is being painted for.</param>
    /// <returns>The matching <see cref="ThemeName"/>.</returns>
    /// <remarks>
    /// Dark+ and Light+ are Visual Studio Code's own defaults, which is what makes them the right
    /// pair here: they have the widest markdown token coverage of the set, and the colours a fenced
    /// code block comes out in are the ones most people already recognise.
    /// <para>
    /// Anything that is not dark is treated as light. A control's <c>ActualThemeVariant</c> is already
    /// resolved to one or the other before it reaches this method, so the fallback is for a variant
    /// this application does not define — and light is the safer guess against an unknown background,
    /// because its foregrounds are dark and stay legible on either.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="variant"/> is <see langword="null"/>.</exception>
    public static ThemeName ThemeNameFor(ThemeVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);

        return ThemeVariant.Dark.Equals(variant) ? ThemeName.DarkPlus : ThemeName.LightPlus;
    }

    /// <summary>
    /// Repaints the editor for <paramref name="variant"/>, live.
    /// </summary>
    /// <param name="variant">The variant the window has just switched to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="variant"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">This instance has been disposed.</exception>
    public void Apply(ThemeVariant variant)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(variant);

        ThemeName name = ThemeNameFor(variant);

        // Loading a theme parses it, and the variant can be re-announced without having changed —
        // switching between two "System" settings that resolve the same way, for one.
        if (_themeApplied != name)
        {
            _installation.SetTheme(_registry.LoadTheme(name));
            _themeApplied = name;
        }

        ApplyChrome(variant);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Takes the line transformer back off the text view and stops the background tokenizer. The
        // editor itself outlives this and goes back to being uncoloured.
        _installation.Dispose();
    }

    private void ApplyChrome(ThemeVariant variant)
    {
        Use(ForegroundKey, variant, brush => _editor.Foreground = brush);
        Use(LineNumbersKey, variant, brush => _editor.LineNumbersForeground = brush);
        Use(ForegroundKey, variant, brush => _editor.TextArea.CaretBrush = brush);
        Use(SelectionKey, variant, brush => _editor.TextArea.SelectionBrush = brush);

        Use(
            CurrentLineKey,
            variant,
            brush =>
            {
                _editor.TextArea.TextView.CurrentLineBackground = brush;
                _editor.TextArea.TextView.CurrentLineBorder = NoBorder;
            });
    }

    private void Use(string key, ThemeVariant variant, Action<IBrush> apply)
    {
        // A key that does not resolve leaves the property as it was rather than blanking it: the
        // palette comes from a package, and a brush renamed there should cost one wrong colour
        // rather than an invisible caret.
        if (_editor.TryFindResource(key, variant, out object? value) && (value is IBrush brush))
        {
            apply(brush);
        }
    }
}
