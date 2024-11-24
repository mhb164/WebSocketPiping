using System.Reflection;

//https://github.com/mhb164/Boilerplates
namespace Boilerplates
{
    public static partial class Aid
    {
        public static string? AppVersion
            => Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString();

        public static string? AppFileVersion
            => Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;

        public static string? AppInformationalVersion
            => Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    }
}
