using System;
using System.IO;
using Enigma.Configuration;

namespace Enigma.MarkdownEditor.Core.Configuration;

/// <summary>
/// Reads a configuration file at start-up, and guarantees a usable configuration even when it cannot.
/// </summary>
/// <remarks>
/// <para>
/// The application composes itself from its settings — the log level and the theme are both decided
/// before the first window exists — so a file is read once, eagerly, rather than lazily on first use.
/// That is also what makes a broken file recoverable: a failed load is caught here, while there is
/// still somewhere to go.
/// </para>
/// <para>
/// Recovery is quarantine-then-defaults. An unreadable file is moved aside — the user's content is
/// preserved, and the next write lays down a clean file — and the configuration is rebuilt over the
/// now-absent path, which yields the settings class's own property defaults. There is no in-place
/// recovery to attempt instead: a configuration whose load failed caches nothing, so every later read
/// of its value re-reads the same broken file and throws again.
/// </para>
/// </remarks>
public static class ConfigRecovery
{
    /// <summary>
    /// Loads a configuration file, recovering from a file that cannot be read.
    /// </summary>
    /// <typeparam name="T">The settings class bound to the file.</typeparam>
    /// <param name="filePath">Absolute path of the file to read.</param>
    /// <param name="validator">The validator run after the load and before every write.</param>
    /// <param name="quarantineFileName">
    /// File name, in the same folder, an unreadable file is moved to. A stamped variant is used when
    /// that name is itself taken by something that cannot be overwritten.
    /// </param>
    /// <returns>
    /// A result whose <see cref="ConfigLoadResult{T}.Config"/> is always readable, and whose
    /// <see cref="ConfigLoadResult{T}.Failure"/> says what went wrong when something did.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath"/> or <paramref name="quarantineFileName"/> is empty or white-space.
    /// </exception>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static ConfigLoadResult<T> Load<T>(
        string filePath,
        ConfigValidator<T> validator,
        string quarantineFileName)
        where T : class, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentException.ThrowIfNullOrWhiteSpace(quarantineFileName);

        (IConfig<T> config, EnigmaConfigurationException? failure) = TryLoad(filePath, validator);
        if (failure is null)
        {
            return new ConfigLoadResult<T>(config, null, null, persistenceDisabled: false);
        }

        string? quarantined = TryQuarantine(filePath, quarantineFileName);
        if (quarantined is not null)
        {
            (IConfig<T> recovered, EnigmaConfigurationException? stillBroken) = TryLoad(filePath, validator);
            if (stillBroken is null)
            {
                return new ConfigLoadResult<T>(recovered, failure, quarantined, persistenceDisabled: false);
            }
        }

        // Neither readable nor movable: the folder is locked down or the file is held open elsewhere.
        // Running from a scratch path keeps the editor usable and keeps the broken file untouched —
        // this session's changes are simply not persisted, which the caller reports to the user.
        IConfig<T> scratch = Create<T>(ScratchPath(filePath), validator);
        return new ConfigLoadResult<T>(scratch, failure, quarantined, persistenceDisabled: true);
    }

    private static IConfig<T> Create<T>(string path, ConfigValidator<T> validator)
        where T : class, new()
        => EnigmaConfig.FromFile(path, validator, optional: true);

    private static (IConfig<T> Config, EnigmaConfigurationException? Failure) TryLoad<T>(
        string path,
        ConfigValidator<T> validator)
        where T : class, new()
    {
        IConfig<T> config = Create(path, validator);
        try
        {
            // Forces the read, the binding and both halves of the validation while a fallback is
            // still possible. Loading is lazy, so without this the failure would surface later, from
            // whichever screen happened to read a value first.
            _ = config.Value;
            return (config, null);
        }
        catch (EnigmaConfigurationException failure)
        {
            return (config, failure);
        }
    }

    private static string? TryQuarantine(string filePath, string quarantineFileName)
    {
        string directory = Path.GetDirectoryName(filePath) ?? string.Empty;

        string primary = Path.Combine(directory, quarantineFileName);
        if (TryMove(filePath, primary))
        {
            return primary;
        }

        // The usual quarantine name is taken by something that cannot be overwritten — an open editor,
        // a read-only file. A stamped name still preserves the content, which matters more than tidiness.
        string stamped = Path.Combine(
            directory,
            $"{Path.GetFileNameWithoutExtension(quarantineFileName)}.{DateTime.Now:yyyyMMdd-HHmmss}" +
            Path.GetExtension(quarantineFileName));

        return TryMove(filePath, stamped) ? stamped : null;
    }

    private static bool TryMove(string source, string destination)
    {
        try
        {
            File.Move(source, destination, overwrite: true);
            return true;
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    private static string ScratchPath(string filePath)
        => Path.Combine(
            Path.GetTempPath(),
            AppInfo.AppDataFolderName,
            Path.GetRandomFileName(),
            Path.GetFileName(filePath));
}
