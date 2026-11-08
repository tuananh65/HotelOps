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

public sealed class CancellationTrialTheoryAttribute : TheoryAttribute
{
    public CancellationTrialTheoryAttribute()
    { if (Environment.GetEnvironmentVariable("FS018_SQL_TRIAL") != "APPROVED_LOCAL_ONLY") Skip = "Opt-in FS018 isolated SQL component, synthetic fixtures, session validation double."; }
}

[Collection("HotelOps isolated SQL")]
public sealed class CleaningCancellationSqlTests
{
    internal const string Connection = "Data Source=.\\SQLEXPRESS;Initial Catalog=HotelOps_Canonical_FS018_Cancellation_V1_Fresh;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=15";
    internal static HotelOpsDbContext Context(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(Connection).AddInterceptors(interceptors).Options);
    internal static ProvisionalCleaningNotification Publisher(HotelOpsDbContext db, bool enabled=true, bool production=false)
        => new(db,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["CleaningCancellation:Candidate"]=enabled?"FS018_PROVISIONAL":null }).Build(),new TrialEnvironment { EnvironmentName=production?"Production":"Development" });
    internal static RoomTypeCreationService Service(HotelOpsDbContext db,long actor,bool enabled=true,bool production=false)
        => new(db,new TurnoverHistorySqlTests.Identity(actor),null!,new TurnoverHistorySqlTests.Sessions(actor),TimeProvider.System,Publisher(db,enabled,production));
    internal sealed class Fixture
    {
        public long TaskId {get;set;} public long Actor {get;set;} public long Assignee {get;set;}
        public long Branch {get;set;} public long Previous {get;set;} public long Room {get;set;}
    }
    internal static async Task<Fixture> CreateFixture(HotelOpsDbContext db,CancellationToken ct,bool assigned=true)
    {
        Assert.True(await Publisher(db).CancellationAvailableAsync(ct));
        var seed=await CleaningResumeSqlTests.CreateFixtureAsync(db,ct);
        var rows=await db.Database.SqlQuery<Fixture>($"""
            SET XACT_ABORT ON; BEGIN TRANSACTION;
            DECLARE @actor bigint,@task bigint,@room bigint,@now datetime2(7)=SYSUTCDATETIME(),@code varchar(40)=CONCAT('F18-',NEWID());
            INSERT dbo.users(employee_code,username,email,full_name,primary_branch_id,employment_status,account_status,created_at)
            VALUES(@code,@code,@code+'@example.invalid','FS018 synthetic supervisor',{seed.Branch},'ACTIVE','ACTIVE',@now); SET @actor=SCOPE_IDENTITY();
            INSERT dbo.user_branch_access(user_id,branch_id,access_level,granted_at) VALUES(@actor,{seed.Branch},'STANDARD',DATEADD(hour,-1,@now));
            INSERT dbo.user_roles(user_id,role_id,branch_id,assigned_at,is_active) SELECT @actor,role_id,{seed.Branch},DATEADD(hour,-1,@now),1 FROM dbo.roles WHERE role_code='HK_SUPERVISOR';
            INSERT dbo.rooms(branch_id,floor_id,room_type_id,room_number,operational_status,is_active)
            SELECT branch_id,floor_id,room_type_id,LEFT(@code,30),'DIRTY',1 FROM dbo.rooms WHERE room_id=(SELECT room_id FROM dbo.cleaning_tasks WHERE cleaning_task_id={seed.TaskId});SET @room=SCOPE_IDENTITY();
            INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,status,workflow_status,created_by,created_at)
            SELECT @code,@room,cleaning_type_id,'PENDING',CASE WHEN {assigned}=1 THEN 'ASSIGNED' ELSE 'PENDING_ASSIGNMENT' END,@actor,@now FROM dbo.cleaning_tasks WHERE cleaning_task_id={seed.TaskId}; SET @task=SCOPE_IDENTITY();
            IF {assigned}=1 INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status,reason) VALUES(@task,{seed.Actor},@actor,DATEADD(minute,-1,@now),'ACTIVE','Original assignment preserved');
            INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES(@room,@task);
            INSERT dbo.room_readiness_events(room_id,cleaning_task_id,readiness,has_effective_block,actor_user_id,occurred_at) VALUES(@room,@task,'PENDING_CLEANING',0,@actor,@now);
            COMMIT; SELECT @task AS TaskId,@actor AS Actor,{seed.Actor} AS Assignee,{seed.Branch} AS Branch,{seed.TaskId} AS Previous,@room AS Room;
            """).ToArrayAsync(ct);
        return Assert.Single(rows);
    }
    internal static Task<string> Tag(HotelOpsDbContext db,long task,CancellationToken ct)=>CleaningClosureWorkflowTests.Tag(db,task,false,ct);
    private static Task<string> Snapshot(HotelOpsDbContext db,Fixture f,CancellationToken ct)=>db.Database.SqlQuery<string>($"""
        SELECT (SELECT (SELECT *,CONVERT(varchar(16),row_version,2) AS revision FROM dbo.cleaning_tasks WHERE room_id={f.Room} ORDER BY cleaning_task_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS tasks,
        (SELECT * FROM dbo.cleaning_assignments WHERE cleaning_task_id={f.TaskId} ORDER BY assignment_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS assignments,
        (SELECT * FROM dbo.work_intervals WHERE user_id={f.Assignee} ORDER BY interval_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS intervals,
        (SELECT COUNT(*) FROM dbo.audit_logs WHERE actor_user_id={f.Actor}) AS audits,
        (SELECT COUNT(*) FROM dbo.room_readiness_events WHERE room_id={f.Room}) AS readiness,
        (SELECT COUNT(*) FROM dbo.notification_operation_reservations WHERE actor_user_id={f.Actor}) AS operations,
        (SELECT COUNT(*) FROM dbo.notification_publications WHERE actor_user_id={f.Actor}) AS publications,
        (SELECT COUNT(*) FROM dbo.notification_delivery_intents i JOIN dbo.notification_publications p ON p.publication_id=i.publication_id WHERE p.actor_user_id={f.Actor}) AS intents,
        (SELECT cleaning_task_id FROM dbo.room_cleaning_cycle_state WHERE room_id={f.Room}) AS pointer,
        (SELECT operational_status FROM dbo.rooms WHERE room_id={f.Room}) AS room
        FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS [Value]
        """).SingleAsync(ct);

    [CancellationTrialTheory]
    [InlineData("assigned")][InlineData("unassigned")][InlineData("stale")][InlineData("started")]
    [InlineData("wrong-branch")][InlineData("not-supervisor")][InlineData("grant-revoked")][InlineData("cycle")]
    [InlineData("previous-cycle")][InlineData("no-readiness")][InlineData("future-readiness")][InlineData("room-status")]
    [InlineData("default-off")][InlineData("production")][InlineData("interval")][InlineData("assignee-revoked")]
    [InlineData("audit-fault")][InlineData("readiness-fault")][InlineData("publication-fault")][InlineData("intent-fault")]
    [InlineData("same-key")][InlineData("concurrent")][InlineData("commit-ack")]
    [InlineData("concurrent-same-key")][InlineData("replay-revoked")][InlineData("room-block")]
    [InlineData("inspection")][InlineData("rework")][InlineData("blocked-readiness")][InlineData("future-assignment")]
    [InlineData("cancelled-history")][InlineData("cancelled-source-conflict")]
    public async Task Cancellation_security_source_atomicity_and_replay(string scenario)
    {
        using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(90));var ct=budget.Token;
        await using var db=Context();var f=await CreateFixture(db,ct,scenario!="unassigned");
        if(scenario=="started")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET status='IN_PROGRESS',workflow_status='IN_PROGRESS',actual_start_at=SYSUTCDATETIME() WHERE cleaning_task_id={f.TaskId}",ct);
        if(scenario=="cycle")await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.room_cleaning_cycle_state WHERE room_id={f.Room}",ct);
        // Negative imported-history fixture: a terminal row without accepted Inspection/readiness proof.
        if(scenario=="previous-cycle")await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,status,workflow_status,created_by,created_at) SELECT CONCAT('F18-',NEWID()),{f.Room},cleaning_type_id,'COMPLETED','COMPLETED',created_by,DATEADD(day,-1,created_at) FROM dbo.cleaning_tasks WHERE cleaning_task_id={f.Previous}",ct);
        if(scenario is "cancelled-history" or "cancelled-source-conflict")await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,status,workflow_status,created_by,created_at,actual_start_at) SELECT CONCAT('F18-',NEWID()),{f.Room},cleaning_type_id,'CANCELLED','CANCELLED',created_by,DATEADD(day,-1,created_at),CASE WHEN {scenario}='cancelled-source-conflict' THEN created_at ELSE NULL END FROM dbo.cleaning_tasks WHERE cleaning_task_id={f.Previous}",ct);
        if(scenario=="no-readiness")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_readiness_events SET readiness='CLEANING_IN_PROGRESS' WHERE cleaning_task_id={f.TaskId}",ct);
        if(scenario=="future-readiness")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_readiness_events SET occurred_at=DATEADD(day,1,SYSUTCDATETIME()) WHERE cleaning_task_id={f.TaskId}",ct);
        if(scenario=="blocked-readiness")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_readiness_events SET has_effective_block=1 WHERE cleaning_task_id={f.TaskId}",ct);
        if(scenario=="future-assignment")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_assignments SET assigned_at=DATEADD(day,1,SYSUTCDATETIME()) WHERE cleaning_task_id={f.TaskId}",ct);
        if(scenario=="room-block")await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_blocks(room_id,reason,blocked_at,blocked_by) VALUES({f.Room},'Synthetic block',SYSUTCDATETIME(),{f.Actor})",ct);
        // Synthetic negative source fixtures only; no authoritative Inspection writer is introduced.
        if(scenario is "inspection" or "rework")await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @checklist bigint=(SELECT t.default_checklist_id FROM dbo.room_types t JOIN dbo.rooms r ON r.room_type_id=t.room_type_id WHERE r.room_id={f.Room}),@version bigint,@inspection bigint;
            INSERT dbo.inspection_checklist_versions(checklist_id,version_no,status) VALUES(@checklist,1,'DRAFT');SET @version=SCOPE_IDENTITY();
            INSERT dbo.inspection_checklist_items(version_id,criterion,is_required,sort_order,is_active) VALUES(@version,'Synthetic source conflict',1,0,1);
            UPDATE dbo.inspection_checklist_versions SET status='PUBLISHED',effective_at=DATEADD(hour,-1,SYSUTCDATETIME()),published_by={f.Actor} WHERE version_id=@version;
            INSERT dbo.room_inspections(inspection_no,room_id,cleaning_task_id,inspector_user_id,version_id,status,workflow_status,created_at)
            VALUES(CONCAT('F18-',NEWID()),{f.Room},{f.TaskId},{f.Actor},@version,'PENDING','ASSIGNED',SYSUTCDATETIME());SET @inspection=SCOPE_IDENTITY();
            IF {scenario}='rework' INSERT dbo.cleaning_reworks(cleaning_task_id,inspection_id,assignee_user_id,reason,status,workflow_status,requested_at)
            VALUES({f.TaskId},@inspection,{f.Assignee},'Synthetic conflicting source','PENDING','PENDING_ACCEPTANCE',SYSUTCDATETIME());
            """,ct);
        if(scenario=="room-status")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.rooms SET operational_status='READY' WHERE room_id={f.Room}",ct);
        if(scenario=="grant-revoked")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={f.Actor}",ct);
        if(scenario=="assignee-revoked")await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET account_status='INACTIVE' WHERE user_id={f.Assignee}",ct);
        if(scenario=="interval")await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.work_intervals(workday_id,user_id,branch_id,work_item_kind,work_item_id,opened_by,opened_at,effective_started_at) SELECT workday_id,user_id,branch_id,'CLEANING',{f.TaskId},user_id,SYSUTCDATETIME(),SYSUTCDATETIME() FROM dbo.workdays WHERE user_id={f.Assignee}",ct);
        var tag=await Tag(db,f.TaskId,ct);var key=Guid.NewGuid().ToString();var input=new CancelCleaningRequest(true,"Synthetic cancellation reason");
        var actor=scenario=="not-supervisor"?f.Assignee:f.Actor;var branch=scenario=="wrong-branch"?f.Branch+99999:f.Branch;
        var before=await Snapshot(db,f,ct);
        var fault=new MandatoryFault(scenario);
        async Task<RoomTypeBatchResult> Invoke(string? requestKey=null,CancelCleaningRequest? body=null)
        {
            IInterceptor[] faults=scenario.EndsWith("fault")?[fault]:scenario=="commit-ack"?[new LostCommitAcknowledgement()]:[];
            await using var command=Context(faults);
            return await Service(command,actor,scenario!="default-off",scenario=="production").CancelCleaningAsync(CleaningClosureWorkflowTests.Ticket(actor),1,branch,f.TaskId,
                scenario=="stale"?"\"0000000000000000\"":tag,requestKey??key,body??input,ct);
        }
        if(scenario.EndsWith("fault")){
            try{Assert.Equal(RoomTypeBatchStatus.Unavailable,(await Invoke()).Status);}catch(InvalidOperationException e){Assert.Contains("FS018_MANDATORY_FAULT",e.Message);}
            Assert.True(fault.Fired);Assert.Equal(before,await Snapshot(db,f,ct));return;
        }
        if(scenario is "concurrent" or "concurrent-same-key"){
            var results=await Task.WhenAll(Invoke(),Invoke(scenario=="concurrent"?Guid.NewGuid().ToString():key));
            if(scenario=="concurrent"){Assert.Single(results,r=>r.Status==RoomTypeBatchStatus.Success);Assert.Single(results,r=>r.Status==RoomTypeBatchStatus.Stale);}
            else Assert.All(results,r=>Assert.Equal(RoomTypeBatchStatus.Success,r.Status));
        }else{
            var r=await Invoke();
            var expected=scenario switch{"assigned" or "unassigned" or "same-key" or "commit-ack" or "replay-revoked" or "cancelled-history"=>RoomTypeBatchStatus.Success,"stale"=>RoomTypeBatchStatus.Stale,
                "wrong-branch" or "not-supervisor" or "grant-revoked"=>RoomTypeBatchStatus.Forbidden,"started" or "cycle" or "interval" or "future-assignment"=>RoomTypeBatchStatus.Inactive,_=>RoomTypeBatchStatus.Unavailable};
            Assert.Equal(expected,r.Status);
            if(expected!=RoomTypeBatchStatus.Success){Assert.Equal(before,await Snapshot(db,f,ct));return;}
            var receipt=Assert.IsType<CleaningCancellationResult>(r.Value);
            Assert.Equal(scenario!="unassigned",receipt.PublicationId!=null);
            var committed=await Snapshot(db,f,ct);
            Assert.Equal(RoomTypeBatchStatus.Success,(await Invoke()).Status);Assert.Equal(committed,await Snapshot(db,f,ct));
            if(scenario=="same-key")Assert.Equal(RoomTypeBatchStatus.Duplicate,(await Invoke(body:input with{Reason="Different"})).Status);
            if(scenario=="replay-revoked"){
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={f.Actor}",ct);
                Assert.Equal(RoomTypeBatchStatus.Forbidden,(await Invoke()).Status);Assert.Equal(committed,await Snapshot(db,f,ct));
            }
        }
        Assert.Equal("CANCELLED",await db.CleaningTasks.Where(t=>t.CleaningTaskId==f.TaskId).Select(t=>t.Status).SingleAsync(ct));
        Assert.Equal("DIRTY",await db.Rooms.Where(r=>r.RoomId==f.Room).Select(r=>r.OperationalStatus).SingleAsync(ct));
        Assert.Equal(1,await db.WorkIntervals.CountAsync(w=>w.WorkItemId==f.Previous&&w.UserId==f.Assignee&&w.ClosedAt!=null,ct));
        Assert.Equal(1,await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.notification_operation_reservations WHERE actor_user_id={f.Actor}").SingleAsync(ct));
    }
    private sealed class TrialEnvironment:IWebHostEnvironment
    {public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="HotelOps.Api";public string ContentRootPath{get;set;}=AppContext.BaseDirectory;public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public string WebRootPath{get;set;}=AppContext.BaseDirectory;public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();}
    private sealed class LostCommitAcknowledgement:DbTransactionInterceptor
    {private bool fired;public override Task TransactionCommittedAsync(DbTransaction transaction,TransactionEndEventData eventData,CancellationToken cancellationToken=default){if(!fired){fired=true;throw new OperationCanceledException("FS018_COMMIT_ACK_FAULT");}return Task.CompletedTask;}}
    private sealed class MandatoryFault(string scenario):DbCommandInterceptor
    {
        public bool Fired {get;private set;}
        private void Check(DbCommand c){var needle=scenario switch{"audit-fault"=>"INSERT INTO [audit_logs]","intent-fault"=>"INSERT dbo.notification_delivery_intents","publication-fault"=>"INSERT dbo.notification_publications",_=>"INSERT dbo.room_readiness_events"};if(c.CommandText.Contains(needle,StringComparison.Ordinal)){Fired=true;throw new InvalidOperationException("FS018_MANDATORY_FAULT");}}
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c,CommandEventData e,InterceptionResult<int> r,CancellationToken ct=default){Check(c);return ValueTask.FromResult(r);}
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c,CommandEventData e,InterceptionResult<DbDataReader> r,CancellationToken ct=default){Check(c);return ValueTask.FromResult(r);}
    }
}
