using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Enigma.MarkdownEditor.Core.Markdown;
using Enigma.MarkdownEditor.Desktop.Controls;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Controls;

/// <summary>
/// The fixture corpus: every file in <c>Fixtures/</c>, rendered.
/// </summary>
/// <remarks>
/// <para>
/// These files are the manual visual checklist as well — they are opened in the running editor in
/// each theme variant, and what they look like is recorded in the phase's completion doc. What is
/// asserted here is the half a test can hold: that every one of them renders, that nothing in any of
/// them produces the inline error block, and that none of them reports a failure.
/// </para>
/// <para>
/// A fixture is also the cheapest possible regression net for the phases still to come: each phase
/// grows the files it draws something new in rather than adding files of its own, so a change that
/// breaks the blocks around that new thing fails here.
/// </para>
/// </remarks>
public sealed class FixtureCorpusTests
{
    private const string FixtureFolder = "Fixtures";

    public static TheoryData<string> Fixtures()
    {
        TheoryData<string> data = [];

        foreach (string name in FixtureNames())
        {
            data.Add(name);
        }

        return data;
    }

    [AvaloniaTheory]
    [MemberData(nameof(Fixtures))]
    public void AFixture_RendersWithoutAnErrorBlockOrAReportedFailure(string fileName)
    {
        string path = Path.Combine(Folder(), fileName);

        MarkdownPreview preview = PreviewTree.Preview(null, Folder());
        List<Exception> failures = [];
        preview.RenderFailed += (_, failure) => failures.Add(failure);

        preview.Markdown = File.ReadAllText(path);

        Assert.NotEmpty(preview.Blocks);
        Assert.Empty(failures);
        Assert.DoesNotContain(
            preview.Blocks,
            block => block.Classes.Contains(MarkdownPreviewTheme.ErrorBlockClass));
    }

    [AvaloniaFact]
    public void TheCorpus_CoversEveryBlockKindThePhaseRenders()
    {
        // A guard on the corpus itself rather than on the renderer: a fixture file quietly deleted or
        // renamed would otherwise take its coverage with it and leave a green suite behind.
        IReadOnlyList<string> names = FixtureNames();

        Assert.Equal(10, names.Count);

        foreach (string expected in new[]
        {
            "headings", "emphasis", "lists", "tasks", "quotes", "tables", "fences", "images",
            "front-matter", "links",
        })
        {
            Assert.Contains(names, name => name.Contains(expected, StringComparison.Ordinal));
        }
    }

    [AvaloniaFact]
    public void TheImageFixture_IsAllPlaceholdersRatherThanASilentGap()
    {
        // Nothing in the corpus ships an actual image file, so every link in it must come out as the
        // bordered placeholder — which is also what makes the manual pass on that fixture meaningful.
        IReadOnlyList<Control> blocks = PreviewTree.Blocks(
            File.ReadAllText(Path.Combine(Folder(), "08-images.md")),
            Folder());

        IReadOnlyList<Border> placeholders = [.. blocks.SelectMany(PreviewTree.Descendants<Border>)];

        Assert.Equal(6, placeholders.Count);
        Assert.All(placeholders, border => Assert.NotEmpty(PreviewTree.Text(border)));
    }

    [AvaloniaTheory]
    [MemberData(nameof(Fixtures))]
    public void EveryLineOfAFixture_TracksThePreviewToItsOwnBlock(string fileName)
    {
        string markdown = File.ReadAllText(Path.Combine(Folder(), fileName));

        MarkdownPreview preview = new();
        Window host = PreviewTree.Host(preview);

        preview.BasePath = Folder();
        preview.Markdown = markdown;

        // The offsets are all zero until a pass has run, which is exactly how this test could be
        // green over a sync that never moves anything.
        host.UpdateLayout();

        ScrollViewer scroll = PreviewTree.Descendants<ScrollViewer>(preview)[0];
        int lines = markdown.AsSpan().Count('\n') + 1;

        for (int line = 1; line <= lines; line++)
        {
            preview.ScrollToSourceLine(line);

            Assert.True(preview.LineMap.TryFind(line, out MarkdownLinePosition position));

            Control block = preview.Blocks[position.Index];
            double top = scroll.Offset.Y;
            double bottom = top + scroll.Viewport.Height;

            // On screen, rather than exactly at the top: near the end of a document the view runs out
            // of room to scroll, and what the reader needs is to see what the editor is showing them.
            Assert.True(
                (block.Bounds.Y < (bottom + 1)) && (block.Bounds.Bottom > (top - 1)),
                $"line {line} of {fileName} left its block ({block.Bounds.Y}–{block.Bounds.Bottom}) "
                    + $"outside the view ({top}–{bottom})");
        }
    }

    private static IReadOnlyList<string> FixtureNames()
        => [.. Directory
            .EnumerateFiles(Folder(), "*.md")
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(name => name, StringComparer.Ordinal)];

    private static string Folder() => Path.Combine(AppContext.BaseDirectory, FixtureFolder);
}
