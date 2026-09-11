using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AvaloniaEdit.Document;
using Enigma.MarkdownEditor.Core.Documents;
using Enigma.MarkdownEditor.Core.Vim;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Controls;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.ViewModels;

/// <summary>
/// Tests for the editor pane's lifecycle: opening and de-duplicating tabs, the dirty flag, the save
/// and close matrix including every cancel path, autosave, and what happens when a file changes
/// underneath an open document.
/// </summary>
/// <remarks>
/// Every test runs on <see cref="UiThread"/>. The ViewModels hold an AvaloniaEdit document, which
/// verifies that whoever touches it is the thread that built it — the same rule they run under in
/// the application.
/// </remarks>
public sealed class DocumentManagerViewModelTests
{
    [Fact]
    public Task OpeningAFile_PutsItInATabAndActivatesIt()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string path = harness.Write("notes.md", "# Title\nBody\n");

            await harness.Documents.OpenAsync(path);

            DocumentViewModel document = Assert.Single(harness.Documents.Documents);

            Assert.Same(document, harness.Documents.ActiveDocument);
            Assert.Equal(path, document.FilePath);
            Assert.Equal("notes.md", document.Title);
            Assert.Equal("# Title\nBody\n", document.Document.Text);
            Assert.False(document.IsDirty);
            Assert.True(harness.Documents.HasDocuments);
            Assert.Equal("notes.md", harness.Documents.FileInfoText);
        });

    [Fact]
    public Task OpeningTheSameFileTwice_ActivatesTheTabItIsAlreadyIn()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string first = harness.Write("first.md");
            string second = harness.Write("second.md");

            await harness.Documents.OpenAsync(first);
            await harness.Documents.OpenAsync(second);
            await harness.Documents.OpenAsync(first);

            Assert.Equal(2, harness.Documents.Documents.Count);
            Assert.Equal(first, harness.Documents.ActiveDocument?.FilePath);
        });

    [Fact]
    public Task OpeningTheSameFileByADifferentSpelling_StillFindsTheOpenTab()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string path = harness.Write("notes.md");

            await harness.Documents.OpenAsync(path);

            // The same file reached through a relative segment — which is what a second workspace
            // root over the same folder produces.
            await harness.Documents.OpenAsync(Path.Combine(harness.Root, ".", "notes.md"));

            Assert.Single(harness.Documents.Documents);
        });

    [Fact]
    public Task AFileThatCannotBeRead_IsReportedRatherThanOpened()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();

            await harness.Documents.OpenAsync(harness.Combine("missing.md"));

            Assert.Empty(harness.Documents.Documents);
            Assert.Single(harness.InfoBar.Shown);
        });

    [Fact]
    public Task EditingADocument_MarksItDirtyAndUndoingBackToTheSavedStateClearsIt()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            DocumentViewModel document = harness.Documents.Documents[0];

            document.Document.Insert(document.Document.TextLength, "two\n");
            Assert.True(document.IsDirty);
            Assert.True(harness.Documents.HasUnsavedChanges);

            document.Document.UndoStack.Undo();

            // The whole reason the flag comes from the undo stack rather than from a text-changed
            // counter: back at the saved state, there is nothing to save.
            Assert.False(document.IsDirty);
            Assert.False(harness.Documents.HasUnsavedChanges);
        });

    [Fact]
    public Task EditingInsideAnOpenUndoGroup_MarksTheDocumentDirtyBeforeTheGroupCloses()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            DocumentViewModel document = harness.Documents.Documents[0];

            // Vim's insert mode in one line: the group is opened on 'i' and stays open until Esc.
            // The undo stack only recalculates IsOriginalFile when the depth is back at zero, so a
            // flag taken from it alone is blind for exactly as long as the user is typing.
            document.Document.UndoStack.StartUndoGroup();
            document.Document.Insert(document.Document.TextLength, "two\n");

            Assert.True(document.IsDirty);
            Assert.True(harness.Documents.HasUnsavedChanges);

            document.Document.UndoStack.EndUndoGroup();

            // And closing the group does not take it back: the buffer still differs from the file.
            Assert.True(document.IsDirty);
        });

    [Fact]
    public Task ClosingADocumentEditedInsideAnOpenUndoGroup_AsksBeforeDiscardingTheWork()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            DocumentViewModel document = harness.Documents.Documents[0];

            document.Document.UndoStack.StartUndoGroup();
            document.Document.Insert(0, "edited ");

            harness.Prompts.Choice = UnsavedChangesChoice.Cancel;

            await harness.Documents.CloseCommand.ExecuteAsync(document);

            // The reported symptom: typing in insert mode and closing the tab took the work with it,
            // because nothing had asked the question the answer was needed for.
            Assert.Single(harness.Documents.Documents);
            Assert.Equal(["notes.md"], harness.Prompts.AskedAbout);
        });

    [Fact]
    public Task SavingInsideAnOpenUndoGroup_ClearsTheDirtyFlag()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            DocumentViewModel document = harness.Documents.Documents[0];

            // Ctrl+S in the middle of a vim insert session: the group the session opened is still
            // open, so nothing the undo stack recalculates on its own can report the save.
            document.Document.UndoStack.StartUndoGroup();
            document.Document.Insert(document.Document.TextLength, "two\n");

            await harness.Documents.SaveCommand.ExecuteAsync(null);

            Assert.False(document.IsDirty);
            Assert.False(harness.Documents.HasUnsavedChanges);
            Assert.Equal("one\ntwo\n", harness.ReadRaw("notes.md"));
        });

    [Fact]
    public Task ClosingTheGroupAfterASaveInsideIt_KeepsTheDocumentClean()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            DocumentViewModel document = harness.Documents.Documents[0];

            // Two operations, so closing the group collapses them into one entry and reports the
            // difference - over a counter the save has already put back to zero.
            document.Document.UndoStack.StartUndoGroup();
            document.Document.Insert(document.Document.TextLength, "two\n");
            document.Document.Insert(document.Document.TextLength, "three\n");

            await harness.Documents.SaveCommand.ExecuteAsync(null);

            document.Document.UndoStack.EndUndoGroup();

            // The Esc that ends the insert session must not bring the dot back: the buffer is what
            // is on disk.
            Assert.False(document.IsDirty);

            await harness.Documents.CloseCommand.ExecuteAsync(document);

            Assert.Empty(harness.Prompts.AskedAbout);
            Assert.Empty(harness.Documents.Documents);
        });

    [Fact]
    public Task ASingleEditSavedInsideItsGroup_ThenSavedAgainInNormalMode_IsClean()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            DocumentViewModel document = harness.Documents.Documents[0];

            // One operation, so closing the group collapses nothing and reports nothing at all -
            // the stack goes on believing the file is original while the flag says otherwise.
            document.Document.UndoStack.StartUndoGroup();
            document.Document.Insert(document.Document.TextLength, "two\n");

            await harness.Documents.SaveCommand.ExecuteAsync(null);

            document.Document.UndoStack.EndUndoGroup();

            // Nothing was collapsed and nothing was reported, so the stack still believes the file
            // is original — while the flag, left behind by the save inside the group, says it is not.
            Assert.False(document.IsDirty);

            document.Document.Insert(document.Document.TextLength, "three\n");

            Assert.True(document.IsDirty);

            // The reported "sometimes even in normal mode": a plain save, no group in sight.
            await harness.Documents.SaveCommand.ExecuteAsync(null);

            Assert.False(document.IsDirty);
            Assert.Equal("one\ntwo\nthree\n", harness.ReadRaw("notes.md"));
        });

    [Fact]
    public Task TypingAfterASaveInsideTheGroup_StaysDirtyWhenTheGroupCloses()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            DocumentViewModel document = harness.Documents.Documents[0];

            document.Document.UndoStack.StartUndoGroup();
            document.Document.Insert(document.Document.TextLength, "two\n");

            await harness.Documents.SaveCommand.ExecuteAsync(null);

            // The other half of the correction: an edit after the save is real work, and closing
            // the group over it must leave the dot where it belongs.
            document.Document.Insert(document.Document.TextLength, "three\n");
            document.Document.UndoStack.EndUndoGroup();

            Assert.True(document.IsDirty);

            await harness.Documents.SaveCommand.ExecuteAsync(null);

            Assert.False(document.IsDirty);
            Assert.Equal("one\ntwo\nthree\n", harness.ReadRaw("notes.md"));
        });

    [Fact]
    public Task UndoingAnInsertSessionSavedMidWay_MarksDirtyAndRedoMarksClean()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            DocumentViewModel document = harness.Documents.Documents[0];

            document.Document.UndoStack.StartUndoGroup();
            document.Document.Insert(document.Document.TextLength, "two\n");
            document.Document.Insert(document.Document.TextLength, "three\n");

            await harness.Documents.SaveCommand.ExecuteAsync(null);

            document.Document.UndoStack.EndUndoGroup();

            // The correction puts the save point back at the top of the stack, which is what keeps
            // the undo stack the honest source it is everywhere else: one u, and there is work to
            // save again.
            document.Document.UndoStack.Undo();
            Assert.True(document.IsDirty);

            document.Document.UndoStack.Redo();
            Assert.False(document.IsDirty);
        });

    [Fact]
    public Task AnEditDuringTheWrite_LeavesTheDocumentDirty()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            DocumentViewModel document = harness.Documents.Documents[0];

            // Tested at this seam and not through the command, because nothing in the harness can
            // hold a real file write open: the version is the snapshot the write went out with, and
            // the insert is the keystroke that lands while the disk is busy with it.
            ITextSourceVersion written = document.Document.Version;
            document.Document.Insert(document.Document.TextLength, "two\n");

            document.MarkSaved(document.FilePath, FileStamp.For(document.FilePath), written);

            // What went to disk is a line short of the buffer, so clearing the dot here would lose
            // that line quietly at the next close.
            Assert.True(document.IsDirty);

            document.MarkSaved(document.FilePath, FileStamp.For(document.FilePath), document.Document.Version);

            Assert.False(document.IsDirty);
        });

    [Fact]
    public Task Saving_WritesTheBufferAndClearsTheDirtyFlag()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            DocumentViewModel document = harness.Documents.Documents[0];
            document.Document.Insert(document.Document.TextLength, "two\n");

            await harness.Documents.SaveCommand.ExecuteAsync(null);

            Assert.False(document.IsDirty);
            Assert.Equal("one\ntwo\n", harness.ReadRaw("notes.md"));
        });

    [Fact]
    public Task SavingACrLfFile_KeepsItCrLfIncludingTheLinesJustTyped()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("windows.md", "one\r\n"));

            DocumentViewModel document = harness.Documents.Documents[0];

            // What the buffer holds is line feeds, whatever the file used.
            Assert.Equal("one\n", document.Document.Text);

            document.Document.Insert(document.Document.TextLength, "two\n");
            await harness.Documents.SaveCommand.ExecuteAsync(null);

            Assert.Equal("one\r\ntwo\r\n", harness.ReadRaw("windows.md"));
        });

    [Fact]
    public Task SaveAs_WritesElsewhereAndTheTabFollowsTheFile()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            harness.Prompts.SaveAsPath = harness.Combine("copy.md");

            DocumentViewModel document = harness.Documents.Documents[0];
            document.Document.Insert(document.Document.TextLength, "two\n");

            await harness.Documents.SaveAsCommand.ExecuteAsync(null);

            Assert.Equal(harness.Combine("copy.md"), document.FilePath);
            Assert.Equal("copy.md", document.Title);
            Assert.False(document.IsDirty);
            Assert.Equal("one\ntwo\n", harness.ReadRaw("copy.md"));

            // The original is left exactly as it was — Save As is a copy, not a move.
            Assert.Equal("one\n", harness.ReadRaw("notes.md"));

            // And the watcher follows: what the tab now cares about is the new file.
            Assert.Equal(harness.Combine("copy.md"), harness.Watchers.Created[^1].FilePath);
            Assert.True(harness.Watchers.For(harness.Combine("notes.md")).IsDisposed);
        });

    [Fact]
    public Task SaveAsThatIsCancelled_WritesNothing()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            harness.Prompts.SaveAsPath = null;

            DocumentViewModel document = harness.Documents.Documents[0];
            document.Document.Insert(document.Document.TextLength, "two\n");

            await harness.Documents.SaveAsCommand.ExecuteAsync(null);

            Assert.Equal(1, harness.Prompts.SaveAsCount);
            Assert.True(document.IsDirty);
            Assert.Equal("one\n", harness.ReadRaw("notes.md"));
        });

    [Fact]
    public Task SaveAll_WritesEveryChangedDocumentAndLeavesTheRestAlone()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("a.md", "a\n"));
            await harness.Documents.OpenAsync(harness.Write("b.md", "b\n"));
            await harness.Documents.OpenAsync(harness.Write("c.md", "c\n"));

            harness.Documents.Documents[0].Document.Insert(0, "1");
            harness.Documents.Documents[2].Document.Insert(0, "3");

            await harness.Documents.SaveAllCommand.ExecuteAsync(null);

            Assert.Equal("1a\n", harness.ReadRaw("a.md"));
            Assert.Equal("b\n", harness.ReadRaw("b.md"));
            Assert.Equal("3c\n", harness.ReadRaw("c.md"));
            Assert.False(harness.Documents.HasUnsavedChanges);
        });

    [Fact]
    public Task SaveAll_SkipsDocumentsThatAreClean()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("a.md", "a\n"));
            await harness.Documents.OpenAsync(harness.Write("b.md", "b\n"));

            harness.Documents.Documents[0].Document.Insert(0, "1");

            // b.md is changed underneath without telling its watcher, so the only thing that could
            // put its buffer back over it is a write Save All had no business making.
            harness.Write("b.md", "changed on disk\n");

            await harness.Documents.SaveAllCommand.ExecuteAsync(null);

            // A flag that tells the truth is what keeps Save All off the files nobody edited.
            Assert.Equal("changed on disk\n", harness.ReadRaw("b.md"));
            Assert.Equal("1a\n", harness.ReadRaw("a.md"));
        });

    [Fact]
    public Task ClosingACleanDocument_AsksNothing()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md"));

            await harness.Documents.CloseCommand.ExecuteAsync(null);

            Assert.Empty(harness.Documents.Documents);
            Assert.Empty(harness.Prompts.AskedAbout);
            Assert.Null(harness.Documents.ActiveDocument);
            Assert.Equal("No file open", harness.Documents.FileInfoText);
        });

    [Fact]
    public Task ClosingAChangedDocumentAndCancelling_LeavesItOpenAndUnsaved()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            DocumentViewModel document = harness.Documents.Documents[0];
            document.Document.Insert(0, "edited ");

            harness.Prompts.Choice = UnsavedChangesChoice.Cancel;

            await harness.Documents.CloseCommand.ExecuteAsync(null);

            Assert.Single(harness.Documents.Documents);
            Assert.Equal(["notes.md"], harness.Prompts.AskedAbout);
            Assert.True(document.IsDirty);
            Assert.Equal("one\n", harness.ReadRaw("notes.md"));
        });

    [Fact]
    public Task ClosingAChangedDocumentAndDiscarding_ClosesItWithoutWriting()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            harness.Documents.Documents[0].Document.Insert(0, "edited ");
            harness.Prompts.Choice = UnsavedChangesChoice.Discard;

            await harness.Documents.CloseCommand.ExecuteAsync(null);

            Assert.Empty(harness.Documents.Documents);
            Assert.Equal("one\n", harness.ReadRaw("notes.md"));
        });

    [Fact]
    public Task ClosingAChangedDocumentAndSaving_WritesItThenCloses()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            harness.Documents.Documents[0].Document.Insert(0, "edited ");
            harness.Prompts.Choice = UnsavedChangesChoice.Save;

            await harness.Documents.CloseCommand.ExecuteAsync(null);

            Assert.Empty(harness.Documents.Documents);
            Assert.Equal("edited one\n", harness.ReadRaw("notes.md"));
        });

    [Fact]
    public Task ClosingATab_ActivatesItsNeighbourRatherThanNothing()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("a.md"));
            await harness.Documents.OpenAsync(harness.Write("b.md"));
            await harness.Documents.OpenAsync(harness.Write("c.md"));

            harness.Documents.ActiveDocument = harness.Documents.Documents[1];

            await harness.Documents.CloseCommand.ExecuteAsync(harness.Documents.Documents[1]);

            Assert.Equal("c.md", harness.Documents.ActiveDocument?.Title);
        });

    [Fact]
    public Task ClosingTheLastTab_ActivatesTheOneBeforeIt()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("a.md"));
            await harness.Documents.OpenAsync(harness.Write("b.md"));

            await harness.Documents.CloseCommand.ExecuteAsync(harness.Documents.Documents[1]);

            Assert.Equal("a.md", harness.Documents.ActiveDocument?.Title);
        });

    [Fact]
    public Task HasActiveDocument_IsNotifiedOnActivation_OnAClose_AndOnTheLastClose()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            List<string?> raised = [];

            Assert.False(harness.Documents.HasActiveDocument);

            harness.Documents.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            await harness.Documents.OpenAsync(harness.Write("a.md"));
            await harness.Documents.OpenAsync(harness.Write("b.md"));

            Assert.True(harness.Documents.HasActiveDocument);
            Assert.Contains(nameof(DocumentManagerViewModel.HasActiveDocument), raised);

            raised.Clear();
            await harness.Documents.CloseCommand.ExecuteAsync(harness.Documents.Documents[1]);

            // Still one tab left, so the flag is unchanged — but the collection changing is exactly
            // when the view has to be told again, because that is when the strip can clear the
            // selection behind the ViewModel's back.
            Assert.True(harness.Documents.HasActiveDocument);
            Assert.Contains(nameof(DocumentManagerViewModel.HasActiveDocument), raised);

            raised.Clear();
            await harness.Documents.CloseCommand.ExecuteAsync(harness.Documents.Documents[0]);

            Assert.False(harness.Documents.HasActiveDocument);
            Assert.Contains(nameof(DocumentManagerViewModel.HasActiveDocument), raised);
        });

    [Fact]
    public Task ClosingATab_NeverLeavesTheEditorShowingNothingWhileTabsRemain()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("a.md"));
            await harness.Documents.OpenAsync(harness.Write("b.md"));
            await harness.Documents.OpenAsync(harness.Write("c.md"));

            // What the tab strip's two-way selection binding does when the selected item leaves the
            // collection: it writes null back before anyone gets to pick a neighbour.
            harness.Documents.Documents.CollectionChanged += (_, _) => harness.Documents.ActiveDocument = null;

            harness.Documents.ActiveDocument = harness.Documents.Documents[1];
            await harness.Documents.CloseCommand.ExecuteAsync(harness.Documents.Documents[1]);

            Assert.Equal("c.md", harness.Documents.ActiveDocument?.Title);
            Assert.True(harness.Documents.HasActiveDocument);
        });

    [Fact]
    public Task ConfirmCloseAll_StopsAtTheFirstCancelAndLeavesThatDocumentOpen()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("clean.md"));
            await harness.Documents.OpenAsync(harness.Write("dirty.md", "one\n"));

            harness.Documents.Documents[1].Document.Insert(0, "edited ");
            harness.Prompts.Choice = UnsavedChangesChoice.Cancel;

            Assert.False(await harness.Documents.ConfirmCloseAllAsync());

            // The clean one went first and is gone; the changed one is what stopped the shutdown.
            Assert.Single(harness.Documents.Documents);
            Assert.Equal("dirty.md", harness.Documents.Documents[0].Title);
        });

    [Fact]
    public Task ConfirmCloseAll_ClosesEverythingWhenNothingIsHeldBack()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("a.md", "a\n"));
            await harness.Documents.OpenAsync(harness.Write("b.md", "b\n"));

            harness.Documents.Documents[0].Document.Insert(0, "1");
            harness.Prompts.Choice = UnsavedChangesChoice.Save;

            Assert.True(await harness.Documents.ConfirmCloseAllAsync());

            Assert.Empty(harness.Documents.Documents);
            Assert.Equal("1a\n", harness.ReadRaw("a.md"));
        });

    [Fact]
    public Task TabSwitching_WrapsRoundInBothDirections()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("a.md"));
            await harness.Documents.OpenAsync(harness.Write("b.md"));
            await harness.Documents.OpenAsync(harness.Write("c.md"));

            harness.Documents.NextDocumentCommand.Execute(null);
            Assert.Equal("a.md", harness.Documents.ActiveDocument?.Title);

            harness.Documents.PreviousDocumentCommand.Execute(null);
            Assert.Equal("c.md", harness.Documents.ActiveDocument?.Title);
        });

    [Fact]
    public Task ACleanDocumentChangedOnDisk_IsReloadedWithoutAsking()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string path = harness.Write("notes.md", "before\n");
            await harness.Documents.OpenAsync(path);

            DocumentViewModel document = harness.Documents.Documents[0];
            document.CaretOffset = 3;

            harness.ChangeOnDisk(path, "after the change\n");
            await harness.Dispatcher.DrainAsync();

            Assert.Equal("after the change\n", document.Document.Text);
            Assert.False(document.IsDirty);
            Assert.False(document.HasExternalChange);
            Assert.Equal(3, document.CaretOffset);
        });

    [Fact]
    public Task ReloadingADocument_LeavesItCleanRatherThanDirtiedByTheTextItReplaced()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            harness.Settings.Value.AutosaveEnabled = true;

            string path = harness.Write("notes.md", "before\n");
            await harness.Documents.OpenAsync(path);

            DocumentViewModel document = harness.Documents.Documents[0];

            harness.ChangeOnDisk(path, "after\n");
            await harness.Dispatcher.DrainAsync();

            // Replacing the buffer raises TextChanged like any other edit does, and what came out of
            // the file is by definition what is in it: no dot, and nothing to write back.
            Assert.Equal("after\n", document.Document.Text);
            Assert.False(document.IsDirty);
            Assert.False(document.HasExternalChange);
            Assert.False(harness.Idle.IsScheduled(document));
        });

    [Fact]
    public Task AChangedDocumentChangedOnDisk_AsksInsteadOfOverwritingEitherSide()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string path = harness.Write("notes.md", "before\n");
            await harness.Documents.OpenAsync(path);

            DocumentViewModel document = harness.Documents.Documents[0];
            document.Document.Insert(0, "mine ");

            harness.ChangeOnDisk(path, "theirs, and longer\n");
            await harness.Dispatcher.DrainAsync();

            Assert.True(document.HasExternalChange);
            Assert.Equal("mine before\n", document.Document.Text);
        });

    [Fact]
    public Task ReloadingAfterThat_TakesTheFileAndThrowsTheBufferAway()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string path = harness.Write("notes.md", "before\n");
            await harness.Documents.OpenAsync(path);

            DocumentViewModel document = harness.Documents.Documents[0];
            document.Document.Insert(0, "mine ");
            harness.ChangeOnDisk(path, "theirs, and longer\n");
            await harness.Dispatcher.DrainAsync();

            await harness.Documents.ReloadCommand.ExecuteAsync(document);

            Assert.Equal("theirs, and longer\n", document.Document.Text);
            Assert.False(document.IsDirty);
            Assert.False(document.HasExternalChange);
        });

    [Fact]
    public Task KeepingMineAfterThat_LeavesTheBufferAndStopsAsking()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string path = harness.Write("notes.md", "before\n");
            await harness.Documents.OpenAsync(path);

            DocumentViewModel document = harness.Documents.Documents[0];
            document.Document.Insert(0, "mine ");
            harness.ChangeOnDisk(path, "theirs, and longer\n");
            await harness.Dispatcher.DrainAsync();

            harness.Documents.KeepMineCommand.Execute(document);

            Assert.False(document.HasExternalChange);
            Assert.True(document.IsDirty);
            Assert.Equal("mine before\n", document.Document.Text);

            // The file's current state is the new baseline, so the next event about it is silent.
            harness.Watchers.For(path).Raise();
            await harness.Dispatcher.DrainAsync();

            Assert.False(document.HasExternalChange);
        });

    [Fact]
    public Task ADocumentDeletedOnDisk_IsFlaggedAndSavingPutsTheFileBack()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string path = harness.Write("notes.md", "content\n");
            await harness.Documents.OpenAsync(path);

            DocumentViewModel document = harness.Documents.Documents[0];

            harness.DeleteOnDisk(path);
            await harness.Dispatcher.DrainAsync();

            Assert.True(document.IsMissing);
            Assert.Equal("notes.md (deleted)", document.TabTitle);
            Assert.True(document.HasUnsavedChanges);
            Assert.Equal("content\n", document.Document.Text);

            await harness.Documents.SaveCommand.ExecuteAsync(document);

            Assert.False(document.IsMissing);
            Assert.Equal("notes.md", document.TabTitle);
            Assert.Equal("content\n", harness.ReadRaw("notes.md"));
        });

    [Fact]
    public Task TheEditorsOwnSave_DoesNotLookLikeAnExternalChange()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string path = harness.Write("notes.md", "one\n");
            await harness.Documents.OpenAsync(path);

            DocumentViewModel document = harness.Documents.Documents[0];
            document.Document.Insert(0, "edited ");

            await harness.Documents.SaveCommand.ExecuteAsync(document);

            // The write the editor just made produces a watcher event of its own. Comparing stamps is
            // what stops it turning into a reload of the file the user is looking at.
            harness.Watchers.For(path).Raise();
            await harness.Dispatcher.DrainAsync();

            Assert.False(document.HasExternalChange);
            Assert.False(document.IsMissing);
            Assert.False(document.IsDirty);
        });

    [Fact]
    public Task AutosaveIsOff_SoNothingIsScheduled()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            harness.Documents.Documents[0].Document.Insert(0, "edited ");

            Assert.Equal(0, harness.Idle.ScheduledCount);
        });

    [Fact]
    public Task AutosaveOn_SchedulesOnEveryEditAndWritesWhenItFires()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            harness.Settings.Value.AutosaveEnabled = true;
            harness.Settings.Value.AutosaveDelaySeconds = 5;

            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            DocumentViewModel document = harness.Documents.Documents[0];
            document.Document.Insert(0, "edited ");

            Assert.True(harness.Idle.IsScheduled(document));
            Assert.Equal(TimeSpan.FromSeconds(5), harness.Idle.DelayFor(document));

            await harness.Idle.FireAsync(document);

            Assert.Equal("edited one\n", harness.ReadRaw("notes.md"));
            Assert.False(document.IsDirty);
        });

    [Fact]
    public Task AutosaveFiringInInsertMode_ClearsTheDotAndCloseDoesNotAsk()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            harness.Settings.Value.AutosaveEnabled = true;

            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            DocumentViewModel document = harness.Documents.Documents[0];

            // The reported case that has nothing to do with a key binding: the user types, stops for
            // the idle delay, and the write happens with the insert group still open.
            document.Document.UndoStack.StartUndoGroup();
            document.Document.Insert(document.Document.TextLength, "two\n");

            await harness.Idle.FireAsync(document);

            Assert.False(document.IsDirty);

            document.Document.UndoStack.EndUndoGroup();

            Assert.False(document.IsDirty);

            await harness.Documents.CloseCommand.ExecuteAsync(document);

            Assert.Empty(harness.Prompts.AskedAbout);
            Assert.Equal("one\ntwo\n", harness.ReadRaw("notes.md"));
        });

    [Fact]
    public Task AutosaveIsHeldBackWhileTheUserIsBeingAskedSomething()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            harness.Settings.Value.AutosaveEnabled = true;

            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            DocumentViewModel document = harness.Documents.Documents[0];
            document.Document.Insert(0, "edited ");
            Assert.True(harness.Idle.IsScheduled(document));

            harness.Prompts.Choice = UnsavedChangesChoice.Cancel;
            await harness.Documents.CloseCommand.ExecuteAsync(document);

            // Every timer is stopped for the duration of the question, so the answer cannot be
            // pre-empted by a write of the very document it is about.
            Assert.True(harness.Idle.CancelAllCount >= 1);
        });

    [Fact]
    public Task AutosaveWaitsForAnAnswerAboutAFileThatChangedUnderneath()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            harness.Settings.Value.AutosaveEnabled = true;

            string path = harness.Write("notes.md", "before\n");
            await harness.Documents.OpenAsync(path);

            DocumentViewModel document = harness.Documents.Documents[0];
            document.Document.Insert(0, "mine ");

            harness.ChangeOnDisk(path, "theirs, and longer\n");
            await harness.Dispatcher.DrainAsync();

            await harness.Idle.FireAsync(document);

            // Writing here would answer "reload or keep mine" on the user's behalf, in favour of the
            // buffer, before they have been given the choice.
            Assert.Equal("theirs, and longer\n", harness.ReadRaw("notes.md"));
        });

    [Fact]
    public Task SwitchingAutosaveOnInSettings_ArmsTheDocumentsThatAreAlreadyOpen()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            DocumentViewModel document = harness.Documents.Documents[0];
            document.Document.Insert(0, "edited ");
            Assert.False(harness.Idle.IsScheduled(document));

            harness.Settings.Value.AutosaveEnabled = true;
            harness.Documents.OnSettingsChanged();

            Assert.True(harness.Idle.IsScheduled(document));
        });

    [Fact]
    public Task TheAppearanceSettings_AreRepublishedWhenTheSettingsDialogCloses()
        => UiThread.RunAsync(() =>
        {
            using EditorHarness harness = new();

            harness.Settings.Value.EditorFontSize = 21;
            harness.Settings.Value.WordWrap = false;
            harness.Settings.Value.EditorFontFamily = TestFonts.Monospace[0];

            harness.Documents.OnSettingsChanged();

            Assert.Equal(21, harness.Documents.FontSize);
            Assert.False(harness.Documents.WordWrap);
            Assert.Equal(TestFonts.Monospace[0], harness.Documents.FontFamily.Name);

            return Task.CompletedTask;
        });

    [Fact]
    public Task WithNoFontChosen_TheEditorRendersInTheBestMonospaceFamilyThisMachineHas()
        => UiThread.RunAsync(() =>
        {
            using EditorHarness harness = new();

            // The default, and the whole point of it: a fresh settings file names no family, and the
            // editor must still come out fixed-width rather than in the proportional interface font.
            Assert.Empty(harness.Settings.Value.EditorFontFamily);
            Assert.Equal(harness.Fonts.DefaultMonospaceFamily, harness.Documents.FontFamily.Name);
            Assert.Contains(harness.Documents.FontFamily.Name, harness.Fonts.MonospaceFamilies);

            return Task.CompletedTask;
        });

    [Fact]
    public Task AStoredFontThisMachineDoesNotHave_IsRenderedWithTheFallbackAndLeftInTheFile()
        => UiThread.RunAsync(() =>
        {
            using EditorHarness harness = new();

            // The comma stack older settings files hold: not a family name this or any machine has, so
            // the editor falls back — and the setting keeps saying what it said, because the file may
            // well be opened next on the machine that does have those fonts.
            const string Stack = "Cascadia Mono, Consolas, DejaVu Sans Mono, Liberation Mono, monospace";
            harness.Settings.Value.EditorFontFamily = Stack;

            harness.Documents.OnSettingsChanged();

            Assert.Equal(harness.Fonts.DefaultMonospaceFamily, harness.Documents.FontFamily.Name);
            Assert.Equal(Stack, harness.Settings.Value.EditorFontFamily);

            return Task.CompletedTask;
        });

    [Fact]
    public Task TheCaretSlot_FollowsTheActiveDocument()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("a.md"));
            await harness.Documents.OpenAsync(harness.Write("b.md"));

            harness.Documents.Documents[0].CaretLine = 4;
            harness.Documents.Documents[0].CaretColumn = 9;

            // The other tab is active, so its position is what the status bar says.
            Assert.Equal("Ln 1, Col 1", harness.Documents.CaretPositionText);

            harness.Documents.ActiveDocument = harness.Documents.Documents[0];
            Assert.Equal("Ln 4, Col 9", harness.Documents.CaretPositionText);

            harness.Documents.Documents[0].CaretLine = 5;
            Assert.Equal("Ln 5, Col 9", harness.Documents.CaretPositionText);
        });

    [Fact]
    public Task ClosingADocument_StopsWatchingItsFile()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string path = harness.Write("notes.md");
            await harness.Documents.OpenAsync(path);

            await harness.Documents.CloseCommand.ExecuteAsync(null);

            Assert.True(harness.Watchers.For(path).IsDisposed);
        });

    [Fact]
    public Task DisposingTheManager_ClosesAndStopsEverything()
        => UiThread.RunAsync(async () =>
        {
            EditorHarness harness = new();

            try
            {
                await harness.Documents.OpenAsync(harness.Write("a.md"));
                await harness.Documents.OpenAsync(harness.Write("b.md"));

                harness.Documents.Dispose();

                Assert.Empty(harness.Documents.Documents);
                Assert.True(harness.Watchers.Created.All(watcher => watcher.IsDisposed));
            }
            finally
            {
                harness.Dispose();
            }
        });

    [Fact]
    public Task TheAssists_AnswerToThePolicyRatherThanToASetting()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md"));

            Assert.True(harness.Documents.AssistsActive);

            // What vim emulation does on the way into normal mode.
            harness.Assists.IsActive = false;

            Assert.False(harness.Documents.AssistsActive);
        });

    [Fact]
    public void TheVimSlots_FollowTheSession()
    {
        using EditorHarness harness = new();
        List<string?> raised = [];
        harness.Documents.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.True(harness.Documents.IsVimEnabled);
        Assert.Equal("NORMAL", harness.Documents.VimModeText);
        Assert.False(harness.Documents.IsVimInsertMode);

        harness.Vim.ReportMode(VimMode.Insert);

        Assert.Equal("INSERT", harness.Documents.VimModeText);
        Assert.True(harness.Documents.IsVimInsertMode);
        Assert.Contains(nameof(DocumentManagerViewModel.VimModeText), raised);
    }

    [Theory]
    [InlineData(VimMode.Normal, "NORMAL", false, false)]
    [InlineData(VimMode.Insert, "INSERT", true, false)]
    [InlineData(VimMode.Visual, "VISUAL", false, true)]
    [InlineData(VimMode.VisualLine, "V-LINE", false, true)]
    public void TheModeSlot_NamesEveryModeAndAccentsThreeOfThem(
        VimMode mode,
        string text,
        bool insert,
        bool visual)
    {
        using EditorHarness harness = new();

        harness.Vim.ReportMode(mode);

        Assert.Equal(text, harness.Documents.VimModeText);
        Assert.Equal(insert, harness.Documents.IsVimInsertMode);
        Assert.Equal(visual, harness.Documents.IsVimVisualMode);
    }

    [Fact]
    public void AMessageFromTheEngine_IsCarriedToTheStatusBar()
    {
        using EditorHarness harness = new();

        harness.Vim.ReportStatus("Not a command: z");

        Assert.Equal("Not a command: z", harness.Documents.VimStatusText);
    }

    [Fact]
    public void TheCommandLineSlot_FollowsTheSession()
    {
        using EditorHarness harness = new();
        List<string?> raised = [];
        harness.Documents.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.Null(harness.Documents.VimCommandLineText);
        Assert.False(harness.Documents.IsVimCommandLineVisible);

        harness.Vim.ReportCommandLine("/foo");

        Assert.Equal("/foo", harness.Documents.VimCommandLineText);
        Assert.True(harness.Documents.IsVimCommandLineVisible);
        Assert.Contains(nameof(DocumentManagerViewModel.IsVimCommandLineVisible), raised);
    }

    [Fact]
    public void TheCommandStrip_IsNotShownWhileVimIsOff()
    {
        using EditorHarness harness = new();
        harness.Vim.ReportCommandLine(":w");

        harness.Settings.Value.VimEnabled = false;
        harness.Documents.OnSettingsChanged();

        Assert.False(harness.Documents.IsVimCommandLineVisible);
    }

    [Fact]
    public Task ExW_WritesTheActiveDocument()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            harness.Documents.Documents[0].Document.Insert(0, "edited ");

            await harness.Documents.ExecuteExCommandAsync(VimExCommand.Write);

            Assert.Equal("edited one\n", harness.ReadRaw("notes.md"));
            Assert.False(harness.Documents.Documents[0].IsDirty);

            // Saved, not closed: ':w' is the one of the three that leaves the tab where it is.
            Assert.Single(harness.Documents.Documents);
        });

    [Fact]
    public Task ExQ_OnACleanDocument_ClosesItWithoutAsking()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            await harness.Documents.ExecuteExCommandAsync(VimExCommand.Quit);

            Assert.Empty(harness.Documents.Documents);
            Assert.Empty(harness.Prompts.AskedAbout);
        });

    [Fact]
    public Task ExQ_OnAChangedDocument_AsksTheSameQuestionTheCloseButtonDoes()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            harness.Documents.Documents[0].Document.Insert(0, "edited ");
            harness.Prompts.Choice = UnsavedChangesChoice.Cancel;

            await harness.Documents.ExecuteExCommandAsync(VimExCommand.Quit);

            // Cancel means cancel, whichever way the close was asked for.
            Assert.Single(harness.Documents.Documents);
            Assert.Equal(["notes.md"], harness.Prompts.AskedAbout);
            Assert.Equal("one\n", harness.ReadRaw("notes.md"));
        });

    [Fact]
    public Task ExQ_OnAChangedDocumentAnsweredWithSave_WritesItThenCloses()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            harness.Documents.Documents[0].Document.Insert(0, "edited ");
            harness.Prompts.Choice = UnsavedChangesChoice.Save;

            await harness.Documents.ExecuteExCommandAsync(VimExCommand.Quit);

            Assert.Empty(harness.Documents.Documents);
            Assert.Equal("edited one\n", harness.ReadRaw("notes.md"));
        });

    [Fact]
    public Task ExWq_OnAChangedDocument_SavesThenClosesWithoutAsking()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            harness.Documents.Documents[0].Document.Insert(0, "edited ");

            await harness.Documents.ExecuteExCommandAsync(VimExCommand.WriteQuit);

            Assert.Empty(harness.Documents.Documents);
            Assert.Equal("edited one\n", harness.ReadRaw("notes.md"));

            // The question is never put: ':wq' has already answered it.
            Assert.Empty(harness.Prompts.AskedAbout);
        });

    [Fact]
    public Task ExWq_WhenTheSaveFails_LeavesTheTabOpenAndTheWorkInIt()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string path = harness.Write("notes.md", "one\n");
            await harness.Documents.OpenAsync(path);

            harness.Documents.Documents[0].Document.Insert(0, "edited ");

            // The file becomes a folder of the same name, which is a write that cannot succeed on
            // any platform.
            File.Delete(path);
            Directory.CreateDirectory(path);

            await harness.Documents.ExecuteExCommandAsync(VimExCommand.WriteQuit);

            Assert.Single(harness.Documents.Documents);
            Assert.True(harness.Documents.Documents[0].IsDirty);
            Assert.Single(harness.InfoBar.Shown);
        });

    [Fact]
    public Task ExQBang_OnAChangedDocument_ClosesItWithoutAskingAndWithoutSaving()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md", "one\n"));

            harness.Documents.Documents[0].Document.Insert(0, "edited ");

            // The fake answers Cancel to any question, so a question asked here would keep the tab.
            harness.Prompts.Choice = UnsavedChangesChoice.Cancel;

            await harness.Documents.ExecuteExCommandAsync(VimExCommand.QuitDiscard);

            // Gone, unasked, and the file is as it was: that is the whole of what the '!' means.
            Assert.Empty(harness.Documents.Documents);
            Assert.Empty(harness.Prompts.AskedAbout);
            Assert.Equal("one\n", harness.ReadRaw("notes.md"));
            Assert.Empty(harness.InfoBar.Shown);
        });

    [Fact]
    public Task ExQBang_OnACleanDocument_ClosesItLikeExQ()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("first.md"));
            await harness.Documents.OpenAsync(harness.Write("second.md"));

            await harness.Documents.ExecuteExCommandAsync(VimExCommand.QuitDiscard);

            // Only the active tab goes; the neighbour is left on screen.
            Assert.Equal("first.md", Assert.Single(harness.Documents.Documents).Title);
            Assert.Empty(harness.Prompts.AskedAbout);
        });

    [Fact]
    public Task AnExCommandWithNothingOpen_DoesNothing()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();

            await harness.Documents.ExecuteExCommandAsync(VimExCommand.Write);
            await harness.Documents.ExecuteExCommandAsync(VimExCommand.Quit);
            await harness.Documents.ExecuteExCommandAsync(VimExCommand.WriteQuit);
            await harness.Documents.ExecuteExCommandAsync(VimExCommand.QuitDiscard);

            Assert.Empty(harness.Documents.Documents);
            Assert.Empty(harness.Prompts.AskedAbout);
            Assert.Empty(harness.InfoBar.Shown);
        });

    [Fact]
    public void SwitchingVimOffInTheSettings_ReachesTheSessionWhenTheWindowCloses()
    {
        using EditorHarness harness = new();
        harness.Vim.ReportMode(VimMode.Insert);

        harness.Settings.Value.VimEnabled = false;
        harness.Documents.OnSettingsChanged();

        Assert.False(harness.Documents.IsVimEnabled);
        Assert.False(harness.Documents.IsVimInsertMode);
    }

    [Fact]
    public Task PastingAnImage_FilesItBesideTheNoteAndReturnsItsLink()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md"));

            FakePastedImage image = new();
            string? markdown = await harness.Documents.PasteImageAsync(image);

            string written = Assert.IsType<string>(image.SavedTo);

            Assert.Equal(Path.Combine(harness.Root, "assets"), Path.GetDirectoryName(written));
            Assert.StartsWith("notes-", Path.GetFileName(written), StringComparison.Ordinal);
            Assert.EndsWith(".png", written, StringComparison.Ordinal);
            Assert.True(File.Exists(written));

            Assert.Equal($"![](assets/{Path.GetFileName(written)})", markdown);
        });

    [Fact]
    public Task PastingASecondImageInTheSameSecond_DoesNotOverwriteTheFirst()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md"));

            FakePastedImage first = new();
            FakePastedImage second = new();

            await harness.Documents.PasteImageAsync(first);
            string? markdown = await harness.Documents.PasteImageAsync(second);

            Assert.NotEqual(first.SavedTo, second.SavedTo);
            Assert.True(File.Exists(first.SavedTo!));
            Assert.True(File.Exists(second.SavedTo!));

            // Numbered the way every file manager numbers, which puts a space and brackets in the
            // name — so the link has to be the angle-bracketed form or it would end at that space.
            Assert.Equal($"![](<assets/{Path.GetFileName(second.SavedTo)}>)", markdown);
        });

    [Fact]
    public Task PastingAnImageWithNothingOpen_DoesNothing()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();

            FakePastedImage image = new();

            Assert.Null(await harness.Documents.PasteImageAsync(image));
            Assert.Null(image.SavedTo);
            Assert.Empty(harness.InfoBar.Shown);
        });

    [Fact]
    public Task AnImageThatCannotBeWritten_IsReportedRatherThanLinked()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            await harness.Documents.OpenAsync(harness.Write("notes.md"));

            FakePastedImage image = new() { Failure = new IOException("the disk is full") };

            Assert.Null(await harness.Documents.PasteImageAsync(image));
            Assert.Single(harness.InfoBar.Shown);
        });

    [Fact]
    public Task ATabUnderAColouredFolder_CarriesThatFoldersColour()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string drafts = Directory.CreateDirectory(harness.Combine("drafts")).FullName;

            harness.Workspaces.ActiveWorkspace!.FolderColors.Add(
                new FolderColorEntry { Path = drafts, Color = FolderColor.Blue });

            await harness.Documents.OpenAsync(Write(drafts, "inside.md"));
            await harness.Documents.OpenAsync(harness.Write("outside.md"));

            // Inherited, not the file's own: the mark on the tab is there to name the folder the note
            // came from, and a file is never coloured itself.
            Assert.Equal(FolderColor.Blue, harness.Documents.Documents[0].Color);
            Assert.Same(FolderColorPalette.Blue, harness.Documents.Documents[0].ColorBrush);

            Assert.Equal(FolderColor.None, harness.Documents.Documents[1].Color);
            Assert.Null(harness.Documents.Documents[1].ColorBrush);
        });

    [Fact]
    public Task ColouringAFolder_RecoloursTheTabsAlreadyOpenUnderIt()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string drafts = Directory.CreateDirectory(harness.Combine("drafts")).FullName;

            await harness.Documents.OpenAsync(Write(drafts, "inside.md"));
            await harness.Documents.OpenAsync(harness.Write("outside.md"));

            Assert.Equal(FolderColor.None, harness.Documents.Documents[0].Color);

            await harness.Workspaces.SetFolderColorAsync(
                harness.Workspaces.ActiveWorkspace!.Id,
                drafts,
                FolderColor.Teal,
                TestContext.Current.CancellationToken);

            // No reload and no reopen: the tabs are re-resolved where they stand.
            Assert.Equal(FolderColor.Teal, harness.Documents.Documents[0].Color);
            Assert.Equal(FolderColor.None, harness.Documents.Documents[1].Color);
        });

    [Fact]
    public Task OpeningAnotherWorkspace_ReResolvesEveryTabAgainstItsColours()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string path = harness.Write("notes.md");

            await harness.Workspaces.SetFolderColorAsync(
                harness.Workspaces.ActiveWorkspace!.Id,
                harness.Root,
                FolderColor.Purple,
                TestContext.Current.CancellationToken);

            await harness.Documents.OpenAsync(path);
            Assert.Equal(FolderColor.Purple, harness.Documents.Documents[0].Color);

            // A second workspace over the same folder, with no colours of its own: the tab must stop
            // claiming a colour the open workspace does not set.
            Workspace other = harness.Workspaces.Seed("Other", active: false, harness.Root);
            await harness.Workspaces.ActivateAsync(other.Id, TestContext.Current.CancellationToken);

            Assert.Equal(FolderColor.None, harness.Documents.Documents[0].Color);
        });

    [Fact]
    public Task SavingATabIntoAnotherFolder_ReResolvesItsColour()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string drafts = Directory.CreateDirectory(harness.Combine("drafts")).FullName;

            harness.Workspaces.ActiveWorkspace!.FolderColors.Add(
                new FolderColorEntry { Path = drafts, Color = FolderColor.Green });

            await harness.Documents.OpenAsync(Write(drafts, "inside.md"));
            Assert.Equal(FolderColor.Green, harness.Documents.Documents[0].Color);

            harness.Prompts.SaveAsPath = harness.Combine("moved.md");
            await harness.Documents.SaveAsCommand.ExecuteAsync(null);

            // The colour follows the file, not the buffer it started as.
            Assert.Equal(harness.Combine("moved.md"), harness.Documents.Documents[0].FilePath);
            Assert.Equal(FolderColor.None, harness.Documents.Documents[0].Color);
        });

    [Fact]
    public Task ItsArguments_AreChecked()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();

            Assert.Throws<ArgumentNullException>(() => new DocumentManagerViewModel(
                null!,
                harness.Workspaces,
                harness.Prompts,
                harness.InfoBar,
                harness.Idle,
                harness.Watchers,
                harness.Dispatcher,
                harness.Assists,
                harness.Vim,
                harness.ImagePaste,
                harness.Fonts,
                NullLogger<DocumentManagerViewModel>.Instance));

            Assert.Throws<ArgumentNullException>(() => new DocumentManagerViewModel(
                harness.Settings,
                null!,
                harness.Prompts,
                harness.InfoBar,
                harness.Idle,
                harness.Watchers,
                harness.Dispatcher,
                harness.Assists,
                harness.Vim,
                harness.ImagePaste,
                harness.Fonts,
                NullLogger<DocumentManagerViewModel>.Instance));

            Assert.Throws<ArgumentNullException>(() => new DocumentManagerViewModel(
                harness.Settings,
                harness.Workspaces,
                null!,
                harness.InfoBar,
                harness.Idle,
                harness.Watchers,
                harness.Dispatcher,
                harness.Assists,
                harness.Vim,
                harness.ImagePaste,
                harness.Fonts,
                NullLogger<DocumentManagerViewModel>.Instance));

            Assert.Throws<ArgumentNullException>(() => new DocumentManagerViewModel(
                harness.Settings,
                harness.Workspaces,
                harness.Prompts,
                null!,
                harness.Idle,
                harness.Watchers,
                harness.Dispatcher,
                harness.Assists,
                harness.Vim,
                harness.ImagePaste,
                harness.Fonts,
                NullLogger<DocumentManagerViewModel>.Instance));

            Assert.Throws<ArgumentNullException>(() => new DocumentManagerViewModel(
                harness.Settings,
                harness.Workspaces,
                harness.Prompts,
                harness.InfoBar,
                null!,
                harness.Watchers,
                harness.Dispatcher,
                harness.Assists,
                harness.Vim,
                harness.ImagePaste,
                harness.Fonts,
                NullLogger<DocumentManagerViewModel>.Instance));

            Assert.Throws<ArgumentNullException>(() => new DocumentManagerViewModel(
                harness.Settings,
                harness.Workspaces,
                harness.Prompts,
                harness.InfoBar,
                harness.Idle,
                null!,
                harness.Dispatcher,
                harness.Assists,
                harness.Vim,
                harness.ImagePaste,
                harness.Fonts,
                NullLogger<DocumentManagerViewModel>.Instance));

            Assert.Throws<ArgumentNullException>(() => new DocumentManagerViewModel(
                harness.Settings,
                harness.Workspaces,
                harness.Prompts,
                harness.InfoBar,
                harness.Idle,
                harness.Watchers,
                null!,
                harness.Assists,
                harness.Vim,
                harness.ImagePaste,
                harness.Fonts,
                NullLogger<DocumentManagerViewModel>.Instance));

            Assert.Throws<ArgumentNullException>(() => new DocumentManagerViewModel(
                harness.Settings,
                harness.Workspaces,
                harness.Prompts,
                harness.InfoBar,
                harness.Idle,
                harness.Watchers,
                harness.Dispatcher,
                null!,
                harness.Vim,
                harness.ImagePaste,
                harness.Fonts,
                NullLogger<DocumentManagerViewModel>.Instance));

            Assert.Throws<ArgumentNullException>(() => new DocumentManagerViewModel(
                harness.Settings,
                harness.Workspaces,
                harness.Prompts,
                harness.InfoBar,
                harness.Idle,
                harness.Watchers,
                harness.Dispatcher,
                harness.Assists,
                harness.Vim,
                null!,
                harness.Fonts,
                NullLogger<DocumentManagerViewModel>.Instance));

            Assert.Throws<ArgumentNullException>(() => new DocumentManagerViewModel(
                harness.Settings,
                harness.Workspaces,
                harness.Prompts,
                harness.InfoBar,
                harness.Idle,
                harness.Watchers,
                harness.Dispatcher,
                harness.Assists,
                null!,
                harness.ImagePaste,
                harness.Fonts,
                NullLogger<DocumentManagerViewModel>.Instance));

            Assert.Throws<ArgumentNullException>(() => new DocumentManagerViewModel(
                harness.Settings,
                harness.Workspaces,
                harness.Prompts,
                harness.InfoBar,
                harness.Idle,
                harness.Watchers,
                harness.Dispatcher,
                harness.Assists,
                harness.Vim,
                harness.ImagePaste,
                null!,
                NullLogger<DocumentManagerViewModel>.Instance));

            Assert.Throws<ArgumentNullException>(() => new DocumentManagerViewModel(
                harness.Settings,
                harness.Workspaces,
                harness.Prompts,
                harness.InfoBar,
                harness.Idle,
                harness.Watchers,
                harness.Dispatcher,
                harness.Assists,
                harness.Vim,
                harness.ImagePaste,
                harness.Fonts,
                null!));

            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => harness.Documents.OpenAsync("  ", TestContext.Current.CancellationToken));
        });

    // A real file in a sub-folder of the harness's root, which EditorHarness.Write does not reach:
    // a colour inherited from a folder needs a file that is actually inside one.
    private static string Write(string directory, string name)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, "# note\n");

        return path;
    }
}
