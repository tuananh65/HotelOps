using System.Diagnostics;
using System.Text.Json;
internal static class SprintNotificationBrowser
{
 internal static async Task<JsonElement> RunAsync(string username,string password,long id,string title,string foreignTitle,CancellationToken ct,object? filters=null)
 {
  var start=new ProcessStartInfo("node") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
  start.ArgumentList.Add(filters==null?"frontend/scripts/sprintNotificationsBrowser.mjs":"frontend/scripts/sprintNotificationFiltersBrowser.mjs");
  using var process=Process.Start(start)??throw new Exception("Browser runner unavailable");
  try
  {
   await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,id,title,foreignTitle,filters}).AsMemory(),ct);process.StandardInput.Close();
   var output=process.StandardOutput.ReadToEndAsync(ct);var errors=process.StandardError.ReadToEndAsync(ct);
   await process.WaitForExitAsync(ct);var text=await output;await errors; // Never emit stderr or credentials.
   using var json=JsonDocument.Parse(text);var result=json.RootElement.Clone();
   return result;
  }
  finally {if(!process.HasExited)process.Kill(entireProcessTree:true);}
 }
}
