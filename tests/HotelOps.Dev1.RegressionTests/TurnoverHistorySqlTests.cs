using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace HotelOps.Dev1.RegressionTests;

public sealed class HistoryTrialFactAttribute:FactAttribute
{public HistoryTrialFactAttribute(){if(Environment.GetEnvironmentVariable("FS017_SQL_READ")!="FS016_READ_ONLY")Skip="Read-only existing synthetic FS016 trial, no business fixtures written";}}

[Collection("HotelOps isolated SQL")]
public sealed class TurnoverHistorySqlTests
{
    internal sealed class Identity(long actor):ICurrentUserAccessor{public bool TryGetCurrentUser(out CurrentUserInfo user){user=new(actor,1,false);return true;}}
    internal sealed class Sessions(long actor):ISqlSessionService
    {
        public Task<SessionValidationResult> ValidateSessionAsync(SessionReference ticket,CancellationToken cancellationToken=default)=>Task.FromResult(new SessionValidationResult(true,new(1,actor,ticket.CreatedAtUtc,DateTime.UtcNow.AddHours(1),DateTime.UtcNow.AddHours(2),false)));
        public Task<CreatedSqlSession>CreateSessionAsync(CredentialLockoutResult credential,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<SessionValidationResult>TouchSessionAsync(SessionReference ticket,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<bool>RevokeSessionAsync(SessionReference ticket,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<int>RevokeAllSessionsAsync(long userId,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
    }
    internal static IPermissionService Permissions(HotelOpsDbContext db,long actor)=>new PermissionService(new Identity(actor),new EfPermissionDataSource(db),new UnresolvedPermissionResourceResolver(),TimeProvider.System);
    internal static TurnoverHistoryService Service(HotelOpsDbContext db,long actor,IPermissionService? permissions=null)=>new(db,new Identity(actor),permissions??Permissions(db,actor),new Sessions(actor));
    [Fact]
    public async Task History_rejects_mismatched_session_before_SQL()
    {var service=new TurnoverHistoryService(null!,new Identity(2),null!,null!);Assert.Equal(TurnoverHistoryStatus.SessionInvalid,(await service.ReadTurnoverAsync(CleaningClosureWorkflowTests.Ticket(1),1,1,new(),null,default)).Status);}
    [HistoryTrialFact]
    public async Task History_readonly_scope_source_identity_current_cycle_ordering_and_pagination()
    {
        using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(120));var ct=budget.Token;await using var db=ReworkWorkflowSqlTests.Context();
        Assert.Equal("HotelOps_Canonical_FS016_Rework_V1_Fresh",db.Database.GetDbConnection().Database);
        var supervisor=await db.Users.AsNoTracking().SingleAsync(u=>u.Username=="fs015.supervisor",ct);var branch=supervisor.PrimaryBranchId!.Value;
        var task=await db.CleaningTasks.AsNoTracking().Where(t=>t.Room.BranchId==branch).OrderByDescending(t=>t.CleaningTaskId).FirstAsync(ct);
        var before=await Snapshot(db,ct);var service=Service(db,supervisor.UserId);var ticket=CleaningClosureWorkflowTests.Ticket(supervisor.UserId);
        var result=await service.ReadTurnoverAsync(ticket,1,task.RoomId,new(BranchId:branch,PageSize:1),null,ct);Assert.Equal(TurnoverHistoryStatus.Success,result.Status);
        var page=Assert.IsType<TurnoverPage>(result.Value);Assert.Single(page.Items);Assert.Equal(task.CleaningTaskId,page.Items[0].Id);Assert.Null(page.Items[0].Result);Assert.Equal("NOT_ACCEPTED",page.Coverage.Inspection);
        var pointer=await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS [Value] FROM dbo.room_cleaning_cycle_state WHERE room_id={task.RoomId}").SingleAsync(ct);Assert.Equal(pointer==task.CleaningTaskId,page.Items[0].IsCurrentCycle);
        var detailResult=await service.ReadTurnoverAsync(ticket,1,task.RoomId,new(BranchId:branch,TimelinePageSize:3),task.CleaningTaskId,ct);Assert.Equal(TurnoverHistoryStatus.Success,detailResult.Status);
        var detail=Assert.IsType<TurnoverDetail>(detailResult.Value);Assert.InRange(detail.Timeline.Count,1,3);Assert.All(detail.Timeline,e=>Assert.Equal(DateTimeKind.Utc,e.AtUtc.Kind));Assert.Equal(TurnoverReadRules.Order(detail.Timeline),detail.Timeline);
        var second=Assert.IsType<TurnoverDetail>((await service.ReadTurnoverAsync(ticket,1,task.RoomId,new(BranchId:branch,TimelinePage:2,TimelinePageSize:3),task.CleaningTaskId,ct)).Value);
        Assert.Empty(detail.Timeline.Select(x=>(x.SourceType,x.SourceId,x.Event)).Intersect(second.Timeline.Select(x=>(x.SourceType,x.SourceId,x.Event))));
        Assert.All(detail.Inspections,i=>{Assert.Equal(task.RoomId,i.RoomId);Assert.Equal(task.CleaningTaskId,i.CleaningTaskId);Assert.Null(i.Result);});
        Assert.All(detail.Reworks,w=>Assert.Contains(detail.Inspections,i=>i.Id==w.InspectionId));
        var empty=Assert.IsType<TurnoverPage>((await service.ReadTurnoverAsync(ticket,1,task.RoomId,new(BranchId:branch,From:DateTime.UtcNow.AddDays(1)),null,ct)).Value);Assert.Empty(empty.Items);
        Assert.Equal(TurnoverHistoryStatus.Unavailable,(await service.ReadTurnoverAsync(ticket,1,task.RoomId,new(BranchId:branch,Result:"FAIL"),null,ct)).Status);
        var foreign=await db.CleaningTasks.Where(t=>t.RoomId!=task.RoomId).Select(t=>t.CleaningTaskId).FirstAsync(ct);
        Assert.Equal(TurnoverHistoryStatus.NotFound,(await service.ReadTurnoverAsync(ticket,1,task.RoomId,new(BranchId:branch),foreign,ct)).Status);
        Assert.Equal(TurnoverHistoryStatus.NotFound,(await service.ReadTurnoverAsync(ticket,1,task.RoomId,new(BranchId:branch),task.CleaningTaskId,ct,long.MaxValue,Path.GetTempPath())).Status);
        Assert.Equal(TurnoverHistoryStatus.Forbidden,(await service.ReadTurnoverAsync(ticket,1,task.RoomId,new(BranchId:branch+999999),null,ct)).Status);
        // Source-specific denial must hide Inspection, Rework and Inspection evidence, while cleaning history remains readable.
        var restricted=Service(db,supervisor.UserId,new CleaningOnly(Permissions(db,supervisor.UserId)));
        var subset=Assert.IsType<TurnoverDetail>((await restricted.ReadTurnoverAsync(ticket,1,task.RoomId,new(BranchId:branch),task.CleaningTaskId,ct)).Value);
        Assert.Empty(subset.Inspections);Assert.Empty(subset.Reworks);Assert.Equal("SOURCE_READ_DENIED",subset.Coverage.Inspection);Assert.All(subset.Evidence,e=>Assert.Equal("CLEANING_TASK",e.EntityType));
        var revoked=Service(db,supervisor.UserId,new RevokeAfterRead(Permissions(db,supervisor.UserId)));
        Assert.Equal(TurnoverHistoryStatus.Forbidden,(await revoked.ReadTurnoverAsync(ticket,1,task.RoomId,new(BranchId:branch),task.CleaningTaskId,ct)).Status);
        var monitor=new CleaningMonitorService(db,new Identity(supervisor.UserId),Permissions(db,supervisor.UserId),new Sessions(supervisor.UserId),TimeProvider.System);
        var item=Assert.IsType<CleaningMonitorDetail>((await monitor.DetailAsync(ticket,1,branch,"cleaning",task.CleaningTaskId,ct)).Value).Item;
        var expected=await db.Database.SqlQuery<string?>($"SELECT TOP(1) e.readiness AS [Value] FROM dbo.room_readiness_events e JOIN dbo.room_cleaning_cycle_state cp ON cp.room_id=e.room_id AND cp.cleaning_task_id=e.cleaning_task_id WHERE e.room_id={task.RoomId} ORDER BY e.occurred_at DESC,e.event_id DESC").FirstOrDefaultAsync(ct);
        var block=await db.RoomBlocks.AnyAsync(b=>b.RoomId==task.RoomId&&b.ReleasedAt==null&&b.BlockedAt<=DateTime.UtcNow,ct);
        Assert.Equal(block?"BLOCKED":expected,item.EffectiveReadiness);
        Assert.Equal(before,await Snapshot(db,ct));
    }
    internal static Task<string> Snapshot(HotelOpsDbContext db,CancellationToken ct)=>db.Database.SqlQuery<string>($"""
        SELECT (SELECT (SELECT COUNT(*) FROM dbo.cleaning_tasks) AS tasks,(SELECT COUNT(*) FROM dbo.cleaning_progress_logs) AS progress,
        (SELECT COUNT(*) FROM dbo.cleaning_reworks) AS reworks,(SELECT COUNT(*) FROM dbo.audit_logs) AS audits,
        (SELECT COUNT(*) FROM dbo.room_readiness_events) AS readiness,(SELECT COUNT(*) FROM dbo.notification_publications) AS publications,
        (SELECT COUNT(*) FROM dbo.work_intervals) AS intervals,(SELECT SUM(CONVERT(bigint,CHECKSUM(*))) FROM dbo.room_cleaning_cycle_state) AS pointer
        FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS [Value]
        """).SingleAsync(ct);
    private sealed class CleaningOnly(IPermissionService source):IPermissionService
    {
        public Task<PermissionEvaluation>EvaluateGlobalAsync(string code,CancellationToken ct)=>code=="INSPECTION_VIEW"?Task.FromResult(new PermissionEvaluation(PermissionDecision.Denied)):source.EvaluateGlobalAsync(code,ct);
        public Task<PermissionEvaluation>EvaluateBranchAsync(string code,long branch,CancellationToken ct)=>code=="INSPECTION_VIEW"?Task.FromResult(new PermissionEvaluation(PermissionDecision.Denied)):source.EvaluateBranchAsync(code,branch,ct);
        public Task<PermissionEvaluation>EvaluateResourceAsync(string code,PermissionResource resource,CancellationToken ct)=>source.EvaluateResourceAsync(code,resource,ct);
    }
    private sealed class RevokeAfterRead(IPermissionService source):IPermissionService
    {
        private int cleaningReads;
        public Task<PermissionEvaluation>EvaluateGlobalAsync(string code,CancellationToken ct)=>source.EvaluateGlobalAsync(code,ct);
        public Task<PermissionEvaluation>EvaluateBranchAsync(string code,long branch,CancellationToken ct)
            =>code=="CLEANING_VIEW"&&++cleaningReads>1?Task.FromResult(new PermissionEvaluation(PermissionDecision.Denied)):source.EvaluateBranchAsync(code,branch,ct);
        public Task<PermissionEvaluation>EvaluateResourceAsync(string code,PermissionResource resource,CancellationToken ct)=>source.EvaluateResourceAsync(code,resource,ct);
    }
}
