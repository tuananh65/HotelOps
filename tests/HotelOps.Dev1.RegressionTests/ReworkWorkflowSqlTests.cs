using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class ReworkTrialTheoryAttribute : TheoryAttribute
{
    public ReworkTrialTheoryAttribute()
    { if (Environment.GetEnvironmentVariable("FS016_SQL_TRIAL") != "APPROVED_LOCAL_ONLY") Skip = "Opt-in isolated FS016 synthetic Inspection adapter; NOT accepted Inspection source."; }
}

[Collection("HotelOps isolated SQL")]
public sealed class ReworkWorkflowSqlTests
{
    internal const string Connection = "Data Source=.\\SQLEXPRESS;Initial Catalog=HotelOps_Canonical_FS016_Rework_V1_Fresh;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=30";
    internal static HotelOpsDbContext Context(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(Connection).AddInterceptors(interceptors).Options);
    internal static ProvisionalCleaningNotification Publisher(HotelOpsDbContext db, bool enabled = true, bool production = false)
        => new(db, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["CleaningRework:Candidate"] = enabled ? "FS016_PROVISIONAL" : null,
            ["CleaningClosure:Candidate"] = "FS015_PROVISIONAL" }).Build(), new TrialEnvironment { EnvironmentName = production ? "Production" : "Development" });
    internal static RoomTypeCreationService Service(HotelOpsDbContext db, long actor, bool enabled = true, bool production = false)
        => new(db,new Identity(actor),null!,new Sessions(actor),TimeProvider.System,Publisher(db,enabled,production));
    internal static Task<string> Tag(HotelOpsDbContext db, long id, CancellationToken ct) => db.Database.SqlQuery<string>($"SELECT CHAR(34)+CONVERT(varchar(16),CONVERT(binary(8),row_version),2)+CHAR(34) AS [Value] FROM dbo.cleaning_reworks WHERE rework_id={id}").SingleAsync(ct);
    internal sealed class Fixture
    {
        public long Id { get; set; }
        public long Actor { get; set; }
        public long Branch { get; set; }
        public long TaskId { get; set; }
        public long InspectionId { get; set; }
        public long ProgressId { get; set; }
    }
    internal static async Task<Fixture> CreateFixture(HotelOpsDbContext db, CancellationToken ct, bool supervisor = true)
    {
        Assert.True(await Publisher(db).ReworkAvailableAsync(ct));
        var cleaning = await CleaningResumeSqlTests.CreateFixtureAsync(db,ct,supervisor ? "BR001" : "BR002");
        if (supervisor) await CleaningClosureWorkflowTests.EnsureSupervisor(db,cleaning.Branch,ct);
        // Explicit synthetic test-only source fixture. No production Inspection result writer is added.
        var rows = await db.Database.SqlQuery<Fixture>($"""
            SET XACT_ABORT ON; BEGIN TRANSACTION;
            DECLARE @task bigint={cleaning.TaskId},@actor bigint={cleaning.Actor},@branch bigint={cleaning.Branch},@now datetime2(7)=SYSUTCDATETIME(),
            @version bigint,@inspection bigint,@rework bigint,@room bigint,@checklist bigint,@inspector bigint;
            SELECT @room=t.room_id,@checklist=rt.default_checklist_id FROM dbo.cleaning_tasks t JOIN dbo.rooms r ON r.room_id=t.room_id JOIN dbo.room_types rt ON rt.room_type_id=r.room_type_id WHERE t.cleaning_task_id=@task;
            SELECT TOP(1) @inspector=u.user_id FROM dbo.users u WHERE u.user_id<>@actor ORDER BY u.user_id;
            UPDATE dbo.fs014_cleaning_blocker_resolutions SET resolved_by=@actor,resolved_at=@now,resolution_note='Synthetic prior blocker explicitly resolved' WHERE progress_id={cleaning.ProgressId};
            UPDATE dbo.cleaning_tasks SET workflow_status='REWORK_REQUIRED' WHERE cleaning_task_id=@task;
            INSERT dbo.inspection_checklist_versions(checklist_id,version_no,status) VALUES(@checklist,1,'DRAFT'); SET @version=SCOPE_IDENTITY();
            INSERT dbo.inspection_checklist_items(version_id,criterion,is_required,sort_order,is_active) VALUES(@version,'Synthetic mirror criterion',1,0,1),(@version,'Synthetic floor criterion',1,1,1);
            UPDATE dbo.inspection_checklist_versions SET status='PUBLISHED',effective_at=DATEADD(hour,-1,@now),published_by=@inspector WHERE version_id=@version;
            INSERT dbo.room_inspections(inspection_no,room_id,cleaning_task_id,inspector_user_id,version_id,status,workflow_status,created_at)
            VALUES(CONCAT('F16-',NEWID()),@room,@task,@inspector,@version,'IN_PROGRESS','INSPECTING',@now); SET @inspection=SCOPE_IDENTITY();
            INSERT dbo.inspection_results(inspection_id,checklist_item_id,outcome,note,inspected_at) SELECT @inspection,item_id,'FAIL','SYNTHETIC_TEST_ONLY',@now FROM dbo.inspection_checklist_items WHERE version_id=@version;
            INSERT dbo.inspection_defects(inspection_id,checklist_item_id,description,status) SELECT @inspection,item_id,criterion,'OPEN' FROM dbo.inspection_checklist_items WHERE version_id=@version;
            UPDATE dbo.room_inspections SET status='COMPLETED',workflow_status='FAILED',result='FAIL',inspected_at=@now WHERE inspection_id=@inspection;
            INSERT dbo.cleaning_reworks(cleaning_task_id,inspection_id,assignee_user_id,reason,status,workflow_status,requested_at,due_at)
            VALUES(@task,@inspection,@actor,'SYNTHETIC_TEST_ONLY','PENDING','PENDING_ACCEPTANCE',@now,DATEADD(hour,1,@now)); SET @rework=SCOPE_IDENTITY();
            INSERT dbo.cleaning_rework_defects(rework_id,defect_id) SELECT @rework,defect_id FROM dbo.inspection_defects WHERE inspection_id=@inspection;
            COMMIT; SELECT @rework AS Id,@actor AS Actor,@branch AS Branch,@task AS TaskId,@inspection AS InspectionId,{cleaning.ProgressId} AS ProgressId;
            """).ToArrayAsync(ct);
        var f = Assert.Single(rows);
        var digest = await Service(db,f.Actor).ReworkSourceDigestAsync(f.Id,ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.fs016_rework_source_proofs(rework_id,inspection_id,inspection_revision,source_digest,origin) SELECT {f.Id},inspection_id,row_version,{digest},'SYNTHETIC_TEST_ONLY' FROM dbo.room_inspections WHERE inspection_id={f.InspectionId}",ct);
        return f;
    }

    [ReworkTrialTheory]
    [InlineData("cycle")][InlineData("stale")][InlineData("missing-note")][InlineData("foreign-defect")]
    [InlineData("blocker")][InlineData("no-proof")][InlineData("source-change")][InlineData("wrong-owner")][InlineData("wrong-branch")]
    [InlineData("no-workday")][InlineData("busy")][InlineData("no-supervisor")][InlineData("default-off")][InlineData("production")]
    [InlineData("concurrent-start")][InlineData("same-key")][InlineData("audit-fault")][InlineData("intent-fault")]
    [InlineData("readiness-fault")][InlineData("publication-fault")][InlineData("commit-ack")][InlineData("evidence")][InlineData("delivery")]
    [InlineData("duplicate")][InlineData("revoked-source-read")]
    [InlineData("invalid-evidence")][InlineData("no-interval")]
    public async Task Rework_SQL_lifecycle_source_security_atomicity_and_replay(string scenario)
    {
        using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(120)); var ct=budget.Token;
        await using var db=Context(); var f=await CreateFixture(db,ct,scenario!="no-supervisor");
        var ticket=CleaningClosureWorkflowTests.Ticket(f.Actor);
        async Task<RoomTypeBatchResult> Command(string action,string? tag=null,string? key=null,ReworkActionRequest? input=null)
            => await Service(db,f.Actor).ExecuteReworkAsync(ticket,1,f.Branch,f.Id,action,tag??await Tag(db,f.Id,ct),key??Guid.NewGuid().ToString(),input??new("Synthetic operation"),null,ct);
        var read=await Command("read");Assert.Equal(RoomTypeBatchStatus.Success,read.Status);Assert.Equal(2,Assert.IsType<ReworkExecutionDto>(read.Value).Defects.Count);
        if(scenario=="duplicate"){
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_reworks(cleaning_task_id,inspection_id,assignee_user_id,reason,status,workflow_status,requested_at,due_at) SELECT cleaning_task_id,inspection_id,assignee_user_id,reason,status,workflow_status,requested_at,due_at FROM dbo.cleaning_reworks WHERE rework_id={f.Id}",ct));return;
        }
        if(scenario is "no-proof" or "source-change" or "wrong-owner" or "wrong-branch" or "default-off" or "production" or "revoked-source-read"){
            if(scenario=="no-proof")await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.fs016_rework_source_proofs WHERE rework_id={f.Id}",ct);
            if(scenario=="source-change")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET summary_note='Changed source' WHERE inspection_id={f.InspectionId}",ct);
            if(scenario=="wrong-owner")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_reworks SET assignee_user_id=NULL WHERE rework_id={f.Id}",ct);
            if(scenario=="revoked-source-read")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={f.Actor}",ct);
            var before=await Snapshot(db,f,ct);
            var result=await Service(db,f.Actor,scenario!="default-off",scenario=="production").ExecuteReworkAsync(ticket,1,scenario=="wrong-branch"?f.Branch+999999:f.Branch,f.Id,"accept",await Tag(db,f.Id,ct),Guid.NewGuid().ToString(),new("Accept"),null,ct);
            Assert.Equal(scenario is "wrong-owner" or "wrong-branch" or "revoked-source-read"?RoomTypeBatchStatus.Forbidden:RoomTypeBatchStatus.Unavailable,result.Status);
            Assert.Equal(before,await Snapshot(db,f,ct));return;
        }
        var acceptTag=await Tag(db,f.Id,ct);var acceptKey=Guid.NewGuid().ToString();
        Assert.Equal(RoomTypeBatchStatus.Success,(await Command("accept",acceptTag,acceptKey)).Status);
        Assert.Equal(RoomTypeBatchStatus.Success,(await Command("accept",acceptTag,acceptKey)).Status);
        var startTag=await Tag(db,f.Id,ct);
        if(scenario=="no-workday")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.workdays SET status='ENDED',ended_at=SYSUTCDATETIME() WHERE user_id={f.Actor}",ct);
        if(scenario=="busy")await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.work_intervals(workday_id,user_id,branch_id,work_item_kind,work_item_id,opened_by,opened_at,effective_started_at) SELECT workday_id,user_id,branch_id,'TASK',1,user_id,SYSUTCDATETIME(),SYSUTCDATETIME() FROM dbo.workdays WHERE user_id={f.Actor}",ct);
        if(scenario is "no-workday" or "busy"){
            var before=await Snapshot(db,f,ct);Assert.Equal(RoomTypeBatchStatus.Inactive,(await Command("start")).Status);Assert.Equal(before,await Snapshot(db,f,ct));return;
        }
        if(scenario=="concurrent-start"){
            async Task<RoomTypeBatchResult> Concurrent(){await using var separate=Context();return await Service(separate,f.Actor).ExecuteReworkAsync(ticket,1,f.Branch,f.Id,"start",startTag,Guid.NewGuid().ToString(),new("Start"),null,ct);}
            var results=await Task.WhenAll(Concurrent(),Concurrent());Assert.Single(results,x=>x.Status==RoomTypeBatchStatus.Success);Assert.Single(results,x=>x.Status==RoomTypeBatchStatus.Stale);
            Assert.Equal(1,await db.WorkIntervals.CountAsync(w=>w.UserId==f.Actor&&w.ClosedAt==null,ct));return;
        }
        Assert.Equal(RoomTypeBatchStatus.Success,(await Command("start")).Status);
        var defs=Assert.IsType<ReworkExecutionDto>((await Command("read")).Value).Defects;
        ReworkRemediation[] remediations=defs.Select(d=>new ReworkRemediation(d.Id,"Synthetic remediation")).ToArray();
        if(scenario=="missing-note")remediations[0]=remediations[0] with{Note=" "};
        if(scenario=="foreign-defect")remediations[0]=remediations[0] with{DefectId=long.MaxValue};
        if(scenario=="blocker")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.fs014_cleaning_blocker_resolutions SET incident_id=1 WHERE progress_id={f.ProgressId}",ct);
        var root=Path.Combine(Path.GetTempPath(),"HotelOps-FS016-"+Guid.NewGuid().ToString("N"));
        if(scenario=="evidence")remediations[0]=remediations[0] with{Photos=[new("proof.png","image/png",Convert.ToBase64String([137,80,78,71,13,10,26,10,0]))]};
        if(scenario=="invalid-evidence"){
            remediations[0]=remediations[0] with{Photos=[new("proof.png","image/png",Convert.ToBase64String([137,80,78,71,13,10,26,10,0]))]};
            remediations[1]=remediations[1] with{Photos=[new("invalid.png","image/png","YWJj")]};
        }
        if(scenario=="no-interval")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.work_intervals SET closed_by={f.Actor},closed_at=SYSUTCDATETIME(),effective_ended_at=SYSUTCDATETIME() WHERE user_id={f.Actor} AND closed_at IS NULL",ct);
        var beforeComplete=await Snapshot(db,f,ct);var tag=await Tag(db,f.Id,ct);var key=Guid.NewGuid().ToString();
        var fault=scenario.EndsWith("fault")?new MandatoryFault(scenario):null;
        async Task<RoomTypeBatchResult> Complete(string? operation=null,string note="Submitted"){
            await using var separate=Context(fault!=null?[fault]:scenario=="commit-ack"?[new LostCommitAcknowledgement()]:[]);
            return await Service(separate,f.Actor).ExecuteReworkAsync(ticket,1,f.Branch,f.Id,"complete",scenario=="stale"?"\"0000000000000000\"":tag,operation??key,new(note,remediations),root,ct);
        }
        if(scenario is "missing-note" or "foreign-defect" or "invalid-evidence")await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(()=>Complete());
        else if(fault!=null){try{Assert.Equal(RoomTypeBatchStatus.Unavailable,(await Complete()).Status);}catch(InvalidOperationException e){Assert.Contains("FS016_MANDATORY_FAULT",e.ToString());}Assert.True(fault.Fired);}
        else if(scenario=="same-key"){
            var results=await Task.WhenAll(Complete(),Complete());Assert.All(results,r=>Assert.Equal(RoomTypeBatchStatus.Success,r.Status));
        }
        else {
            var result=await Complete();Assert.Equal(scenario=="stale"?RoomTypeBatchStatus.Stale:scenario is "blocker" or "no-interval"?RoomTypeBatchStatus.Inactive:scenario=="no-supervisor"?RoomTypeBatchStatus.Unavailable:RoomTypeBatchStatus.Success,result.Status);
        }
        if(scenario is "stale" or "blocker" or "no-supervisor" or "missing-note" or "foreign-defect" or "invalid-evidence" or "no-interval" || fault!=null){Assert.Equal(beforeComplete,await Snapshot(db,f,ct));if(scenario=="invalid-evidence")Assert.Empty(Directory.GetFiles(root));return;}
        Assert.Equal(RoomTypeBatchStatus.Success,(await Complete()).Status);
        Assert.Equal(RoomTypeBatchStatus.Duplicate,(await Complete(note:"Changed body")).Status);
        Assert.Equal(0,await db.WorkIntervals.CountAsync(w=>w.UserId==f.Actor&&w.ClosedAt==null,ct));
        Assert.Equal(1,await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.notification_publications WHERE rework_id={f.Id}").SingleAsync(ct));
        Assert.Null(await db.CleaningReworks.Where(w=>w.ReworkId==f.Id).Select(w=>w.CompletedAt).SingleAsync(ct));
        Assert.Equal("FAIL",await db.RoomInspections.Where(i=>i.InspectionId==f.InspectionId).Select(i=>i.Result).SingleAsync(ct));
        Assert.Equal("REWORK_REQUIRED",await db.Database.SqlQuery<string>($"SELECT workflow_status AS [Value] FROM dbo.cleaning_tasks WHERE cleaning_task_id={f.TaskId}").SingleAsync(ct));
        if(scenario=="evidence"){
            var file=await db.Database.SqlQuery<long>($"SELECT attachment_id AS [Value] FROM dbo.fs016_rework_evidence WHERE rework_id={f.Id}").SingleAsync(ct);
            var download=await Service(db,f.Actor).ExecuteReworkAsync(ticket,1,f.Branch,f.Id,"read",null,null,null,root,ct,file);
            Assert.Equal(RoomTypeBatchStatus.Success,download.Status);Assert.Equal(9,Assert.IsType<HotelOps.Api.DTOs.HotelStructure.PrivateHistoryFileDto>(download.Value).Content.Length);
            Assert.Equal(RoomTypeBatchStatus.NotFound,(await Service(db,f.Actor).ExecuteReworkAsync(ticket,1,f.Branch,f.Id,"read",null,null,null,root,ct,long.MaxValue)).Status);
        }
        if(scenario=="delivery"){
            var pub=await db.Database.SqlQuery<Guid>($"SELECT publication_id AS [Value] FROM dbo.notification_publications WHERE rework_id={f.Id}").SingleAsync(ct);
            var delivery=new ProvisionalCleaningDelivery(db,Publisher(db),Service(db,f.Actor),TimeProvider.System);
            Assert.Equal("DELIVERED",await delivery.DeliverOneAsync(ct,selectedPublication:pub));Assert.Equal("EMPTY",await delivery.DeliverOneAsync(ct,selectedPublication:pub));
        }
        if(scenario=="cycle"){
            var supervisor=await db.Users.Where(u=>u.Username=="fs015.supervisor").Select(u=>u.UserId).SingleAsync(ct);
            var identity=new Identity(supervisor);
            var permission=new HotelOps.Api.Security.Authorization.PermissionService(identity,new HotelOps.Api.Security.Authorization.EfPermissionDataSource(db),new HotelOps.Api.Security.Authorization.UnresolvedPermissionResourceResolver(),TimeProvider.System);
            var monitor=new HotelOps.Api.Services.Housekeeping.CleaningMonitorService(db,identity,permission,new Sessions(supervisor),TimeProvider.System);
            var result=await monitor.DetailAsync(CleaningClosureWorkflowTests.Ticket(supervisor),1,f.Branch,"rework",f.Id,ct);
            Assert.Equal(HotelOps.Api.Services.Housekeeping.CleaningMonitorStatus.Success,result.Status);
            var item=Assert.IsType<CleaningMonitorDetail>(result.Value).Item;
            Assert.Equal(100,item.ProgressPercent);Assert.True(item.IsCurrentCycle);Assert.False(item.HasUnresolvedBlocker);Assert.Equal(f.Actor,item.AssigneeId);Assert.Equal("PENDING_REINSPECTION",item.WorkflowState);
        }
    }

    internal static Task<string> Snapshot(HotelOpsDbContext db,Fixture f,CancellationToken ct)=>db.Database.SqlQuery<string>($"""
        SELECT (SELECT (SELECT * FROM dbo.cleaning_reworks WHERE rework_id={f.Id} FOR JSON PATH,INCLUDE_NULL_VALUES) AS source,
        (SELECT * FROM dbo.cleaning_rework_defects WHERE rework_id={f.Id} ORDER BY defect_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS defects,
        (SELECT * FROM dbo.work_intervals WHERE user_id={f.Actor} ORDER BY interval_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS intervals,
        (SELECT COUNT(*) FROM dbo.notification_operation_reservations WHERE actor_user_id={f.Actor}) AS operations,
        (SELECT COUNT(*) FROM dbo.notification_publications WHERE actor_user_id={f.Actor}) AS publications,
        (SELECT COUNT(*) FROM dbo.notification_delivery_intents i JOIN dbo.notification_publications p ON p.publication_id=i.publication_id WHERE p.actor_user_id={f.Actor}) AS intents,
        (SELECT COUNT(*) FROM dbo.audit_logs WHERE actor_user_id={f.Actor}) AS audits,
        (SELECT COUNT(*) FROM dbo.room_readiness_events WHERE cleaning_task_id={f.TaskId}) AS readiness
        FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS [Value]
        """).SingleAsync(ct);
    private sealed class Identity(long actor):HotelOps.Api.Security.Authorization.ICurrentUserAccessor
    {public bool TryGetCurrentUser(out HotelOps.Api.Security.Authorization.CurrentUserInfo user){user=new(actor,1,false);return true;}}
    private sealed class Sessions(long actor):HotelOps.Api.Services.Authentication.ISqlSessionService
    {
        public Task<HotelOps.Api.Services.Authentication.SessionValidationResult> ValidateSessionAsync(HotelOps.Api.Services.Authentication.SessionReference ticket,CancellationToken cancellationToken=default)=>Task.FromResult(new HotelOps.Api.Services.Authentication.SessionValidationResult(true,new(1,actor,ticket.CreatedAtUtc,DateTime.UtcNow.AddHours(1),DateTime.UtcNow.AddHours(2),false)));
        public Task<HotelOps.Api.Services.Authentication.CreatedSqlSession> CreateSessionAsync(HotelOps.Api.Services.Authentication.CredentialLockoutResult credential,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<HotelOps.Api.Services.Authentication.SessionValidationResult> TouchSessionAsync(HotelOps.Api.Services.Authentication.SessionReference ticket,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<bool> RevokeSessionAsync(HotelOps.Api.Services.Authentication.SessionReference ticket,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<int> RevokeAllSessionsAsync(long userId,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
    }
    private sealed class TrialEnvironment:IWebHostEnvironment
    {public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="HotelOps.Api";public string ContentRootPath{get;set;}=AppContext.BaseDirectory;public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public string WebRootPath{get;set;}=AppContext.BaseDirectory;public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();}
    private sealed class LostCommitAcknowledgement:DbTransactionInterceptor
    {private bool fired;public override Task TransactionCommittedAsync(DbTransaction transaction,TransactionEndEventData eventData,CancellationToken cancellationToken=default){if(!fired){fired=true;throw new OperationCanceledException("FS016_COMMIT_ACK_FAULT");}return Task.CompletedTask;}}
    private sealed class MandatoryFault(string kind):DbCommandInterceptor
    {
        public bool Fired{get;private set;}
        private void Check(DbCommand command){var needle=kind switch{"audit-fault"=>"INSERT INTO [audit_logs]","intent-fault"=>"INSERT dbo.notification_delivery_intents","publication-fault"=>"INSERT dbo.notification_publications",_=>"INSERT dbo.room_readiness_events"};if(command.CommandText.Contains(needle,StringComparison.Ordinal)){Fired=true;throw new InvalidOperationException("FS016_MANDATORY_FAULT");}}
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<int> result,CancellationToken cancellationToken=default){Check(command);return ValueTask.FromResult(result);}
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default){Check(command);return ValueTask.FromResult(result);}
    }
}
