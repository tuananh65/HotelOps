using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

// Internal capability retained only by the approved Users-lock hook. Never a request DTO.
internal sealed class SupervisorBindingUsers
{
    private readonly HotelOpsDbContext context;
    private readonly DbTransaction physical;
    private readonly long[] users;
    private readonly long actorId,supervisorId;
    internal long InspectorId{get;}
    private SupervisorBindingUsers(HotelOpsDbContext db,long[] users,long actor,long inspector,long supervisor)
    {context=db;physical=db.Database.CurrentTransaction!.GetDbTransaction();this.users=users;actorId=actor;InspectorId=inspector;supervisorId=supervisor;}
    internal static SupervisorBindingUsers Retain(HotelOpsDbContext db,
        IReadOnlyDictionary<long,UserEligibilityLock.LockedUser?> actual,long actor,long inspector,long supervisor)
    {
        Fe05CallerTransaction.Require(db);
        var ids=new[]{actor,inspector,supervisor}.Distinct().Order().ToArray();
        if(ids.Any(x=>x<=0)||!ids.SequenceEqual(actual.Keys.Order()))throw new Fe05NotificationUnavailableException("FE05_BINDING_LOCKED_ROSTER_MISMATCH");
        foreach(var id in ids)
        {UserEligibilityLock.RequireEligible(id,actual);if(actual[id]!.UserId!=id)throw new Fe05NotificationUnavailableException("FE05_BINDING_LOCKED_ID_MISMATCH");}
        return new(db,ids,actor,inspector,supervisor);
    }
    internal void Require(HotelOpsDbContext db,long actor,long inspector,long supervisor)
    {
        Fe05CallerTransaction.Require(db,db.Database.GetDbConnection(),physical);
        if(!ReferenceEquals(db,context)||actor!=actorId||inspector!=InspectorId||supervisor!=supervisorId||!users.SequenceEqual(new[]{actor,inspector,supervisor}.Distinct().Order()))
            throw new Fe05NotificationUnavailableException("FE05_BINDING_LATE_USER_FORBIDDEN");
    }
}

public sealed partial class RoomTypeCreationService
{
    internal sealed class SupervisorAssignmentSource
    {
        public long TaskId{get;set;}public long RoomId{get;set;}public long InspectorId{get;set;}
        public byte[] RootRevision{get;set;}=[];public byte[] CycleRevision{get;set;}=[];
    }
    internal async Task<SupervisorAssignmentSource> ReadSupervisorAssignmentSourceAsync(long branch,long inspection,CancellationToken ct)
    {
        Fe05CallerTransaction.Require(db);
        // Same catalog serialization as UC041; Users have already been locked by Batch.
        if(await db.Database.SqlQuery<string>($"SELECT APPLOCK_MODE('public','HotelOps:RoomTypes:v1','Transaction') AS Value").SingleAsync(ct)!="Exclusive")
            throw new Fe05NotificationUnavailableException("FE05_ASSIGNMENT_CATALOG_LOCK_REQUIRED");
        return await db.Database.SqlQuery<SupervisorAssignmentSource>($"""
            SELECT i.cleaning_task_id AS TaskId,i.room_id AS RoomId,i.inspector_user_id AS InspectorId,
                i.row_version AS RootRevision,c.row_version AS CycleRevision
            FROM dbo.room_inspections i WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.cleaning_tasks t WITH(UPDLOCK,HOLDLOCK) ON t.cleaning_task_id=i.cleaning_task_id AND t.room_id=i.room_id
            JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=i.room_id AND r.branch_id={branch} AND r.is_active=1
            JOIN dbo.room_cleaning_cycle_state c WITH(UPDLOCK,HOLDLOCK) ON c.room_id=i.room_id AND c.cleaning_task_id=t.cleaning_task_id
            WHERE i.inspection_id={inspection} AND i.status='PENDING' AND i.workflow_status='ASSIGNED'
              AND (t.workflow_status='PENDING_INSPECTION' OR EXISTS(SELECT 1 FROM dbo.cleaning_reworks w WITH(HOLDLOCK)
                WHERE w.cleaning_task_id=t.cleaning_task_id AND w.workflow_status='PENDING_REINSPECTION'))
              AND NOT EXISTS(SELECT 1 FROM dbo.room_inspections newer WITH(HOLDLOCK)
                WHERE newer.cleaning_task_id=i.cleaning_task_id AND newer.inspection_id>i.inspection_id)
            """).SingleOrDefaultAsync(ct)??throw new Fe05NotificationUnavailableException("FE05_ASSIGNMENT_SOURCE_REQUIRED");
    }
    // No caller COMMIT, new transaction, raw public endpoint or responsibility backfill.
    // Used immediately after creation by the internal UC041 candidate, never after conclusion.
    internal async Task<string> BindSupervisorInAssignmentAsync(SessionReference ticket,long sid,long branch,long inspection,
        long supervisor,byte[] expectedRoot,byte[] expectedCycle,string reason,SupervisorBindingUsers retained,CancellationToken ct)
    {
        Fe05CallerTransaction.Require(db);ct.ThrowIfCancellationRequested();
        if(supervisor<=0||branch<=0||inspection<=0||expectedRoot.Length!=8||expectedCycle.Length!=8)
            throw new ValidationException("Exact supervisor/source/revisions required.");
        var note=WorkflowReason(reason);
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted||ticket.IsRestricted)
            throw new Fe05NotificationUnavailableException("FE05_BINDING_ACTOR_INVALID");
        // Must not take source locks before proving the caller's immutable Users capability.
        retained.Require(db,actor.UserId,retained.InspectorId,supervisor);
        var session=await sessions.ValidateSessionAsync(ticket,ct);
        if(!session.IsValid||session.Session?.SessionId!=sid||session.Session.IsRestricted
            ||!(await permissions.EvaluateBranchAsync("INSPECTION_PERFORM",branch,ct)).IsGranted)
            throw new Fe05NotificationUnavailableException("FE05_BINDING_AUTHORIZATION_REQUIRED");
        if(!await EligibleInspector(supervisor,branch,ct))
            throw new Fe05NotificationUnavailableException("FE05_EXPLICIT_SUPERVISOR_NOT_ELIGIBLE");
        var source=await ReadSupervisorAssignmentSourceAsync(branch,inspection,ct);
        retained.Require(db,actor.UserId,source.InspectorId,supervisor);
        if(!source.RootRevision.SequenceEqual(expectedRoot)||!source.CycleRevision.SequenceEqual(expectedCycle))
            throw new Fe05NotificationUnavailableException("FE05_BINDING_STALE_SOURCE");
        if(await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.inspection_supervisor_bindings','U') IS NULL THEN 0 ELSE 1 END AS Value").SingleAsync(ct)!=1)
            throw new Fe05NotificationUnavailableException("FE05_SUPERVISOR_SCHEMA_CANDIDATE_REQUIRED");
        if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.inspection_supervisor_bindings WITH(UPDLOCK,HOLDLOCK) WHERE inspection_id={inspection}").SingleAsync(ct)!=0)
            throw new Fe05NotificationUnavailableException("FE05_RESPONSIBILITY_REASSIGNMENT_NOT_APPROVED");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.inspection_supervisor_bindings(inspection_id,cleaning_task_id,room_id,branch_id,
                responsible_supervisor_user_id,cycle_revision,inspection_revision_at_binding,bound_by_user_id,reason)
            VALUES({inspection},{source.TaskId},{source.RoomId},{branch},{supervisor},{expectedCycle},{expectedRoot},{actor.UserId},{note})
            """,ct);
        return WorkflowTag((await InspectionState(inspection,ct)).Revision);
    }
}
