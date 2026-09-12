using System.Reflection;

namespace AudioFlow.Configuration;

/// <summary>
/// Single source of truth for the application version at runtime. The value
/// comes from <c>&lt;Version&gt;</c> in Directory.Build.props, which also feeds
/// the executable metadata, the installer and the release workflow.
/// </summary>
public static class AudioFlowVersion
{
    public static string Current { get; } = Read();

    public static Version CurrentVersion =>
        Version.TryParse(Current.Split('-', '+')[0], out var version) ? version : new Version(0, 0, 0);

    private static string Read()
    {
        var informational = typeof(AudioFlowVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return "0.0.0";
        }

        var plus = informational.IndexOf('+');
        return plus >= 0 ? informational[..plus] : informational;
    }
}
