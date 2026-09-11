using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// <c>:set</c> — reads and writes the options the engine has.
/// </summary>
/// <remarks>
/// <para>
/// Vim's five spellings for each option: <c>name</c> turns a switch on (and shows a number),
/// <c>noname</c> turns it off, <c>name!</c> flips it, <c>name=value</c> sets a number, and
/// <c>name?</c> shows either. The names are vim's own, long and short — <c>ignorecase</c> or
/// <c>ic</c> — because they are the ones a vim user's fingers already know; the answers are written
/// with the long name, as vim writes them. Several arguments may follow one <c>:set</c>, and a bare
/// <c>:set</c> shows every option there is, which is short enough here to be the useful answer.
/// </para>
/// <para>
/// Only the options the engine reads are here — the three the search consults and the two the shift
/// does — and an unknown name is named as unknown rather than accepted and ignored, so that a
/// <c>:set hlsearch</c> that will never do anything says so.
/// </para>
/// </remarks>
internal static class VimSet
{
    private const string Unknown = "Unknown option: ";
    private const string Invalid = "Invalid argument: ";

    private static readonly Switch[] Switches =
    [
        new("ignorecase", "ic", options => options.IgnoreCase, (options, value) => options.IgnoreCase = value),
        new("smartcase", "scs", options => options.SmartCase, (options, value) => options.SmartCase = value),
        new("wrapscan", "ws", options => options.WrapScan, (options, value) => options.WrapScan = value),
        new("expandtab", "et", options => options.ExpandTab, (options, value) => options.ExpandTab = value),
    ];

    private static readonly Number[] Numbers =
    [
        // Never zero: vim reads a shiftwidth of 0 as "use tabstop", and there is no tabstop here for
        // it to fall back on — a shift of nothing would be a '>>' that does nothing, without a word.
        new("shiftwidth", "sw", options => options.ShiftWidth, (options, value) => options.ShiftWidth = value, Minimum: 1),
    ];

    /// <summary>
    /// Runs the argument of a <c>:set</c>.
    /// </summary>
    /// <param name="argument">What followed <c>set</c>, trimmed; the empty string shows everything.</param>
    /// <param name="options">The options to read and write.</param>
    /// <param name="message">
    /// What to show: the values that were asked for, or why an argument was refused. <see langword="null"/>
    /// when nothing was asked for and nothing went wrong.
    /// </param>
    /// <returns>
    /// Whether every argument was carried out. A refusal stops at the first, with what came before it
    /// already done — vim's rule.
    /// </returns>
    public static bool TryApply(string argument, VimOptions options, [NotNullWhen(false)] out string? message)
    {
        List<string> shown = [];

        if (argument.Length == 0)
        {
            foreach (Switch toggle in Switches)
            {
                shown.Add(Show(toggle, options));
            }

            foreach (Number number in Numbers)
            {
                shown.Add(Show(number, options));
            }

            message = string.Join(' ', shown);

            return true;
        }

        foreach (string word in argument.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryApplyOne(word, options, shown, out message))
            {
                return false;
            }
        }

        message = shown.Count > 0 ? string.Join(' ', shown) : null;

        return true;
    }

    private static bool TryApplyOne(string word, VimOptions options, List<string> shown, [NotNullWhen(false)] out string? message)
    {
        message = null;

        // The shape first, then the name: 'noic', 'ic!', 'ic?', 'sw=4', 'ic'.
        (string name, string? value, char suffix) = Split(word);

        if (Find(Switches, name) is { } toggle)
        {
            switch (suffix)
            {
                case '?':
                    shown.Add(Show(toggle, options));
                    break;

                case '!':
                    toggle.Write(options, !toggle.Read(options));
                    break;

                case '=':
                    message = Invalid + word;

                    return false;

                default:
                    toggle.Write(options, true);
                    break;
            }

            return true;
        }

        if (Find(Numbers, name) is { } number)
        {
            switch (suffix)
            {
                case '?':
                case '\0':
                    shown.Add(Show(number, options));
                    break;

                case '!':
                    message = Invalid + word;

                    return false;

                default:
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
                        || (parsed < number.Minimum))
                    {
                        message = Invalid + word;

                        return false;
                    }

                    number.Write(options, parsed);
                    break;
            }

            return true;
        }

        // 'no' is a prefix, not part of the name — and only a switch has an off to be turned to:
        // 'nosw' names an option, and asks it something a number cannot answer.
        if (name.StartsWith("no", StringComparison.Ordinal))
        {
            if (Find(Switches, name[2..]) is { } off)
            {
                if (suffix != '\0')
                {
                    message = Invalid + word;

                    return false;
                }

                off.Write(options, false);

                return true;
            }

            if (Find(Numbers, name[2..]) is not null)
            {
                message = Invalid + word;

                return false;
            }
        }

        message = Unknown + word;

        return false;
    }

    /// <summary>
    /// Takes an argument apart: the name, the value after an <c>=</c>, and which of the suffixes it
    /// carried — <c>=</c>, <c>?</c>, <c>!</c>, or <c>'\0'</c> for none.
    /// </summary>
    private static (string Name, string? Value, char Suffix) Split(string word)
    {
        int equals = word.IndexOf('=', StringComparison.Ordinal);

        if (equals >= 0)
        {
            return (word[..equals], word[(equals + 1)..], '=');
        }

        if ((word.Length > 1) && (word[^1] is '?' or '!'))
        {
            return (word[..^1], null, word[^1]);
        }

        return (word, null, '\0');
    }

    private static Switch? Find(Switch[] switches, string name)
        => Array.Find(switches, toggle => (toggle.Name == name) || (toggle.Short == name));

    private static Number? Find(Number[] numbers, string name)
        => Array.Find(numbers, number => (number.Name == name) || (number.Short == name));

    private static string Show(Switch toggle, VimOptions options)
        => toggle.Read(options) ? toggle.Name : "no" + toggle.Name;

    private static string Show(Number number, VimOptions options)
        => $"{number.Name}={number.Read(options).ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// A boolean option: its two names, and how to read and write it.
    /// </summary>
    private sealed record Switch(string Name, string Short, Func<VimOptions, bool> Read, Action<VimOptions, bool> Write);

    /// <summary>
    /// A numeric option: its two names, how to read and write it, and the least it may be set to.
    /// </summary>
    private sealed record Number(string Name, string Short, Func<VimOptions, int> Read, Action<VimOptions, int> Write, int Minimum);
}
