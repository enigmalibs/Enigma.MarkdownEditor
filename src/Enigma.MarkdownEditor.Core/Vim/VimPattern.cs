using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Turns what was typed after a <c>/</c>, a <c>?</c> or a <c>:s</c> into the regular expression the
/// search runs.
/// </summary>
/// <remarks>
/// <para>
/// The one place a pattern is compiled, so that <c>/</c>, <c>?</c>, <c>*</c>, <c>#</c> and <c>:s</c>
/// cannot disagree about what a pattern means. The dialect is .NET's, not vim's: <c>\&lt;</c>,
/// <c>\(</c>, <c>\{n,m}</c> and <c>\v</c> are not translated, and the README says so. A half
/// translation — one that read <c>\(</c> as a group but left <c>\{</c> alone — would be worse than
/// either dialect whole, and vim's own is a large enough language that translating it is a project
/// rather than a phase.
/// </para>
/// <para>
/// Case is the one thing taken from vim rather than from .NET, because it is what a vim user's
/// fingers know: <c>ignorecase</c> and <c>smartcase</c> from the options, and <c>\c</c> or <c>\C</c>
/// anywhere in the pattern to settle it for this search alone — both stripped before the expression
/// is compiled, because .NET has no such escapes. Every expression is compiled with
/// <see cref="RegexOptions.Multiline"/>, so <c>^</c> and <c>$</c> are a line's ends as they are in
/// vim, and with a timeout of one second, so that a pattern that would never finish is reported
/// rather than waited for.
/// </para>
/// </remarks>
internal static class VimPattern
{
    /// <summary>
    /// What the status bar says about a match that ran out of time.
    /// </summary>
    public const string TimedOut = "Match timed out after one second.";

    /// <summary>
    /// How long one match may take before it is called a mistake.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    private const RegexOptions Base = RegexOptions.Multiline | RegexOptions.CultureInvariant;

    /// <summary>
    /// Compiles a pattern.
    /// </summary>
    /// <param name="pattern">The pattern as typed, <c>\c</c> and <c>\C</c> included.</param>
    /// <param name="options">The engine's options, for <c>ignorecase</c> and <c>smartcase</c>.</param>
    /// <param name="smartCase">
    /// Whether <c>smartcase</c> may apply. <c>*</c> and <c>#</c> say no, as vim's do: a capital in a
    /// word taken off the text says nothing about what the user meant.
    /// </param>
    /// <param name="ignoreCase">
    /// An answer that overrides the options — the <c>i</c> and <c>I</c> flags of <c>:s</c> — or
    /// <see langword="null"/> to read them.
    /// </param>
    /// <param name="regex">The expression, when the pattern is one.</param>
    /// <param name="error">
    /// Why it is not one, when it is not: what .NET says about it, without the pattern quoted back.
    /// </param>
    /// <returns>Whether the pattern compiled.</returns>
    public static bool TryCompile(
        string pattern,
        VimOptions options,
        bool smartCase,
        bool? ignoreCase,
        [NotNullWhen(true)] out Regex? regex,
        [NotNullWhen(false)] out string? error)
    {
        (string stripped, bool? forced) = StripCaseEscapes(pattern);

        // '\c' in the pattern beats the flag, the flag beats the options, and smartcase only ever
        // turns ignorecase off — which is vim's order too.
        bool ignore = forced
            ?? ignoreCase
            ?? (options.IgnoreCase && !(smartCase && options.SmartCase && HasUppercase(stripped)));

        try
        {
            regex = new Regex(stripped, ignore ? Base | RegexOptions.IgnoreCase : Base, Timeout);
            error = null;

            return true;
        }
        catch (ArgumentException exception)
        {
            regex = null;
            error = Describe(exception, stripped);

            return false;
        }
    }

    /// <summary>
    /// Takes <c>\c</c> and <c>\C</c> out of a pattern and says what they asked for.
    /// </summary>
    /// <returns>
    /// The pattern without them, and <see langword="true"/> for ignore case, <see langword="false"/>
    /// for match case, or <see langword="null"/> when neither was written. <c>\c</c> wins when both
    /// are, as it does in vim.
    /// </returns>
    private static (string Pattern, bool? IgnoreCase) StripCaseEscapes(string pattern)
    {
        StringBuilder stripped = new(pattern.Length);
        bool ignore = false;
        bool match = false;

        for (int index = 0; index < pattern.Length; index++)
        {
            char character = pattern[index];

            if ((character != '\\') || ((index + 1) >= pattern.Length))
            {
                stripped.Append(character);

                continue;
            }

            char next = pattern[++index];

            switch (next)
            {
                case 'c':
                    ignore = true;
                    break;

                case 'C':
                    match = true;
                    break;

                default:
                    stripped.Append(character).Append(next);
                    break;
            }
        }

        bool? forced = null;

        if (match)
        {
            forced = false;
        }

        if (ignore)
        {
            forced = true;
        }

        return (stripped.ToString(), forced);
    }

    /// <summary>
    /// Whether a pattern has a capital letter in it, escapes excluded — <c>\W</c> is a class, not a
    /// capital.
    /// </summary>
    private static bool HasUppercase(string pattern)
    {
        for (int index = 0; index < pattern.Length; index++)
        {
            if (pattern[index] == '\\')
            {
                index++;

                continue;
            }

            if (char.IsUpper(pattern[index]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// What is wrong with a pattern, in a line short enough for the status bar.
    /// </summary>
    /// <remarks>
    /// .NET opens every parse error by quoting the pattern and the offset back — <c>Invalid pattern
    /// 'a[' at offset 2. Unterminated [] set.</c> — and the status bar has just shown the pattern.
    /// The prefix is rebuilt from what is known and taken off when it matches, which leaves the part
    /// that says what is wrong; a message in any other shape is passed on whole.
    /// </remarks>
    private static string Describe(ArgumentException error, string pattern)
    {
        string message = error.Message;

        if (error is RegexParseException parse)
        {
            string prefix = $"Invalid pattern '{pattern}' at offset {parse.Offset}. ";

            if ((message.Length > prefix.Length) && message.StartsWith(prefix, StringComparison.Ordinal))
            {
                return message[prefix.Length..];
            }
        }

        return message;
    }
}
