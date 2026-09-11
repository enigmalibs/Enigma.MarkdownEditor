using System;
using System.IO;
using Enigma.MarkdownEditor.Core.Markdown;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Markdown;

/// <summary>
/// What a file the editor has just created starts with: a title for a note, nothing for anything else.
/// </summary>
/// <remarks>
/// The decision is over a name alone, so all of it is reachable without a file system — which is the
/// point of it living in <c>Core</c> rather than in the tree that calls it.
/// </remarks>
public sealed class MarkdownScaffoldTests
{
    [Theory]
    [InlineData("notes.md", "# notes\n\n")]
    [InlineData("notes.markdown", "# notes\n\n")]
    [InlineData("NOTES.MD", "# NOTES\n\n")]
    [InlineData("stand-up notes.md", "# stand-up notes\n\n")]
    [InlineData("2026-09-10.stand-up.md", "# 2026-09-10.stand-up\n\n")]
    [InlineData("  spaced  .md", "# spaced\n\n")]
    public void AMarkdownFile_StartsWithItsOwnNameAsATitle(string name, string expected)
        => Assert.Equal(expected, MarkdownScaffold.For(name));

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("photo.png")]
    [InlineData("data.json")]
    [InlineData("script.ps1")]
    [InlineData("notes")]
    public void AFileThatIsNotMarkdown_StartsEmpty(string name) => Assert.Equal(string.Empty, MarkdownScaffold.For(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoNameAtAll_StartsEmpty(string? name) => Assert.Equal(string.Empty, MarkdownScaffold.For(name));

    [Theory]
    [InlineData(".md")]
    [InlineData("   .md")]
    [InlineData(".markdown")]
    public void AMarkdownFileWithNoNameInFrontOfItsExtension_StartsEmpty(string name)
    {
        // '# ' on its own is not a title, and a heading the user has to finish is worse than no
        // heading at all.
        Assert.Equal(string.Empty, MarkdownScaffold.For(name));
    }

    [Fact]
    public void ANameCarryingItsFolder_IsReadAsTheFileAlone()
    {
        // The tree hands over what the user typed, but a full path has to give the same answer — the
        // title is the file's, never the folder's.
        Assert.Equal("# idea\n\n", MarkdownScaffold.For(Path.Combine("C:", "notes", "idea.md")));
    }

    [Fact]
    public void TheScaffold_EndsWithABlankLine()
    {
        string scaffold = MarkdownScaffold.For("idea.md");

        // The first paragraph is one Enter away rather than against the heading, and the caret has a
        // line of its own to land on.
        Assert.EndsWith("\n\n", scaffold, StringComparison.Ordinal);
        Assert.Equal(["# idea", string.Empty, string.Empty], scaffold.Split('\n'));
    }
}
