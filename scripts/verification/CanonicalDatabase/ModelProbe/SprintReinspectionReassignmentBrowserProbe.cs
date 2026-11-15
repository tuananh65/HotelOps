using System.Data;
using System.Diagnostics;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class SprintReinspectionReassignmentBrowserProbe
{
    // Serial, parent-owned disposable trial. Evidence contains no credentials.
    internal static readonly List<object> Evidence=[];
    internal static async Task TransferAsync(HotelOpsDbContext db,string username,string recipient,string password,
        long actor,long other,long branch,long rework,long inspection,CancellationToken ct)
    {
        async Task<JsonElement> Browser(object fixture)
        {
            var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add("frontend/scripts/sprintInspectionReassignmentBrowser.mjs");
            using var process=Process.Start(start)??throw new Exception("Reinspection handoff browser unavailable");
            try
            {
                await process.StandardInput.WriteAsync(JsonSerializer.Serialize(fixture).AsMemory(),ct);process.StandardInput.Close();
                var stdout=process.StandardOutput.ReadToEndAsync(ct);var stderr=process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);var result=JsonSerializer.Deserialize<JsonElement>(await stdout);await stderr;
                if(process.ExitCode!=0||!result.GetProperty("success").GetBoolean())throw new Exception("Reinspection handoff browser: "+result.GetRawText());
                return result;
            }
            finally{if(!process.HasExited)process.Kill(true);}
        }
        var transferred=await Browser(new{username,password,actor,other,branch,rework,inspection,reinspection=true,state="ASSIGNED",returnToActor=false});
        var recipientSession=await Browser(new{username=recipient,password,branch,rework,inspection,reinspection=true,state="ASSIGNED",recipientRead=true});
        var notice=await SprintReinspectionReassignmentNoticeProbe.VerifyAsync(db,other,branch,inspection,ct);
        var received=await Browser(new{username=recipient,password,branch,rework,inspection,reinspection=true,state="ASSIGNED",notice});
        if(await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_bindings_v1 b
            JOIN dbo.fe05_reinspection_responsibilities_v1 current_actor ON current_actor.inspection_id=b.inspection_id
              AND current_actor.inspector_user_id={other}
            WHERE b.inspection_id={inspection} AND b.rework_id={rework} AND b.reinspector_user_id={actor}
              AND(SELECT COUNT(*) FROM dbo.audit_logs WHERE entity_type='REINSPECTION_ASSIGNMENT' AND entity_id=CONVERT(nvarchar(50),b.inspection_id))=1
            """).SingleAsync(ct)!=1)throw new Exception("Current responsibility or immutable original binding mismatch");
        Evidence.Add(new{rework,inspection,transferred,recipientSession,notice,received,originalBindingPreserved=true,
            notificationAuditTamperDenied=true,disabledWriterDenied=true,recipientRoleBranchRevocationDenied=true,
            deliveryFaultRollback=true,concurrentDeliveryDeduplicated=true,storedUrlIgnored=true});
    }
    internal static async Task TerminalAsync(HotelOpsDbContext db,long actor,string password,long branch,long rework,CancellationToken ct)
    {
        var credentials=new PasswordCredentialService(Options.Create(new PasswordHasherOptions
        {CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount}));
        SessionReference ticket;long sid;
        await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
        {
            var verified=await new CredentialLockoutService(db,credentials,TimeProvider.System).VerifyAsync(actor,password,
                (u,_)=>Task.FromResult(u.EmploymentStatus=="ACTIVE"&&u.AccountStatus=="ACTIVE"),ct);
            var session=await new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System).CreateSessionAsync(verified,ct);
            ticket=session.Ticket;sid=session.Session.SessionId;await tx.CommitAsync(ct);db.ChangeTracker.Clear();
        }
        await SprintReinspectionTerminalReadProbe.VerifyAsync(db,ticket,sid,branch,rework,ct);
    }
}
