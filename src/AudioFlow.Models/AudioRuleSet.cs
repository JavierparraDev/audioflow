namespace AudioFlow.Models;

/// <summary>
/// The persisted configuration: explicit rules, the default output for
/// applications without a rule, and the Audio Lock policy.
/// </summary>
public sealed class AudioRuleSet
{
    public List<AudioRule> Rules { get; set; } = new();

    /// <summary>Output device used when an application has no explicit rule.</summary>
    public string? DefaultOutputDeviceId { get; set; }

    /// <summary>When true, only allowed applications may use <see cref="AudioLockDeviceId"/>.</summary>
    public bool AudioLockEnabled { get; set; }

    /// <summary>Device protected by Audio Lock (typically the speakers).</summary>
    public string? AudioLockDeviceId { get; set; }

    /// <summary>Device that blocked applications are redirected to (typically the headphones).</summary>
    public string? AudioLockFallbackDeviceId { get; set; }

    /// <summary>Applications explicitly allowed to use the locked device.</summary>
    public List<string> AudioLockAllowedApplications { get; set; } = new();
}
