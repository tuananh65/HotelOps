using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Dev2Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal static class BrowserAcceptance
{
    internal static async Task Run(string output,string frontend)
    {
        HostAcceptance.RequireNormalEnvironment();
        if(!Uri.TryCreate(frontend,UriKind.Absolute,out var uri)||!uri.IsLoopback||uri.Scheme!="https")throw new InvalidOperationException("Local HTTPS frontend required");
        var cs=new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DEV2_TRIAL_CONNECTION")??throw new InvalidOperationException("Trial connection required"));
        if(cs.InitialCatalog!="HotelOps_DEV2_FullStack_Trial"||!cs.IntegratedSecurity||cs.DataSource is not("KIENPC" or "lpc:KIENPC")||Environment.GetEnvironmentVariable("DEV2_DEMO_AUTHORITY_V1")!=Dev2ExpandedPolicyIdentity.Sha256)throw new InvalidOperationException("Exact approved trial required");
        var options=new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(cs.ConnectionString).Options;
        HotelOpsDbContext Open()=>new(options);
        await using var lease=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(new SqlConnectionStringBuilder(cs.ConnectionString){Pooling=false}.ConnectionString).Options);
        await HostAcceptance.VerifyIdentity(lease);
        foreach(var name in new[]{"BACKUP_AND_ROLLBACK.json","EF_SCHEMA_COMPATIBILITY.json","NORMAL_API_STARTUP.json"}){
            var r=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(output,name)))!;
            if(!string.Equals(r["databaseGuid"]?.GetValue<string>(),HostAcceptance.DatabaseGuid,StringComparison.OrdinalIgnoreCase)
                ||(name.StartsWith("BACKUP")?r["backupVerified"]?.GetValue<bool>()!=true:name.StartsWith("EF_")?r["status"]?.GetValue<string>()!="PASS":r["hooksDisabled"]?.GetValue<bool>()!=true))throw new InvalidOperationException("Target-bound preflight and standard API receipts required");
        }
        var fixtures=new Fixtures(Open,output);var passed=false;var cleanup=false;
        try
        {
            await FixtureRunSafety.Gate(lease,output);await fixtures.Create();
            async Task Child(string phase)
            {
                var payload=new{phase,output,frontend,marker=fixtures.Marker,password=fixtures.Password,users=fixtures.Users.Select(x=>new{x.Username,x.UserId}),branchId=fixtures.Branch.BranchId,departmentId=fixtures.Department.DepartmentId,areaId=fixtures.Area.AreaId,warehouseId=fixtures.Warehouse.WarehouseId,itemId=fixtures.Items[1].ItemId,uomId=fixtures.Uom.UomId,assetId=fixtures.Assets[16].AssetId,item56=fixtures.Items[56].ItemId,thresholdId=fixtures.Thresholds[56].ThresholdId};
                var start=new ProcessStartInfo("node"){UseShellExecute=false,RedirectStandardInput=true,CreateNoWindow=true};
                start.Environment.Remove("DEBUG");start.Environment.Remove("PWDEBUG");
                start.ArgumentList.Add(Path.GetFullPath("frontend/tests/dev2-live-browser.mjs"));
                using var child=Process.Start(start)??throw new InvalidOperationException("Browser process did not start");
                await FixtureRunSafety.Write(Path.Combine(output,"BROWSER_PROCESS_"+phase+".json"),new{processId=child.Id,phase,credentialsTransport="anonymous stdin pipe; never persisted"});
                await child.StandardInput.WriteAsync(JsonSerializer.Serialize(payload));child.StandardInput.Close();
                await child.WaitForExitAsync();
                await FixtureRunSafety.Write(Path.Combine(output,"BROWSER_EXIT_"+phase+".json"),new{phase,exitCode=child.ExitCode});
                if(child.ExitCode!=0)throw new InvalidOperationException("Browser acceptance failed; inspect sanitized action receipts");
            }
            var before=await fixtures.Snapshot();await Child("negative");
            if(before!=await fixtures.Snapshot())throw new InvalidOperationException("Denied browser actions changed feature/audit graph");
            await FixtureRunSafety.Write(Path.Combine(output,"NEGATIVE_SQL_NO_EFFECTS.json"),new{status="PASS",businessAndAuditUnchanged=true});
            await Child("positive");
            await using var db=Open();
            var incident=await db.Incidents.AsNoTracking().SingleAsync(x=>x.BranchId==fixtures.Branch.BranchId&&x.IncidentTitle==fixtures.Marker+" Browser incident");
            var work=await db.MaintenanceWorks.AsNoTracking().SingleAsync(x=>x.IncidentId==incident.IncidentId);
            var request=await db.MaterialRequests.AsNoTracking().SingleAsync(x=>x.BranchId==fixtures.Branch.BranchId&&x.Purpose==fixtures.Marker+" Browser request");
            var fulfillment=await db.MaterialFulfillments.AsNoTracking().SingleAsync(x=>x.RequestId==request.RequestId);
            var browser=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(output,"BROWSER_positive.json")))!;
            var docId=browser["ids"]!["documentId"]!.GetValue<long>();
            var document=await db.StockDocuments.AsNoTracking().SingleAsync(x=>x.DocumentId==docId);
            var checks=new Dictionary<string,bool>{
                ["incident-trusted-reporter"]=incident.ReportedBy==fixtures.Users[0].UserId&&incident.Status=="IN_PROGRESS",
                ["work-incident-assignment"]=work.Status=="IN_PROGRESS"&&work.AssigneeId==fixtures.Users[0].UserId&&work.CreatedBy==fixtures.Users[0].UserId,
                ["request-link-and-fulfillment-actor"]=request.IncidentId==incident.IncidentId&&request.RequesterId==fixtures.Users[0].UserId&&fulfillment.FulfilledBy==fixtures.Users[2].UserId,
                ["real-stock-issue"]=await db.StockMovements.AnyAsync(x=>x.DocumentLine.Document.ReferenceId==fulfillment.FulfillmentId&&x.SignedBaseQty==-2&&x.CreatedBy==fixtures.Users[2].UserId),
                ["posted-browser-receipt"]=document.Status=="POSTED"&&document.CreatedBy==fixtures.Users[2].UserId&&document.PostedBy==fixtures.Users[2].UserId&&await db.StockMovements.AnyAsync(x=>x.DocumentLine.DocumentId==docId&&x.SignedBaseQty==3&&x.CreatedBy==fixtures.Users[2].UserId),
                ["asset-location-audit"]=await db.AssetLocationHistories.AnyAsync(x=>x.AssetId==fixtures.Assets[16].AssetId&&x.MovedBy==fixtures.Users[0].UserId),
                ["replenishment-owned-warehouse"]=await db.MaterialReplenishments.AnyAsync(x=>x.ReplenishmentId==browser["ids"]!["replenishmentId"]!.GetValue<long>()&&x.WarehouseId==fixtures.Warehouse.WarehouseId&&x.RequestedBy==fixtures.Users[0].UserId),
                ["fresh-audit-all-actors"]=await db.AuditLogs.AnyAsync(x=>x.BranchId==fixtures.Branch.BranchId&&x.ActorUserId==fixtures.Users[0].UserId)&&await db.AuditLogs.AnyAsync(x=>x.BranchId==fixtures.Branch.BranchId&&x.ActorUserId==fixtures.Users[1].UserId)&&await db.AuditLogs.AnyAsync(x=>x.BranchId==fixtures.Branch.BranchId&&x.ActorUserId==fixtures.Users[2].UserId)
            };
            await FixtureRunSafety.Write(Path.Combine(output,"BROWSER_SQL_EFFECTS.json"),new{status=checks.Values.All(x=>x)?"PASS":"FAIL",checks});
            passed=checks.Values.All(x=>x);
        }
        catch(Exception error){await FixtureRunSafety.Write(Path.Combine(output,"BROWSER_FAILURE.json"),new{status="FAIL",exceptionType=error.GetType().Name,rawValuesRecorded=false});}
        finally
        {
            fixtures.Password="";
            try{if(fixtures.Committed)await fixtures.Cleanup();await fixtures.VerifyNoRoots();cleanup=true;await FixtureRunSafety.Write(Path.Combine(output,"FIXTURE_CLEANUP.json"),new{status="PASS",fixtures.Marker,remainingRoots=0,databaseGuid=HostAcceptance.DatabaseGuid});}
            catch(Exception error){await FixtureRunSafety.Write(Path.Combine(output,"FIXTURE_CLEANUP.json"),new{status="FAIL",exceptionType=error.GetType().Name});}
            await FixtureRunSafety.Write(Path.Combine(output,"CHECKPOINT005_DEMO.json"),new{status=passed&&cleanup?"PASS":"FAIL",browserPositive=passed,browserNegativeNoEffects=passed,cleanup,databaseGuid=HostAcceptance.DatabaseGuid,sqlBacked=true,credentialsPersisted=false});
            if(!passed||!cleanup)Environment.ExitCode=1;
        }
    }
}
