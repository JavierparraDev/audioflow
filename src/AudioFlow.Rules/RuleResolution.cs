namespace AudioFlow.Rules;

/// <summary>
/// Result of applying the rule set to an application.
/// </summary>
/// <param name="OutputDeviceId">Target output endpoint, or null when nothing is configured.</param>
/// <param name="HasExplicitRule">True when an application-specific rule matched.</param>
/// <param name="BlockedByAudioLock">True when Audio Lock redirected the app away from the locked device.</param>
/// <param name="Reason">Human readable explanation, useful for logs.</param>
public sealed record RuleResolution(
    string? OutputDeviceId,
    bool HasExplicitRule,
    bool BlockedByAudioLock,
    string Reason);
