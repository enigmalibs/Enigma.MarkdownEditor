using System;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Turns what was typed after a <c>:</c> into an ex command.
/// </summary>
/// <remarks>
/// <para>
/// The second of the two grammars in this engine, and still the smaller one. Separate from
/// <see cref="KeySequenceParser"/> because it reads a finished line rather than a stream of
/// keystrokes: there is nothing pending about <c>:wq</c>, and nothing to cancel — the whole string
/// arrives at once when Enter is pressed.
/// </para>
/// <para>
/// The grammar is vim's: an optional range, a name, an optional <c>!</c>, and whatever follows as the
/// argument. The range is where most of the grammar is — <c>%</c>, a number, <c>.</c>, <c>$</c>,
/// <c>'&lt;</c> and <c>'&gt;</c>, each with <c>+N</c> and <c>-N</c> after it, one or two of them with
/// a comma between — and it is read into a shape the engine resolves against the buffer, because the
/// parser has no buffer to ask which line is <c>.</c>.
/// </para>
/// <para>
/// What a name <em>means</em> is not decided here. <see cref="ParseLine"/> hands back every line that
/// has the shape of one, and the engine runs the ones it knows — <c>s</c>, <c>set</c>, a range on its
/// own — and names the rest in the status bar. The four file requests are the exception, because they
/// leave the engine: <see cref="Parse"/> reads a line as one of them or as nothing, and it is
/// deliberately strict. Not the spelled-out <c>:write</c>, not <c>:wa</c>, not <c>:w!</c>: every one
/// of those is a command this editor would have to mean something by, and a parser that quietly read
/// <c>:wqa</c> as <c>:wq</c> would be acting on a command the user meant something else by. <c>:q!</c>
/// is read as exactly what it says — discard — rather than as a <c>:q</c> with a typo, which is the
/// one place a near miss would cost work.
/// </para>
/// </remarks>
public static class ExCommandParser
{
    // Far past any line a file has, and low enough that ten times it plus nine cannot overflow: a
    // number in a range is the one part of this grammar a stuck key can grow without limit.
    private const int MaxLine = 100_000_000;

    /// <summary>
    /// Reads an ex command that leaves the engine.
    /// </summary>
    /// <param name="line">
    /// What was typed after the <c>:</c>. Surrounding whitespace is ignored — <c>: w </c> is
    /// <c>:w</c>, because a stray space is a slip rather than a different command.
    /// </param>
    /// <returns>
    /// The command, or <see langword="null"/> when the line is not one of the four file requests —
    /// including when it is an ex line the engine runs itself.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="line"/> is <see langword="null"/>.</exception>
    public static VimExCommand? Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return ParseLine(line) is { } parsed ? FileCommand(parsed) : null;
    }

    /// <summary>
    /// Reads an ex line into its parts.
    /// </summary>
    /// <param name="line">What was typed after the <c>:</c>.</param>
    /// <returns>
    /// The line, or <see langword="null"/> when it has not the shape of one: nothing on it, a range
    /// that does not read, or something other than a name where the name should be.
    /// </returns>
    internal static VimExLine? ParseLine(string line)
    {
        string text = line.Trim();
        int at = 0;

        if (!TryReadRange(text, ref at, out VimExRange? range))
        {
            return null;
        }

        SkipBlanks(text, ref at);

        int nameStart = at;

        while ((at < text.Length) && char.IsAsciiLetter(text[at]))
        {
            at++;
        }

        string name = text[nameStart..at];
        bool bang = (name.Length > 0) && (at < text.Length) && (text[at] == '!');

        if (bang)
        {
            at++;
        }

        string argument = text[at..].Trim();

        // A range on its own is a jump. A range — or nothing — followed by something that is not a
        // name is not a line at all: ':3 foo' and ':!ls' are refused here rather than read as a jump
        // with a tail.
        if ((name.Length == 0) && ((argument.Length > 0) || (range is null)))
        {
            return null;
        }

        return new VimExLine(range, name, bang, argument);
    }

    /// <summary>
    /// Which file request a line is, for the four that leave the engine.
    /// </summary>
    /// <param name="line">A parsed line.</param>
    /// <returns>
    /// The request, or <see langword="null"/>: for a name that is not one of the four, for a bang the
    /// four do not take, and for a range or an argument, which none of them takes here — <c>:1,3w</c>
    /// writes part of a file to another in vim, and this editor has no such thing.
    /// </returns>
    internal static VimExCommand? FileCommand(VimExLine line)
    {
        if ((line.Range is not null) || (line.Argument.Length > 0))
        {
            return null;
        }

        return (line.Name, line.Bang) switch
        {
            ("w", false) => VimExCommand.Write,
            ("q", false) => VimExCommand.Quit,
            ("q", true) => VimExCommand.QuitDiscard,

            // ':x' writes only when the buffer has changed, where ':wq' writes regardless. The
            // application saves an unchanged buffer as a no-op, so the two come out the same here.
            ("wq", _) or ("x", false) => VimExCommand.WriteQuit,
            _ => null,
        };
    }

    /// <summary>
    /// Reads the range at the front of a line, if there is one.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when there is the start of a range that does not finish — a comma with
    /// nothing after it, a <c>'</c> with no mark the engine knows.
    /// </returns>
    private static bool TryReadRange(string text, ref int at, out VimExRange? range)
    {
        range = null;
        SkipBlanks(text, ref at);

        if ((at < text.Length) && (text[at] == '%'))
        {
            at++;
            range = new VimExRange(VimExAddress.Number(1), VimExAddress.Last);

            return true;
        }

        if (!TryReadAddress(text, ref at, out VimExAddress? first))
        {
            return false;
        }

        if (first is not { } start)
        {
            return true;
        }

        SkipBlanks(text, ref at);

        if ((at >= text.Length) || (text[at] != ','))
        {
            range = new VimExRange(start, start);

            return true;
        }

        at++;
        SkipBlanks(text, ref at);

        if (!TryReadAddress(text, ref at, out VimExAddress? second) || (second is not { } end))
        {
            return false;
        }

        range = new VimExRange(start, end);

        return true;
    }

    /// <summary>
    /// Reads one address: an optional base and any number of <c>+N</c> / <c>-N</c> after it.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> for a <c>'</c> followed by anything but <c>&lt;</c> or <c>&gt;</c>;
    /// otherwise <see langword="true"/>, with <paramref name="address"/> <see langword="null"/> when
    /// there was no address to read.
    /// </returns>
    private static bool TryReadAddress(string text, ref int at, out VimExAddress? address)
    {
        address = null;
        VimExAddressKind? kind = null;
        int line = 0;

        if (at < text.Length)
        {
            switch (text[at])
            {
                case >= '0' and <= '9':
                    kind = VimExAddressKind.Line;
                    line = ReadNumber(text, ref at);
                    break;

                case '.':
                    kind = VimExAddressKind.Current;
                    at++;
                    break;

                case '$':
                    kind = VimExAddressKind.Last;
                    at++;
                    break;

                case '\'':

                    // The two marks a selection leaves. Vim has twenty-six more, and none of them
                    // exists here, so a mark that is not one of the two is not an address.
                    if ((at + 1) >= text.Length)
                    {
                        return false;
                    }

                    kind = text[at + 1] switch
                    {
                        '<' => VimExAddressKind.VisualStart,
                        '>' => VimExAddressKind.VisualEnd,
                        _ => null,
                    };

                    if (kind is null)
                    {
                        return false;
                    }

                    at += 2;
                    break;

                default:
                    break;
            }
        }

        int offset = 0;
        bool shifted = false;

        while ((at < text.Length) && (text[at] is '+' or '-'))
        {
            int sign = text[at] == '+' ? 1 : -1;
            at++;

            int amount = ((at < text.Length) && char.IsAsciiDigit(text[at])) ? ReadNumber(text, ref at) : 1;

            offset = Math.Clamp(offset + (sign * amount), -MaxLine, MaxLine);
            shifted = true;
        }

        if ((kind is null) && !shifted)
        {
            return true;
        }

        address = new VimExAddress(kind ?? VimExAddressKind.Current, line, offset);

        return true;
    }

    private static int ReadNumber(string text, ref int at)
    {
        int value = 0;

        while ((at < text.Length) && char.IsAsciiDigit(text[at]))
        {
            value = Math.Min((value * 10) + (text[at] - '0'), MaxLine);
            at++;
        }

        return value;
    }

    private static void SkipBlanks(string text, ref int at)
    {
        while ((at < text.Length) && ((text[at] == ' ') || (text[at] == '\t')))
        {
            at++;
        }
    }
}
