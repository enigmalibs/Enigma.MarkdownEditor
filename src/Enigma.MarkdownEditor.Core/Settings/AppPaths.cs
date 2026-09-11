using System.IO;
using Enigma.Configuration;

namespace Enigma.MarkdownEditor.Core.Settings;

/// <summary>
/// Where the application keeps the state it writes at run time.
/// </summary>
/// <remarks>
/// Everything sits under one per-user folder — <c>%APPDATA%\Enigma.MarkdownEditor</c> on Windows,
/// <c>~/.config/Enigma.MarkdownEditor</c> on Linux — so uninstalling is one folder to delete and a
/// support request is one folder to zip. Nothing here touches the disk; a write creates what it needs.
/// </remarks>
public static class AppPaths
{
    /// <summary>
    /// File name of the settings file.
    /// </summary>
    public const string SettingsFileName = "settings.json";

    /// <summary>
    /// File name the settings file is moved to when it cannot be read (see
    /// <see cref="SettingsLoader"/>).
    /// </summary>
    public const string QuarantinedSettingsFileName = "settings.invalid.json";

    /// <summary>
    /// File name of the workspaces file.
    /// </summary>
    public const string WorkspacesFileName = "workspaces.json";

    /// <summary>
    /// File name the workspaces file is moved to when it cannot be read.
    /// </summary>
    public const string QuarantinedWorkspacesFileName = "workspaces.invalid.json";

    /// <summary>
    /// File name of the session file.
    /// </summary>
    public const string SessionFileName = "session.json";

    /// <summary>
    /// File name the session file is moved to when it cannot be read.
    /// </summary>
    public const string QuarantinedSessionFileName = "session.invalid.json";

    /// <summary>
    /// Folder name, under the application-data folder, holding the log files.
    /// </summary>
    public const string LogsFolderName = "logs";

    /// <summary>
    /// File name of the current log file. Older days are archived beside it.
    /// </summary>
    public const string LogFileName = "app.log";

    /// <summary>
    /// Gets the absolute path of the settings file.
    /// </summary>
    public static string SettingsFile
        => ConfigPaths.InApplicationData(AppInfo.AppDataFolderName, SettingsFileName);

    /// <summary>
    /// Gets the absolute path of the workspaces file.
    /// </summary>
    public static string WorkspacesFile
        => ConfigPaths.InApplicationData(AppInfo.AppDataFolderName, WorkspacesFileName);

    /// <summary>
    /// Gets the absolute path of the session file.
    /// </summary>
    public static string SessionFile
        => ConfigPaths.InApplicationData(AppInfo.AppDataFolderName, SessionFileName);

    /// <summary>
    /// Gets the absolute path of the current log file.
    /// </summary>
    public static string LogFile
        => ConfigPaths.InApplicationData(
            AppInfo.AppDataFolderName,
            Path.Combine(LogsFolderName, LogFileName));

    /// <summary>
    /// Gets the absolute path of the folder holding the log files.
    /// </summary>
    public static string LogsFolder => Path.GetDirectoryName(LogFile)!;
}
