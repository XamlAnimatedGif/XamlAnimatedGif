using System;
using System.IO;
using System.Runtime.CompilerServices;
using McMaster.Extensions.CommandLineUtils;
using static Bullseye.Targets;
using static SimpleExec.Command;

namespace build
{
    [Command("build")]
    [SuppressDefaultHelpOption]
    class Program
    {
        static void Main(string[] args) =>
            CommandLineApplication.Execute<Program>(args);

        [Option("-h|-?|--help", "Show help message", CommandOptionType.NoValue)]
        public bool ShowHelp { get; } = false;

        [Option("-c|--configuration", "The configuration to build", CommandOptionType.SingleValue)]
        public string Configuration { get; } = "Release";

        public string[] RemainingArguments { get; } = null;

        public void OnExecute(CommandLineApplication app)
        {
            if (ShowHelp)
            {
                app.ShowHelp();
                app.Out.WriteLine("Bullseye help:");
                app.Out.WriteLine();
                RunTargetsAndExit(new[] { "-h" });
                return;
            }

            Directory.SetCurrentDirectory(GetSolutionDirectory());

            string artifactsDir = Path.GetFullPath("artifacts");
            string logsDir = Path.Combine(artifactsDir, "logs");
            string buildLogFile = Path.Combine(logsDir, "build.binlog");
            string packagesDir = Path.Combine(artifactsDir, "packages");

            string solutionFile = "XamlAnimatedGif.sln";
            string libraryProject = "XamlAnimatedGif/XamlAnimatedGif.csproj";

            Target(
                "artifactDirectories",
                () =>
                {
                    Directory.CreateDirectory(artifactsDir);
                    Directory.CreateDirectory(logsDir);
                    Directory.CreateDirectory(packagesDir);
                });

            Target(
                "build",
                DependsOn("artifactDirectories"),
                () => Run(
                    "dotnet",
                    $"build -c \"{Configuration}\" /bl:\"{buildLogFile}\" \"{solutionFile}\""));

            Target(
                "pack",
                DependsOn("artifactDirectories", "build"),
                () => Run(
                    "dotnet",
                    $"pack -c \"{Configuration}\" --no-build -o \"{packagesDir}\" \"{libraryProject}\""));

            Target("default", DependsOn("pack"));

            RunTargetsAndExit(RemainingArguments);
        }

        private static string GetSolutionDirectory() =>
            Path.GetFullPath(Path.Combine(GetScriptDirectory(), @"..\.."));

        private static string GetScriptDirectory([CallerFilePath] string filename = null) => Path.GetDirectoryName(filename);

    }
}
