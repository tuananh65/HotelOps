using System.Diagnostics;
using System.Text.Json;

internal static class SprintInternalRequestDecisionBrowser
{
    internal static async Task<JsonElement> RunAsync(string username,string password,long request,bool accept,CancellationToken ct)
    {
        var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add("frontend/scripts/sprintInternalRequestDecisionBrowser.mjs");
        using var process=Process.Start(start)??throw new Exception("Decision browser start failed");
        var output=process.StandardOutput.ReadToEndAsync(ct);var errors=process.StandardError.ReadToEndAsync(ct);
        try
        {
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,request,accept,assign=accept&&Environment.GetEnvironmentVariable("DEV3_VERIFY_IR_ASSIGNMENT")=="1"}));process.StandardInput.Close();
            await process.WaitForExitAsync(ct);var result=JsonSerializer.Deserialize<JsonElement>(await output);_=await errors;
            if(process.ExitCode!=0||!result.GetProperty("success").GetBoolean())throw new Exception("Decision browser failed at "+result.GetProperty("stage").GetString());
            return result;
        }
        finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}
    }
}
