using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.LogicalTree;
using Enigma.MarkdownEditor.Desktop.Controls;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Controls;

/// <summary>
/// Reading a rendered preview back: the blocks it produced, the controls inside them, and the text
/// they carry.
/// </summary>
/// <remarks>
/// <para>
/// The renderer's output is a control tree, so asserting on it means walking one. Text is collected
/// from the inlines rather than from <see cref="TextBlock.Text"/> because that is where the renderer
/// puts it — a paragraph is a sequence of runs and spans, and its <c>Text</c> property is never set.
/// </para>
/// <para>
/// Every render happens inside a shown window, and that is not incidental: a dynamic resource
/// resolves through the tree the visual is in, so a preview with no window over it comes out with
/// every brush unset — which is exactly how a green structural test can hide a colourless preview.
/// </para>
/// <para>
/// A fresh window per render, never a cached one: <c>[AvaloniaFact]</c> gives each test its own
/// <see cref="Avalonia.Application"/>, so a window held in a static field would be attached to the
/// previous test's application by the time the next one looked at it — and would resolve nothing.
/// </para>
/// </remarks>
internal static class PreviewTree
{
    /// <summary>
    /// The type every render here is set in unless a test asks for another: no family named, at the
    /// size this renderer was fixed at before the size became a setting.
    /// </summary>
    /// <remarks>
    /// Naming no family is not laziness — it is what keeps a laid-out preview measurable here. The
    /// headless platform has one synthetic typeface and hands it back for any name asked of it, but
    /// only for a name that was <em>asked</em>: a visual that inherits its family measures sensibly,
    /// and the same visual with a family assigned to it comes out of the same measure pass eighteen
    /// lines tall, because the metrics it is then given belong to nothing. A paragraph with a hard
    /// line break in it does not converge at all. So the families are named only by
    /// <see cref="NamedFamilies"/>, and only where what is asserted is the property rather than the
    /// layout.
    /// </remarks>
    public static MarkdownPreviewTypography Typography { get; } =
        new(string.Empty, string.Empty, BodyFontSize: 14, CodeFontSize: 13, LineHeight: 1.5);

    /// <summary>
    /// The same type with both families named, for a test asserting which family a visual asks for.
    /// </summary>
    /// <remarks>
    /// The imaginary machine's names (see <see cref="TestFonts"/>), so prose and code are tellable
    /// apart. Only for tests that read a property off the tree — see the remark on
    /// <see cref="Typography"/> for what naming a family costs a test that measures one.
    /// </remarks>
    public static MarkdownPreviewTypography NamedFamilies { get; } =
        Typography with { FontFamily = TestFonts.Proportional[1], CodeFontFamily = TestFonts.Monospace[0] };

    /// <summary>
    /// Renders <paramref name="markdown"/> in a window of its own and returns the control, so a test
    /// can read both its blocks and the failures it reported.
    /// </summary>
    public static MarkdownPreview Preview(
        string? markdown,
        string? basePath = null,
        MarkdownPreviewTypography? typography = null)
    {
        MarkdownPreview preview = new();

        // Attached first, then given its content: the brushes are resolved as each visual is built,
        // and a visual built outside a tree has nothing to resolve them through. The window stays
        // alive through the preview's own parent chain.
        Host(preview);

        preview.Typography = typography ?? Typography;
        preview.BasePath = basePath;

        // After BasePath, so the render that stands happened against the right folder.
        preview.Markdown = markdown;

        return preview;
    }

    /// <summary>
    /// Puts <paramref name="preview"/> in a shown window and returns it.
    /// </summary>
    public static Window Host(MarkdownPreview preview)
    {
        Window host = new() { Width = 420, Height = 640, Content = preview };
        host.Show();

        return host;
    }

    /// <summary>
    /// Renders <paramref name="markdown"/> and returns its block visuals.
    /// </summary>
    public static IReadOnlyList<Control> Blocks(
        string? markdown,
        string? basePath = null,
        MarkdownPreviewTypography? typography = null)
        => Preview(markdown, basePath, typography).Blocks;

    /// <summary>
    /// Renders <paramref name="markdown"/> and returns its one block visual, as
    /// <typeparamref name="T"/>.
    /// </summary>
    public static T Block<T>(
        string? markdown,
        string? basePath = null,
        MarkdownPreviewTypography? typography = null)
        where T : Control
    {
        IReadOnlyList<Control> blocks = Blocks(markdown, basePath, typography);

        Xunit.Assert.Single(blocks);

        return Xunit.Assert.IsType<T>(blocks[0]);
    }

    /// <summary>
    /// Every control of type <typeparamref name="T"/> inside <paramref name="root"/>, itself
    /// included.
    /// </summary>
    public static IReadOnlyList<T> Descendants<T>(Control root)
        where T : Control
        => [.. new[] { root }.OfType<T>().Concat(root.GetLogicalDescendants().OfType<T>())];

    /// <summary>
    /// All the text inside <paramref name="root"/>, in tree order, one text block per line.
    /// </summary>
    public static string Text(Control root)
        => string.Join('\n', Descendants<TextBlock>(root).Select(Text));

    /// <summary>
    /// The text of one text block, from its inlines when it has them and from its
    /// <see cref="TextBlock.Text"/> when it does not.
    /// </summary>
    public static string Text(TextBlock block)
    {
        if (block.Inlines is not { Count: > 0 } inlines)
        {
            return block.Text ?? string.Empty;
        }

        StringBuilder text = new();
        Append(inlines, text);

        return text.ToString();
    }

    /// <summary>
    /// The code chips inside <paramref name="block"/>, at any depth.
    /// </summary>
    /// <remarks>
    /// By the class the renderer puts on them, never by their text: a chip's text carries the
    /// padding that makes it a chip (see <see cref="MarkdownCodePads"/>), so a test looking for the
    /// run that says <c>code</c> would not find one.
    /// </remarks>
    public static IReadOnlyList<Run> CodeSpans(TextBlock block)
        => [.. Inlines<Run>(block).Where(run => run.Classes.Contains(MarkdownPreviewTheme.CodeSpanClass))];

    /// <summary>
    /// The text of <paramref name="run"/> as the document wrote it — without the padding, when it is
    /// a code chip.
    /// </summary>
    public static string Unpadded(Run run)
    {
        string text = run.Text ?? string.Empty;

        return run.Classes.Contains(MarkdownPreviewTheme.CodeSpanClass)
            && (text.Length >= MarkdownCodePads.Pad.Length * 2)
                ? text[MarkdownCodePads.Pad.Length..^MarkdownCodePads.Pad.Length]
                : text;
    }

    /// <summary>
    /// Every inline of type <typeparamref name="T"/> inside <paramref name="block"/>, at any depth.
    /// </summary>
    public static IReadOnlyList<T> Inlines<T>(TextBlock block)
        where T : Inline
    {
        List<T> found = [];
        Collect(block.Inlines, found);

        return found;
    }

    private static void Collect<T>(InlineCollection? inlines, List<T> found)
        where T : Inline
    {
        if (inlines is null)
        {
            return;
        }

        foreach (Inline inline in inlines)
        {
            if (inline is T match)
            {
                found.Add(match);
            }

            if (inline is Span span)
            {
                Collect(span.Inlines, found);
            }
        }
    }

    private static void Append(InlineCollection? inlines, StringBuilder text)
    {
        if (inlines is null)
        {
            return;
        }

        foreach (Inline inline in inlines)
        {
            switch (inline)
            {
                // Unpadded, because what a block "reads as" is what the document said: the two
                // characters either side of a chip are this renderer's, and a test asserting a
                // sentence should no more see them than the reader's clipboard does.
                case Run run:
                    text.Append(Unpadded(run));
                    break;
                case LineBreak:
                    text.Append('\n');
                    break;

                // Bold, Italic and Underline are all Spans, so this arm covers every nesting the
                // renderer produces.
                case Span span:
                    Append(span.Inlines, text);
                    break;
                default:
                    break;
            }
        }
    }
}
