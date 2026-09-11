using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// Where a pasted image lands, and what happens when it cannot.
/// </summary>
/// <remarks>
/// Over real files in a throw-away folder, for the same reason the save path is tested that way: the
/// point of this service is what appears on disk. The one thing faked is the bitmap, which cannot be
/// built without an initialized Avalonia application.
/// </remarks>
public sealed class ImagePasteServiceTests
{
    [Fact]
    public async Task TheImageFolderIsCreatedOnDemand()
    {
        using EditorHarness harness = new();
        string note = harness.Write("notes.md");

        Assert.False(Directory.Exists(Path.Combine(harness.Root, "assets")));

        FakePastedImage image = new();

        Assert.NotNull(await harness.ImagePaste.SaveAsync(image, note, TestContext.Current.CancellationToken));
        Assert.True(Directory.Exists(Path.Combine(harness.Root, "assets")));
    }

    [Fact]
    public async Task TheFolderComesFromTheSetting()
    {
        using EditorHarness harness = new();
        string note = harness.Write("notes.md");

        harness.Settings.Value.ImageFolderName = "media";

        FakePastedImage image = new();
        string? markdown = await harness.ImagePaste.SaveAsync(image, note, TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(harness.Root, "media"), Path.GetDirectoryName(image.SavedTo));
        Assert.StartsWith("![](media/", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASubfolderOfItsOwn_IsLinkedRelativelyToTheNote()
    {
        using EditorHarness harness = new();

        string folder = Path.Combine(harness.Root, "sub");
        Directory.CreateDirectory(folder);

        string note = Path.Combine(folder, "notes.md");
        File.WriteAllText(note, "# note\n");

        FakePastedImage image = new();
        string? markdown = await harness.ImagePaste.SaveAsync(image, note, TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(folder, "assets"), Path.GetDirectoryName(image.SavedTo));
        Assert.StartsWith("![](assets/notes-", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedWrite_IsReportedAndNothingIsLinked()
    {
        using EditorHarness harness = new();
        string note = harness.Write("notes.md");

        FakePastedImage image = new() { Failure = new IOException("no room left") };

        Assert.Null(await harness.ImagePaste.SaveAsync(image, note, TestContext.Current.CancellationToken));

        Action<Enigma.Avalonia.Desktop.Controls.InfoBar.InfoBar>? shown = Assert.Single(harness.InfoBar.Shown);
        Assert.NotNull(shown);
    }

    [Fact]
    public async Task ACancelledPaste_WritesNothing()
    {
        using EditorHarness harness = new();
        string note = harness.Write("notes.md");

        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        FakePastedImage image = new();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => harness.ImagePaste.SaveAsync(image, note, cancelled.Token));

        Assert.Null(image.SavedTo);
    }

    [Fact]
    public async Task ItsArguments_AreChecked()
    {
        using EditorHarness harness = new();

        Assert.Throws<ArgumentNullException>(() => new ImagePasteService(
            null!,
            harness.InfoBar,
            NullLogger<ImagePasteService>.Instance));

        Assert.Throws<ArgumentNullException>(() => new ImagePasteService(
            harness.Settings,
            null!,
            NullLogger<ImagePasteService>.Instance));

        Assert.Throws<ArgumentNullException>(() => new ImagePasteService(
            harness.Settings,
            harness.InfoBar,
            null!));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => harness.ImagePaste.SaveAsync(null!, "a.md", TestContext.Current.CancellationToken));

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => harness.ImagePaste.SaveAsync(new FakePastedImage(), "  ", TestContext.Current.CancellationToken));
    }
}
