using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Documents;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Documents;

/// <summary>
/// Tests for the stamp the editor uses to tell its own save apart from somebody else's edit.
/// </summary>
public sealed class FileStampTests
{
    [Fact]
    public void AFileThatIsNotThere_StampsAsMissing()
    {
        using TempDocumentFolder folder = new();

        Assert.Equal(FileStamp.Missing, FileStamp.For(folder.Combine("gone.md")));
    }

    [Fact]
    public void MissingIsTheDefaultValue()
    {
        Assert.Equal(default, FileStamp.Missing);
        Assert.False(FileStamp.Missing.Exists);
    }

    [Fact]
    public void AFileThatIsThere_StampsWithItsLengthAndWriteTime()
    {
        using TempDocumentFolder folder = new();
        folder.WriteBytes("notes.md", Encoding.ASCII.GetBytes("hello"));

        FileStamp stamp = FileStamp.For(folder.Combine("notes.md"));

        Assert.True(stamp.Exists);
        Assert.Equal(5, stamp.Length);
        Assert.NotEqual(default, stamp.LastWriteTimeUtc);
    }

    [Fact]
    public void TwoStampsOfAnUntouchedFile_AreEqual()
    {
        using TempDocumentFolder folder = new();
        string path = folder.WriteBytes("notes.md", Encoding.ASCII.GetBytes("hello"));

        // This is the comparison that stops the editor from reacting to the watcher event its own
        // save just produced.
        Assert.Equal(FileStamp.For(path), FileStamp.For(path));
    }

    [Fact]
    public async Task AStampTakenBeforeAWrite_DiffersFromOneTakenAfter()
    {
        using TempDocumentFolder folder = new();
        string path = folder.WriteBytes("notes.md", Encoding.ASCII.GetBytes("hello"));

        FileStamp before = FileStamp.For(path);

        // A same-length rewrite inside the file system's timestamp resolution is the case Length
        // alone cannot see and LastWriteTimeUtc alone cannot either; the delay makes the clock move.
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("world"));

        Assert.NotEqual(before, FileStamp.For(path));
    }

    [Fact]
    public void ALengthChange_IsEnoughOnItsOwn()
    {
        using TempDocumentFolder folder = new();
        string path = folder.WriteBytes("notes.md", Encoding.ASCII.GetBytes("hello"));

        FileStamp before = FileStamp.For(path);
        FileStamp longer = before with { Length = before.Length + 1 };

        Assert.NotEqual(before, longer);
    }

    [Fact]
    public void ItsArguments_AreChecked()
        => Assert.ThrowsAny<ArgumentException>(() => FileStamp.For("  "));
}
