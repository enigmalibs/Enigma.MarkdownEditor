using Enigma.Configuration;

namespace Enigma.MarkdownEditor.Core.Configuration;

/// <summary>
/// What <see cref="ConfigRecovery.Load{T}(string, ConfigValidator{T}, string)"/> came back with: always
/// a usable configuration, plus the story of how it got there when the file on disk could not be used.
/// </summary>
/// <typeparam name="T">The settings class bound to the file.</typeparam>
public sealed class ConfigLoadResult<T>
    where T : class, new()
{
    internal ConfigLoadResult(
        IConfig<T> config,
        EnigmaConfigurationException? failure,
        string? quarantinedFilePath,
        bool persistenceDisabled)
    {
        Config = config;
        Failure = failure;
        QuarantinedFilePath = quarantinedFilePath;
        PersistenceDisabled = persistenceDisabled;
    }

    /// <summary>
    /// Gets the configuration to register and inject. Its value is always readable: on a failure it
    /// holds <typeparamref name="T"/>'s own defaults rather than throwing.
    /// </summary>
    public IConfig<T> Config { get; }

    /// <summary>
    /// Gets the reason the file could not be used, or <see langword="null"/> when it loaded normally.
    /// </summary>
    public EnigmaConfigurationException? Failure { get; }

    /// <summary>
    /// Gets the path the unreadable file was moved to, or <see langword="null"/> when nothing was
    /// moved — either because there was no failure, or because the move itself failed.
    /// </summary>
    public string? QuarantinedFilePath { get; }

    /// <summary>
    /// Gets a value indicating whether changes made in this session will be lost.
    /// </summary>
    /// <remarks>
    /// True only in the double-failure case: the file could neither be read nor moved aside, so
    /// <see cref="Config"/> is backed by a scratch path instead of the real one.
    /// </remarks>
    public bool PersistenceDisabled { get; }
}
