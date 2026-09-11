using AudioFlow.Core.Logging;
using NAudio.CoreAudioApi;

namespace AudioFlow.Core;

/// <summary>Peak level observed on one render endpoint during a measurement window.</summary>
public sealed record EndpointPeak(string DeviceId, string DeviceName, float Peak);

/// <summary>Result of checking whether audio is present on the expected endpoint.</summary>
public sealed record RoutingVerification(
    string ExpectedDeviceId,
    string ExpectedDeviceName,
    float ExpectedPeak,
    bool SignalOnExpected,
    bool SignalOnOtherDevices,
    IReadOnlyList<EndpointPeak> Peaks)
{
    public bool Verified => SignalOnExpected && !SignalOnOtherDevices;
}

/// <summary>
/// Measures the actual audio level present on each render endpoint.
///
/// This is the objective check required by the project: a routing API returning
/// success is NOT proof that audio physically goes to the requested device. The
/// endpoint peak meter (IAudioMeterInformation) reports the real signal level of
/// the device mix.
///
/// Limitations (documented in docs/LIMITATIONS.md):
///   - It measures the whole endpoint mix, not a single application. If another
///     app is playing on the same device, the measurement is not isolated.
///   - Digital silence (a paused track) yields ~0 even if routing is correct.
///   - DRM/protected streams may not be reflected in the meter on some drivers.
/// </summary>
public sealed class AudioOutputVerifier : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private bool _disposed;

    /// <summary>Measures the peak of every active render endpoint over the same window.</summary>
    public IReadOnlyList<EndpointPeak> MeasureAllPeaks(TimeSpan window, int intervalMs = 50)
    {
        var collection = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        var devices = new List<MMDevice>();
        var meters = new List<AudioMeterInformation>();
        var ids = new List<(string Id, string Name)>();

        try
        {
            for (var i = 0; i < collection.Count; i++)
            {
                try
                {
                    var device = collection[i];
                    devices.Add(device);
                    meters.Add(device.AudioMeterInformation);
                    ids.Add((device.ID, device.FriendlyName));
                }
                catch (Exception ex)
                {
                    Log.Debug($"Verifier: could not open endpoint #{i}: {ex.Message}");
                }
            }

            var peaks = new float[devices.Count];
            var deadline = DateTime.UtcNow + window;

            do
            {
                for (var i = 0; i < meters.Count; i++)
                {
                    try
                    {
                        peaks[i] = Math.Max(peaks[i], meters[i].MasterPeakValue);
                    }
                    catch
                    {
                        // Metering is best-effort.
                    }
                }

                if (DateTime.UtcNow >= deadline)
                {
                    break;
                }

                Thread.Sleep(intervalMs);
            }
            while (true);

            return ids.Select((id, i) => new EndpointPeak(id.Id, id.Name, peaks[i])).ToList();
        }
        finally
        {
            foreach (var device in devices)
            {
                device.Dispose();
            }
        }
    }

    /// <summary>
    /// Checks that signal is present on <paramref name="expectedDeviceId"/> and,
    /// optionally, that it is not present on the other endpoints.
    /// </summary>
    public RoutingVerification Verify(
        string expectedDeviceId,
        TimeSpan window,
        float threshold = 0.001f)
    {
        var peaks = MeasureAllPeaks(window);

        var expected = peaks.FirstOrDefault(p =>
            string.Equals(p.DeviceId, expectedDeviceId, StringComparison.OrdinalIgnoreCase));

        var expectedPeak = expected?.Peak ?? 0f;
        var signalOnExpected = expectedPeak >= threshold;
        var signalOnOthers = peaks.Any(p =>
            !string.Equals(p.DeviceId, expectedDeviceId, StringComparison.OrdinalIgnoreCase) &&
            p.Peak >= threshold);

        var result = new RoutingVerification(
            expectedDeviceId,
            expected?.DeviceName ?? "(unknown)",
            expectedPeak,
            signalOnExpected,
            signalOnOthers,
            peaks);

        Log.Info(
            $"Verification: expected '{result.ExpectedDeviceName}' peak={expectedPeak:0.0000} " +
            $"signalOnExpected={signalOnExpected} signalOnOthers={signalOnOthers} " +
            $"-> {(result.Verified ? "VERIFIED" : "NOT VERIFIED")}");

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _enumerator.Dispose();
    }
}
