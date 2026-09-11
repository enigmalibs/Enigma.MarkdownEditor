using Enigma.MarkdownEditor.Core.Documents;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Documents;

/// <summary>
/// Tests for the line-ending detection and conversion the save path depends on: what the editor gets
/// wrong here shows up as a whole-file diff on a file the user changed one word in.
/// </summary>
public sealed class LineEndingsTests
{
    [Theory]
    [InlineData("one\r\ntwo\r\nthree", LineEndingStyle.CrLf)]
    [InlineData("one\ntwo\nthree", LineEndingStyle.Lf)]
    [InlineData("one\r\n", LineEndingStyle.CrLf)]
    [InlineData("one\n", LineEndingStyle.Lf)]
    public void AConsistentFile_IsDetectedAsWhatItIs(string text, LineEndingStyle expected)
        => Assert.Equal(expected, LineEndings.Detect(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a single line with no break at all")]
    public void AFileWithNoLineBreak_FallsBackToTheDefault(string? text)
        => Assert.Equal(LineEndings.Default, LineEndings.Detect(text));

    [Fact]
    public void AMixedFile_IsDetectedAsItsDominantStyle()
    {
        Assert.Equal(LineEndingStyle.CrLf, LineEndings.Detect("a\r\nb\r\nc\nd"));
        Assert.Equal(LineEndingStyle.Lf, LineEndings.Detect("a\nb\nc\r\nd"));
    }

    [Fact]
    public void AnEvenlyMixedFile_GoesToTheNormalizedForm()
        => Assert.Equal(LineEndingStyle.Lf, LineEndings.Detect("a\r\nb\nc"));

    [Fact]
    public void ALoneCarriageReturn_CountsAsNeitherAndNormalizesAway()
    {
        // Classic Mac OS. It is read as a break, but it is never written back as one.
        Assert.Equal(LineEndingStyle.Lf, LineEndings.Detect("a\rb\rc"));
        Assert.Equal("a\nb\nc", LineEndings.Normalize("a\rb\rc"));
    }

    [Theory]
    [InlineData("a\r\nb", "a\nb")]
    [InlineData("a\nb", "a\nb")]
    [InlineData("a\rb", "a\nb")]
    [InlineData("a\r\nb\nc\rd", "a\nb\nc\nd")]
    public void Normalizing_LeavesLineFeedsAlone(string text, string expected)
        => Assert.Equal(expected, LineEndings.Normalize(text));

    [Fact]
    public void NormalizingWhatIsAlreadyNormal_ReturnsTheVerySameString()
    {
        string text = "nothing to do here\nat all\n";

        // Not just equal — the same instance. Every autosave runs this over the whole document.
        Assert.Same(text, LineEndings.Normalize(text));
    }

    [Theory]
    [InlineData("a\nb\n", LineEndingStyle.CrLf, "a\r\nb\r\n")]
    [InlineData("a\nb\n", LineEndingStyle.Lf, "a\nb\n")]
    [InlineData("a\r\nb\r\n", LineEndingStyle.Lf, "a\nb\n")]
    [InlineData("a\r\nb\r\n", LineEndingStyle.CrLf, "a\r\nb\r\n")]
    public void Applying_ProducesExactlyOneTerminatorPerLine(string text, LineEndingStyle style, string expected)
        => Assert.Equal(expected, LineEndings.Apply(text, style));

    [Fact]
    public void ApplyingCrLfToTextThatAlreadyHasIt_DoesNotDoubleTheCarriageReturns()
    {
        // The bug this guards is a\r\nb becoming a\r\r\nb, which is what a naive replace produces.
        Assert.Equal("a\r\nb", LineEndings.Apply("a\r\nb", LineEndingStyle.CrLf));
    }

    [Fact]
    public void DetectAndApply_RoundTripAnyText()
    {
        const string original = "# Title\r\n\r\nBody text.\r\n";

        LineEndingStyle style = LineEndings.Detect(original);
        string normalized = LineEndings.Normalize(original);

        Assert.Equal(original, LineEndings.Apply(normalized, style));
    }
}
