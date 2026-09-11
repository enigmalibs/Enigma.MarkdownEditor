using System;
using System.Collections.Generic;
using Enigma.MarkdownEditor.Core.Markdown;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Markdown;

/// <summary>
/// The arithmetic behind the preview's scroll sync: which block a source line belongs to, and where
/// inside it.
/// </summary>
/// <remarks>
/// All of the sync that can be asserted without a window, which is why it is a Core type at all. What
/// the control then does with the answer — measure the block and set an offset — is covered by the
/// renderer's own tests.
/// </remarks>
public sealed class MarkdownLineMapTests
{
    [Fact]
    public void AnEmptyMap_FindsNothing()
    {
        Assert.True(MarkdownLineMap.Empty.IsEmpty);
        Assert.Equal(0, MarkdownLineMap.Empty.Count);
        Assert.False(MarkdownLineMap.Empty.TryFind(1, out _));
    }

    [Fact]
    public void NoStartLines_IsTheEmptyMap()
        => Assert.Same(MarkdownLineMap.Empty, MarkdownLineMap.For([], 10));

    [Fact]
    public void AnEmptyMap_HasNoLineToGiveBack()
        => Assert.Equal(0, MarkdownLineMap.Empty.LineAt(new MarkdownLinePosition(0, 0.5)));

    [Fact]
    public void EachBlock_OwnsTheLinesUpToTheNextOne()
    {
        // A heading on line 1, a paragraph on 3, a fence on 6, in a document of 12 lines.
        MarkdownLineMap map = MarkdownLineMap.For([1, 3, 6], 12);

        Assert.Equal(3, map.Count);
        Assert.False(map.IsEmpty);

        Assert.Equal((1, 2), (map.StartLineAt(0), map.EndLineAt(0)));
        Assert.Equal((3, 5), (map.StartLineAt(1), map.EndLineAt(1)));

        // The last block runs to the end of the document: the blank space under it is its own.
        Assert.Equal((6, 12), (map.StartLineAt(2), map.EndLineAt(2)));
    }

    [Fact]
    public void ADocumentShorterThanItsLastBlockClaims_IsNotBelievedOverTheBlock()
    {
        MarkdownLineMap map = MarkdownLineMap.For([1, 40], 5);

        Assert.Equal(40, map.EndLineAt(1));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(12, 2)]
    public void ALine_FindsTheBlockThatOwnsIt(int line, int expected)
    {
        MarkdownLineMap map = MarkdownLineMap.For([1, 3, 6], 12);

        Assert.True(map.TryFind(line, out MarkdownLinePosition position));
        Assert.Equal(expected, position.Index);
    }

    [Fact]
    public void TheFirstLineOfABlock_IsAtItsStart()
    {
        MarkdownLineMap map = MarkdownLineMap.For([1, 3, 6], 12);

        Assert.True(map.TryFind(3, out MarkdownLinePosition position));
        Assert.Equal(0d, position.Fraction);
    }

    [Fact]
    public void ALineInsideALongBlock_IsPartWayThroughIt()
    {
        // One block, lines 1 to 10.
        MarkdownLineMap map = MarkdownLineMap.For([1], 10);

        Assert.True(map.TryFind(6, out MarkdownLinePosition position));

        // Five lines into ten: half way, and never all the way — a fraction of 1 would put the view
        // on the block below.
        Assert.Equal(0.5, position.Fraction, 3);
        Assert.True(position.Fraction < 1);
    }

    [Fact]
    public void ASingleLineBlock_IsNeverPartWayThrough()
    {
        MarkdownLineMap map = MarkdownLineMap.For([1, 2, 3], 3);

        Assert.True(map.TryFind(2, out MarkdownLinePosition position));
        Assert.Equal(0d, position.Fraction);
    }

    [Fact]
    public void TwoBlocksOnOneLine_LeaveTheFirstWithNoLinesOfItsOwn()
    {
        MarkdownLineMap map = MarkdownLineMap.For([1, 1, 4], 6);

        Assert.Equal((1, 1), (map.StartLineAt(0), map.EndLineAt(0)));

        // The later of the two wins the line: it is the one whose content actually starts there.
        Assert.True(map.TryFind(1, out MarkdownLinePosition position));
        Assert.Equal(1, position.Index);
    }

    [Fact]
    public void ALineAboveTheFirstBlock_AnswersWithTheFirstBlock()
    {
        // Front matter, say: the render starts below line 1, but the viewport can still sit on it.
        MarkdownLineMap map = MarkdownLineMap.For([5, 9], 12);

        Assert.True(map.TryFind(1, out MarkdownLinePosition position));
        Assert.Equal(new MarkdownLinePosition(0, 0), position);
    }

    [Fact]
    public void ALinePastTheEnd_AnswersWithTheLastBlock()
    {
        MarkdownLineMap map = MarkdownLineMap.For([1, 3], 6);

        Assert.True(map.TryFind(400, out MarkdownLinePosition position));
        Assert.Equal(1, position.Index);
        Assert.True(position.Fraction <= 1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(11)]
    [InlineData(20)]
    public void EveryLine_FindsTheSameBlockAWalkWouldHaveFound(int line)
    {
        int[] starts = [1, 4, 7, 11, 19];
        MarkdownLineMap map = MarkdownLineMap.For(starts, 30);

        int expected = 0;

        for (int index = 0; index < starts.Length; index++)
        {
            if (starts[index] <= line)
            {
                expected = index;
            }
        }

        Assert.True(map.TryFind(line, out MarkdownLinePosition position));
        Assert.Equal(expected, position.Index);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    public void ALine_SurvivesTheRoundTripThroughAPosition(int line)
    {
        MarkdownLineMap map = MarkdownLineMap.For([1, 3, 8], 12);

        Assert.True(map.TryFind(line, out MarkdownLinePosition position));

        // What keeps the preview where the reader left it across a re-render: the line is read off
        // the old visuals as a position and looked up again in the new map.
        Assert.Equal(line, map.LineAt(position));
    }

    [Fact]
    public void APositionOutsideTheMap_IsClampedRatherThanRefused()
    {
        MarkdownLineMap map = MarkdownLineMap.For([1, 5], 9);

        Assert.Equal(9, map.LineAt(new MarkdownLinePosition(40, 4)));
        Assert.Equal(1, map.LineAt(new MarkdownLinePosition(-3, -1)));
    }

    [Fact]
    public void ABlockOutsideTheMap_CannotBeAskedAbout()
    {
        MarkdownLineMap map = MarkdownLineMap.For([1, 5], 9);

        Assert.Throws<ArgumentOutOfRangeException>(() => map.StartLineAt(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.StartLineAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.EndLineAt(2));
    }

    [Fact]
    public void StartLinesMustBeGiven()
        => Assert.Throws<ArgumentNullException>(() => MarkdownLineMap.For(null!, 1));

    [Fact]
    public void AStartLineBelowOne_IsRejected()
    {
        // Markdig counts from zero and the editor from one; a zero here means the conversion was
        // missed, and every answer the map gave would be one line out.
        IReadOnlyList<int> starts = [0, 4];

        Assert.Throws<ArgumentOutOfRangeException>(() => MarkdownLineMap.For(starts, 9));
    }

    [Fact]
    public void StartLinesOutOfDocumentOrder_AreRejected()
    {
        // Not sorted for the caller: out-of-order starts mean the render and the map disagree about
        // which block is which, and a plausible wrong answer is worse than a throw.
        IReadOnlyList<int> starts = [1, 9, 4];

        Assert.Throws<ArgumentException>(() => MarkdownLineMap.For(starts, 12));
    }
}
