using System.Text.Json;
using System.Text.Json.Serialization;
using AudioFlow.Configuration;

namespace AudioFlow.Session;

/// <summary>
/// Atomically persists the session snapshot. The marker is written BEFORE any
/// routing change so a crash can always be recovered on the next launch.
/// </summary>
public sealed class SessionMarker
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SessionMarker(string? filePath = null) =>
        FilePath = filePath ?? Path.Combine(AppPaths.UserDataDirectory, "session", "session.json");

    public string FilePath { get; }

    public bool Exists => File.Exists(FilePath);

    public AudioRoutingSnapshot? Read()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var json = File.ReadAllText(FilePath);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<AudioRoutingSnapshot>(json, Options);
        }
        catch
        {
            return null;
        }
    }

    public bool Write(AudioRoutingSnapshot snapshot)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(snapshot, Options);
            var tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, json);

            if (File.Exists(FilePath))
            {
                File.Replace(tempPath, FilePath, FilePath + ".bak", ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, FilePath);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch
        {
            // best effort
        }
    }
}
