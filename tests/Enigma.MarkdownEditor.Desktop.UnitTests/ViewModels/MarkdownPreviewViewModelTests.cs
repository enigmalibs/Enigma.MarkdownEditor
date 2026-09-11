using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Documents;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Desktop.Controls;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.ViewModels;

/// <summary>
/// What the preview pane follows, and what it deliberately does not.
/// </summary>
/// <remarks>
/// Through a real <see cref="DocumentManagerViewModel"/> over real files, like the rest of the editor
/// suite: which text and which folder reach the renderer is exactly the question, and both come off a
/// document that was actually opened.
/// </remarks>
public sealed class MarkdownPreviewViewModelTests
{
    [Fact]
    public void WithNothingOpen_ThereIsNothingToPreview()
    {
        using EditorHarness harness = new();
        using MarkdownPreviewViewModel preview = Create(harness);

        Assert.Null(preview.Markdown);
        Assert.Null(preview.BasePath);
        Assert.False(preview.HasDocument);
    }

    [Fact]
    public Task OpeningADocument_HandsItsTextAndItsFolderToTheRenderer()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            string path = harness.Write("note.md", "# A note\n\nWith a body.\n");
            await harness.Documents.OpenAsync(path);

            Assert.Equal("# A note\n\nWith a body.\n", preview.Markdown);
            Assert.Equal(harness.Root, preview.BasePath);
            Assert.True(preview.HasDocument);
        });

    [Fact]
    public Task SwitchingTabs_SwitchesWhatIsPreviewed()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await harness.Documents.OpenAsync(harness.Write("first.md", "first\n"));
            await harness.Documents.OpenAsync(harness.Write("second.md", "second\n"));

            Assert.Equal("second\n", preview.Markdown);

            harness.Documents.ActiveDocument = harness.Documents.Documents[0];

            Assert.Equal("first\n", preview.Markdown);
        });

    [Fact]
    public Task Typing_DoesNotReachTheRendererUntilTheEditorHasBeenIdle()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await harness.Documents.OpenAsync(harness.Write("note.md", "body\n"));
            harness.Documents.ActiveDocument!.Document.Insert(0, "more ");

            // Still the text the tab was opened with: a render per keystroke would rebuild the whole
            // visual tree on every character typed.
            Assert.Equal("body\n", preview.Markdown);
            Assert.True(harness.Idle.IsScheduled(preview));
            Assert.Equal(MarkdownPreviewViewModel.LiveUpdateDelay, harness.Idle.DelayFor(preview));

            await harness.Idle.FireAsync(preview);

            Assert.Equal("more body\n", preview.Markdown);
        });

    [Fact]
    public Task TypingAgain_PushesThePendingRenderBackRatherThanAddingASecond()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await harness.Documents.OpenAsync(harness.Write("note.md", "body\n"));

            harness.Documents.ActiveDocument!.Document.Insert(0, "a");
            harness.Documents.ActiveDocument.Document.Insert(0, "b");
            harness.Documents.ActiveDocument.Document.Insert(0, "c");

            // One pending render for the pane however much was typed into it: the timer is pushed
            // back, not stacked.
            Assert.True(harness.Idle.IsScheduled(preview));
            Assert.Equal(1, harness.Idle.ScheduledCount);

            await harness.Idle.FireAsync(preview);

            Assert.Equal("cbabody\n", preview.Markdown);
        });

    [Fact]
    public Task AFiredRefresh_PublishesOnlyTheTextAndOnlyOnce()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await harness.Documents.OpenAsync(harness.Write("note.md", "body\n"));
            harness.Documents.ActiveDocument!.Document.Insert(0, "x");

            List<string?> raised = [];
            preview.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            await harness.Idle.FireAsync(preview);

            // The folder and the path have not moved, and each assignment is a render. CanCopy is
            // derived from the text and renders nothing, so it rides along once at the end.
            Assert.Equal(
                [nameof(MarkdownPreviewViewModel.Markdown), nameof(MarkdownPreviewViewModel.CanCopy)],
                raised);
        });

    [Fact]
    public Task SwitchingTabs_DropsAPendingRenderOfTheTabBeingLeft()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await harness.Documents.OpenAsync(harness.Write("first.md", "first\n"));
            await harness.Documents.OpenAsync(harness.Write("second.md", "second\n"));

            harness.Documents.ActiveDocument = harness.Documents.Documents[0];
            harness.Documents.ActiveDocument!.Document.Insert(0, "edited ");

            harness.Documents.ActiveDocument = harness.Documents.Documents[1];

            // Fired afterwards, the pending refresh would publish the first tab's text over the
            // second tab's — so switching has to cancel it rather than let it land.
            Assert.False(harness.Idle.IsScheduled(preview));
            Assert.Equal("second\n", preview.Markdown);
        });

    [Fact]
    public Task ADisposedPane_StopsFollowingTyping()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await harness.Documents.OpenAsync(harness.Write("note.md", "body\n"));

            DocumentViewModel document = harness.Documents.ActiveDocument!;
            preview.Dispose();

            document.Document.Insert(0, "after disposal ");

            Assert.False(harness.Idle.IsScheduled(preview));
        });

    [Fact]
    public Task ClosingTheLastTab_TakesThePreviewBackToItsEmptyState()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await harness.Documents.OpenAsync(harness.Write("note.md"));
            await harness.Documents.CloseCommand.ExecuteAsync(null);

            Assert.Null(preview.Markdown);
            Assert.False(preview.HasDocument);
        });

    [Fact]
    public Task ADocumentReloadedFromDisk_IsPreviewedAsItNowIs()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            string path = harness.Write("note.md", "before\n");
            await harness.Documents.OpenAsync(path);

            harness.ChangeOnDisk(path, "after\n");
            await harness.Documents.ReloadCommand.ExecuteAsync(null);

            Assert.Equal("after\n", preview.Markdown);
        });

    [Fact]
    public Task ADocumentThatMovesFolder_ResolvesItsImagesAgainstTheNewOne()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await harness.Documents.OpenAsync(harness.Write("note.md", "![](shot.png)\n"));

            string elsewhere = Path.Combine(harness.Root, "elsewhere");
            Directory.CreateDirectory(elsewhere);
            string moved = Path.Combine(elsewhere, "note.md");
            File.WriteAllText(moved, "![](shot.png)\n");

            // What Save As does: the buffer is the same, the folder every relative link means is not.
            DocumentViewModel document = harness.Documents.ActiveDocument!;
            document.MarkSaved(moved, FileStamp.For(moved), document.Document.Version);

            Assert.Equal(elsewhere, preview.BasePath);
        });

    [Fact]
    public Task Typing_DoesNotRePreviewUntilTheDebounceArrivesInTheNextPhase()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await harness.Documents.OpenAsync(harness.Write("note.md", "before\n"));
            harness.Documents.ActiveDocument!.Document.Text = "after\n";

            // Deliberate, and the boundary of this phase: a render per keystroke is what FEATURE-6054
            // PHASE03's debounce exists to prevent, and this ViewModel is where that timer will live.
            // Until then the preview is what the tab held when it became active.
            Assert.Equal("before\n", preview.Markdown);
        });

    [Fact]
    public Task AfterDisposal_ThePreviewStopsFollowingTheEditor()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            MarkdownPreviewViewModel preview = Create(harness);

            preview.Dispose();

            // Twice, because Dispose must be idempotent — the container disposes it, and so does a
            // test's using.
            preview.Dispose();

            await harness.Documents.OpenAsync(harness.Write("note.md"));

            Assert.Null(preview.Markdown);
            Assert.False(preview.HasDocument);
        });

    [Fact]
    public Task EveryChangeIsAnnouncedOnce()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            List<string?> raised = [];
            preview.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            await harness.Documents.OpenAsync(harness.Write("note.md", "body\n"));

            // The renderer re-renders on every assignment, so a property that announces itself when
            // nothing changed costs a walk of the document. The order is the contract too: the path
            // goes first, so both renders know which document they are drawing, and CanCopy comes
            // last and exactly once — it is derived from two of the others, and announcing it from
            // each of their setters would say the same thing twice for one refresh.
            Assert.Equal(
                [
                    nameof(MarkdownPreviewViewModel.SourcePath),
                    nameof(MarkdownPreviewViewModel.BasePath),
                    nameof(MarkdownPreviewViewModel.Markdown),
                    nameof(MarkdownPreviewViewModel.HasDocument),
                    nameof(MarkdownPreviewViewModel.CanCopy),
                ],
                raised);
        });

    [Fact]
    public void AReportedFailure_IsRecordedRatherThanRethrown()
    {
        using EditorHarness harness = new();
        using MarkdownPreviewViewModel preview = Create(harness);

        // Nothing is shown to the user: the reader already has the bordered error block where the
        // content would have been.
        preview.OnRenderFailed(new InvalidOperationException("a block that could not be built"));

        Assert.Throws<ArgumentNullException>(() => preview.OnRenderFailed(null!));
    }

    [Fact]
    public Task AnExternalLink_GoesToThePlatform()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            FakeDocumentOpener opener = new();
            FakeFileLauncher launcher = new();
            using MarkdownPreviewViewModel preview = Create(harness, opener, launcher);

            await harness.Documents.OpenAsync(harness.Write("note.md", "body\n"));
            await preview.ActivateLinkAsync("https://example.com/docs");

            Assert.Equal(["https://example.com/docs"], launcher.Addresses.Select(address => address.ToString()));
            Assert.Empty(opener.Opened);
        });

    [Fact]
    public Task ARelativeLink_IsOpenedTheWayTheTreeOpensAFile()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            FakeDocumentOpener opener = new();
            FakeFileLauncher launcher = new();
            using MarkdownPreviewViewModel preview = Create(harness, opener, launcher);

            harness.Write("other-note.md", "another note\n");
            await harness.Documents.OpenAsync(harness.Write("note.md", "body\n"));

            await preview.ActivateLinkAsync("./other-note.md");

            // The same service the file tree activates a file through, so a link to a note and a
            // double-click on it in the tree do the same thing — including for a file that is not a
            // note, which that service hands to the operating system.
            Assert.Equal([harness.Combine("other-note.md")], opener.Opened);
            Assert.Empty(launcher.Addresses);
        });

    [Fact]
    public Task ALinkIntoTheDocument_DoesNothingYet()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            FakeDocumentOpener opener = new();
            FakeFileLauncher launcher = new();
            using MarkdownPreviewViewModel preview = Create(harness, opener, launcher);

            await harness.Documents.OpenAsync(harness.Write("note.md", "body\n"));
            await preview.ActivateLinkAsync("#a-heading");

            // Nothing to scroll to until PHASE03 builds the line-to-block index. What it must not do
            // is try to open a file called `#a-heading`.
            Assert.Empty(opener.Opened);
            Assert.Empty(launcher.Addresses);
        });

    [Fact]
    public Task ARelativeLinkWithNothingOpen_IsRefusedRatherThanGuessedAt()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            FakeDocumentOpener opener = new();
            FakeFileLauncher launcher = new();
            using MarkdownPreviewViewModel preview = Create(harness, opener, launcher);

            await preview.ActivateLinkAsync("other-note.md");

            Assert.Empty(opener.Opened);
            Assert.Empty(launcher.Addresses);
        });

    [Fact]
    public Task APlatformThatThrows_IsRecordedRatherThanRethrown()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            FakeFileLauncher launcher = new() { Failure = new InvalidOperationException("no browser") };
            using MarkdownPreviewViewModel preview = Create(harness, launcher: launcher);

            await harness.Documents.OpenAsync(harness.Write("note.md", "body\n"));

            // Called from a pointer handler: an exception out of here is an unhandled one on the UI
            // thread, over a link the reader merely clicked.
            await preview.ActivateLinkAsync("https://example.com");
        });

    [Fact]
    public Task ALinkMustBeGiven()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await Assert.ThrowsAsync<ArgumentNullException>(() => preview.ActivateLinkAsync(null!));
        });

    [Fact]
    public void WithNothingChosen_TheTypographyIsTheSettingsDefaultsWithBothFamiliesResolved()
    {
        using EditorHarness harness = new();
        using MarkdownPreviewViewModel preview = Create(harness);

        MarkdownPreviewTypography typography = preview.Typography;

        // Empty means "automatic", and the two automatics are different questions: prose falls back to
        // the font the interface is drawn in, code to the best fixed-width font this machine has.
        Assert.Equal(TestFonts.UiFamily, typography.FontFamily);
        Assert.Equal(harness.Fonts.DefaultMonospaceFamily, typography.CodeFontFamily);
        Assert.Equal(18, typography.BodyFontSize);
        Assert.Equal(14, typography.CodeFontSize);
        Assert.Equal(1.5, typography.LineHeight);
    }

    [Fact]
    public void AStoredFamilyThisMachineHas_IsUsedAsItIsWritten()
    {
        using EditorHarness harness = new();
        harness.Settings.Value.PreviewFontFamily = TestFonts.Proportional[1];
        harness.Settings.Value.PreviewCodeFontFamily = TestFonts.Monospace[1];

        using MarkdownPreviewViewModel preview = Create(harness);

        Assert.Equal(TestFonts.Proportional[1], preview.Typography.FontFamily);
        Assert.Equal(TestFonts.Monospace[1], preview.Typography.CodeFontFamily);
    }

    [Fact]
    public void AStoredFamilyThisMachineDoesNotHave_RendersWithTheFallbackAndStaysInTheFile()
    {
        using EditorHarness harness = new();
        harness.Settings.Value.PreviewFontFamily = TestFonts.NotInstalled;
        harness.Settings.Value.PreviewCodeFontFamily = TestFonts.NotInstalled;

        using MarkdownPreviewViewModel preview = Create(harness);

        Assert.Equal(TestFonts.UiFamily, preview.Typography.FontFamily);
        Assert.Equal(harness.Fonts.DefaultMonospaceFamily, preview.Typography.CodeFontFamily);

        // Resolved, never corrected: a settings file carried between two machines keeps naming the
        // font the other one has.
        Assert.Equal(TestFonts.NotInstalled, harness.Settings.Value.PreviewFontFamily);
        Assert.Equal(TestFonts.NotInstalled, harness.Settings.Value.PreviewCodeFontFamily);
    }

    [Fact]
    public void OnSettingsChanged_RepublishesTheTypographyAsOneChangeRatherThanOnePerSetting()
    {
        using EditorHarness harness = new();
        using MarkdownPreviewViewModel preview = Create(harness);

        List<string?> raised = [];
        preview.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        harness.Settings.Value.PreviewFontSize = 30;
        harness.Settings.Value.PreviewCodeFontSize = 11;
        harness.Settings.Value.PreviewLineHeight = 2;

        // Nothing has been said yet: the settings window edits the very object this reads, and this
        // side is told once, when the window closes.
        Assert.Empty(raised);

        preview.OnSettingsChanged();

        // One change, because every one the renderer hears is a walk of the whole document.
        Assert.Equal([nameof(MarkdownPreviewViewModel.Typography)], raised);
        Assert.Equal(30, preview.Typography.BodyFontSize);
        Assert.Equal(11, preview.Typography.CodeFontSize);
        Assert.Equal(2, preview.Typography.LineHeight);
    }

    [Fact]
    public Task TickingATask_RewritesTheOneCharacterBetweenItsBrackets()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await harness.Documents.OpenAsync(harness.Write("notes.md", "- [ ] todo\n- [x] done\n"));

            DocumentViewModel document = harness.Documents.Documents[0];

            Assert.True(preview.ToggleTask(StateOffsets(document.Document.Text)[0], isChecked: true));

            // Exactly one character, and the one between the brackets: everything either side of it
            // is the reader's own text.
            Assert.Equal("- [x] todo\n- [x] done\n", document.Document.Text);
            Assert.True(document.IsDirty);
        });

    [Fact]
    public Task UntickingATask_WritesTheSpaceBackInItsPlace()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await harness.Documents.OpenAsync(harness.Write("notes.md", "- [x] done\n"));

            DocumentViewModel document = harness.Documents.Documents[0];

            Assert.True(preview.ToggleTask(StateOffsets(document.Document.Text)[0], isChecked: false));

            Assert.Equal("- [ ] done\n", document.Document.Text);
        });

    [Fact]
    public Task ATick_IsOneEntryOnTheUndoStackRatherThanTwo()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await harness.Documents.OpenAsync(harness.Write("notes.md", "- [ ] todo\n"));

            DocumentViewModel document = harness.Documents.Documents[0];
            preview.ToggleTask(StateOffsets(document.Document.Text)[0], isChecked: true);

            document.Document.UndoStack.Undo();

            // A tick is an edit of the buffer like any other, so Ctrl+Z in the editor takes it back
            // in one step and the tab is clean again.
            Assert.Equal("- [ ] todo\n", document.Document.Text);
            Assert.False(document.IsDirty);
        });

    [Fact]
    public Task ANestedOrQuotedTask_TogglesItsOwnCharacterAndNoOther()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await harness.Documents.OpenAsync(
                harness.Write("notes.md", "- [ ] outer\n  - [ ] nested\n\n> - [ ] quoted\n"));

            DocumentViewModel document = harness.Documents.Documents[0];

            // Each replacement is one character for one character, so the offsets taken before the
            // first of them are still the right ones for the last.
            foreach (int offset in StateOffsets(document.Document.Text))
            {
                Assert.True(preview.ToggleTask(offset, isChecked: true));
            }

            Assert.Equal("- [x] outer\n  - [x] nested\n\n> - [x] quoted\n", document.Document.Text);
        });

    [Fact]
    public Task AnOffsetThatNoLongerHoldsATaskBox_ChangesNothingAtAll()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            // What a render of `- [ ] todo` would have recorded, over a buffer typed over since. The
            // preview is rebuilt a quarter of a second behind the editor, so this is an ordinary
            // click and not a rare one.
            await harness.Documents.OpenAsync(harness.Write("notes.md", "an ordinary sentence\n"));

            DocumentViewModel document = harness.Documents.Documents[0];

            Assert.False(preview.ToggleTask(3, isChecked: true));

            Assert.Equal("an ordinary sentence\n", document.Document.Text);
            Assert.False(document.IsDirty);
        });

    [Fact]
    public Task AnOffsetOutsideTheBufferOrBesideTheWrongCharacters_IsRefusedRatherThanClamped()
        => UiThread.RunAsync(async () =>
        {
            const string text = "- [ ] todo\n";

            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await harness.Documents.OpenAsync(harness.Write("notes.md", text));

            DocumentViewModel document = harness.Documents.Documents[0];

            // Either end of the buffer, past both of them, and the two positions inside the box that
            // are not the state character. Only offset 3 is a task box.
            foreach (int offset in new[] { -1, 0, 2, 4, text.Length - 1, text.Length, text.Length + 50 })
            {
                Assert.False(preview.ToggleTask(offset, isChecked: true));
            }

            Assert.Equal(text, document.Document.Text);
            Assert.False(document.IsDirty);
        });

    [Fact]
    public void TogglingWithNothingOpen_IsRefusedRatherThanThrown()
    {
        using EditorHarness harness = new();
        using MarkdownPreviewViewModel preview = Create(harness);

        Assert.False(preview.ToggleTask(3, isChecked: true));
    }

    [Fact]
    public Task WithAutosaveOn_ATickIsWrittenByThePathEveryOtherEditGoesThrough()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            harness.Settings.Value.AutosaveEnabled = true;

            using MarkdownPreviewViewModel preview = Create(harness);

            await harness.Documents.OpenAsync(harness.Write("notes.md", "- [ ] todo\n"));

            DocumentViewModel document = harness.Documents.Documents[0];

            Assert.True(preview.ToggleTask(StateOffsets(document.Document.Text)[0], isChecked: true));

            // No save of its own: the buffer changed, so the document manager's own autosave timer is
            // armed exactly as it would be for a keystroke, and that is what writes the file.
            Assert.True(harness.Idle.IsScheduled(document));

            await harness.Idle.FireAsync(document);

            Assert.Equal("- [x] todo\n", harness.ReadRaw("notes.md"));
        });

    [Fact]
    public Task CopyingTheWholeNote_WritesItAsThePaneReadsIt()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            FakeClipboardWriter clipboard = new();
            using MarkdownPreviewViewModel preview = Create(harness, clipboard: clipboard);

            await harness.Documents.OpenAsync(
                harness.Write("note.md", "# A note\n\nWith **bold** in it.\n\n- an item\n"));

            await preview.CopyAsync(MarkdownCopyRequest.WholeDocument);

            // The projection, not the markdown: what the reader is looking at is the rendered pane,
            // and the editor's own Ctrl+A already copies the source.
            Assert.Equal(
                string.Join(Environment.NewLine, "A note", string.Empty, "With bold in it.", string.Empty, "• an item"),
                clipboard.Last);
        });

    [Fact]
    public Task CopyingASelection_WritesExactlyWhatWasSelected()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            FakeClipboardWriter clipboard = new();
            using MarkdownPreviewViewModel preview = Create(harness, clipboard: clipboard);

            await harness.Documents.OpenAsync(harness.Write("note.md", "# A note\n\nWith bold in it.\n"));

            // Text off the screen already, so it goes through untouched — no second projection, and
            // no chance of the copy disagreeing with the highlight the reader can see.
            await preview.CopyAsync(MarkdownCopyRequest.ForSelection("bold in it"));

            Assert.Equal("bold in it", clipboard.Last);
        });

    [Fact]
    public Task CopyingTheWholeNote_AlsoWritesItAsHtml()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            FakeClipboardWriter clipboard = new();
            using MarkdownPreviewViewModel preview = Create(harness, clipboard: clipboard);

            await harness.Documents.OpenAsync(
                harness.Write("note.md", "# A note\n\nWith **bold** in it.\n\n| a |\n|---|\n| 1 |\n"));

            await preview.CopyAsync(MarkdownCopyRequest.WholeDocument);

            // Both flavours in one write, because a second write would replace the first rather than
            // sit beside it. Which one is used is the receiving application's choice at paste time.
            Assert.Single(clipboard.Written);
            Assert.True(clipboard.LastCarriedHtml);
            Assert.Contains("<h1>A note</h1>", clipboard.LastHtml, StringComparison.Ordinal);
            Assert.Contains("<strong>bold</strong>", clipboard.LastHtml, StringComparison.Ordinal);
            Assert.Contains("<table>", clipboard.LastHtml, StringComparison.Ordinal);
        });

    [Fact]
    public Task CopyingASelection_WritesNoHtmlFlavour()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            FakeClipboardWriter clipboard = new();
            using MarkdownPreviewViewModel preview = Create(harness, clipboard: clipboard);

            await harness.Documents.OpenAsync(harness.Write("note.md", "# A note\n\nWith bold in it.\n"));

            await preview.CopyAsync(MarkdownCopyRequest.ForSelection("bold in it"));

            // Deliberate, and the one asymmetry in the feature: rendering an arbitrary character
            // range as HTML means mapping it back to the markdown that produced it, and the pane
            // renders in one direction only.
            Assert.False(clipboard.LastCarriedHtml);
        });

    [Fact]
    public Task TheHtmlFlavour_ResolvesImagesAgainstTheNotesOwnFolder()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            FakeClipboardWriter clipboard = new();
            using MarkdownPreviewViewModel preview = Create(harness, clipboard: clipboard);

            string path = harness.Write("note.md", "![a shot](assets/shot.png)\n");

            await harness.Documents.OpenAsync(path);
            await preview.CopyAsync(MarkdownCopyRequest.WholeDocument);

            // BasePath is what makes the paste survive leaving this process: a relative src means
            // nothing inside somebody else's document.
            string expected = new Uri(Path.Combine(Path.GetDirectoryName(path)!, "assets", "shot.png")).AbsoluteUri;

            Assert.Contains($"src=\"{expected}\"", clipboard.LastHtml, StringComparison.Ordinal);
        });

    [Fact]
    public void WithNothingOpen_ThereIsNothingToCopy()
    {
        using EditorHarness harness = new();
        FakeClipboardWriter clipboard = new();
        using MarkdownPreviewViewModel preview = Create(harness, clipboard: clipboard);

        Assert.False(preview.CanCopy);
    }

    [Fact]
    public Task WithNothingOpen_ACopyWritesNothingAtAll()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            FakeClipboardWriter clipboard = new();
            using MarkdownPreviewViewModel preview = Create(harness, clipboard: clipboard);

            await preview.CopyAsync(MarkdownCopyRequest.WholeDocument);

            Assert.Empty(clipboard.Written);
        });

    [Fact]
    public Task ANoteOfNothingButBlankLines_HasNothingToCopy()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await harness.Documents.OpenAsync(harness.Write("empty.md", "\n\n   \n"));

            Assert.True(preview.HasDocument);
            Assert.False(preview.CanCopy);
        });

    [Fact]
    public Task OpeningANote_SaysThatItCanNowBeCopied()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            List<string?> changed = [];
            preview.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            await harness.Documents.OpenAsync(harness.Write("note.md", "# A note\n"));

            // The button and the menu entries are bound to it, so the change has to be announced or
            // they stay disabled over a note that is plainly there.
            Assert.True(preview.CanCopy);
            Assert.Contains(nameof(MarkdownPreviewViewModel.CanCopy), changed);
        });

    [Fact]
    public Task APlatformWithNoClipboard_IsRecordedAndNeverShown()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            FakeClipboardWriter clipboard = new() { Declines = true };
            RecordingLogger<MarkdownPreviewViewModel> logger = new();
            using MarkdownPreviewViewModel preview = Create(harness, clipboard: clipboard, logger: logger);

            await harness.Documents.OpenAsync(harness.Write("note.md", "# A note\n"));

            await preview.CopyAsync(MarkdownCopyRequest.WholeDocument);

            Assert.Empty(clipboard.Written);
            Assert.Single(logger.At(LogLevel.Warning));
        });

    [Fact]
    public Task AClipboardThatThrows_IsRecordedAndNeverShown()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            InvalidOperationException failure = new("another application is holding the clipboard");
            FakeClipboardWriter clipboard = new() { Failure = failure };
            RecordingLogger<MarkdownPreviewViewModel> logger = new();
            using MarkdownPreviewViewModel preview = Create(harness, clipboard: clipboard, logger: logger);

            await harness.Documents.OpenAsync(harness.Write("note.md", "# A note\n"));

            // Not rethrown: this is reached from a key handler and from a button, where an exception
            // would reach the dispatcher over a copy the reader can simply repeat.
            await preview.CopyAsync(MarkdownCopyRequest.WholeDocument);

            Assert.Same(failure, Assert.Single(logger.At(LogLevel.Warning)).Failure);
        });

    [Fact]
    public Task TheCopyRequest_IsRequired()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            using MarkdownPreviewViewModel preview = Create(harness);

            await Assert.ThrowsAsync<ArgumentNullException>(() => preview.CopyAsync(null!));
        });

    [Fact]
    public void EveryDependency_IsRequired()
    {
        using EditorHarness harness = new();
        FakeDocumentOpener opener = new();
        FakeFileLauncher launcher = new();
        FakeClipboardWriter clipboard = new();
        FakeIdleScheduler idle = harness.Idle;
        NullLogger<MarkdownPreviewViewModel> logger = NullLogger<MarkdownPreviewViewModel>.Instance;

        IConfig<AppSettings> settings = harness.Settings;
        FontCatalog fonts = harness.Fonts;

        Assert.Throws<ArgumentNullException>(
            () => new MarkdownPreviewViewModel(null!, settings, fonts, opener, launcher, clipboard, idle, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MarkdownPreviewViewModel(harness.Documents, null!, fonts, opener, launcher, clipboard, idle, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MarkdownPreviewViewModel(harness.Documents, settings, null!, opener, launcher, clipboard, idle, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MarkdownPreviewViewModel(harness.Documents, settings, fonts, null!, launcher, clipboard, idle, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MarkdownPreviewViewModel(harness.Documents, settings, fonts, opener, null!, clipboard, idle, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MarkdownPreviewViewModel(harness.Documents, settings, fonts, opener, launcher, null!, idle, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MarkdownPreviewViewModel(harness.Documents, settings, fonts, opener, launcher, clipboard, null!, logger));
        Assert.Throws<ArgumentNullException>(
            () => new MarkdownPreviewViewModel(harness.Documents, settings, fonts, opener, launcher, clipboard, idle, null!));
    }

    /// <summary>
    /// Where the state character of every task box in <paramref name="text"/> is — the offset the
    /// renderer records on the box it drew, read here off the source instead so the two halves of
    /// the toggle are asserted independently.
    /// </summary>
    private static IReadOnlyList<int> StateOffsets(string text)
    {
        List<int> offsets = [];

        for (int bracket = text.IndexOf('['); bracket >= 0; bracket = text.IndexOf('[', bracket + 1))
        {
            offsets.Add(bracket + 1);
        }

        return offsets;
    }

    private static MarkdownPreviewViewModel Create(
        EditorHarness harness,
        FakeDocumentOpener? opener = null,
        FakeFileLauncher? launcher = null,
        FakeClipboardWriter? clipboard = null,
        ILogger<MarkdownPreviewViewModel>? logger = null)
        => new(
            harness.Documents,
            harness.Settings,
            harness.Fonts,
            opener ?? new FakeDocumentOpener(),
            launcher ?? new FakeFileLauncher(),
            clipboard ?? new FakeClipboardWriter(),
            harness.Idle,
            logger ?? NullLogger<MarkdownPreviewViewModel>.Instance);
}
