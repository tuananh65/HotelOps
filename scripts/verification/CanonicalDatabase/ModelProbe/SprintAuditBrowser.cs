using System.Diagnostics;
using System.Text.Json;

internal static class SprintAuditBrowser
{
    internal static async Task<JsonElement> RunAsync(object fixture, CancellationToken ct)
    {
        var start = new ProcessStartInfo("node") { UseShellExecute=false, CreateNoWindow=true,
            RedirectStandardInput=true, RedirectStandardOutput=true, RedirectStandardError=true };
        start.ArgumentList.Add("frontend/scripts/sprintAuditBrowser.mjs");
        using var process=Process.Start(start) ?? throw new Exception("Audit browser unavailable");
        try
        {
            var output=process.StandardOutput.ReadToEndAsync(ct);var errors=process.StandardError.ReadToEndAsync(ct);
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(fixture).AsMemory(),ct);process.StandardInput.Close();
            await process.WaitForExitAsync(ct);var text=await output;_ = await errors;
            return JsonSerializer.Deserialize<JsonElement>(text);
        }
        finally { if(!process.HasExited)process.Kill(entireProcessTree:true); }
    }
}
