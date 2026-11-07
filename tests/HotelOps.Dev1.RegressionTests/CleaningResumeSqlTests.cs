using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class ResumeTrialTheoryAttribute : TheoryAttribute
{
    public ResumeTrialTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("FS014_SQL_TRIAL") != "APPROVED_LOCAL_ONLY"
            && Environment.GetEnvironmentVariable("FS015_SQL_TRIAL") != "APPROVED_LOCAL_ONLY")
            Skip = "Opt-in isolated SQL component tests; not unit or live HTTP tests.";
    }
}

// Real service/EF/SQL transactions; only session validation is a test double.
// Each case appends a fresh synthetic fixture. Never resets or deletes a prior fixture.
[Collection("HotelOps isolated SQL")]
public sealed class CleaningResumeSqlTests
{
    private const string Connection = "Data Source=.\\SQLEXPRESS;Initial Catalog=HotelOps_Canonical_FS014_Resume_V1_Fresh;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5";
    internal sealed class Fixture
    {
        public long Actor { get; set; }
        public long Branch { get; set; }
        public long TaskId { get; set; }
        public long ProgressId { get; set; }
    }
    private static HotelOpsDbContext Context(DbCommandInterceptor? fault = null)
    {
        var connection = Environment.GetEnvironmentVariable("FS015_SQL_TRIAL") == "APPROVED_LOCAL_ONLY"
            ? CleaningClosureWorkflowTests.Connection : Connection;
        var options = new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection);
        if (fault != null) options.AddInterceptors(fault);
        return new(options.Options);
    }

    [ResumeTrialTheory]
    [InlineData("success")]
    [InlineData("concurrent")]
    [InlineData("stale")]
    [InlineData("no-workday")]
    [InlineData("busy")]
    [InlineData("incident")]
    [InlineData("material")]
    [InlineData("unreconciled")]
    [InlineData("wrong-owner")]
    [InlineData("readiness-fault")]
    [InlineData("audit-fault")]
    [InlineData("resolved")]
    [InlineData("report-default-off")]
    [InlineData("complete-default-off")]
    public async Task Resume_component_preserves_transaction_and_source_guards(string scenario)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var ct = budget.Token;
        await using var db = Context();
        Assert.Equal(1, await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))=HOST_NAME() AND
            ((DB_NAME()='HotelOps_Canonical_FS014_Resume_V1_Fresh' AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS014.Owner' AND CONVERT(nvarchar(128),value)='DEV1_FS014'))
            OR (DB_NAME()='HotelOps_Canonical_FS015_Cleaning_V1_Fresh' AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS015.Owner' AND CONVERT(nvarchar(128),value)='DEV1_FS015')))
            THEN 1 ELSE 0 END AS [Value]
            """).SingleAsync(ct));
        var f = await CreateFixtureAsync(db, ct);
        if (scenario == "no-workday") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.workdays SET status='ENDED',ended_at=SYSUTCDATETIME() WHERE user_id={f.Actor}", ct);
        if (scenario == "busy") await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.work_intervals(workday_id,user_id,branch_id,work_item_kind,work_item_id,opened_by,opened_at,effective_started_at) SELECT workday_id,user_id,branch_id,'TASK',1,user_id,SYSUTCDATETIME(),SYSUTCDATETIME() FROM dbo.workdays WHERE user_id={f.Actor}", ct);
        if (scenario == "incident") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.fs014_cleaning_blocker_resolutions SET incident_id=1 WHERE progress_id={f.ProgressId}", ct);
        if (scenario == "material") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.fs014_cleaning_blocker_resolutions SET material_request_id=1 WHERE progress_id={f.ProgressId}", ct);
        if (scenario == "resolved") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.fs014_cleaning_blocker_resolutions SET resolved_by={f.Actor},resolved_at=SYSUTCDATETIME(),resolution_note='Guest has left' WHERE progress_id={f.ProgressId}", ct);
        if (scenario == "unreconciled") await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_progress_logs(cleaning_task_id,progress_percent,status,blocker_type_id,note,logged_by,logged_at) SELECT cleaning_task_id,progress_percent,status,blocker_type_id,note,logged_by,logged_at FROM dbo.cleaning_progress_logs WHERE progress_id={f.ProgressId}", ct);
        var actor = f.Actor;
        if (scenario == "wrong-owner") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_assignments SET ended_at=SYSUTCDATETIME(),assignment_status='REASSIGNED' WHERE cleaning_task_id={f.TaskId}", ct);
        var tag = await Tag(db, f.TaskId, ct);
        var before = await Snapshot(db, f, ct);
        var firstStart = await db.CleaningTasks.Where(x => x.CleaningTaskId == f.TaskId).Select(x => x.ActualStartAt).SingleAsync(ct);
        var closedHistory = await ClosedHistory(db, f.Actor, ct);
        var ticket = SessionReference.FromProtectedTicket(actor, SessionCredential.Generate(), false, DateTime.UtcNow);
        async Task<RoomTypeBatchResult> Invoke()
        {
            await using var commandDb = Context(scenario.EndsWith("fault") ? new MandatoryWriteFault(scenario) : null);
            var service = new RoomTypeCreationService(commandDb, new Identity(actor), null!, new Sessions(actor), TimeProvider.System);
            if (scenario == "report-default-off") return await service.ReportCleaningBlockerAsync(ticket, 1, f.Branch, f.TaskId, tag, Guid.NewGuid().ToString(), new(await commandDb.ReferenceValues.Where(x => x.GroupCode == "BLOCKER").Select(x => x.ReferenceId).FirstAsync(ct), "note"), ct);
            if (scenario == "complete-default-off") return await service.CompleteCleaningAsync(ticket, 1, f.Branch, f.TaskId, tag, Guid.NewGuid().ToString(), new("note"), ct);
            return await service.StartCleaningAsync(ticket, 1, f.Branch, f.TaskId, scenario == "stale" ? "\"0000000000000000\"" : tag, new(true, "Guest has left"), ct);
        }
        // Closure fixtures need IN_PROGRESS, but must never record a notification or completion.
        if (scenario.EndsWith("default-off"))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET status='IN_PROGRESS',workflow_status='IN_PROGRESS' WHERE cleaning_task_id={f.TaskId}", ct);
            tag = await Tag(db, f.TaskId, ct); before = await Snapshot(db, f, ct);
        }
        if (scenario.EndsWith("fault"))
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(Invoke);
            Assert.Contains("FS014_COMPONENT_WRITE_FAULT", error.ToString());
        }
        else if (scenario == "concurrent")
        {
            var results = await Task.WhenAll(Invoke(), Invoke());
            Assert.Single(results, x => x.Status == RoomTypeBatchStatus.Success);
            Assert.Single(results, x => x.Status == RoomTypeBatchStatus.Stale);
        }
        else
        {
            var result = await Invoke();
            Assert.Equal(scenario switch { "success" or "resolved" => RoomTypeBatchStatus.Success, "stale" => RoomTypeBatchStatus.Stale,
                "wrong-owner" => RoomTypeBatchStatus.Forbidden, "no-workday" or "busy" => RoomTypeBatchStatus.Inactive,
                _ => RoomTypeBatchStatus.Unavailable }, result.Status);
        }
        if (scenario is "success" or "concurrent" or "resolved")
        {
            Assert.Equal("IN_PROGRESS", await db.CleaningTasks.Where(x => x.CleaningTaskId == f.TaskId).Select(x => x.Status).SingleAsync(ct));
            Assert.Equal(1, await db.WorkIntervals.CountAsync(x => x.UserId == actor && x.ClosedAt == null, ct));
            Assert.Equal(1, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.fs014_cleaning_blocker_resolutions WHERE progress_id={f.ProgressId} AND resolved_by={actor} AND resolution_note='Guest has left' AND resolved_at IS NOT NULL").SingleAsync(ct));
            Assert.NotEqual(tag, await Tag(db, f.TaskId, ct));
            var actualStart = await db.CleaningTasks.Where(x => x.CleaningTaskId == f.TaskId).Select(x => x.ActualStartAt).SingleAsync(ct);
            Assert.Equal(firstStart, actualStart);
            Assert.Equal(closedHistory, await ClosedHistory(db, actor, ct));
            Assert.Equal(2, await db.WorkIntervals.CountAsync(x => x.UserId == actor, ct));
            Assert.True(await db.AuditLogs.AnyAsync(x => x.ActorUserId == actor && x.EntityType == "cleaning_tasks"
                && x.NewValues != null && x.NewValues.Contains("RESUME_CLEANING"), ct));
            var second = await Invoke(); Assert.Equal(RoomTypeBatchStatus.Stale, second.Status);
            Assert.Equal(1, await db.WorkIntervals.CountAsync(x => x.UserId == actor && x.ClosedAt == null, ct));
        }
        else Assert.Equal(before, await Snapshot(db, f, ct));
    }

    internal static async Task<Fixture> CreateFixtureAsync(HotelOpsDbContext db, CancellationToken ct, string branchCode = "BR001")
    {
        var prefix = "F14" + Guid.NewGuid().ToString("N")[..12];
        var fixtures = await db.Database.SqlQuery<Fixture>($"""
            SET XACT_ABORT ON; BEGIN TRANSACTION;
            DECLARE @code varchar(30)={prefix},@branch bigint=(SELECT branch_id FROM dbo.branches WHERE branch_code={branchCode}),
            @actor bigint,@task bigint,@progress bigint,@floor bigint,@type bigint,@checklist bigint,@template bigint,@shift bigint,@day bigint,
            @dept bigint,@now datetime2(7)=SYSUTCDATETIME();
            SELECT @dept=department_id FROM dbo.departments WHERE branch_id=@branch AND department_code='HK';
            INSERT dbo.users(employee_code,username,email,full_name,primary_branch_id,department_id,employment_status,account_status,created_at)
            VALUES(@code,@code,@code+'@example.invalid',@code,@branch,@dept,'ACTIVE','ACTIVE',@now);
            SET @actor=SCOPE_IDENTITY();
            INSERT dbo.user_branch_access(user_id,branch_id,access_level,granted_at) VALUES(@actor,@branch,'STANDARD',DATEADD(hour,-1,@now));
            INSERT dbo.user_roles(user_id,role_id,branch_id,assigned_at,is_active) SELECT @actor,role_id,@branch,DATEADD(hour,-1,@now),1 FROM dbo.roles WHERE role_code='HK_STAFF';
            INSERT dbo.shift_templates(branch_id,shift_code,shift_name,start_time,end_time,break_minutes,crosses_midnight,is_active) VALUES(@branch,@code,@code,'00:00','23:59',0,0,1);
            SET @template=SCOPE_IDENTITY();
            INSERT dbo.work_shifts(branch_id,department_id,shift_template_id,shift_date,scheduled_start_at,scheduled_end_at,required_staff_count,status,created_by,created_at)
            VALUES(@branch,@dept,@template,CONVERT(date,@now),DATEADD(hour,-1,@now),DATEADD(hour,8,@now),1,'PLANNED',@actor,@now);
            SET @shift=SCOPE_IDENTITY();
            INSERT dbo.shift_assignments(shift_id,user_id,assignment_status,assigned_by,assigned_at) VALUES(@shift,@actor,'ASSIGNED',@actor,DATEADD(hour,-1,@now));
            INSERT dbo.inspection_checklists(checklist_code,checklist_name,is_active,created_by,created_at) VALUES(@code,@code,1,@actor,@now);
            SET @checklist=SCOPE_IDENTITY();
            INSERT dbo.room_types(type_code,type_name,is_active,branch_id,standard_cleaning_minutes,default_checklist_id) VALUES(@code,@code,1,@branch,25,@checklist);
            SET @type=SCOPE_IDENTITY();
            INSERT dbo.floors(branch_id,floor_code,floor_name,floor_number,is_active) VALUES(@branch,@code,@code,1,1);
            SET @floor=SCOPE_IDENTITY();
            INSERT dbo.rooms(branch_id,floor_id,room_type_id,room_number,operational_status,is_active) VALUES(@branch,@floor,@type,@code,'DIRTY',1);
            DECLARE @room bigint=SCOPE_IDENTITY();
            INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,status,workflow_status,actual_start_at,created_by,created_at)
            SELECT @code,@room,reference_id,'IN_PROGRESS','ON_HOLD',DATEADD(minute,-20,@now),@actor,@now FROM dbo.reference_values WHERE group_code='CLEANING_TYPE' AND value_code='CHECKOUT';
            SET @task=SCOPE_IDENTITY();
            INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status) VALUES(@task,@actor,@actor,DATEADD(hour,-1,@now),'ACTIVE');
            INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES(@room,@task);
            INSERT dbo.cleaning_progress_logs(cleaning_task_id,progress_percent,status,blocker_type_id,note,logged_by,logged_at)
            SELECT @task,50,'BLOCKED',reference_id,'FS014 synthetic blocker',@actor,@now FROM dbo.reference_values WHERE group_code='BLOCKER' AND value_code='GUEST_IN_ROOM';
            SET @progress=SCOPE_IDENTITY();
            INSERT dbo.fs014_cleaning_blocker_resolutions(progress_id) VALUES(@progress);
            INSERT dbo.cleaning_item_definitions(room_type_id,label,is_required,sort_order) VALUES(@type,'FS014 item',1,0);
            INSERT dbo.cleaning_progress_versions(cleaning_task_id) VALUES(@task);
            INSERT dbo.cleaning_task_items(cleaning_task_id,source_item_id,label,is_required,sort_order) SELECT @task,item_id,label,is_required,sort_order FROM dbo.cleaning_item_definitions WHERE room_type_id=@type;
            INSERT dbo.workdays(user_id,branch_id,department_id,work_date,is_unscheduled,status,started_at)
            VALUES(@actor,@branch,@dept,CONVERT(date,@now),0,'WORKING',DATEADD(hour,-1,@now));
            SET @day=SCOPE_IDENTITY();
            INSERT dbo.work_intervals(workday_id,user_id,branch_id,work_item_kind,work_item_id,opened_by,opened_at,effective_started_at,closed_by,closed_at,effective_ended_at)
            VALUES(@day,@actor,@branch,'CLEANING',@task,@actor,DATEADD(minute,-20,@now),DATEADD(minute,-20,@now),@actor,DATEADD(minute,-10,@now),DATEADD(minute,-10,@now));
            COMMIT; SELECT @actor AS Actor,@branch AS Branch,@task AS TaskId,@progress AS ProgressId;
            """).ToArrayAsync(ct);
        return Assert.Single(fixtures);
    }

    private static async Task<string> Tag(HotelOpsDbContext db, long task, CancellationToken ct) =>
        await db.Database.SqlQuery<string>($"SELECT CHAR(34)+CONVERT(varchar(16),CONVERT(binary(8),row_version),2)+CHAR(34) AS [Value] FROM dbo.cleaning_tasks WHERE cleaning_task_id={task}").SingleAsync(ct);
    private static async Task<string> ClosedHistory(HotelOpsDbContext db, long actor, CancellationToken ct) =>
        await db.Database.SqlQuery<string>($"SELECT (SELECT * FROM dbo.work_intervals WHERE user_id={actor} AND closed_at IS NOT NULL ORDER BY interval_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS [Value]").SingleAsync(ct);
    private static async Task<string> Snapshot(HotelOpsDbContext db, Fixture f, CancellationToken ct) =>
        await db.Database.SqlQuery<string>($"""
        SELECT (SELECT (SELECT t.status,t.workflow_status,t.actual_start_at,t.actual_end_at,CONVERT(varchar(16),t.row_version,2) AS revision FROM dbo.cleaning_tasks t WHERE cleaning_task_id={f.TaskId} FOR JSON PATH,INCLUDE_NULL_VALUES) AS task,
        (SELECT * FROM dbo.fs014_cleaning_blocker_resolutions WHERE progress_id={f.ProgressId} FOR JSON PATH,INCLUDE_NULL_VALUES) AS blocker,
        (SELECT * FROM dbo.work_intervals WHERE user_id={f.Actor} ORDER BY interval_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS intervals,
        (SELECT COUNT(*) FROM dbo.audit_logs WHERE actor_user_id={f.Actor}) AS audits,
        (SELECT COUNT(*) FROM dbo.room_readiness_events WHERE cleaning_task_id={f.TaskId}) AS readiness,
        (SELECT COUNT(*) FROM dbo.notifications WHERE created_by={f.Actor}) AS notifications FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS [Value]
        """).SingleAsync(ct);
    private sealed class Identity(long actor) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(actor, 1, false); return true; } }
    private sealed class Sessions(long actor) : ISqlSessionService
    {
        public Task<SessionValidationResult> ValidateSessionAsync(SessionReference ticket, CancellationToken cancellationToken = default)
            => Task.FromResult(new SessionValidationResult(true, new(1, actor, ticket.CreatedAtUtc, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), false)));
        public Task<CreatedSqlSession> CreateSessionAsync(CredentialLockoutResult credential, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SessionValidationResult> TouchSessionAsync(SessionReference ticket, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RevokeSessionAsync(SessionReference ticket, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> RevokeAllSessionsAsync(long userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class MandatoryWriteFault(string scenario) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (scenario == "readiness-fault" && command.CommandText.Contains("INSERT dbo.room_readiness_events", StringComparison.Ordinal))
                throw new InvalidOperationException("FS014_COMPONENT_WRITE_FAULT");
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (scenario == "audit-fault" && command.CommandText.Contains("INSERT INTO [audit_logs]", StringComparison.Ordinal))
                throw new InvalidOperationException("FS014_COMPONENT_WRITE_FAULT");
            return ValueTask.FromResult(result);
        }
    }
}
