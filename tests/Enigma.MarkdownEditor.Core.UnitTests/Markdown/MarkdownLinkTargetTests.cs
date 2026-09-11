using System;
using System.IO;
using Enigma.MarkdownEditor.Core.Markdown;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Markdown;

/// <summary>
/// Where a link in a note points, and therefore what clicking it does.
/// </summary>
/// <remarks>
/// The whole of the link-activation matrix that can be decided without a window: the browser, a file,
/// the document itself, or nothing at all. What the application then does with each is one switch in
/// the preview's ViewModel.
/// </remarks>
public sealed class MarkdownLinkTargetTests
{
    [Theory]
    [InlineData("https://example.com/docs")]
    [InlineData("http://example.com")]
    [InlineData("mailto:someone@example.com")]
    public void AnAbsoluteAddress_GoesToThePlatform(string url)
    {
        MarkdownLinkTarget target = MarkdownLinkTarget.Resolve(@"C:\notes", url);

        Assert.Equal(MarkdownLinkKind.External, target.Kind);
        Assert.Equal(new Uri(url), target.Address);
        Assert.Null(target.LocalPath);
        Assert.Equal(url, target.Display);
    }

    [Fact]
    public void ARelativeLink_IsResolvedAgainstTheNotesOwnFolder()
    {
        using TempLinkFolder folder = new();

        MarkdownLinkTarget target = MarkdownLinkTarget.Resolve(folder.Root, "./other-note.md");

        Assert.Equal(MarkdownLinkKind.Local, target.Kind);
        Assert.Equal(Path.Combine(folder.Root, "other-note.md"), target.LocalPath);
    }

    [Fact]
    public void ARelativeLinkIntoASubfolder_KeepsIt()
    {
        using TempLinkFolder folder = new();

        MarkdownLinkTarget target = MarkdownLinkTarget.Resolve(folder.Root, "sub/deep.md");

        Assert.Equal(MarkdownLinkKind.Local, target.Kind);
        Assert.Equal(Path.Combine(folder.Root, "sub", "deep.md"), target.LocalPath);
    }

    [Fact]
    public void ARelativeLinkWithAnAnchor_PointsAtTheFileRatherThanAtTheAnchor()
    {
        using TempLinkFolder folder = new();

        // Written all the time, and resolved naively it becomes a file called `other.md#a-heading`,
        // which is a missing file on every note that uses one.
        MarkdownLinkTarget target = MarkdownLinkTarget.Resolve(folder.Root, "other.md#a-heading");

        Assert.Equal(MarkdownLinkKind.Local, target.Kind);
        Assert.Equal(Path.Combine(folder.Root, "other.md"), target.LocalPath);
    }

    [Theory]
    [InlineData("#a-heading")]
    [InlineData("#")]
    public void ALinkIntoTheDocumentItself_IsNeitherAFileNorAnAddress(string url)
    {
        MarkdownLinkTarget target = MarkdownLinkTarget.Resolve(@"C:\notes", url);

        Assert.Equal(MarkdownLinkKind.Fragment, target.Kind);
        Assert.Null(target.LocalPath);
        Assert.Null(target.Address);
    }

    [Fact]
    public void ARelativeLinkWithNoFolderToResolveItAgainst_IsUnresolvable()
    {
        // An unsaved buffer. Guessing the working directory here would quietly open the wrong file.
        MarkdownLinkTarget target = MarkdownLinkTarget.Resolve(basePath: null, "other-note.md");

        Assert.Equal(MarkdownLinkKind.Unresolvable, target.Kind);
        Assert.Equal("other-note.md", target.Display);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyLink_IsUnresolvable(string? url)
    {
        MarkdownLinkTarget target = MarkdownLinkTarget.Resolve(@"C:\notes", url);

        Assert.Equal(MarkdownLinkKind.Unresolvable, target.Kind);
        Assert.NotNull(target.Display);
    }

    [Fact]
    public void AnAbsolutePath_IsTakenAsWritten()
    {
        using TempLinkFolder folder = new();
        string path = Path.Combine(folder.Root, "elsewhere.md");

        MarkdownLinkTarget target = MarkdownLinkTarget.Resolve(basePath: null, path);

        Assert.Equal(MarkdownLinkKind.Local, target.Kind);
        Assert.Equal(path, target.LocalPath);
    }

    [Fact]
    public void AnEscapedLink_FindsTheFileTheEscapingMeant()
    {
        using TempLinkFolder folder = new();
        string spaced = folder.Write("my note.md");

        // What a file manager writes when it turns a path into a link. The file on disk has a space
        // in it, and nothing else is going to fix that up.
        MarkdownLinkTarget target = MarkdownLinkTarget.Resolve(folder.Root, "my%20note.md");

        Assert.Equal(MarkdownLinkKind.Local, target.Kind);
        Assert.Equal(spaced, target.LocalPath);
    }

    [Fact]
    public void AFileThatIsNotThere_IsStillALocalPath()
    {
        using TempLinkFolder folder = new();

        // Deliberately not an existence check: whether the file is there is the opener's problem to
        // report, and a link to a note the reader is about to create is not a broken link.
        MarkdownLinkTarget target = MarkdownLinkTarget.Resolve(folder.Root, "not-written-yet.md");

        Assert.Equal(MarkdownLinkKind.Local, target.Kind);
        Assert.Equal(Path.Combine(folder.Root, "not-written-yet.md"), target.LocalPath);
    }

    [Fact]
    public void ADirectory_IsLocalToo()
    {
        using TempLinkFolder folder = new();
        string sub = Path.Combine(folder.Root, "attachments");
        Directory.CreateDirectory(sub);

        MarkdownLinkTarget target = MarkdownLinkTarget.Resolve(folder.Root, "attachments");

        Assert.Equal(MarkdownLinkKind.Local, target.Kind);
        Assert.Equal(sub, target.LocalPath);
    }

    private sealed class TempLinkFolder : IDisposable
    {
        public TempLinkFolder()
        {
            Root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string Write(string name)
        {
            string path = Path.Combine(Root, name);
            File.WriteAllText(path, "a note\n");

            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // A throw-away folder that outlives the run is litter, not a failed test.
            }
        }
    }
}
