using AudioFlow.Models;

namespace AudioFlow.Rules;

/// <summary>
/// Decides which output device an application should use.
///
///   IF the application has an enabled explicit rule
///   THEN use its output device
///   ELSE use the default output device
///
/// Audio Lock is an extra policy on top: when enabled, only allowed
/// applications may use the locked device; everyone else falls back to the
/// default output device.
/// </summary>
public sealed class RuleEngine
{
    private readonly RuleStorage _storage;

    public RuleEngine(RuleStorage? storage = null)
    {
        _storage = storage ?? new RuleStorage();
        Rules = _storage.Load();
    }

    /// <summary>Raised after any change to the rule set.</summary>
    public event EventHandler? Changed;

    public AudioRuleSet Rules { get; }

    public string RulesFilePath => _storage.FilePath;

    public string? LastStorageError => _storage.LastError;

    public AudioRule? FindRule(string applicationKey) =>
        Rules.Rules.FirstOrDefault(r =>
            string.Equals(r.ApplicationIdentifier, applicationKey, StringComparison.OrdinalIgnoreCase));

    public RuleResolution Resolve(ApplicationIdentity identity) => Resolve(identity.Key);

    public RuleResolution Resolve(string applicationKey)
    {
        var rule = Rules.Rules.FirstOrDefault(r =>
            r.Enabled &&
            string.Equals(r.ApplicationIdentifier, applicationKey, StringComparison.OrdinalIgnoreCase));

        var target = rule?.OutputDeviceId ?? Rules.DefaultOutputDeviceId;
        var hasExplicitRule = rule is not null;

        if (IsBlockedByAudioLock(applicationKey, target))
        {
            var fallback = Rules.AudioLockFallbackDeviceId ?? Rules.DefaultOutputDeviceId;
            return new RuleResolution(
                fallback,
                hasExplicitRule,
                BlockedByAudioLock: true,
                Reason: "blocked by Audio Lock");
        }

        return new RuleResolution(
            target,
            hasExplicitRule,
            BlockedByAudioLock: false,
            Reason: hasExplicitRule ? "explicit rule" : "default rule");
    }

    private bool IsBlockedByAudioLock(string applicationKey, string? target)
    {
        if (!Rules.AudioLockEnabled || string.IsNullOrWhiteSpace(Rules.AudioLockDeviceId))
        {
            return false;
        }

        if (!string.Equals(target, Rules.AudioLockDeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !Rules.AudioLockAllowedApplications.Contains(applicationKey, StringComparer.OrdinalIgnoreCase);
    }

    public void SetDefaultDevice(string deviceId)
    {
        Rules.DefaultOutputDeviceId = deviceId;
        Persist();
    }

    public AudioRule SetRule(string applicationKey, string applicationName, string outputDeviceId)
    {
        var rule = FindRule(applicationKey);
        if (rule is null)
        {
            rule = new AudioRule
            {
                ApplicationIdentifier = applicationKey,
                ApplicationName = applicationName,
                OutputDeviceId = outputDeviceId
            };
            Rules.Rules.Add(rule);
        }
        else
        {
            rule.ApplicationName = applicationName;
            rule.OutputDeviceId = outputDeviceId;
            rule.Enabled = true;
        }

        Persist();
        return rule;
    }

    public bool RemoveRule(string applicationKey)
    {
        var removed = Rules.Rules.RemoveAll(r =>
            string.Equals(r.ApplicationIdentifier, applicationKey, StringComparison.OrdinalIgnoreCase));

        if (removed > 0)
        {
            Persist();
            return true;
        }

        return false;
    }

    public void SetRuleEnabled(string applicationKey, bool enabled)
    {
        var rule = FindRule(applicationKey);
        if (rule is null)
        {
            return;
        }

        rule.Enabled = enabled;
        Persist();
    }

    public void EnableAudioLock(string lockedDeviceId, string fallbackDeviceId, IEnumerable<string> allowedApplicationKeys)
    {
        Rules.AudioLockEnabled = true;
        Rules.AudioLockDeviceId = lockedDeviceId;
        Rules.AudioLockFallbackDeviceId = fallbackDeviceId;
        Rules.AudioLockAllowedApplications = allowedApplicationKeys.ToList();
        Persist();
    }

    public void DisableAudioLock()
    {
        Rules.AudioLockEnabled = false;
        Persist();
    }

    public bool Save() => _storage.Save(Rules);

    private void Persist()
    {
        _storage.Save(Rules);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
