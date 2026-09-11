using System;
using System.IO;
using Enigma.MarkdownEditor.Core.Markdown;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Markdown;

/// <summary>
/// Which file a note's image link means.
/// </summary>
/// <remarks>
/// Over real files in a throw-away folder, for the reason the rest of this suite gives: whether a
/// path resolves is the operating system's answer, and a stand-in file system would get it wrong in
/// the same way the code under test does.
/// </remarks>
public sealed class MarkdownImageSourceTests
{
    [Fact]
    public void ARelativeLink_ResolvesAgainstTheNotesFolder()
        => WithFolder(folder =>
        {
            string image = folder.WriteImage("assets/shot.png");

            MarkdownImageSource source = MarkdownImageSource.Resolve(folder.Root, "assets/shot.png");

            Assert.Equal(MarkdownImageKind.Local, source.Kind);
            Assert.True(source.Exists);
            Assert.Equal(image, source.LocalPath);
        });

    [Fact]
    public void ALinkOutOfTheNotesFolder_ResolvesToo()
        => WithFolder(folder =>
        {
            string image = folder.WriteImage("shared/logo.png");

            MarkdownImageSource source = MarkdownImageSource.Resolve(
                Path.Combine(folder.Root, "notes"),
                "../shared/logo.png");

            Assert.Equal(MarkdownImageKind.Local, source.Kind);
            Assert.True(source.Exists);
            Assert.Equal(image, source.LocalPath);
        });

    [Fact]
    public void AMissingFile_IsStillLocalAndSaysWhereItLookedAndWhatWasWritten()
        => WithFolder(folder =>
        {
            MarkdownImageSource source = MarkdownImageSource.Resolve(folder.Root, "assets/gone.png");

            Assert.Equal(MarkdownImageKind.Local, source.Kind);
            Assert.False(source.Exists);
            Assert.Equal(Path.Combine(folder.Root, "assets", "gone.png"), source.LocalPath);

            // The placeholder shows the link, not the resolved path: what the reader can act on is
            // the text that is in their document.
            Assert.Equal("assets/gone.png", source.Display);
        });

    [Fact]
    public void APercentEncodedName_FallsBackToTheNameItStandsFor()
        => WithFolder(folder =>
        {
            string image = folder.WriteImage("assets/my shot.png");

            MarkdownImageSource source = MarkdownImageSource.Resolve(folder.Root, "assets/my%20shot.png");

            Assert.True(source.Exists);
            Assert.Equal(image, source.LocalPath);
        });

    [Fact]
    public void AFileWhoseNameHasAPercentSign_WinsOverTheDecodedForm()
        => WithFolder(folder =>
        {
            // Tried first, so a name that genuinely contains the escape is not "corrected" past.
            string image = folder.WriteImage("assets/100%25.png");

            MarkdownImageSource source = MarkdownImageSource.Resolve(folder.Root, "assets/100%25.png");

            Assert.True(source.Exists);
            Assert.Equal(image, source.LocalPath);
        });

    [Fact]
    public void AnAbsolutePath_NeedsNoFolderToResolveAgainst()
        => WithFolder(folder =>
        {
            string image = folder.WriteImage("shot.png");

            MarkdownImageSource source = MarkdownImageSource.Resolve(basePath: null, image);

            Assert.Equal(MarkdownImageKind.Local, source.Kind);
            Assert.True(source.Exists);
        });

    [Fact]
    public void ARelativeLinkWithNoFolder_IsUnresolvable()
    {
        // An unsaved buffer has no folder. Resolving against the working directory would silently
        // show whatever happens to be beside the executable.
        MarkdownImageSource source = MarkdownImageSource.Resolve(basePath: null, "assets/shot.png");

        Assert.Equal(MarkdownImageKind.Unresolvable, source.Kind);
        Assert.Null(source.LocalPath);
        Assert.False(source.Exists);
    }

    [Theory]
    [InlineData("https://example.com/shot.png")]
    [InlineData("http://example.com/shot.png")]
    public void AnAddress_IsRemoteAndNotFetched(string url)
    {
        MarkdownImageSource source = MarkdownImageSource.Resolve(@"C:\notes", url);

        Assert.Equal(MarkdownImageKind.Remote, source.Kind);
        Assert.Null(source.LocalPath);
        Assert.Equal(url, source.Display);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("data:image/png;base64,iVBORw0K")]
    [InlineData("ftp://example.com/shot.png")]
    public void ALinkNothingCanOpen_IsUnresolvable(string url)
    {
        MarkdownImageSource source = MarkdownImageSource.Resolve(@"C:\notes", url);

        Assert.Equal(MarkdownImageKind.Unresolvable, source.Kind);
        Assert.Null(source.LocalPath);
    }

    [Fact]
    public void ANullLink_IsUnresolvableRatherThanAFailure()
    {
        MarkdownImageSource source = MarkdownImageSource.Resolve(@"C:\notes", url: null);

        Assert.Equal(MarkdownImageKind.Unresolvable, source.Kind);
        Assert.Equal(string.Empty, source.Display);
    }

    private static void WithFolder(Action<TempImageFolder> body)
    {
        using TempImageFolder folder = new();

        body(folder);
    }

    private sealed class TempImageFolder : IDisposable
    {
        public TempImageFolder()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "Enigma.MarkdownEditor.Tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        /// <summary>
        /// Writes a file at <paramref name="relativePath"/> and returns its absolute path. The bytes
        /// are irrelevant — nothing here decodes an image, only finds one.
        /// </summary>
        public string WriteImage(string relativePath)
        {
            string path = Path.GetFullPath(Path.Combine(Root, relativePath));

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47]);

            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A leftover temp folder is not worth failing a green test over.
            }
        }
    }
}
