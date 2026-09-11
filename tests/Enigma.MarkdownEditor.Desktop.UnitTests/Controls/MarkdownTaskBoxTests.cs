using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Enigma.MarkdownEditor.Desktop.Controls;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Controls;

/// <summary>
/// The task boxes in the preview: what each one remembers about the document it was drawn from, and
/// what ticking one reports back.
/// </summary>
/// <remarks>
/// <para>
/// Structural like the rest of the renderer's suite, with two exceptions that earn their keep: the
/// pointer and <c>Space</c> are driven through the headless window rather than by assigning
/// <see cref="ToggleButton.IsChecked"/>, because "a click and a key do the same thing" is the claim,
/// and a test that sets the property itself proves neither of them.
/// </para>
/// <para>
/// Everything else does assign the property, which is where every one of those paths ends up anyway
/// — and is the only way to reach a case like a handler that throws without also depending on how a
/// button decides it was pressed.
/// </para>
/// </remarks>
public sealed class MarkdownTaskBoxTests
{
    [AvaloniaFact]
    public void ATaskItem_IsMarkedWithARealBoxShowingWhatTheDocumentSays()
    {
        IReadOnlyList<CheckBox> boxes = Boxes(PreviewTree.Preview("- [ ] todo\n- [x] done"));

        Assert.Equal([false, true], boxes.Select(box => box.IsChecked));
    }

    [AvaloniaFact]
    public void EachBox_RecordsTheOffsetOfTheCharacterBetweenItsBrackets()
    {
        const string markdown = "- [ ] todo\n- [x] done";

        Assert.Equal(
            [markdown.IndexOf("[ ]", StringComparison.Ordinal) + 1, markdown.IndexOf("[x]", StringComparison.Ordinal) + 1],
            Boxes(PreviewTree.Preview(markdown)).Select(MarkdownTaskBox.GetSourceOffset));
    }

    [AvaloniaFact]
    public void ANestedOrQuotedTask_RecordsWhereItIsInTheWholeDocument()
    {
        // Offsets into the document, not into the list or the quote the item sits in: the buffer a
        // toggle is written into knows nothing about either.
        const string markdown = "- [x] outer\n  - [ ] nested\n\n> - [X] quoted";

        IReadOnlyList<CheckBox> boxes = Boxes(PreviewTree.Preview(markdown));

        Assert.Equal([true, false, true], boxes.Select(box => box.IsChecked));
        Assert.Equal(
            [
                markdown.IndexOf("[x]", StringComparison.Ordinal) + 1,
                markdown.IndexOf("[ ]", StringComparison.Ordinal) + 1,
                markdown.IndexOf("[X]", StringComparison.Ordinal) + 1,
            ],
            boxes.Select(MarkdownTaskBox.GetSourceOffset));
    }

    [AvaloniaFact]
    public void ACheckboxThisRendererDidNotBuild_StandsForNothingInAnyDocument()
        => Assert.Equal(MarkdownTaskBox.NoOffset, MarkdownTaskBox.GetSourceOffset(new CheckBox()));

    [AvaloniaFact]
    public void TickingABox_ReportsWhereItsCharacterIsAndWhatWasAskedOfIt()
    {
        const string markdown = "- [ ] todo";
        MarkdownPreview preview = PreviewTree.Preview(markdown);

        List<MarkdownTaskToggledEventArgs> reported = [];
        preview.TaskToggled += (_, toggle) =>
        {
            reported.Add(toggle);
            toggle.Applied = true;
        };

        CheckBox box = Boxes(preview)[0];
        box.IsChecked = true;

        MarkdownTaskToggledEventArgs only = Assert.Single(reported);

        Assert.Equal(markdown.IndexOf("[ ]", StringComparison.Ordinal) + 1, only.SourceOffset);
        Assert.True(only.IsChecked);
        Assert.True(box.IsChecked);
    }

    [AvaloniaFact]
    public void UntickingABox_AsksForTheOppositeState()
    {
        MarkdownPreview preview = PreviewTree.Preview("- [x] done");

        List<bool> asked = [];
        preview.TaskToggled += (_, toggle) =>
        {
            asked.Add(toggle.IsChecked);
            toggle.Applied = true;
        };

        Boxes(preview)[0].IsChecked = false;

        Assert.Equal([false], asked);
    }

    [AvaloniaFact]
    public void AToggleNobodyApplied_PutsTheBoxBackWithoutAskingASecondTime()
    {
        MarkdownPreview preview = PreviewTree.Preview("- [ ] todo");

        List<bool> asked = [];

        // Applied deliberately left false: the offset no longer holds a task box, so the document
        // refused it.
        preview.TaskToggled += (_, toggle) => asked.Add(toggle.IsChecked);

        CheckBox box = Boxes(preview)[0];
        box.IsChecked = true;

        Assert.False(box.IsChecked);

        // Putting the box back is a state change of its own, and reported as a second toggle it
        // would ask for exactly the character the refusal declined to write.
        Assert.Equal([true], asked);
    }

    [AvaloniaFact]
    public void APreviewNobodyIsListeningTo_LeavesItsBoxesShowingWhatTheDocumentSays()
    {
        CheckBox box = Boxes(PreviewTree.Preview("- [ ] todo"))[0];

        box.IsChecked = true;

        Assert.False(box.IsChecked);
    }

    [AvaloniaFact]
    public void AHandlerThatThrows_PutsTheBoxBackAndIsReportedAsAFailure()
    {
        MarkdownPreview preview = PreviewTree.Preview("- [x] done");

        List<Exception> failures = [];
        preview.RenderFailed += (_, failure) => failures.Add(failure);
        preview.TaskToggled += (_, _) => throw new InvalidOperationException("the handler broke");

        CheckBox box = Boxes(preview)[0];
        box.IsChecked = false;

        // Whether the character was written is unknowable from here, so the box shows what the last
        // render said and the one 250ms behind it will show what the document really holds.
        Assert.True(box.IsChecked);
        Assert.Single(failures);
    }

    [AvaloniaFact]
    public void ClickingABox_TicksItAndReportsIt()
    {
        (MarkdownPreview preview, Window host) = Hosted("- [ ] todo");

        List<int> offsets = [];
        preview.TaskToggled += (_, toggle) =>
        {
            offsets.Add(toggle.SourceOffset);
            toggle.Applied = true;
        };

        CheckBox box = Boxes(preview)[0];
        host.UpdateLayout();

        Point centre = box.TranslatePoint(new Point(box.Bounds.Width / 2, box.Bounds.Height / 2), host)!.Value;
        host.MouseDown(centre, MouseButton.Left);
        host.MouseUp(centre, MouseButton.Left);

        Assert.True(box.IsChecked);
        Assert.Equal([3], offsets);
    }

    [AvaloniaFact]
    public void SpaceOnAFocusedBox_DoesWhatAClickDoes()
    {
        (MarkdownPreview preview, Window host) = Hosted("- [ ] todo");

        List<int> offsets = [];
        preview.TaskToggled += (_, toggle) =>
        {
            offsets.Add(toggle.SourceOffset);
            toggle.Applied = true;
        };

        CheckBox box = Boxes(preview)[0];
        host.UpdateLayout();

        // The glyph this replaced could not be reached at all. A focusable box is the whole of the
        // keyboard story, because Space on one is already a click as far as the framework is
        // concerned.
        Assert.True(box.Focus());

        host.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        host.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);

        Assert.True(box.IsChecked);
        Assert.Equal([3], offsets);
    }

    [AvaloniaFact]
    public void ATaskAndABulletInOneList_KeepTheSameMarkerColumnAndTheSameRowHeight()
    {
        (MarkdownPreview preview, Window host) = Hosted("- [ ] task\n- bullet");

        host.UpdateLayout();

        IReadOnlyList<Grid> rows = [.. PreviewTree.Descendants<Grid>(preview.Blocks[0])];

        Assert.Equal(2, rows.Count);

        // The box is a control where the bullet was a glyph, and a form's checkbox is padded and a
        // good deal taller than a line of prose. Left as it comes, a list of tasks would be spaced
        // differently from every other list and a mixed one would step at the first task.
        Assert.Equal(rows[1].Children[1].Bounds.Left, rows[0].Children[1].Bounds.Left);
        Assert.Equal(rows[1].Bounds.Height, rows[0].Bounds.Height, tolerance: 1);
    }

    [AvaloniaTheory]
    [InlineData(14, 1.5)]
    [InlineData(18, 1.5)]
    [InlineData(32, 1.9)]
    public void ATaskBox_IsCentredOnTheFirstLineOfItsOwnText(double bodyFontSize, double lineHeight)
    {
        // Three points on the two settings the reader can move, because the defect was not a
        // constant offset: a top-aligned box is out by half the difference between the line box and
        // itself, so the error grew with the size and with the line height together. 18 x 1.5 is
        // what a fresh settings file gives the preview, and 32 x 1.9 is both settings well up.
        MarkdownPreviewTypography type =
            PreviewTree.Typography with { BodyFontSize = bodyFontSize, LineHeight = lineHeight };

        Grid row = Rows(Hosted("- [ ] a task with some text beside it", type))[0];

        Control marker = (Control)row.Children[0];
        Control content = (Control)row.Children[1];

        // The text of a line sits centred in its own line box rather than at the top of it, so the
        // centre of the first line is half a line height down from where the prose column starts.
        Assert.Equal(
            content.Bounds.Top + (type.LineHeightAt(bodyFontSize) / 2),
            marker.Bounds.Top + (marker.Bounds.Height / 2),
            tolerance: 1);
    }

    [AvaloniaFact]
    public void TheBox_IsDrawnToTheScaleOfTheProseBesideIt()
    {
        double small = BoxSize(8);
        double shipped = BoxSize(18);
        double large = BoxSize(32);

        Assert.True(small < shipped, $"A box of {small} at 8pt is not smaller than the {shipped} at 18pt.");
        Assert.True(shipped < large, $"A box of {shipped} at 18pt is not smaller than the {large} at 32pt.");

        // 18pt is what a fresh settings file gives the preview and 20 is what this renderer drew a
        // box at before the box joined the type scale. The default has to come out unchanged, or
        // sizing the box would be a visible change to a preview nobody complained about.
        Assert.Equal(20, shipped, tolerance: 0.5);
    }

    [AvaloniaTheory]
    [InlineData(14)]
    [InlineData(32)]
    public void ATaskABulletAndANumber_StartAtTheSameX(double bodyFontSize)
    {
        // The marker column is shared, so growing the box has to move the bullet and the number with
        // it: a box wider than the old 22 would otherwise indent every task one step past the
        // ordinary item under it. 32pt is a size whose box does outgrow that floor.
        MarkdownPreviewTypography type = PreviewTree.Typography with { BodyFontSize = bodyFontSize };

        MarkdownPreview preview = Hosted("- [ ] task\n- bullet\n\n1. numbered", type);

        IReadOnlyList<Grid> rows =
            [.. preview.Blocks.SelectMany(block => PreviewTree.Descendants<Grid>(block))];

        Assert.Equal(3, rows.Count);
        Assert.Single(rows.Select(row => row.Children[0].Bounds.Left).Distinct());
        Assert.Single(rows.Select(row => row.Children[1].Bounds.Left).Distinct());
    }

    /// <summary>
    /// The height of the box a preview set at <paramref name="bodyFontSize"/> draws — which is the
    /// size of the box itself, the wrapper being sized by the scale it applies to it.
    /// </summary>
    private static double BoxSize(double bodyFontSize)
        => Rows(Hosted("- [ ] task", PreviewTree.Typography with { BodyFontSize = bodyFontSize }))[0]
            .Children[0]
            .Bounds
            .Height;

    private static IReadOnlyList<Grid> Rows(MarkdownPreview preview)
        => PreviewTree.Descendants<Grid>(preview.Blocks[0]);

    private static (MarkdownPreview Preview, Window Host) Hosted(string markdown)
    {
        MarkdownPreview preview = new();
        Window host = PreviewTree.Host(preview);

        preview.Typography = PreviewTree.Typography;
        preview.Markdown = markdown;

        return (preview, host);
    }

    /// <summary>
    /// The same, laid out and set in <paramref name="typography"/>, for a test that measures rather
    /// than one that clicks.
    /// </summary>
    private static MarkdownPreview Hosted(string markdown, MarkdownPreviewTypography typography)
    {
        MarkdownPreview preview = new();
        Window host = PreviewTree.Host(preview);

        preview.Typography = typography;
        preview.Markdown = markdown;

        host.UpdateLayout();

        return preview;
    }

    private static IReadOnlyList<CheckBox> Boxes(MarkdownPreview preview)
        => PreviewTree.Descendants<CheckBox>(preview);
}
