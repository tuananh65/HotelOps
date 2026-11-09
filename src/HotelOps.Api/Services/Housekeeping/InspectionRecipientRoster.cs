using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.HotelStructure;

// Discovery is advisory, outside the caller transaction. Only the locked reread can attest it.
internal sealed record InspectionRecipientRoster(long ActorId,long BranchId,long InspectionId,long TaskId,long RoomId,
    long InspectorId,long? AssigneeId,long? SupervisorId,long? OriginalCleanerId,
    string InspectionRevision,string CycleRevision,string? SupervisorRevision,Fe05NotificationKind Kind)
{
    internal long[] Users => new[]{(long?)ActorId,InspectorId,AssigneeId,SupervisorId,OriginalCleanerId}
        .Where(x=>x.HasValue).Select(x=>x!.Value).Distinct().Order().ToArray();
    internal bool SameParticipants(InspectionRecipientRoster other)=>(this with{InspectionRevision=other.InspectionRevision})==other;
}
public interface IInspectionOriginalCleanerSource
{
    // Must read FE04-R04's immutable exact-cycle binding. No latest-assignment/log fallback.
    Task<long> ReadAsync(long inspection,long task,long room,long branch,string cycleRevision,CancellationToken ct);
}
internal sealed class MissingInspectionOriginalCleanerSource : IInspectionOriginalCleanerSource
{
    public Task<long> ReadAsync(long inspection,long task,long room,long branch,string cycleRevision,CancellationToken ct)
    {ct.ThrowIfCancellationRequested();throw new Fe05NotificationUnavailableException("FE04_R04_ORIGINAL_CLEANER_SOURCE_REQUIRED");}
}
public sealed class InspectionRecipientRosterReader(HotelOpsDbContext db,IInspectionOriginalCleanerSource original)
{
    internal async Task<InspectionRecipientRoster> DiscoverAsync(long actor,long branch,long inspection,long? assignee,
        Fe05NotificationKind kind,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction!=null || System.Transactions.Transaction.Current!=null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Roster discovery must finish before the caller transaction/User locks.");
        return await ReadAsync(actor,branch,inspection,assignee,kind,false,ct);
    }
    internal async Task<InspectionRecipientRoster> ReadAsync(long actor,long branch,long inspection,long? assignee,
        Fe05NotificationKind kind,bool locked,CancellationToken ct)
    {
        if(actor<=0||branch<=0||inspection<=0||!Enum.IsDefined(kind)||assignee<=0||(kind==Fe05NotificationKind.CleaningReworkRequested&&assignee is null))
            throw new Fe05NotificationUnavailableException("FE05_ROSTER_IDENTITY_INVALID");
        if(locked)Fe05CallerTransaction.Require(db);
        // No serialized source read before Users locks. Normal READ COMMITTED discovery ends
        // before BeginTransaction. Under the caller SERIALIZABLE transaction, these reads retain locks.
        var row=await db.Database.SqlQuery<RosterRow>($"""
            SELECT i.inspection_id AS InspectionId,i.cleaning_task_id AS TaskId,i.room_id AS RoomId,
              i.inspector_user_id AS InspectorId,i.row_version AS InspectionRevision,
              c.row_version AS CycleRevision
            FROM dbo.room_inspections i JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=i.cleaning_task_id AND t.room_id=i.room_id
            JOIN dbo.rooms r ON r.room_id=i.room_id AND r.branch_id={branch} AND r.is_active=1
            JOIN dbo.room_cleaning_cycle_state c ON c.room_id=i.room_id AND c.cleaning_task_id=t.cleaning_task_id
            WHERE i.inspection_id={inspection}
            """).SingleOrDefaultAsync(ct)??throw new Fe05NotificationUnavailableException("FE05_ROSTER_SOURCE_UNAVAILABLE");
        long? supervisor=null,cleaner=null;string? bindingRevision=null;
        if(kind==Fe05NotificationKind.InspectionFailed)
        {
            if(await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.inspection_supervisor_bindings','U') IS NULL THEN 0 ELSE 1 END AS Value").SingleAsync(ct)!=1)
                throw new Fe05NotificationUnavailableException("FE05_PERSISTED_SUPERVISOR_BINDING_REQUIRED");
            var binding=await db.Database.SqlQuery<SupervisorRow>($"""
                SELECT responsible_supervisor_user_id AS SupervisorId,row_version AS Revision
                FROM dbo.inspection_supervisor_bindings
                WHERE inspection_id={inspection} AND cleaning_task_id={row.TaskId} AND room_id={row.RoomId}
                  AND branch_id={branch} AND cycle_revision={row.CycleRevision}
                """).SingleOrDefaultAsync(ct)??throw new Fe05NotificationUnavailableException("FE05_PERSISTED_SUPERVISOR_BINDING_REQUIRED");
            supervisor=binding.SupervisorId;bindingRevision=Convert.ToHexString(binding.Revision);
            cleaner=await original.ReadAsync(inspection,row.TaskId,row.RoomId,branch,Convert.ToHexString(row.CycleRevision),ct);
            if(cleaner<=0)throw new Fe05NotificationUnavailableException("FE04_R04_ORIGINAL_CLEANER_SOURCE_REQUIRED");
        }
        return new(actor,branch,inspection,row.TaskId,row.RoomId,row.InspectorId,assignee,supervisor,cleaner,
            Convert.ToHexString(row.InspectionRevision),Convert.ToHexString(row.CycleRevision),bindingRevision,kind);
    }
    private sealed class RosterRow
    {
        public long InspectionId{get;set;}public long TaskId{get;set;}public long RoomId{get;set;}public long InspectorId{get;set;}
        public byte[] InspectionRevision{get;set;}=[];public byte[] CycleRevision{get;set;}=[];
    }
    private sealed class SupervisorRow {public long SupervisorId{get;set;}public byte[] Revision{get;set;}=[];}
}
