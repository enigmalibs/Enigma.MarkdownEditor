using Enigma.MarkdownEditor.Core;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests;

/// <summary>
/// Tests for the parsing behind <see cref="AppInfo.Version"/> and <see cref="AppInfo.BuildSha"/>.
/// </summary>
/// <remarks>
/// The properties themselves report the assembly this suite is running against, which a test cannot
/// vary — <see cref="AppInfoTests"/> covers what can be said about those. Everything that is a
/// decision rather than a lookup lives in <see cref="ProductVersion.From(string?, string?)"/>, and is
/// here.
/// </remarks>
public sealed class ProductVersionTests
{
    [Fact]
    public void AStampedBuild_ReportsTheVersionAndTheShortRevision()
    {
        ProductVersion product = ProductVersion.From("1.1.0+abcdef1234", "1.1.0");

        Assert.Equal("1.1.0", product.Version);
        Assert.Equal("abcdef1", product.BuildSha);
    }

    [Fact]
    public void AnUnstampedBuild_ReportsTheVersionAndNoRevision()
    {
        ProductVersion product = ProductVersion.From("1.1.0", "1.1.0");

        Assert.Equal("1.1.0", product.Version);
        Assert.Null(product.BuildSha);
    }

    [Fact]
    public void APreReleaseVersion_IsKeptWhole()
    {
        // Taken verbatim up to the '+', not re-parsed: System.Version would reject this outright, and
        // the suffix is part of what the csproj said the product is.
        ProductVersion product = ProductVersion.From("1.1.0-rc.1+abcdef1234", "1.1.0");

        Assert.Equal("1.1.0-rc.1", product.Version);
        Assert.Equal("abcdef1", product.BuildSha);
    }

    [Theory]
    [InlineData("1.1.0+abcdef1", "abcdef1")]
    [InlineData("1.1.0+abcd", "abcd")]
    public void AShortRevision_IsKeptAsItIs(string informational, string expected)
        => Assert.Equal(expected, ProductVersion.From(informational, "1.1.0").BuildSha);

    [Theory]
    [InlineData("1.1.0+")]
    [InlineData("1.1.0+   ")]
    public void ASeparatorWithNothingAfterIt_IsNoRevisionRatherThanAnEmptyOne(string informational)
    {
        ProductVersion product = ProductVersion.From(informational, "1.1.0");

        Assert.Equal("1.1.0", product.Version);
        Assert.Null(product.BuildSha);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+abcdef1234")]
    public void AnUnusableInformationalVersion_FallsBackRatherThanThrowing(string? informational)
    {
        ProductVersion product = ProductVersion.From(informational, "2.3.4");

        Assert.Equal("2.3.4", product.Version);

        // The fallback is the plain assembly version, which carries no revision — reporting the one
        // from a value that was rejected would be reporting half of something unusable.
        Assert.Null(product.BuildSha);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithNoFallbackEither_TheVersionIsStillSomething(string? fallback)
    {
        ProductVersion product = ProductVersion.From(null, fallback);

        Assert.Equal(ProductVersion.Unknown, product.Version);
        Assert.Null(product.BuildSha);
    }

    [Fact]
    public void SurroundingWhiteSpace_IsTrimmedOffBothParts()
    {
        ProductVersion product = ProductVersion.From("  1.1.0+ abcdef1234  ", "1.1.0");

        Assert.Equal("1.1.0", product.Version);
        Assert.Equal("abcdef1", product.BuildSha);
    }
}
