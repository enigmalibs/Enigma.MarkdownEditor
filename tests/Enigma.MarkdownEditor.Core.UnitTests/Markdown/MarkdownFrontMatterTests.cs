using System.Collections.Generic;
using Enigma.MarkdownEditor.Core.Markdown;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Markdown;

/// <summary>
/// What the front-matter chip lists.
/// </summary>
/// <remarks>
/// The reading is deliberately shallow — top-level names off the left margin, nothing else — so these
/// pin down what it does <em>not</em> claim to understand as firmly as what it does.
/// </remarks>
public sealed class MarkdownFrontMatterTests
{
    [Fact]
    public void TheTopLevelKeys_AreListedInTheOrderTheyWereWritten()
        => Assert.Equal(
            ["title", "tags", "created"],
            MarkdownFrontMatter.Keys(
                """
                title: A note with front matter
                tags: [preview, fixture]
                created: 2026-08-28
                """));

    [Fact]
    public void TheFences_AreNotKeys()
        => Assert.Equal(
            ["title"],
            MarkdownFrontMatter.Keys(
                """
                ---
                title: A note
                ---
                """));

    [Fact]
    public void NestedEntries_BelongToTheKeyAboveThem()
        => Assert.Equal(
            ["author", "tags"],
            MarkdownFrontMatter.Keys(
                """
                author:
                  name: Someone
                  email: someone@example.com
                tags:
                  - one
                  - two
                """));

    [Fact]
    public void CommentsAndSequenceItems_AreNotKeys()
        => Assert.Equal(
            ["title"],
            MarkdownFrontMatter.Keys(
                """
                # a comment
                - not an entry
                title: A note
                """));

    [Fact]
    public void AColonInsideAValue_DoesNotMakeTheLineAnEntry()
    {
        // `https://example.com` on a line of its own is a continuation of the value above it, not a
        // key called `https`.
        IReadOnlyList<string> keys = MarkdownFrontMatter.Keys(
            """
            source:
              https://example.com
            """);

        Assert.Equal(["source"], keys);
    }

    [Fact]
    public void ADuplicatedKey_IsListedOnce()
        => Assert.Equal(["tags"], MarkdownFrontMatter.Keys("tags: one\ntags: two\n"));

    [Fact]
    public void AQuotedKey_LosesItsQuotes()
        => Assert.Equal(["my title"], MarkdownFrontMatter.Keys("\"my title\": A note\n"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\n")]
    [InlineData("---\n---")]
    public void NothingToList_IsAnEmptyList(string? text)
        => Assert.Empty(MarkdownFrontMatter.Keys(text));

    [Fact]
    public void CarriageReturns_AreNotPartOfAKey()
        => Assert.Equal(["title", "tags"], MarkdownFrontMatter.Keys("title: A note\r\ntags: one\r\n"));
}
