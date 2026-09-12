using System.Runtime.InteropServices;
using Xunit;

namespace AudioFlow.Windows.Tests;

/// <summary>
/// A [Fact] that is skipped automatically on non-Windows hosts, so the build
/// and unit-test run on Linux/macOS never fails because of missing hardware.
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = "Windows integration test - skipped on this operating system.";
        }
    }
}
