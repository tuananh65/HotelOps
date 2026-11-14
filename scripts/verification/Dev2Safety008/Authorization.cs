using System.Security.Cryptography;
using System.Text.Json;

namespace Safety008;

internal sealed record Authority(bool Approved,string Scope,string AllowedMode,string Run,string OwnerName,DateTime ApprovedUtc,string Database,string DatabaseGuid,string CandidateReleaseSha256,string FreshPreflightDirectory,string FreshBackupReceipt,string FreshBackupReceiptSha256,bool AllowFixtureCreate,bool AllowExactPkCleanup,bool AllowOwnedBrowserClose,bool AllowOwnedApiFrontendStop,bool AllowDeveloperTlsExport,int MaximumMinutes);
internal static class Authorization
{
    internal static string HashFile(string p)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p)));
    internal static async Task<Authority> Verify(string file,string release,string repo,string mode,string output)
    {
        var a=JsonSerializer.Deserialize<Authority>(await File.ReadAllTextAsync(file),Durable.Json)??throw new InvalidOperationException("Authority required");
        if(!a.Approved||a.Scope!="CHECKPOINT008_SAFETY_ACCEPTANCE_ONLY"||a.Database!=SqlStore.Database||!Guid.TryParseExact(a.DatabaseGuid,"D",out var g)||g!=SqlStore.DatabaseGuid||a.CandidateReleaseSha256!=HashFile(release)||string.IsNullOrWhiteSpace(a.OwnerName)||a.OwnerName.Contains('<')||a.ApprovedUtc.Kind!=DateTimeKind.Utc||a.ApprovedUtc>DateTime.UtcNow||DateTime.UtcNow-a.ApprovedUtc>TimeSpan.FromHours(2)||!a.AllowFixtureCreate||!a.AllowExactPkCleanup||mode!="fixture-acceptance"&&(!a.AllowOwnedBrowserClose||!a.AllowOwnedApiFrontendStop||!a.AllowDeveloperTlsExport)||a.MaximumMinutes is <1 or >120||!System.Text.RegularExpressions.Regex.IsMatch(a.Run,@"^SAFETY008_[A-Za-z0-9_]{8,80}$"))throw new InvalidOperationException("EXPLICIT_BOUND_SAFETY_EXECUTION_AUTHORIZATION_REQUIRED");
        if(mode is not ("fixture-acceptance" or "browser-acceptance")||a.AllowedMode!=mode)throw new InvalidOperationException("Exact safety acceptance mode required; interactive demo not authorized by this runner");
        using var descriptor=JsonDocument.Parse(await File.ReadAllTextAsync(release));
        var executing=System.Reflection.Assembly.GetExecutingAssembly().Location;
        if(!string.Equals(Path.GetFullPath(Path.Combine(repo,descriptor.RootElement.GetProperty("runtimeDll").GetString()!)),Path.GetFullPath(executing),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Executing assembly is not the approved release artifact");
        var expectedOutput=Path.Combine(repo,"docs/integration-notes/DEV2-FULLSTACK-008-SAFETY-IMPLEMENT-01/host-receipts",a.Run,"worker");
        if(!string.Equals(Path.GetFullPath(output),Path.GetFullPath(expectedOutput),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Run-bound output required");
        foreach(var pin in descriptor.RootElement.GetProperty("files").EnumerateArray()){
            var relative=pin.GetProperty("path").GetString()!;var p=Path.GetFullPath(Path.Combine(repo,relative));if(!p.StartsWith(Path.GetFullPath(repo)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||HashFile(p)!=pin.GetProperty("sha256").GetString())throw new InvalidOperationException("Candidate release drift");
        }
        var acceptedManifest=Path.Combine(repo,"docs/integration-notes/DEV2-FULLSTACK-007-RUNTIME/CHECKPOINT007_SOURCE_SHA256.json");
        if(HashFile(acceptedManifest)!="11483526be3e092a4f7e95e7034979d3559cf84e263a1285c6e68772c8abf3fe")throw new InvalidOperationException("Accepted source manifest changed; no rebaseline permitted");
        using var pinned=JsonDocument.Parse(await File.ReadAllTextAsync(acceptedManifest));
        if(pinned.RootElement.GetArrayLength()!=682)throw new InvalidOperationException("682 accepted pins required");
        foreach(var pin in pinned.RootElement.EnumerateArray())if(HashFile(Path.Combine(repo,pin.GetProperty("path").GetString()!))!=pin.GetProperty("sha256").GetString())throw new InvalidOperationException("Accepted source drift");
        var pdir=Path.GetFullPath(a.FreshPreflightDirectory);VerifyReceipt(pdir);
        using var preflight=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pdir,"PREFLIGHT_READINESS.json")));
        var observation=preflight.RootElement;
        if(observation.GetProperty("readOnlyTechnicalObservations").GetString()!="PASS_READONLY_OBSERVATIONS_ONLY"||!DateTime.TryParse(observation.GetProperty("utc").GetString(),null,System.Globalization.DateTimeStyles.AdjustToUniversal,out var at)||at>DateTime.UtcNow||DateTime.UtcNow-at>TimeSpan.FromMinutes(15))throw new InvalidOperationException("Fresh read-only preflight required; not a demo gate waiver");
        if(HashFile(a.FreshBackupReceipt)!=a.FreshBackupReceiptSha256)throw new InvalidOperationException("Backup receipt drift");
        using var backup=JsonDocument.Parse(await File.ReadAllTextAsync(a.FreshBackupReceipt));var b=backup.RootElement;
        var backupTime=b.GetProperty("createdUtc").GetDateTime().ToUniversalTime();
        if(!b.GetProperty("backupVerified").GetBoolean()||b.GetProperty("database").GetString()!=SqlStore.Database||Guid.Parse(b.GetProperty("databaseGuid").GetString()!)!=SqlStore.DatabaseGuid||!b.GetProperty("copyOnly").GetBoolean()||!b.GetProperty("checksum").GetBoolean()||b.GetProperty("restoreVerifyOnly").GetString()!="PASS"||backupTime>DateTime.UtcNow||DateTime.UtcNow-backupTime>TimeSpan.FromMinutes(30))throw new InvalidOperationException("Fresh exact trial backup evidence required; no backup is performed here");
        Directory.CreateDirectory(output);await Durable.File(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file))!,a.Run+".authority-used.json"),new{authoritySha256=HashFile(file),run=a.Run,mode,output,utc=DateTime.UtcNow});return a;
    }
    internal static void VerifyReceipt(string dir)
    {
        using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"RECEIPT_SHA256.json")));
        foreach(var pin in manifest.RootElement.EnumerateArray()){var name=pin.GetProperty("file").GetString()!;if(Path.GetFileName(name)!=name||HashFile(Path.Combine(dir,name))!=pin.GetProperty("sha256").GetString())throw new InvalidOperationException("Receipt drift");}
    }
}
