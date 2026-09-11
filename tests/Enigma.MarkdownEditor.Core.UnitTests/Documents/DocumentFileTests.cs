using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Documents;
using Enigma.MarkdownEditor.Core.Files;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Documents;

/// <summary>
/// Tests for the read/write pair over real files, asserting on the bytes rather than on the text: the
/// whole point of the pair is what lands on disk, and a byte-order mark or a carriage return is
/// invisible in a string comparison.
/// </summary>
public sealed class DocumentFileTests
{
    private static readonly byte[] Utf8Mark = [0xEF, 0xBB, 0xBF];

    [Fact]
    public async Task ACrLfFile_RoundTripsAsCrLf()
    {
        using TempDocumentFolder folder = new();
        string path = folder.WriteBytes("windows.md", Encoding.ASCII.GetBytes("one\r\ntwo\r\n"));

        DocumentContent content = await DocumentFile.ReadAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal("one\ntwo\n", content.Text);
        Assert.Equal(LineEndingStyle.CrLf, content.Format.LineEndings);

        await DocumentFile.WriteAsync(path, content.Text, content.Format, TestContext.Current.CancellationToken);

        Assert.Equal("one\r\ntwo\r\n", Encoding.ASCII.GetString(folder.ReadBytes("windows.md")));
    }

    [Fact]
    public async Task AnLfFile_RoundTripsAsLf()
    {
        using TempDocumentFolder folder = new();
        string path = folder.WriteBytes("unix.md", Encoding.ASCII.GetBytes("one\ntwo\n"));

        DocumentContent content = await DocumentFile.ReadAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(LineEndingStyle.Lf, content.Format.LineEndings);

        await DocumentFile.WriteAsync(path, content.Text, content.Format, TestContext.Current.CancellationToken);

        Assert.Equal("one\ntwo\n", Encoding.ASCII.GetString(folder.ReadBytes("unix.md")));
    }

    [Fact]
    public async Task EditingACrLfFile_StillWritesCrLfForTheNewLinesToo()
    {
        using TempDocumentFolder folder = new();
        string path = folder.WriteBytes("windows.md", Encoding.ASCII.GetBytes("one\r\n"));

        DocumentContent content = await DocumentFile.ReadAsync(path, TestContext.Current.CancellationToken);

        // What the editor holds: line feeds alone, whatever the file used.
        await DocumentFile.WriteAsync(
            path,
            content.Text + "two\nthree\n",
            content.Format,
            TestContext.Current.CancellationToken);

        Assert.Equal("one\r\ntwo\r\nthree\r\n", Encoding.ASCII.GetString(folder.ReadBytes("windows.md")));
    }

    [Fact]
    public async Task AUtf8ByteOrderMark_IsPreserved()
    {
        using TempDocumentFolder folder = new();
        byte[] original = [.. Utf8Mark, .. Encoding.UTF8.GetBytes("héllo\n")];
        string path = folder.WriteBytes("marked.md", original);

        DocumentContent content = await DocumentFile.ReadAsync(path, TestContext.Current.CancellationToken);

        Assert.True(content.Format.HasByteOrderMark);
        Assert.Equal("héllo\n", content.Text);

        await DocumentFile.WriteAsync(path, content.Text, content.Format, TestContext.Current.CancellationToken);

        Assert.Equal(original, folder.ReadBytes("marked.md"));
    }

    [Fact]
    public async Task AFileWithoutAByteOrderMark_DoesNotGainOne()
    {
        using TempDocumentFolder folder = new();
        byte[] original = Encoding.UTF8.GetBytes("héllo\n");
        string path = folder.WriteBytes("bare.md", original);

        DocumentContent content = await DocumentFile.ReadAsync(path, TestContext.Current.CancellationToken);

        Assert.False(content.Format.HasByteOrderMark);

        await DocumentFile.WriteAsync(path, content.Text, content.Format, TestContext.Current.CancellationToken);

        // The regression this guards is Encoding.UTF8, which silently prepends three bytes.
        Assert.Equal(original, folder.ReadBytes("bare.md"));
    }

    [Fact]
    public async Task AUtf16File_IsReadAsUtf16AndWrittenBackAsUtf16()
    {
        using TempDocumentFolder folder = new();
        byte[] original = [.. new UnicodeEncoding(false, true).GetPreamble(), .. Encoding.Unicode.GetBytes("wide\r\n")];
        string path = folder.WriteBytes("wide.md", original);

        DocumentContent content = await DocumentFile.ReadAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal("wide\n", content.Text);
        Assert.True(content.Format.HasByteOrderMark);
        Assert.Equal(LineEndingStyle.CrLf, content.Format.LineEndings);

        await DocumentFile.WriteAsync(path, content.Text, content.Format, TestContext.Current.CancellationToken);

        Assert.Equal(original, folder.ReadBytes("wide.md"));
    }

    [Fact]
    public async Task AnEmptyFile_ReadsAsTheDefaultFormatSoANewFileGetsLineFeeds()
    {
        using TempDocumentFolder folder = new();
        string path = folder.WriteBytes("new.md", []);

        DocumentContent content = await DocumentFile.ReadAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, content.Text);
        Assert.False(content.Format.HasByteOrderMark);
        Assert.Equal(LineEndingStyle.Lf, content.Format.LineEndings);

        await DocumentFile.WriteAsync(path, "typed\nin\n", content.Format, TestContext.Current.CancellationToken);

        Assert.Equal("typed\nin\n", Encoding.ASCII.GetString(folder.ReadBytes("new.md")));
    }

    [Fact]
    public async Task WritingAFileThatIsNoLongerThere_CreatesItAgain()
    {
        using TempDocumentFolder folder = new();
        string path = folder.Combine("deleted.md");

        // Exactly what happens when the user deletes an open document from outside the editor and
        // then saves the tab.
        await DocumentFile.WriteAsync(path, "back\n", TextFileFormat.Default, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(path));
        Assert.Equal("back\n", Encoding.ASCII.GetString(folder.ReadBytes("deleted.md")));
    }

    [Fact]
    public async Task AFileBeyondTheLimit_IsRefusedWithASentenceForTheUser()
    {
        using TempDocumentFolder folder = new();
        string path = folder.Combine("huge.md");

        using (FileStream stream = File.Create(path))
        {
            stream.SetLength(DocumentFile.MaximumBytes + 1);
        }

        FileOperationException failure = await Assert.ThrowsAsync<FileOperationException>(
            () => DocumentFile.ReadAsync(path, TestContext.Current.CancellationToken));

        Assert.Contains("MB", failure.Message, StringComparison.Ordinal);
        Assert.Contains("huge.md", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ItsArguments_AreChecked()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        await Assert.ThrowsAnyAsync<ArgumentException>(() => DocumentFile.ReadAsync("  ", token));
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => DocumentFile.WriteAsync("  ", "x", TextFileFormat.Default, token));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => DocumentFile.WriteAsync("x.md", null!, TextFileFormat.Default, token));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => DocumentFile.WriteAsync("x.md", "x", null!, token));
    }
}
