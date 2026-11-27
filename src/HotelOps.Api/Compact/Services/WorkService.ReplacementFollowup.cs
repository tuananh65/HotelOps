using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    // The failed attempt remains terminal. Approval starts a separate assignable
    // installation order under the same incident, within the approval transaction.
    private async Task<long> CreateReplacementFollowup(WorkItem failed,ReplacementSnapshot snapshot,ReplacementRequest request,CancellationToken ct)
    {
        Output.Require(!await db.WorkLinks.AnyAsync(l=>l.TargetWorkId==failed.WorkId&&l.Relation=="SOURCE"
            &&db.WorkItems.Any(w=>w.WorkId==l.SourceWorkId&&w.Kind=="MAINTENANCE"),ct),"REPLACEMENT_ALREADY_OPEN");
        if(failed.ParentWorkId is long incidentId){
            var incident=await db.WorkItems.SingleAsync(w=>w.WorkId==incidentId&&w.BranchId==failed.BranchId&&w.Kind=="INCIDENT",ct);
            Output.Require(!WorkRules.Terminal.Contains(incident.Status),"ACTIVE_INCIDENT_REQUIRED");
            Output.Require(!await db.WorkItems.AnyAsync(w=>w.ParentWorkId==incidentId&&w.WorkId!=failed.WorkId&&w.Kind=="MAINTENANCE"
                &&w.Status!="CANCELLED"&&w.Status!="CLOSED"&&w.Status!="COMPLETED",ct),"MAINTENANCE_ALREADY_ACTIVE");
        }
        var title="Thay thế thiết bị · "+failed.Title;
        var followup=new WorkItem{BranchId=failed.BranchId,Kind="MAINTENANCE",Status="PENDING",WorkNo=Guid.NewGuid().ToString("N"),
            Title=title.Length>250?title[..250]:title,Description=snapshot.Plan,ParentWorkId=failed.ParentWorkId,
            DepartmentId=failed.DepartmentId,RoomId=failed.RoomId,LocationId=failed.LocationId,AssetId=failed.AssetId,TurnoverNo=failed.TurnoverNo,
            CategoryId=failed.CategoryId,PriorityId=failed.PriorityId,SeverityId=failed.SeverityId,ChecklistTemplateId=failed.ChecklistTemplateId,
            CreatedBy=actor.Id,CreatedAt=Now};
        db.WorkItems.Add(followup);await db.SaveChangesAsync(ct);
        db.WorkLinks.Add(new WorkLink{BranchId=failed.BranchId,SourceWorkId=followup.WorkId,TargetWorkId=failed.WorkId,Relation="SOURCE",CreatedBy=actor.Id,CreatedAt=Now});
        // Preserve the published snapshot even if its template was subsequently retired.
        foreach(var item in await db.WorkChecklistItems.AsNoTracking().Where(i=>i.WorkId==failed.WorkId).ToArrayAsync(ct))
            db.WorkChecklistItems.Add(new WorkChecklistItem{BranchId=followup.BranchId,WorkId=followup.WorkId,ChecklistTemplateId=item.ChecklistTemplateId,
                ChecklistItemId=item.ChecklistItemId,DescriptionSnapshot=item.DescriptionSnapshot,RequiredSnapshot=item.RequiredSnapshot,WeightSnapshot=item.WeightSnapshot});
        Event(followup,"REPLACEMENT_APPROVED",request.OperationKey,request.Reason,null);
        db.WorkEvents.Local.Single(e=>e.WorkId==followup.WorkId&&e.EventType=="REPLACEMENT_APPROVED").SnapshotJson=JsonSerializer.Serialize(snapshot);
        await db.SaveChangesAsync(ct);
        return followup.WorkId;
    }
}
