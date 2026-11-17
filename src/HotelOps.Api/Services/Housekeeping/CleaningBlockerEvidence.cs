using System.Globalization;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.MaterialRequests;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private sealed record CleaningBlockerProof(string? Failure, object? Receipt);

    // Caller owns the serializable Cleaning transaction. Source reads neither
    // grant permissions nor mutate Incident/Material Request lifecycles.
    private async Task<CleaningBlockerProof> CleaningBlockerProofAsync(ResumeBlocker blocker,
        long actor,long branch,long task,long room,CancellationToken ct)
    {
        if (!await CanLinkCleaningBlocker(actor,branch,task,room,blocker.IncidentId,blocker.MaterialRequestId,ct))
            return new("BLOCKER_SOURCE_NOT_ACCESSIBLE",null);
        var receipts = new List<object>();
        if (blocker.IncidentId is {} incidentId)
        {
            var incident=await db.Incidents.AsNoTracking().SingleOrDefaultAsync(i=>i.IncidentId==incidentId&&i.BranchId==branch,ct);
            if (incident?.Status!="CLOSED") return new("BLOCKER_INCIDENT_NOT_CLOSED",null);
            var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            if(incident.ClosedAt==null||incident.ClosedAt>now)return new("BLOCKER_INCIDENT_RECEIPT_REQUIRED",null);
            var works=await db.MaintenanceWorks.AsNoTracking().Where(w=>w.IncidentId==incidentId
                &&!w.PreventiveMaintenanceRuns.Any()&&w.Status=="CLOSED"&&w.ReviewedAt==incident.ClosedAt)
                .Select(w=>w.MaintenanceWorkId).ToArrayAsync(ct);
            if(works.Length!=1)return new("BLOCKER_INCIDENT_RECEIPT_REQUIRED",null);
            var key=works[0].ToString(CultureInfo.InvariantCulture);
            var audits=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="maintenance_works"&&a.EntityId==key
                &&a.BranchId==branch&&a.Action=="UPDATE"&&a.OccurredAt==incident.ClosedAt).ToArrayAsync(ct);
            var matching=audits.Select(MaintenanceClosureSource.Parse).Where(p=>p!=null&&p.IncidentId==incidentId).ToArray();
            if(matching.Length!=1||await MaintenanceClosureSource.ReadAsync(db,matching[0]!.AuditId,ct) is not {} proof)
                return new("BLOCKER_INCIDENT_RECEIPT_REQUIRED",null);
            receipts.Add(new {kind="INCIDENT",id=incidentId,proof.AuditId,proof.WorkId,proof.At});
        }
        if(blocker.MaterialRequestId is {} materialId)
        {
            var proof=await MaterialCompletionEvidence.ReadAsync(db,materialId,branch,ct);
            if(!proof.FullyIssuedProven&&!proof.RemainderClosureProven)
                return new(proof.Failure??"BLOCKER_MATERIAL_RECEIPT_REQUIRED",null);
            var issues=await db.MaterialFulfillments.AsNoTracking().Where(f=>f.RequestId==materialId)
                .OrderBy(f=>f.FulfillmentId).Select(f=>new{f.FulfillmentId,f.StockDocumentId,f.FulfilledAt}).ToArrayAsync(ct);
            var closure=await db.MaterialRequests.AsNoTracking().Where(m=>m.RequestId==materialId)
                .Select(m=>new{m.CompletedAt,m.RemainderClosedAt,m.RemainderClosedBy,m.RemainderCloseReason}).SingleAsync(ct);
            receipts.Add(new{kind="MATERIAL_REQUEST",id=materialId,proof.Status,issues,closure});
        }
        if(blocker.ResolvedAt!=null&&(blocker.IncidentId!=null||blocker.MaterialRequestId!=null)
            &&!await db.Database.SqlQuery<int>($"""
                SELECT 1 AS Value FROM dbo.fs014_cleaning_blocker_resolutions b
                JOIN dbo.audit_logs a ON a.entity_type='cleaning_progress_logs' AND a.entity_id=CONVERT(nvarchar(30),b.progress_id)
                  AND a.branch_id={branch} AND a.actor_user_id=b.resolved_by AND a.occurred_at=b.resolved_at
                CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END AS payload) j
                WHERE b.progress_id={blocker.ProgressId}
                  AND JSON_VALUE(j.payload,'$.action')='RESOLVE_CLEANING_BLOCKER'
                  AND JSON_VALUE(j.payload,'$.contract')='EXPLICIT_HOUSEKEEPER_CONFIRMATION'
                  AND JSON_VALUE(j.payload,'$.resolutionNote')=b.resolution_note
                  AND (b.incident_id IS NULL OR TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.incidentId'))=b.incident_id)
                  AND (b.material_request_id IS NULL OR TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.materialRequestId'))=b.material_request_id)
                """).AnyAsync(ct))return new("BLOCKER_CONFIRMATION_RECEIPT_REQUIRED",null);
        return new(null,receipts);
    }

    private async Task<IReadOnlyList<CleaningBlockerView>> CleaningBlockerViewsAsync(long actor,long branch,long task,long room,CancellationToken ct)
    {
        if(!await ResumeTrialInstalled(ct))return [];
        var blockers=await ReadCleaningBlockersAsync(task,ct);
        var views=new List<CleaningBlockerView>();
        var missing=await db.Database.SqlQuery<long>($"""
            SELECT p.progress_id AS Value FROM dbo.cleaning_progress_logs p
            WHERE p.cleaning_task_id={task} AND p.status='BLOCKED'
              AND NOT EXISTS(SELECT 1 FROM dbo.fs014_cleaning_blocker_resolutions b WHERE b.progress_id=p.progress_id)
            """).ToArrayAsync(ct);
        views.AddRange(missing.Select(id=>new CleaningBlockerView(id,null,null,false,false,"BLOCKER_STATE_UNRECONCILED")));
        foreach(var blocker in blockers)
        {
            var proof=await CleaningBlockerProofAsync(blocker,actor,branch,task,room,ct);
            var accessible=proof.Failure!="BLOCKER_SOURCE_NOT_ACCESSIBLE";
            views.Add(new(blocker.ProgressId,accessible?blocker.IncidentId:null,accessible?blocker.MaterialRequestId:null,
                blocker.ResolvedAt!=null,proof.Failure==null,proof.Failure));
        }
        return views;
    }

    private Task<ResumeBlocker[]> ReadCleaningBlockersAsync(long task,CancellationToken ct)
        => db.Database.SqlQuery<ResumeBlocker>($"""
            SELECT p.progress_id AS ProgressId,b.incident_id AS IncidentId,b.material_request_id AS MaterialRequestId,
              b.resolved_at AS ResolvedAt
            FROM dbo.cleaning_progress_logs p WITH(HOLDLOCK)
            JOIN dbo.fs014_cleaning_blocker_resolutions b WITH(HOLDLOCK) ON b.progress_id=p.progress_id
            WHERE p.cleaning_task_id={task} AND p.status='BLOCKED'
            """).ToArrayAsync(ct);
}
