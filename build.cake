using System.Xml.Linq;

////////////////////
// Cake arguments //
////////////////////

var target = Argument<string>("target", "Default");
var configuration = Argument<string>("configuration", "Release");

//////////////////////////
// Variable definitions //
//////////////////////////

// Name of the project
var projectName = "XamlAnimatedGif";
// Solution to build
var solutionFile = $"./{projectName}.sln";
// Projects in the solution
var projects = new[]
{
    new { Name = $"{projectName}.Shared", MSBuildPlatform = MSBuildPlatform.Automatic },
    new { Name = $"{projectName}.Wpf", MSBuildPlatform = MSBuildPlatform.Automatic },
};
var projectDirsToClean = new[] { "bin", "obj", "AppPackages" };

// NuGet package ID; change if different from project name
var nugetId = projectName;
// Path to the nuspec file
var nuspecFile = $"./NuGet/{nugetId}.nuspec";
// Directory where the package will be generated
var nugetDir = $"./NuGet/{configuration}";
// Directory containing the package tree
var nupkgDir = $"{nugetDir}/nupkg";
// Files to include in NuGet package for each target
var nugetTargets = new[]
{
    new
    {
        Name = "net45",
        Files = new[]
        {
            $"{projectName}.Shared/bin/{configuration}/{projectName}.dll",
            $"{projectName}.Shared/bin/{configuration}/{projectName}.pdb"
            $"{projectName}.Wpf/bin/{configuration}/{projectName}.dll",
            $"{projectName}.Wpf/bin/{configuration}/{projectName}.pdb"
        }
    }
};

//////////////////////
// Setup / Teardown //
//////////////////////

Setup(context =>
{
    // Executed BEFORE the first task.
    Information("Running tasks...");
});

Teardown(context =>
{
    // Executed AFTER the last task.
    Information("Finished running tasks.");
});

//////////////////////
// Task definitions //
//////////////////////

// Clears the output directories
Task("Clean")
    .Does(() =>
{
    var dirsToClean =
        from p in projects
        from d in projectDirsToClean
        select $"{p.Name}/{d}";
    CleanDirectories(dirsToClean);
    CleanDirectory(nugetDir);
});

// Restores NuGet packages
Task("Restore")
    .Does(() =>
{
    NuGetRestore(solutionFile);
});

// Builds the solution
Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .Does(() =>
{
    foreach (var project in projects)
    {
        MSBuild(
            $"{project.Name}/{project.Name}.csproj",
            settings => settings.SetMSBuildPlatform(project.MSBuildPlatform)
                                .SetConfiguration(configuration));
    }
});

// Creates the NuGet package
Task("Pack")
    .IsDependentOn("Build")
    .Does(() =>
{
    CreateDirectory(nupkgDir);
    foreach (var target in nugetTargets)
    {
        string targetDir = $"{nupkgDir}/lib/{target.Name}";
        CreateDirectory(targetDir);
        foreach (var file in target.Files)
        {
            CopyFileToDirectory(file, targetDir);
        }
    }
    var packSettings = new NuGetPackSettings
    {
        BasePath = nupkgDir,
        OutputDirectory = nugetDir,
        Symbols = true
    };
    NuGetPack(nuspecFile, packSettings);
});

// Pushes the NuGet package to nuget.org
Task("Push")
    .IsDependentOn("Pack")
    .Does(() =>
{
    var doc = XDocument.Load(nuspecFile);
    var ns = XNamespace.Get("http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd");
    string version = doc.Root.Element(ns + "metadata").Element(ns + "version").Value;
    string package = $"{nugetDir}/{nugetId}.{version}.nupkg";
    NuGetPush(package, new NuGetPushSettings());
});

/////////////
// Targets //
/////////////

Task("Default")
    .IsDependentOn("Build");

///////////////
// Execution //
///////////////

RunTarget(target);
