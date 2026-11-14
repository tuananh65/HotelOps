using System.Text.Json;

internal static class NormalModeVerification
{
    internal static async Task<int> Run(string output)
    {
        var names=new[]{"DOTNET_STARTUP_HOOKS","ASPNETCORE_HOSTINGSTARTUPASSEMBLIES","DEV2_TRIAL_HOOK_DIRECTORY"};
        var saved=names.ToDictionary(x=>x,Environment.GetEnvironmentVariable);var count=0;
        try
        {
            foreach(var key in names)Environment.SetEnvironmentVariable(key,null);
            HostAcceptance.RequireNormalEnvironment();count++;
            foreach(var key in names)
            {
                Environment.SetEnvironmentVariable(key,"verification-must-not-load");
                var denied=false;
                try{await HostAcceptance.Run(output,"https://localhost:7044",normalAuthority:true);}
                catch(InvalidOperationException e)when(e.Message=="Normal API authority forbids verification hooks"){denied=true;}
                if(!denied)throw new InvalidOperationException("Hooked normal mode was not rejected before SQL");
                count++;Environment.SetEnvironmentVariable(key,null);
            }
            await File.WriteAllTextAsync(Path.Combine(output,"NORMAL_MODE_OFFLINE.json"),JsonSerializer.Serialize(new{status="PASS",assertions=count,sqlConnections=0,normalSqlAcceptance="NOT_RUN"}));
            return count;
        }
        finally{foreach(var (key,value) in saved)Environment.SetEnvironmentVariable(key,value);}
    }
}
