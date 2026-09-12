namespace AudioFlow.Session;

/// <summary>
/// Detects and recovers from an unclean previous session. Recovery restores
/// Windows audio and never re-activates routing automatically.
/// </summary>
public sealed class CrashRecoveryService
{
    private readonly AudioFlowSessionManager _manager;

    public CrashRecoveryService(AudioFlowSessionManager manager) => _manager = manager;

    public bool HasStaleSession => _manager.HasStaleSession;

    public SessionRecoveryReport Recover() => _manager.RecoverIfNeeded();
}
