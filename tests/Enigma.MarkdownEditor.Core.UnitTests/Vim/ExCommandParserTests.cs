using System;
using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The four requests that leave the engine: six spellings, and everything else.
/// </summary>
/// <remarks>
/// Half of what these cases are for is the refusals. A parser that quietly read <c>:wqa</c> as
/// <c>:wq</c> or <c>:w!</c> as <c>:w</c> would be acting on a command the user meant something else
/// by, so every near miss is written down as one. <c>:q!</c> is the one former near miss that is now
/// read — as exactly what it says, which is the only reading that cannot cost work. The lines the
/// engine runs itself — <c>:s</c>, <c>:set</c>, a bare line number — are not file requests and come
/// back as nothing here; what they do is the engine's, and is held in <see cref="VimSubstituteTests"/>,
/// <see cref="VimSetTests"/> and <see cref="VimCommandLineTests"/>.
/// </remarks>
public sealed class ExCommandParserTests
{
    [Theory]
    [InlineData("w", VimExCommand.Write)]
    [InlineData("q", VimExCommand.Quit)]
    [InlineData("wq", VimExCommand.WriteQuit)]
    // The three spellings this phase adds. ':x' and ':wq!' are ':wq' to an editor whose save of an
    // unchanged buffer is a no-op, and ':q!' is its own request: discard, and do not ask.
    [InlineData("x", VimExCommand.WriteQuit)]
    [InlineData("wq!", VimExCommand.WriteQuit)]
    [InlineData("q!", VimExCommand.QuitDiscard)]
    public void TheSixSpellings_AreRead(string line, VimExCommand expected)
        => Assert.Equal(expected, ExCommandParser.Parse(line));

    [Theory]
    [InlineData(" w")]
    [InlineData("w ")]
    [InlineData("  wq  ")]
    [InlineData("\tq")]
    [InlineData(" q! ")]
    public void SurroundingWhitespace_IsASlipRatherThanADifferentCommand(string line)
        => Assert.NotNull(ExCommandParser.Parse(line));

    [Theory]
    // The near misses, each of which means something else in vim and none of which is implemented.
    // Read as the command it resembles, every one of them would write over a file the user did not
    // name or close tabs they did not name.
    [InlineData("w!")]
    [InlineData("x!")]
    [InlineData("wa")]
    [InlineData("qa")]
    [InlineData("qa!")]
    [InlineData("wqa")]
    [InlineData("e")]
    [InlineData("write")]
    [InlineData("quit")]
    [InlineData("w notes.md")]
    [InlineData("s/a/b/")]
    [InlineData("set ic")]
    [InlineData("42")]
    // A range on a file request: vim writes part of a file with ':1,3w', and there is no such thing
    // here.
    [InlineData("1,3w")]
    [InlineData("%q")]
    // Case matters: vim's own commands are lower case, and ':W' is a typo rather than ':w'.
    [InlineData("W")]
    [InlineData("Q")]
    [InlineData("X")]
    public void AnythingElse_IsNotACommand(string line)
        => Assert.Null(ExCommandParser.Parse(line));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyLine_IsNotACommandEither(string line)
        => Assert.Null(ExCommandParser.Parse(line));

    [Fact]
    public void ALineIsRequired()
        => Assert.Throws<ArgumentNullException>(() => ExCommandParser.Parse(null!));
}
