using System;
using System.IO;
using System.Linq;
using Fallout.Common;
using Fallout.Common.CI;
using Fallout.Common.Execution;
using Fallout.Common.IO;
using Fallout.Solutions;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.DotNet;
using Fallout.Common.Tools.Git;
using Fallout.Common.Tools.NuGet;
using Fallout.Common.Utilities.Collections;
using Serilog;
using static Fallout.Common.EnvironmentInfo;
using static Fallout.Common.IO.PathConstruction;


class Build : FalloutBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Nuget feed URL")] 
    string Source = "https://api.nuget.org/v3/index.json";

    [Parameter("API key to push to nuget feed"), Secret]
    string ApiKey;

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath CsharpProjectFile => RootDirectory / "csharp" / "MediaControls.csproj";
    AbsolutePath NuspecFile => RootDirectory / "deployment" / "VL.MediaControls.HDE.nuspec";

    AbsolutePath VersionFile => RootDirectory / "version.txt";
    string Version => File.ReadAllText(VersionFile).Trim();
    
    Target Clean => _ => _
        .Executes(() => { ArtifactsDirectory.CreateOrCleanDirectory(); });

    Target Compile => _ => _
        .DependsOn(Clean)
        .Requires(() => File.Exists(VersionFile))
        .Executes(() =>
        {
            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(CsharpProjectFile)
                .SetVersion(Version)
                .SetConfiguration("Release"));
        });
    
    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            NuGetTasks.NuGetPack(s => s
                .SetTargetPath(NuspecFile)
                .SetOutputDirectory(ArtifactsDirectory)
                .SetVersion(Version));
        });
    
    private Target Tag => _ => _
        .Requires(() => GitTasks.GitHasCleanWorkingCopy())
        .DependsOn(Pack)
        .Executes(() =>
        {
            Log.Information($"Creating tag {Version}");
            try
            {
                GitTasks.Git($"tag -a {Version} -m \"{Version}\"");
            }
            catch (Exception e)
            {
                Log.Error($"Could not create tag {Version}");
                throw;
            }
        });
    
    Target Push => _ => _
        .DependsOn(Pack)
        .Requires(() => ApiKey)
        .After(Tag)
        .Requires(() => GitTasks.GitHasCleanWorkingCopy())
        .Executes(() =>
        {
            var packageFile = ArtifactsDirectory / $"VL.MediaControls.HDE.{Version}.nupkg";
            Assert.FileExists(packageFile);

            DotNetTasks.DotNetNuGetPush(s => s
                .SetSource(Source)
                .SetApiKey(ApiKey)
                .SetTargetPath(packageFile));
        });

    private Target Release => _ => _
        .DependsOn(Tag)
        .DependsOn(Push)
        .Executes(() =>
        {
            Log.Information($"Creating release {Version}");
        });
}