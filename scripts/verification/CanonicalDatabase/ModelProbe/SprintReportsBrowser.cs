using System.Diagnostics;
using System.Text.Json;

internal static class SprintReportsBrowser
{
    internal static async Task<JsonElement> RunAsync(string username, string password, long branch, long department, string mode, CancellationToken ct, long? housekeeper = null, long? inspector = null, string? from = null, string? to = null, string? requestType = null)
    {
        var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("frontend/scripts/sprintReportsBrowser.mjs");
        using var process = Process.Start(start) ?? throw new Exception("Report browser did not start");
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var errors = process.StandardError.ReadToEndAsync(ct);
        try
        {
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { username, password, branch, department, mode, housekeeper, inspector, from, to, requestType }));
            process.StandardInput.Close();
            await process.WaitForExitAsync(ct);
            var result = JsonSerializer.Deserialize<JsonElement>(await output);
            _ = await errors; // Do not publish credential-bearing browser diagnostics.
            if (process.ExitCode != 0 || !result.GetProperty("success").GetBoolean())
                throw new Exception("Report browser failed at " + (result.TryGetProperty("stage", out var stage) ? stage.GetString() : "unknown"));
            return result;
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }
}
