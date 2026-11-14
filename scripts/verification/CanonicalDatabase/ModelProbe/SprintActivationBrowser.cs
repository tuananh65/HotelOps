using System.Diagnostics;
using System.Text.Json;

internal static class SprintActivationBrowser
{
    internal static async Task<JsonElement> RunAsync(string username, string password, long targetId, CancellationToken ct)
    {
        var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("frontend/scripts/sprintActivationBrowser.mjs");
        using var process = Process.Start(start) ?? throw new Exception("Browser runner unavailable");
        try
        {
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { username, password, targetId }).AsMemory(), ct);
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync(ct);
            var errors = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            var text = await output; await errors; // No credential, response-body or stderr output.
            using var json = JsonDocument.Parse(text);
            return json.RootElement.Clone();
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }
}
