using System.Diagnostics;
using System.Runtime.InteropServices;

// Test-only child launch: intentional fatal startup errors must not wait for a
// Windows Error Reporting dialog. Child inherits the process error mode.
// Restore immediately after synchronous launch; no machine or API setting changes.
internal static class StartupProbeProcess
{
    internal static Process Start(ProcessStartInfo start)
    {
        if (!OperatingSystem.IsWindows()) return Process.Start(start) ?? throw new InvalidOperationException("Child not started");
        var previous = GetErrorMode();
        try
        {
            SetErrorMode(previous | 0x0002); // SEM_NOGPFAULTERRORBOX
            return Process.Start(start) ?? throw new InvalidOperationException("Child not started");
        }
        finally { SetErrorMode(previous); }
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetErrorMode();
    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);
}
