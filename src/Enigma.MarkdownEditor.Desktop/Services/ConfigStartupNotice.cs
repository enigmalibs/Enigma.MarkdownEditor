using System;
using System.Collections.Generic;
using System.Linq;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Configuration;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Turns a configuration load that went wrong into something worth showing the user.
/// </summary>
/// <remarks>
/// Shared by every file the editor recovers rather than crashes on, because the three things the user
/// needs to know are the same each time: what the editor started with instead, where their file went,
/// and what was wrong with it. Only the wording of the first two is per-file.
/// </remarks>
internal static class ConfigStartupNotice
{
    private const int MaxListedErrors = 5;

    /// <summary>
    /// Builds the notice for one configuration load.
    /// </summary>
    /// <typeparam name="T">The settings class bound to the file.</typeparam>
    /// <param name="result">What the load came back with.</param>
    /// <param name="title">The heading, naming the file in the user's terms.</param>
    /// <param name="startedWith">
    /// What the editor is running on instead, as a clause following "The editor started" — for example
    /// "with default settings".
    /// </param>
    /// <returns>
    /// <see cref="StartupNotice.None"/> when the file loaded normally; otherwise the notice to show.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    internal static StartupNotice For<T>(ConfigLoadResult<T> result, string title, string startedWith)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Failure is null)
        {
            return StartupNotice.None;
        }

        string outcome = result switch
        {
            { PersistenceDisabled: true } =>
                $"The editor started {startedWith}, and could not move the file aside either — " +
                "changes made in this session will not be saved.",
            { QuarantinedFilePath: not null } =>
                $"The editor started {startedWith}. Your file was kept as " +
                $"'{result.QuarantinedFilePath}'; the next change you make writes a fresh one.",
            _ => $"The editor started {startedWith}.",
        };

        return new StartupNotice(title, $"{outcome} {Describe(result.Failure)}");
    }

    private static string Describe(EnigmaConfigurationException failure)
    {
        if (failure is not ConfigurationValidationException invalid)
        {
            return failure.Message;
        }

        // A validation failure already aggregates every broken rule, and its own Message repeats the
        // file path the notice has just quoted — so the errors are listed on their own instead.
        IReadOnlyList<ValidationError> errors = invalid.Errors;

        string listed = string.Join(" ", errors.Take(MaxListedErrors).Select(AsSentence));

        return errors.Count > MaxListedErrors
            ? $"{listed} And {errors.Count - MaxListedErrors} more."
            : listed;
    }

    // Both halves of the validation already punctuate their messages, so the notice must not add a
    // second full stop — but it cannot assume one either.
    private static string AsSentence(ValidationError error)
    {
        string text = error.ToString();

        return text.EndsWith('.') ? text : $"{text}.";
    }
}
