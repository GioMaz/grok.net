#addin nuget:?package=Cake.Coveralls&version=1.1.0
#addin nuget:?package=Cake.Coverlet&version=2.5.4

#tool nuget:?package=coveralls.io&version=1.4.2

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

var coverallsRepoToken = EnvironmentVariable("COVERALLS_REPO_TOKEN");
var nugetApiKey = EnvironmentVariable("NUGET_API_KEY");
var nugetApiUrl = EnvironmentVariable("NUGET_API_URL");

DirectoryPath artifactsDir = Directory("./artifacts/");
var testResultsDir = artifactsDir.Combine("test-results");
var testCoverageOutputFilePath = testResultsDir.CombineWithFilePath("OpenCover.xml");
var projectFileMain = File("./src/Grok.Net/Grok.Net.csproj");
var projectFilePowerShell = File("./src/Grok.Net.PowerShell/Grok.Net.PowerShell.csproj");

Task("Clean")
    .Does(() =>
{
    CleanDirectory(artifactsDir);
    CleanDirectories(GetDirectories("./**/obj") + GetDirectories("./**/bin"));
});

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() =>
{
    DotNetRestore(projectFileMain);
    DotNetRestore(projectFilePowerShell);
});

Task("Build")
    .IsDependentOn("Restore")
    .Does(() =>
{
    var settings = new DotNetBuildSettings
    { 
        Configuration = configuration,
        NoRestore = true,
        NoLogo = true
    };
    DotNetBuild(projectFileMain, settings);
    DotNetBuild(projectFilePowerShell, settings);
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    var settings = new DotNetCoreTestSettings
    {
        Configuration = configuration,
        NoLogo = true
    };

    DotNetCoreTest("./src/Grok.Net.Tests/Grok.Net.Tests.csproj", settings, new CoverletSettings
    {
        CollectCoverage = true,
        CoverletOutputFormat = CoverletOutputFormat.opencover,
        CoverletOutputDirectory = testResultsDir,
        CoverletOutputName = testCoverageOutputFilePath.GetFilename().ToString()
    });
});

Task("UploadTestReport")
    .IsDependentOn("Test")
    .WithCriteria((context) => FileExists(testCoverageOutputFilePath))
    .WithCriteria(!string.IsNullOrWhiteSpace(coverallsRepoToken))
    .WithCriteria((context) => !BuildSystem.IsPullRequest)
    .WithCriteria((context) => !BuildSystem.IsLocalBuild)
    .Does(() =>
{
    CoverallsIo(testCoverageOutputFilePath, new CoverallsIoSettings
    {
        RepoToken = coverallsRepoToken,
        Debug = true
    });
});

Task("NuGetPack")
    .IsDependentOn("UploadTestReport")
    .Does(() =>
{
    DotNetPack(projectFileMain, new DotNetCorePackSettings
    {
        Configuration = configuration,
        NoRestore = true,
        NoBuild = true,
        NoLogo = true,
        OutputDirectory = artifactsDir
    });
});

Task("NuGetPush")
    .IsDependentOn("NuGetPack")
    .WithCriteria(!string.IsNullOrWhiteSpace(nugetApiUrl))
    .WithCriteria(!string.IsNullOrWhiteSpace(nugetApiKey))
    .Does(() =>
{
    DotNetNuGetPush(artifactsDir.CombineWithFilePath("*.nupkg").FullPath, new DotNetCoreNuGetPushSettings
    {
        Source = nugetApiUrl,
        ApiKey = nugetApiKey
    });
});

Task("Default")
    .IsDependentOn("Test");

RunTarget(target);
