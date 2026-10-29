using System;
using System.Collections.Generic;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// A crash reporter that keeps what it was handed, in order.
/// </summary>
internal sealed class RecordingCrashReporter : ICrashReporter
{
    public List<(Exception Exception, UnhandledErrorSource Source)> Reports { get; } = [];

    public void Report(Exception exception, UnhandledErrorSource source) => Reports.Add((exception, source));
}
