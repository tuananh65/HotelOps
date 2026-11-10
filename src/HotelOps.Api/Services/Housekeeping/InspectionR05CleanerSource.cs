using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

// Immutable copied source identity. CompletionRevision is NOT a live task/root/cycle ETag.
internal sealed record InspectionCleanerBinding(long TaskId,long RoomId,long BranchId,long AssignmentId,long CleanerUserId,
    long CompletionProgressId,DateTime CompletedAtUtc,string CompletionRevision,long WriterTransactionId)
{
    internal static InspectionCleanerBinding From(CleanerAttribution value)=>new(value.CleaningTaskId,value.RoomId,value.BranchId,
        value.AssignmentId,value.CleanerUserId,value.CompletionProgressId,value.CompletedAtUtc,
        Convert.ToHexString(value.CompletionRevision),value.WriterTransactionId);
}

// Separate advisory hint from the authoritative R05 transaction-only reader.
internal interface IInspectionCleanerAttributionSource:IInspectionOriginalCleanerSource
{
    void BindCaller(SessionReference ticket,long sessionId);
    Task<InspectionCleanerBinding> ReadBindingAsync(long inspection,long task,long room,long branch,string cycleRevision,bool locked,CancellationToken ct);
}

internal sealed class InspectionR05CleanerSource(HotelOpsDbContext db,ICurrentUserAccessor current,IPermissionService permissions,
    Func<RoomTypeCreationService> owner):IInspectionCleanerAttributionSource
{
    private SessionReference? ticket;private long sessionId;
    public void BindCaller(SessionReference value,long sid)
    {
        if(db.Database.CurrentTransaction!=null||ticket!=null||sid<=0)throw new Fe05NotificationUnavailableException("FE05_CLEANER_CALLER_BINDING_INVALID");
        ticket=value;sessionId=sid;
    }
    public async Task<long> ReadAsync(long inspection,long task,long room,long branch,string cycleRevision,CancellationToken ct)
        =>(await ReadBindingAsync(inspection,task,room,branch,cycleRevision,db.Database.CurrentTransaction!=null,ct)).CleanerUserId;
    public async Task<InspectionCleanerBinding> ReadBindingAsync(long inspection,long task,long room,long branch,string cycleRevision,bool locked,CancellationToken ct)
    {
        if(ticket==null||!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sessionId
            ||actor.IsRestricted||ticket.IsRestricted||!(await permissions.EvaluateBranchAsync("INSPECTION_PERFORM",branch,ct)).IsGranted)
            throw new Fe05NotificationUnavailableException("FE05_CLEANER_INSPECTION_AUTHORIZATION_REQUIRED");
        InspectionCleanerBinding binding;
        if(locked)
        {
            Fe05CallerTransaction.Require(db);
            // Owner rechecks actual SQL session and assigned Inspector/branch before invoking R05.
            binding=InspectionCleanerBinding.From(await owner().ReadAuthorizedInspectionCleanerAsync(ticket,sessionId,branch,inspection,ct)
                ??throw new Fe05NotificationUnavailableException("FE04_R05_ATTRIBUTION_MISSING_AMBIGUOUS_OR_STALE"));
        }
        else
        {
            if(db.Database.CurrentTransaction!=null||System.Transactions.Transaction.Current!=null)
                throw new Fe05NotificationUnavailableException("FE05_CLEANER_DISCOVERY_BEFORE_USERS_REQUIRED");
            if(await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.fe04_cleaner_attributions','U') IS NULL THEN 0 ELSE 1 END AS Value").SingleAsync(ct)!=1)
                throw new Fe05NotificationUnavailableException("FE04_R05_SCHEMA_REQUIRED");
            // Advisory only, no serializable read or authority claim before Users locks.
            var row=await db.Database.SqlQuery<Hint>($"""
                SELECT a.cleaning_task_id AS TaskId,a.room_id AS RoomId,a.branch_id AS BranchId,
                    a.assignment_id AS AssignmentId,a.cleaner_user_id AS CleanerUserId,
                    a.completion_progress_id AS CompletionProgressId,a.completed_at AS CompletedAt,
                    a.completion_revision AS CompletionRevision,a.writer_transaction_id AS WriterTransactionId
                FROM dbo.room_inspections i JOIN dbo.rooms r ON r.room_id=i.room_id AND r.branch_id={branch} AND r.is_active=1
                JOIN dbo.fe04_cleaner_attributions a ON a.cleaning_task_id=i.cleaning_task_id AND a.room_id=i.room_id AND a.branch_id=r.branch_id
                WHERE i.inspection_id={inspection} AND i.inspector_user_id={actor.UserId}
                """).SingleOrDefaultAsync(ct)??throw new Fe05NotificationUnavailableException("FE04_R05_ATTRIBUTION_HINT_REQUIRED");
            binding=new(row.TaskId,row.RoomId,row.BranchId,row.AssignmentId,row.CleanerUserId,row.CompletionProgressId,
                DateTime.SpecifyKind(row.CompletedAt,DateTimeKind.Utc),Convert.ToHexString(row.CompletionRevision),row.WriterTransactionId);
        }
        if(binding.TaskId!=task||binding.RoomId!=room||binding.BranchId!=branch||binding.AssignmentId<=0||binding.CleanerUserId<=0
            ||binding.CompletionProgressId<=0||binding.CompletionRevision.Length!=16||binding.WriterTransactionId<=0)
            throw new Fe05NotificationUnavailableException("FE04_R05_ATTRIBUTION_IDENTITY_MISMATCH");
        var cycle=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_cleaning_cycle_state WHERE room_id={room} AND cleaning_task_id={task}").SingleOrDefaultAsync(ct);
        if(cycle==null||Convert.ToHexString(cycle)!=cycleRevision)throw new Fe05NotificationUnavailableException("FE05_CLEANER_CURRENT_CYCLE_CHANGED");
        return binding;
    }
    private sealed class Hint
    {
        public long TaskId{get;set;}public long RoomId{get;set;}public long BranchId{get;set;}public long AssignmentId{get;set;}
        public long CleanerUserId{get;set;}public long CompletionProgressId{get;set;}public long WriterTransactionId{get;set;}
        public DateTime CompletedAt{get;set;}public byte[] CompletionRevision{get;set;}=[];
    }
}

public sealed partial class RoomTypeCreationService
{
    // This is FE05's authorization wrapper, not a change to FE04's original reader.
    internal async Task<CleanerAttribution?> ReadAuthorizedInspectionCleanerAsync(SessionReference ticket,long sid,long branch,long inspection,CancellationToken ct)
    {
        Fe05CallerTransaction.Require(db);
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted||ticket.IsRestricted)
            throw new Fe05NotificationUnavailableException("FE05_CLEANER_INSPECTION_AUTHORIZATION_REQUIRED");
        (inspectionUsers??throw new Fe05NotificationUnavailableException("FE05_COMPLETE_USERS_ROSTER_REQUIRED")).RequireMember(actor.UserId);
        if(inspectionUsers.Roster.InspectionId!=inspection||inspectionUsers.Roster.BranchId!=branch)
            throw new Fe05NotificationUnavailableException("FE05_CLEANER_RETAINED_SOURCE_MISMATCH");
        var valid=await sessions.ValidateSessionAsync(ticket,ct);
        if(!valid.IsValid||valid.Session?.SessionId!=sid||valid.Session.IsRestricted
            ||!(await permissions.EvaluateBranchAsync("INSPECTION_PERFORM",branch,ct)).IsGranted)
            throw new Fe05NotificationUnavailableException("FE05_CLEANER_INSPECTION_AUTHORIZATION_REQUIRED");
        if(!await db.RoomInspections.AsNoTracking().AnyAsync(i=>i.InspectionId==inspection&&i.InspectorUserId==actor.UserId
            &&i.Room.BranchId==branch&&i.Room.IsActive,ct))throw new Fe05NotificationUnavailableException("FE05_CLEANER_EXACT_INSPECTION_REQUIRED");
        var retained=inspectionUsers.Roster;
        if(retained.CleanerBinding==null||retained.OriginalCleanerId!=retained.CleanerBinding.CleanerUserId)
            throw new Fe05NotificationUnavailableException("FE04_R05_COMPLETE_ATTRIBUTION_BINDING_REQUIRED");
        inspectionUsers.RequireMember(retained.CleanerBinding.CleanerUserId);
        var source=await ReadInspectionCleanerAttributionAsync(db.Database.GetDbConnection(),db.Database.CurrentTransaction!.GetDbTransaction(),inspection,branch,ct);
        if(source!=null&&InspectionCleanerBinding.From(source)!=retained.CleanerBinding)
            throw new Fe05NotificationUnavailableException("FE04_R05_RETAINED_ATTRIBUTION_CHANGED");
        return source;
    }
}
