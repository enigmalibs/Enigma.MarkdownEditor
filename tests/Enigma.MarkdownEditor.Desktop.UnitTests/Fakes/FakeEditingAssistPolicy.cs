using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// A policy a test can switch, standing in for the modes vim emulation will bring.
/// </summary>
internal sealed class FakeEditingAssistPolicy : IEditingAssistPolicy
{
    public bool IsActive { get; set; } = true;
}
