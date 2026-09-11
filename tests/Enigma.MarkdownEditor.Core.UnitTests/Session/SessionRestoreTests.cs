using System;
using Enigma.MarkdownEditor.Core.Session;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Session;

/// <summary>
/// What a saved tab list comes to when it is measured against the disk. Every case here is a way the
/// world moves between one run and the next — a note deleted, a workspace switched — and none of them
/// is allowed to be an error.
/// </summary>
public sealed class SessionRestoreTests : IDisposable
{
    private readonly TempSessionFolder _folder = new();

    [Fact]
    public void EveryNoteStillThere_ComesBackInOrderWithTheSameActiveTab()
    {
        Guid workspace = Guid.NewGuid();
        string first = _folder.ExistingNote("first.md");
        string second = _folder.ExistingNote("second.md");
        string third = _folder.ExistingNote("third.md");

        SessionDocuments documents = SessionRestore.Resolve(
            State(workspace, [first, second, third], activeIndex: 2),
            workspace);

        Assert.Equal([first, second, third], documents.Paths);
        Assert.Equal(2, documents.ActiveIndex);
        Assert.Empty(documents.Missing);
        Assert.False(documents.IsForAnotherWorkspace);
    }

    [Fact]
    public void ANoteThatHasGone_IsSkippedAndReported()
    {
        Guid workspace = Guid.NewGuid();
        string kept = _folder.ExistingNote("kept.md");
        string deleted = _folder.MissingNote("deleted.md");

        SessionDocuments documents = SessionRestore.Resolve(
            State(workspace, [kept, deleted], activeIndex: 0),
            workspace);

        Assert.Equal([kept], documents.Paths);
        Assert.Equal([deleted], documents.Missing);
        Assert.Equal(0, documents.ActiveIndex);
    }

    [Fact]
    public void ANoteThatHasGone_ShiftsTheActiveTabDownRatherThanMovingIt()
    {
        Guid workspace = Guid.NewGuid();
        string deleted = _folder.MissingNote("deleted.md");
        string wanted = _folder.ExistingNote("wanted.md");

        // The tab the user was reading is at index 1 in the file and at index 0 once the note before
        // it is dropped. Carrying the number over would land on the wrong note.
        SessionDocuments documents = SessionRestore.Resolve(
            State(workspace, [deleted, wanted], activeIndex: 1),
            workspace);

        Assert.Equal([wanted], documents.Paths);
        Assert.Equal(0, documents.ActiveIndex);
    }

    [Fact]
    public void TheActiveNoteHavingGone_LandsOnTheFirstSurvivor()
    {
        Guid workspace = Guid.NewGuid();
        string survivor = _folder.ExistingNote("survivor.md");
        string deleted = _folder.MissingNote("deleted.md");

        SessionDocuments documents = SessionRestore.Resolve(
            State(workspace, [survivor, deleted], activeIndex: 1),
            workspace);

        Assert.Equal([survivor], documents.Paths);
        Assert.Equal(0, documents.ActiveIndex);
    }

    [Fact]
    public void EveryNoteHavingGone_IsAnEmptyEditorRatherThanAnError()
    {
        Guid workspace = Guid.NewGuid();
        string one = _folder.MissingNote("one.md");
        string two = _folder.MissingNote("two.md");

        SessionDocuments documents = SessionRestore.Resolve(
            State(workspace, [one, two], activeIndex: 0),
            workspace);

        Assert.Empty(documents.Paths);
        Assert.Equal(-1, documents.ActiveIndex);
        Assert.Equal([one, two], documents.Missing);
    }

    [Fact]
    public void ASessionFromAnotherWorkspace_IsDroppedWholeRatherThanOpened()
    {
        string note = _folder.ExistingNote("note.md");

        // The paths are absolute and would all open perfectly well, which is exactly the problem:
        // they would be another workspace's notes in this workspace's window.
        SessionDocuments documents = SessionRestore.Resolve(
            State(Guid.NewGuid(), [note], activeIndex: 0),
            Guid.NewGuid());

        Assert.True(documents.IsForAnotherWorkspace);
        Assert.Empty(documents.Paths);
        Assert.Equal(-1, documents.ActiveIndex);
        Assert.Empty(documents.Missing);
    }

    [Fact]
    public void ASessionWrittenWithNoWorkspace_MatchesAStartWithNoWorkspace()
    {
        string note = _folder.ExistingNote("note.md");

        SessionDocuments documents = SessionRestore.Resolve(State(null, [note], activeIndex: 0), null);

        Assert.False(documents.IsForAnotherWorkspace);
        Assert.Equal([note], documents.Paths);
    }

    [Fact]
    public void ASessionWrittenWithAWorkspace_IsDroppedWhenNoneOpens()
    {
        string note = _folder.ExistingNote("note.md");

        SessionDocuments documents = SessionRestore.Resolve(State(Guid.NewGuid(), [note], activeIndex: 0), null);

        Assert.True(documents.IsForAnotherWorkspace);
        Assert.Empty(documents.Paths);
    }

    [Fact]
    public void ANothingSession_ResolvesToNothing()
    {
        SessionDocuments documents = SessionRestore.Resolve(new SessionState(), null);

        Assert.Empty(documents.Paths);
        Assert.Equal(-1, documents.ActiveIndex);
        Assert.Empty(documents.Missing);
        Assert.False(documents.IsForAnotherWorkspace);
    }

    [Fact]
    public void ABlankPathInAHandEditedFile_IsPassedOverWithoutBeingCalledMissing()
    {
        Guid workspace = Guid.NewGuid();
        string note = _folder.ExistingNote("note.md");

        SessionDocuments documents = SessionRestore.Resolve(
            State(workspace, ["   ", note], activeIndex: 1),
            workspace);

        Assert.Equal([note], documents.Paths);
        Assert.Empty(documents.Missing);
        Assert.Equal(0, documents.ActiveIndex);
    }

    [Fact]
    public void NoState_IsRejected()
        => Assert.Throws<ArgumentNullException>(() => SessionRestore.Resolve(null!, null));

    [Fact]
    public void TheNothingAnswer_IsShared()
    {
        Assert.Empty(SessionDocuments.None.Paths);
        Assert.Equal(-1, SessionDocuments.None.ActiveIndex);
        Assert.Empty(SessionDocuments.None.Missing);
        Assert.False(SessionDocuments.None.IsForAnotherWorkspace);
    }

    public void Dispose() => _folder.Dispose();

    private static SessionState State(Guid? workspace, string[] paths, int activeIndex)
        => new()
        {
            ActiveWorkspaceId = workspace,
            OpenDocuments = [.. paths],
            ActiveDocumentIndex = activeIndex,
        };
}
