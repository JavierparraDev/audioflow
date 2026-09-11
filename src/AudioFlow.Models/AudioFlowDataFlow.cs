namespace AudioFlow.Models;

/// <summary>
/// Direction of an audio endpoint. Mirrors Windows <c>EDataFlow</c>.
/// </summary>
public enum AudioFlowDataFlow
{
    Render = 0,
    Capture = 1,
    All = 2
}
