// Using directives stay at file scope, above the namespace declaration, throughout this project:
// inside a namespace beginning with "Enigma.", the simple name "Avalonia" resolves against the
// referenced Enigma.Avalonia.* assemblies instead of Avalonia itself.
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;

namespace Enigma.MarkdownEditor.Desktop.Controls;

/// <summary>
/// A fenced code block: the code, in the family <see cref="Typography"/> names, coloured by the
/// grammar its fence named.
/// </summary>
/// <remarks>
/// <para>
/// A control of its own rather than a <see cref="Border"/> the builder fills, because token colours
/// are the one thing in the preview that cannot be a <c>DynamicResource</c> — they come out of a
/// TextMate theme as literal values. Something has to notice the window changing variant and ask for
/// them again, and the block that owns them is the smallest thing that can: the rest of the rendered
/// document still repaints without being rebuilt.
/// </para>
/// <para>
/// Its lines do not wrap. Wrapped code says something the code does not — a continuation looks like a
/// statement — so a long line goes behind a horizontal scroll bar of the block's own, which is also
/// what keeps it out of the page's own scrolling.
/// </para>
/// </remarks>
public sealed class MarkdownCodeBlock : Border
{
    /// <summary>
    /// Defines the <see cref="Code"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<MarkdownCodeBlock, string?>(nameof(Code));

    /// <summary>
    /// Defines the <see cref="Language"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> LanguageProperty =
        AvaloniaProperty.Register<MarkdownCodeBlock, string?>(nameof(Language));

    /// <summary>
    /// Defines the <see cref="Typography"/> property.
    /// </summary>
    public static readonly StyledProperty<MarkdownPreviewTypography> TypographyProperty =
        AvaloniaProperty.Register<MarkdownCodeBlock, MarkdownPreviewTypography>(
            nameof(Typography),
            MarkdownPreviewTypography.Default);

    private readonly SelectableTextBlock _text;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownCodeBlock"/> class.
    /// </summary>
    public MarkdownCodeBlock()
    {
        _text = new SelectableTextBlock { TextWrapping = TextWrapping.NoWrap };

        ApplyTypography();

        Child = new ScrollViewer
        {
            Content = _text,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(4);
        Padding = new Thickness(10, 8);
        Classes.Add(MarkdownPreviewTheme.CodeBlockClass);

        MarkdownPreviewTheme.BindBrush(this, BackgroundProperty, MarkdownPreviewTheme.CodeBlockBackgroundKey);
        MarkdownPreviewTheme.BindBrush(this, BorderBrushProperty, MarkdownPreviewTheme.BorderSubtleKey);

        // The colours are read from a TextMate theme rather than bound to one, so this is the only
        // thing that keeps a fence in step with the window. Self-subscription: the handler dies with
        // the block.
        ActualThemeVariantChanged += (_, _) => Render();
    }

    /// <summary>
    /// The contents of the block, without its fences.
    /// </summary>
    public string? Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    /// <summary>
    /// The word after the opening fence. <see langword="null"/>, empty or unknown leaves the code
    /// uncoloured.
    /// </summary>
    public string? Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    /// <summary>
    /// The type the code is set in. Only <see cref="MarkdownPreviewTypography.CodeFontFamily"/>,
    /// <see cref="MarkdownPreviewTypography.CodeFontSize"/> and the line height are read; the prose
    /// family and size are not.
    /// </summary>
    /// <remarks>
    /// Assigning it restyles the block in place rather than re-highlighting it — the tokens are the
    /// same tokens, set in a different face — which is why this is not one of the properties that
    /// triggers <c>Render</c>.
    /// </remarks>
    public MarkdownPreviewTypography Typography
    {
        get => GetValue(TypographyProperty);
        set => SetValue(TypographyProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);

        base.OnPropertyChanged(change);

        if ((change.Property == CodeProperty) || (change.Property == LanguageProperty))
        {
            Render();
        }
        else if (change.Property == TypographyProperty)
        {
            ApplyTypography();
        }
    }

    private void ApplyTypography()
    {
        MarkdownPreviewTypography typography = Typography ?? MarkdownPreviewTypography.Default;

        // An empty family means "inherit", and inheriting is what leaving the property unset does —
        // assigning an empty FontFamily would throw rather than fall back.
        if (typography.CodeFontFamily is { Length: > 0 } family)
        {
            _text.FontFamily = new FontFamily(family);
        }
        else
        {
            _text.ClearValue(TextBlock.FontFamilyProperty);
        }

        _text.FontSize = typography.CodeFontSize;
        _text.LineHeight = typography.LineHeightAt(typography.CodeFontSize);
    }

    private void Render()
    {
        if (_text.Inlines is not { } inlines)
        {
            return;
        }

        inlines.Clear();

        if (Code is not { Length: > 0 } code)
        {
            return;
        }

        // Null until the block is in a tree, and it is built before it is added to one. The render
        // that stands is the one the variant change triggers a moment later.
        ThemeVariant? variant = ActualThemeVariant;

        bool first = true;

        foreach (IReadOnlyList<MarkdownCodeToken> line in
                 MarkdownCodeHighlighter.Highlight(code, Language, variant ?? ThemeVariant.Default))
        {
            if (!first)
            {
                inlines.Add(new LineBreak());
            }

            first = false;

            foreach (MarkdownCodeToken token in line)
            {
                inlines.Add(Run(token));
            }
        }
    }

    private static Run Run(MarkdownCodeToken token)
    {
        Run run = new(token.Text);

        if (token.Foreground is { } color)
        {
            run.Foreground = new SolidColorBrush(color);
        }

        // The weight the theme asks for is deliberately not applied. Dark+ and Light+ mark whole
        // scopes bold, so a fence came out heavier than the same code in the editor, which paints its
        // tokens through another path; in a preview nobody types into, the colour already carries the
        // syntax. MarkdownCodeToken.IsBold stays a faithful reading of the theme either way.
        if (token.IsItalic)
        {
            run.FontStyle = FontStyle.Italic;
        }

        if (token.IsUnderline)
        {
            run.TextDecorations = TextDecorations.Underline;
        }

        return run;
    }
}
