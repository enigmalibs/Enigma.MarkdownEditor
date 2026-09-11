using System.IO;
using Enigma.Configuration;

namespace Enigma.MarkdownEditor.Core.Settings;

/// <summary>
/// The half of <see cref="AppSettings"/>' validation that DataAnnotations cannot express.
/// </summary>
/// <remarks>
/// Ranges and required-ness stay on the properties as attributes — repeating them here would give
/// one broken value two error messages. This class carries only the rules about a value's *shape*.
/// </remarks>
public sealed class AppSettingsValidator : ConfigValidator<AppSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppSettingsValidator"/> class.
    /// </summary>
    public AppSettingsValidator()
        => RuleFor(settings => settings.ImageFolderName)
            .Must(IsSingleFolderName,
                "must be a single folder name, without a drive, a path separator, '.' or '..' — it is " +
                "resolved relative to the document it belongs to.");

    private static bool IsSingleFolderName(string? name)
    {
        // Null and empty are the [Required] attribute's business; this rule only judges the shape of a
        // value that is actually there, so it stays quiet rather than piling on a second message.
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        if (name is "." or "..")
        {
            return false;
        }

        return name.IndexOfAny(SeparatorsAndInvalidCharacters) < 0;
    }

    private static char[] SeparatorsAndInvalidCharacters { get; } = BuildForbiddenCharacters();

    private static char[] BuildForbiddenCharacters()
    {
        // Both separators are rejected on both platforms: a settings file is portable between a
        // Windows and a Linux install, so "assets/img" must fail the same way on either.
        char[] invalid = Path.GetInvalidFileNameChars();
        char[] forbidden = new char[invalid.Length + 3];
        invalid.CopyTo(forbidden, 0);
        forbidden[invalid.Length] = '/';
        forbidden[invalid.Length + 1] = '\\';
        forbidden[invalid.Length + 2] = ':';
        return forbidden;
    }
}
