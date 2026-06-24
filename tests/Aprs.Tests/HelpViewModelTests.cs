using System.Xml.Linq;
using Aprs.Desktop.ViewModels;
using Xunit;

namespace Aprs.Tests;

public sealed class HelpViewModelTests : IDisposable
{
    private readonly string docsRoot = Path.Combine(Path.GetTempPath(), "APRSCommandHelpTests", Guid.NewGuid().ToString("N"));

    public HelpViewModelTests()
    {
        Directory.CreateDirectory(docsRoot);
        File.WriteAllText(Path.Combine(docsRoot, "USER_MANUAL.md"), "# APRS Command User Manual\n\nMap-first user documentation with `inline code`.");
        File.WriteAllText(Path.Combine(docsRoot, "QUICK_START.md"), "# Quick Start\n\nStart APRS Command receive-only.");
        File.WriteAllText(Path.Combine(docsRoot, "APRS_IS_SETUP_GUIDE.md"), "# APRS-IS Setup Guide\n\nConfigure APRS-IS receive only.");
        File.WriteAllText(Path.Combine(docsRoot, "RF_TNC_SETUP_GUIDE.md"), "# RF/TNC Setup Guide\n\nConfigure a TNC for receive only.");
        File.WriteAllText(Path.Combine(docsRoot, "MAP_AND_OFFLINE_MAPS_GUIDE.md"), "# Map and Offline Maps Guide\n\nUse the map and offline cache.");
        File.WriteAllText(Path.Combine(docsRoot, "SAFETY_AND_TRANSMIT_GUIDE.md"), "# Safety\n\nAPRS Command does not transmit by default.");
        File.WriteAllText(Path.Combine(docsRoot, "TROUBLESHOOTING.md"), "# Troubleshooting\n\nMap blank and APRS-IS connection checks.");
        File.WriteAllText(Path.Combine(docsRoot, "GLOSSARY.md"), "# Glossary\n\nAPRS, TNC, KISS, and iGate.");
    }

    public void Dispose()
    {
        if (Directory.Exists(docsRoot))
        {
            Directory.Delete(docsRoot, recursive: true);
        }
    }

    [Fact]
    public void HelpTopicsListLoadsCoreTopics()
    {
        var service = new HelpDocumentService(docsRoot);
        var topics = service.LoadTopics();

        Assert.Contains(topics, topic => topic.Title == "User Manual");
        Assert.Contains(topics, topic => topic.Title == "Quick Start");
        Assert.Contains(topics, topic => topic.Title == "Safety and Transmit Guide");
        Assert.Contains(topics, topic => topic.Title == "Troubleshooting");
        Assert.Contains(topics, topic => topic.Title == "Glossary");
        Assert.Contains(topics, topic => topic.Title == "About APRS Command");
    }

    [Fact]
    public void SelectingTopicLoadsContent()
    {
        var viewModel = new HelpViewModel(new HelpDocumentService(docsRoot));
        var quickStart = viewModel.AllTopics.Single(topic => topic.Title == "Quick Start");

        viewModel.SelectedTopic = quickStart;

        Assert.Equal("Quick Start", viewModel.SelectedTitle);
        Assert.Contains("receive-only", viewModel.SelectedContent, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Available offline", viewModel.SelectedAvailability);
    }

    [Fact]
    public void SelectedContentUsesReadablePlainText()
    {
        var viewModel = new HelpViewModel(new HelpDocumentService(docsRoot));
        var userManual = viewModel.AllTopics.Single(topic => topic.Title == "User Manual");

        viewModel.SelectedTopic = userManual;

        Assert.StartsWith("APRS Command User Manual", viewModel.SelectedContent, StringComparison.Ordinal);
        Assert.Contains("inline code", viewModel.SelectedContent, StringComparison.Ordinal);
        Assert.DoesNotContain("# APRS Command User Manual", viewModel.SelectedContent, StringComparison.Ordinal);
        Assert.DoesNotContain("`inline code`", viewModel.SelectedContent, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingDocumentDoesNotCrash()
    {
        var viewModel = new HelpViewModel(new HelpDocumentService(docsRoot));
        var installation = viewModel.AllTopics.Single(topic => topic.Title == "Installation Guide");

        viewModel.SelectedTopic = installation;

        Assert.False(installation.IsAvailable);
        Assert.Equal(HelpDocumentService.MissingDocumentMessage, viewModel.SelectedContent);
        Assert.Equal("Missing from this build", viewModel.SelectedAvailability);
    }

    [Fact]
    public void SearchFindsMatchingTopicAndContent()
    {
        var viewModel = new HelpViewModel(new HelpDocumentService(docsRoot));

        viewModel.SearchText = "blank";

        Assert.Contains(viewModel.FilteredTopics, topic => topic.Title == "Troubleshooting");
        Assert.DoesNotContain(viewModel.FilteredTopics, topic => topic.Title == "Quick Start");
    }

    [Theory]
    [InlineData("APRS-IS", "APRS-IS Setup Guide")]
    [InlineData("TNC", "RF/TNC Setup Guide")]
    [InlineData("map", "Map and Offline Maps Guide")]
    [InlineData("safety", "Safety and Transmit Guide")]
    [InlineData("troubleshooting", "Troubleshooting")]
    public void SearchFindsReleaseReadinessTopics(string searchText, string expectedTopicTitle)
    {
        var viewModel = new HelpViewModel(new HelpDocumentService(docsRoot));

        viewModel.SearchText = searchText;

        Assert.Contains(viewModel.FilteredTopics, topic => topic.Title == expectedTopicTitle);
    }

    [Fact]
    public void DefaultHelpViewModelInitializesSafely()
    {
        var viewModel = HelpViewModel.CreateDefault();

        Assert.NotEmpty(viewModel.AllTopics);
        Assert.NotNull(viewModel.SelectedTopic);
        Assert.Contains(viewModel.AllTopics, topic => topic.Title == "User Manual");
    }

    [Fact]
    public void DesktopProjectCopiesDocsToOutputAndPublishFolders()
    {
        var projectPath = Path.Combine(RepositoryRoot, "src", "Aprs.Desktop", "Aprs.Desktop.csproj");
        var project = XDocument.Load(projectPath);
        var docsItem = project
            .Descendants("Content")
            .Single(element => element.Attribute("Include")?.Value == @"..\..\docs\*.md");

        Assert.Equal(@"docs\%(Filename)%(Extension)", docsItem.Attribute("Link")?.Value);
        Assert.Equal("PreserveNewest", docsItem.Attribute("CopyToOutputDirectory")?.Value);
        Assert.Equal("PreserveNewest", docsItem.Attribute("CopyToPublishDirectory")?.Value);
    }

    [Fact]
    public void UserManualIsCopiedToTestOutput()
    {
        var outputDoc = Path.Combine(AppContext.BaseDirectory, "docs", "USER_MANUAL.md");

        Assert.True(File.Exists(outputDoc), outputDoc);
        Assert.Contains("APRS Command User Manual", File.ReadAllText(outputDoc), StringComparison.Ordinal);
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrossPlatformAprs.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }
}
