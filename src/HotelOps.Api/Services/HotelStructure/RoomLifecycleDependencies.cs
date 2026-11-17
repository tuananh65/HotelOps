using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed record RoomLifecycleDependency(string Kind,long Count);

public sealed partial class RoomTypeCreationService
{
    private async Task<RoomLifecycleDependency[]> RoomLifecycleDependencies(long branch,long room,CancellationToken ct)
    {
        var facts=new List<RoomLifecycleDependency>();
        void Add(string kind,long count){if(count>0)facts.Add(new(kind,count));}
        // All statuses are read in the retained Serializable transaction. A physical
        // COMPLETED value cannot hide a current rework/pending-inspection lifecycle.
        Add("CLEANING_OPEN_OR_UNPROVEN",await db.Database.SqlQuery<long>($"""
            SELECT COUNT_BIG(*) AS Value FROM dbo.cleaning_tasks t WITH(HOLDLOCK) WHERE t.room_id={room}
              AND (t.workflow_status IS NULL OR NOT((t.workflow_status='COMPLETED' AND t.status='COMPLETED' AND t.actual_end_at IS NOT NULL)
                OR (t.workflow_status='CANCELLED' AND t.status='CANCELLED')))
            """).SingleAsync(ct));
        Add("REWORK_OPEN_OR_UNPROVEN",await db.Database.SqlQuery<long>($"""
            SELECT COUNT_BIG(*) AS Value FROM dbo.cleaning_reworks w WITH(HOLDLOCK)
            JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=w.cleaning_task_id WHERE t.room_id={room}
              AND (w.workflow_status IS NULL OR NOT((w.workflow_status IN('COMPLETED','FAILED') AND w.status='COMPLETED' AND w.completed_at IS NOT NULL)
                OR (w.workflow_status='CANCELLED' AND w.status='CANCELLED')))
            """).SingleAsync(ct));
        Add("INSPECTION_OPEN_OR_UNPROVEN",await db.Database.SqlQuery<long>($"""
            SELECT COUNT_BIG(*) AS Value FROM dbo.room_inspections i WITH(HOLDLOCK) WHERE i.room_id={room}
              AND (i.workflow_status IS NULL OR NOT((i.status='COMPLETED' AND i.inspected_at IS NOT NULL
                AND ((i.workflow_status='PASSED' AND ISNULL(i.result,'')='PASS') OR(i.workflow_status='FAILED' AND ISNULL(i.result,'')='FAIL')))
                OR(i.workflow_status='CANCELLED' AND i.status='CANCELLED')))
            """).SingleAsync(ct));
        var mapped=db.Database.SqlQuery<long>($"SELECT incident_id AS Value FROM dbo.incident_affected_rooms WITH(HOLDLOCK) WHERE room_id={room}");
        Add("OPEN_INCIDENT",await db.Incidents.AsNoTracking().LongCountAsync(i=>(i.RoomId==room||mapped.Contains(i.IncidentId))
            &&i.Status!="CLOSED"&&i.Status!="CANCELLED",ct));
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        Add("EFFECTIVE_BLOCK",await db.RoomBlocks.AsNoTracking().LongCountAsync(b=>b.RoomId==room&&b.BlockedAt<=now
            &&(b.ReleasedAt==null||b.ReleasedAt>now),ct));
        // GBR-040 requires moving the assets, including retired assets still physically here.
        Add("ASSET_IN_ROOM",await db.Assets.AsNoTracking().LongCountAsync(a=>a.CurrentRoomId==room,ct));
        Add("INSPECTION_SCOPE_MISMATCH",await db.RoomInspections.AsNoTracking().LongCountAsync(i=>i.CleaningTask!=null
            &&i.RoomId!=i.CleaningTask.RoomId&&(i.RoomId==room||i.CleaningTask.RoomId==room),ct));
        Add("OPEN_WORK_INTERVAL",await db.WorkIntervals.AsNoTracking().LongCountAsync(w=>w.BranchId==branch&&w.ClosedAt==null
            &&(w.WorkItemKind=="CLEANING"&&db.CleaningTasks.Any(t=>t.CleaningTaskId==w.WorkItemId&&t.RoomId==room)
                ||w.WorkItemKind=="REWORK"&&db.CleaningReworks.Any(r=>r.ReworkId==w.WorkItemId&&r.CleaningTask.RoomId==room)),ct));
        return facts.ToArray();
    }
}
