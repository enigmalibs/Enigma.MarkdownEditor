using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// Tests for the one decision made when the tree activates a file: this editor, or the operating
/// system.
/// </summary>
/// <remarks>
/// On <see cref="UiThread"/> because the editor half of the decision ends in a document, and an
/// AvaloniaEdit document may only be touched from the thread that built it.
/// </remarks>
public sealed class DocumentOpenerTests
{
    [Fact]
    public Task AMarkdownFile_GoesToTheEditor()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string path = harness.Write("notes.md");
            FakeFileLauncher launcher = new();

            await Create(harness, launcher).OpenAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal(path, harness.Documents.ActiveDocument?.FilePath);
            Assert.Empty(launcher.Launched);
        });

    [Fact]
    public Task AnImage_GoesToTheOperatingSystem()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string path = harness.Write("photo.png");
            FakeFileLauncher launcher = new();

            await Create(harness, launcher).OpenAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal([path], launcher.Launched);
            Assert.False(harness.Documents.HasDocuments);
        });

    [Fact]
    public Task AnotherTextFile_GoesToTheEditorOnlyWhileTheTreeIsShowingIt()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            string path = harness.Write("notes.txt");
            FakeFileLauncher launcher = new();
            DocumentOpener opener = Create(harness, launcher);

            await opener.OpenAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal([path], launcher.Launched);
            Assert.False(harness.Documents.HasDocuments);

            harness.Settings.Value.ShowAllFiles = true;
            await opener.OpenAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal(path, harness.Documents.ActiveDocument?.FilePath);
        });

    [Fact]
    public Task APlatformThatDeclines_TellsTheUserRatherThanDoingNothing()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            FakeInfoBarService infoBar = new();
            FakeFileLauncher launcher = new() { Declines = true };

            await Create(harness, launcher, infoBar)
                .OpenAsync(harness.Write("archive.zip"), TestContext.Current.CancellationToken);

            Assert.Single(infoBar.Shown);
        });

    [Fact]
    public Task APlatformThatThrows_IsCaughtAndReported()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            FakeInfoBarService infoBar = new();
            FakeFileLauncher launcher = new() { Failure = new InvalidOperationException("no shell") };

            await Create(harness, launcher, infoBar)
                .OpenAsync(harness.Write("archive.zip"), TestContext.Current.CancellationToken);

            Assert.Single(infoBar.Shown);
        });

    [Fact]
    public Task ItsArguments_AreChecked()
        => UiThread.RunAsync(async () =>
        {
            using EditorHarness harness = new();
            FakeFileLauncher launcher = new();
            FakeInfoBarService infoBar = new();
            NullLogger<DocumentOpener> logger = NullLogger<DocumentOpener>.Instance;

            Assert.Throws<ArgumentNullException>(
                () => new DocumentOpener(null!, launcher, harness.Settings, infoBar, logger));
            Assert.Throws<ArgumentNullException>(
                () => new DocumentOpener(harness.Documents, null!, harness.Settings, infoBar, logger));
            Assert.Throws<ArgumentNullException>(
                () => new DocumentOpener(harness.Documents, launcher, null!, infoBar, logger));
            Assert.Throws<ArgumentNullException>(
                () => new DocumentOpener(harness.Documents, launcher, harness.Settings, null!, logger));
            Assert.Throws<ArgumentNullException>(
                () => new DocumentOpener(harness.Documents, launcher, harness.Settings, infoBar, null!));

            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => Create(harness, launcher).OpenAsync("  ", TestContext.Current.CancellationToken));
        });

    private static DocumentOpener Create(
        EditorHarness harness,
        FakeFileLauncher launcher,
        FakeInfoBarService? infoBar = null)
        => new(
            harness.Documents,
            launcher,
            harness.Settings,
            infoBar ?? new FakeInfoBarService(),
            NullLogger<DocumentOpener>.Instance);
}
