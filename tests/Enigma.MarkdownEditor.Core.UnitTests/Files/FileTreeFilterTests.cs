using Enigma.MarkdownEditor.Core.Files;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Files;

/// <summary>
/// Tests for the tree's filter rules. They are the whole of what the tree does or does not show, so
/// they are pinned exactly rather than sampled.
/// </summary>
public sealed class FileTreeFilterTests
{
    [Theory]
    [InlineData("notes.md")]
    [InlineData("NOTES.MD")]
    [InlineData("readme.markdown")]
    public void AMarkdownFile_IsShownWithoutTheSetting(string name)
        => Assert.True(FileTreeFilter.Includes(name, isDirectory: false, isHidden: false, showAllFiles: false));

    [Theory]
    [InlineData("photo.png")]
    [InlineData("notes.txt")]
    [InlineData("archive.zip")]
    [InlineData("Makefile")]
    [InlineData("notes.mdx")]
    public void AnythingElse_IsHiddenUntilTheSettingSaysOtherwise(string name)
    {
        Assert.False(FileTreeFilter.Includes(name, isDirectory: false, isHidden: false, showAllFiles: false));
        Assert.True(FileTreeFilter.Includes(name, isDirectory: false, isHidden: false, showAllFiles: true));
    }

    [Fact]
    public void ADirectory_IsAlwaysShown()
    {
        Assert.True(FileTreeFilter.Includes("docs", isDirectory: true, isHidden: false, showAllFiles: false));
        Assert.True(FileTreeFilter.Includes("docs", isDirectory: true, isHidden: false, showAllFiles: true));
    }

    [Theory]
    [InlineData(".git", true)]
    [InlineData(".obsidian", true)]
    [InlineData(".gitignore", false)]
    [InlineData(".hidden.md", false)]
    public void ADotEntry_IsExcludedWhateverTheSettingSays(string name, bool isDirectory)
    {
        Assert.False(FileTreeFilter.Includes(name, isDirectory, isHidden: false, showAllFiles: false));

        // The setting is there to reveal the user's own files, not a repository's object database —
        // and a tree that can be made to walk into one is a tree that hangs.
        Assert.False(FileTreeFilter.Includes(name, isDirectory, isHidden: false, showAllFiles: true));
    }

    [Theory]
    [InlineData("notes.md", false)]
    [InlineData("secrets", true)]
    public void AHiddenOrSystemEntry_IsExcludedWhateverTheSettingSays(string name, bool isDirectory)
    {
        Assert.False(FileTreeFilter.Includes(name, isDirectory, isHidden: true, showAllFiles: false));
        Assert.False(FileTreeFilter.Includes(name, isDirectory, isHidden: true, showAllFiles: true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ANamelessEntry_IsExcluded(string? name)
        => Assert.False(FileTreeFilter.Includes(name, isDirectory: true, isHidden: false, showAllFiles: true));

    [Theory]
    [InlineData("notes.md", true)]
    [InlineData("notes.MARKDOWN", true)]
    [InlineData("notes.txt", false)]
    [InlineData("notes", false)]
    [InlineData(null, false)]
    public void IsMarkdown_LooksOnlyAtTheExtension(string? path, bool expected)
        => Assert.Equal(expected, FileTreeFilter.IsMarkdown(path));

    [Fact]
    public void Markdown_AlwaysOpensInTheEditor()
    {
        Assert.True(FileTreeFilter.OpensInEditor("/tmp/notes.md", showAllFiles: false));
        Assert.True(FileTreeFilter.OpensInEditor("/tmp/notes.md", showAllFiles: true));
    }

    [Theory]
    [InlineData("/tmp/notes.txt")]
    [InlineData("/tmp/config.json")]
    [InlineData("/tmp/Program.cs")]
    public void AnotherTextFile_OpensInTheEditorOnlyWhileTheTreeIsShowingIt(string path)
    {
        Assert.False(FileTreeFilter.OpensInEditor(path, showAllFiles: false));
        Assert.True(FileTreeFilter.OpensInEditor(path, showAllFiles: true));
    }

    [Theory]
    [InlineData("/tmp/photo.png")]
    [InlineData("/tmp/archive.zip")]
    [InlineData("/tmp/report.pdf")]
    [InlineData("/tmp/Makefile")]
    public void AnythingBinaryOrUnknown_GoesToTheOperatingSystem(string path)
    {
        // The safe direction to be wrong in: the OS opening a text file beats this editor opening a
        // 400 MB binary as one string.
        Assert.False(FileTreeFilter.OpensInEditor(path, showAllFiles: false));
        Assert.False(FileTreeFilter.OpensInEditor(path, showAllFiles: true));
    }
}
