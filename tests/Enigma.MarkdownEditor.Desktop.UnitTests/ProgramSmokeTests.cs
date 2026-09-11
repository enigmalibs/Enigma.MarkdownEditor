using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests;

/// <summary>
/// Bootstrap smoke test: proves the xUnit v3 harness runs and that the Desktop assembly is
/// referenced and loadable from the suite. The shell's own behaviour is covered by
/// <c>ViewModels/MainWindowViewModelTests</c> and <c>Extensions/ServiceCollectionExtensionsTests</c>.
/// </summary>
public sealed class ProgramSmokeTests
{
    [Fact]
    public void DesktopAssembly_IsLoadableAndNamedAsExpected()
        => Assert.Equal("Enigma.MarkdownEditor.Desktop", typeof(Program).Assembly.GetName().Name);
}
