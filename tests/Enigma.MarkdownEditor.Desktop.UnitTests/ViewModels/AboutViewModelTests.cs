using System;
using System.Collections.Generic;
using System.Linq;
using Enigma.MarkdownEditor.Core;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.ViewModels;

/// <summary>
/// Tests for what the About dialog says: the running build's identity, and the attribution list.
/// </summary>
public sealed class AboutViewModelTests
{
    [Fact]
    public void TheDefaultViewModel_DescribesTheRunningBuild()
    {
        AboutViewModel viewModel = new();

        Assert.Equal(AppInfo.DisplayName, viewModel.DisplayName);
        Assert.Equal(AppInfo.Version, viewModel.Version);
        Assert.Equal(AppInfo.BuildSha, viewModel.BuildSha);
        Assert.Equal(AppInfo.Copyright, viewModel.Copyright);
        Assert.NotEmpty(viewModel.Credits);
    }

    [Fact]
    public void AStampedBuild_HasABuildLineToShow()
        => Assert.True(Create(buildSha: "abcdef1").HasBuildSha);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ABuildWithNoRevision_HasNoBuildLine(string? buildSha)
    {
        AboutViewModel viewModel = Create(buildSha: buildSha);

        // The view binds IsVisible to this, so the line is absent rather than blank — which is the
        // difference between "built from a source drop" and "built from a commit nobody recorded".
        Assert.False(viewModel.HasBuildSha);
    }

    [Fact]
    public void ACopyrightNotice_IsShownWhenThereIsOne()
        => Assert.True(Create(copyright: "Copyright © 2026 Someone").HasCopyright);

    [Fact]
    public void NoCopyrightNotice_MeansNoCopyrightLine()
        => Assert.False(Create(copyright: string.Empty).HasCopyright);

    [Fact]
    public void TheCredits_NameEveryRedistributedComponentWithItsLicence()
    {
        IReadOnlyList<CreditEntry> credits = AboutViewModel.DefaultCredits;

        // The attribution the MIT, BSD and OFL terms ask for: a row missing its licence is the one
        // way this list can be present and still not do its job.
        Assert.All(credits, credit =>
        {
            Assert.NotEmpty(credit.Name);
            Assert.NotEmpty(credit.License);
        });

        Assert.Equal(credits.Count, credits.Select(credit => credit.Name).Distinct(StringComparer.Ordinal).Count());

        string[] names = [.. credits.Select(credit => credit.Name)];
        Assert.Contains("Avalonia", names);
        Assert.Contains("Markdig", names);
        Assert.Contains("NLog", names);
    }

    [Fact]
    public void EveryValue_IsRequired()
    {
        Assert.Throws<ArgumentNullException>(() => new AboutViewModel(null!, "abcdef1", "c", AboutViewModel.DefaultCredits));
        Assert.Throws<ArgumentNullException>(() => new AboutViewModel("1.0.0", "abcdef1", null!, AboutViewModel.DefaultCredits));
        Assert.Throws<ArgumentNullException>(() => new AboutViewModel("1.0.0", "abcdef1", "c", null!));
    }

    private static AboutViewModel Create(
        string version = "1.2.3",
        string? buildSha = "abcdef1",
        string copyright = "Copyright © 2026 Josué Clément")
        => new(version, buildSha, copyright, AboutViewModel.DefaultCredits);
}
