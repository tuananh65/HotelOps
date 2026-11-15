using System.Data;
using System.Diagnostics;
using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

internal static class SprintRejectionAcceptanceProbe
{
    private sealed class State
    {
        public string InspectionRevision {get;set;}="";
        public string InspectionState {get;set;}="";
        public string InspectionStatus {get;set;}="";
        public string? Result {get;set;}
        public string? Summary {get;set;}
        public DateTime? InspectedAt {get;set;}
        public string TaskState {get;set;}="";
        public string TaskStatus {get;set;}="";
        public string RoomStatus {get;set;}="";
        public int Defects {get;set;}
        public int Audits {get;set;}
        public int Notifications {get;set;}
        public int Recipients {get;set;}
        public int Reworks {get;set;}
        public int Histories {get;set;}
        public int Attachments {get;set;}
    }
    private const string InstallFault="""
        ALTER TABLE dbo.audit_logs ADD CONSTRAINT CK_DEV3_RejectionAuditFault CHECK
        (ISNULL(JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.notification.kind'),'')<>'INSPECTION_FAILED');
        """;
    private const string DropFault="ALTER TABLE dbo.audit_logs DROP CONSTRAINT CK_DEV3_RejectionAuditFault";
    // Parent has verified exact disposable identity, an empty baseline and cleanup rehearsal.
    internal static async Task VerifyAsync(HotelOpsDbContext db,long actor,long branch,long inspection,string password,CancellationToken ct)
    {
        async Task<string> Snapshot()
        {
            var state=await db.Database.SqlQuery<State>($"""
                SELECT CONVERT(varchar(16),i.row_version,2) AS InspectionRevision,i.workflow_status AS InspectionState,
                  i.status AS InspectionStatus,i.result AS Result,i.summary_note AS Summary,i.inspected_at AS InspectedAt,
                  t.workflow_status AS TaskState,t.status AS TaskStatus,r.operational_status AS RoomStatus,
                  (SELECT COUNT(*) FROM dbo.inspection_defects WHERE inspection_id=i.inspection_id) AS Defects,
                  (SELECT COUNT(*) FROM dbo.audit_logs WHERE entity_type='INSPECTION' AND entity_id=CONVERT(nvarchar(50),i.inspection_id)) AS Audits,
                  (SELECT COUNT(*) FROM dbo.notifications) AS Notifications,(SELECT COUNT(*) FROM dbo.notification_recipients) AS Recipients,
                  (SELECT COUNT(*) FROM dbo.cleaning_reworks WHERE cleaning_task_id=t.cleaning_task_id) AS Reworks,
                  (SELECT COUNT(*) FROM dbo.room_status_history WHERE room_id=r.room_id) AS Histories,
                  (SELECT COUNT(*) FROM dbo.attachments WHERE entity_type='INSPECTION' AND entity_id=i.inspection_id) AS Attachments
                FROM dbo.room_inspections i JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=i.cleaning_task_id
                JOIN dbo.rooms r ON r.room_id=i.room_id AND r.branch_id={branch}
                WHERE i.inspection_id={inspection} AND i.inspector_user_id={actor} AND i.inspection_no='D3-EVIDENCE'
                """).SingleAsync(ct);
            if(state.InspectionState!="PENDING_CONCLUSION"||state.TaskState!="PENDING_INSPECTION"||state.Result!=null||state.Audits!=0||state.Notifications!=0||state.Recipients!=0||state.Reworks!=0)
                throw new Exception("Expected unconcluded owned Reject source");
            return JsonSerializer.Serialize(new{state,files=Directory.GetFiles(SprintReworkFixture.EvidenceRoot).Length});
        }
        async Task Browser(string mode)
        {
            var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.ArgumentList.Add("frontend/scripts/sprintRejectionAcceptanceBrowser.mjs");
            using var process=Process.Start(start)??throw new Exception("Reject acceptance browser unavailable");
            try
            {
                await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username="D3-EVIDENCE",password,branch,inspection,mode}).AsMemory(),ct);process.StandardInput.Close();
                var output=process.StandardOutput.ReadToEndAsync(ct);var errors=process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);var result=JsonSerializer.Deserialize<JsonElement>(await output);await errors;
                if(process.ExitCode!=0||!result.GetProperty("success").GetBoolean())throw new Exception("Reject acceptance: "+result.GetRawText());
            }
            finally{if(!process.HasExited)process.Kill(true);}
        }
        Directory.CreateDirectory(SprintReworkFixture.EvidenceRoot);
        if(await db.InspectionDefects.AnyAsync(d=>d.InspectionId==inspection,ct))throw new Exception("No-defect fixture required");
        var empty=await Snapshot();await Browser("NO_DEFECT");
        if(await Snapshot()!=empty)throw new Exception("No-defect rejection changed source/history/notification");
        await using(var preparation=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.inspection_defects(inspection_id,checklist_item_id,description,status)
                SELECT inspection_id,checklist_item_id,'Synthetic rejection defect','OPEN' FROM dbo.inspection_results WHERE inspection_id={inspection} AND outcome='FAIL';
                """,ct);
            await SprintReworkFixture.SeedEvidenceAsync(db,actor,inspection,ct);
            await preparation.CommitAsync(ct);
        }
        await using(var rehearsal=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
        {await db.Database.ExecuteSqlRawAsync(InstallFault,ct);await db.Database.ExecuteSqlRawAsync(DropFault,ct);await rehearsal.RollbackAsync(ct);}
        var before=await Snapshot();
        await Browser("SHORT_REASON");
        if(await Snapshot()!=before)throw new Exception("Invalid rejection reason changed source/history/notification");
        await db.Database.ExecuteSqlRawAsync(InstallFault,ct);
        try{await Browser("AUDIT_FAULT");if(await Snapshot()!=before)throw new Exception("Reject audit fault leaked aggregate/history/notification/evidence");}
        finally{await db.Database.ExecuteSqlRawAsync(DropFault,CancellationToken.None);}
    }
}
