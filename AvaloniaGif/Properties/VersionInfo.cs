using System.Reflection;
using AvaloniaGif.Properties;

[assembly: AssemblyVersion(VersionInfo.Version)]
[assembly: AssemblyFileVersion(VersionInfo.Version)]
[assembly: AssemblyInformationalVersion(VersionInfo.Version + VersionInfo.PreRelease)]

namespace AvaloniaGif.Properties
{
    internal static class VersionInfo
    {
        public const string Version = "1.1.10.0";
        public const string PreRelease = "";
    }
}
