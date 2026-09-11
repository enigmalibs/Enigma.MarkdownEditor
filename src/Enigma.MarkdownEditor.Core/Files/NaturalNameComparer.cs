using System;
using System.Collections.Generic;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// Orders file and directory names the way a person reading them expects: case-insensitively, and
/// with runs of digits compared as numbers rather than as text.
/// </summary>
/// <remarks>
/// Ordinal order puts <c>chapter-10.md</c> before <c>chapter-2.md</c>, which is wrong in the only way
/// that matters — the tree is read top to bottom. This comparer reads each digit run as a number, so
/// the same two files come out 2 then 10.
/// </remarks>
public sealed class NaturalNameComparer : IComparer<string>
{
    /// <summary>
    /// Gets the shared instance. The comparer holds no state.
    /// </summary>
    public static NaturalNameComparer Instance { get; } = new();

    /// <summary>
    /// Compares two names.
    /// </summary>
    /// <param name="x">The first name.</param>
    /// <param name="y">The second name.</param>
    /// <returns>A negative value, zero, or a positive value, as the ordering demands.</returns>
    /// <remarks>
    /// Names that differ only in case or in leading zeros fall through to an ordinal comparison rather
    /// than being reported equal, so the ordering stays total — two distinct names never compare equal,
    /// and a sort over them is reproducible.
    /// </remarks>
    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        int left = 0;
        int right = 0;

        while ((left < x.Length) && (right < y.Length))
        {
            if (char.IsAsciiDigit(x[left]) && char.IsAsciiDigit(y[right]))
            {
                int leftStart = left;
                int rightStart = right;

                while ((left < x.Length) && char.IsAsciiDigit(x[left]))
                {
                    left++;
                }

                while ((right < y.Length) && char.IsAsciiDigit(y[right]))
                {
                    right++;
                }

                int numbers = CompareDigitRuns(
                    x.AsSpan(leftStart, left - leftStart),
                    y.AsSpan(rightStart, right - rightStart));

                if (numbers != 0)
                {
                    return numbers;
                }

                continue;
            }

            int letters = char.ToUpperInvariant(x[left]).CompareTo(char.ToUpperInvariant(y[right]));
            if (letters != 0)
            {
                return letters;
            }

            left++;
            right++;
        }

        if (left < x.Length)
        {
            return 1;
        }

        if (right < y.Length)
        {
            return -1;
        }

        return string.CompareOrdinal(x, y);
    }

    // Compared without ever parsing: a run of digits can be longer than any integer type, and a file
    // name is free to hold one.
    private static int CompareDigitRuns(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
    {
        ReadOnlySpan<char> left = TrimLeadingZeros(x);
        ReadOnlySpan<char> right = TrimLeadingZeros(y);

        return left.Length != right.Length
            ? left.Length - right.Length
            : left.SequenceCompareTo(right);
    }

    private static ReadOnlySpan<char> TrimLeadingZeros(ReadOnlySpan<char> digits)
    {
        int start = 0;

        // Stops one short of the end so that "000" trims to "0" rather than to nothing.
        while ((start < (digits.Length - 1)) && (digits[start] == '0'))
        {
            start++;
        }

        return digits[start..];
    }
}
