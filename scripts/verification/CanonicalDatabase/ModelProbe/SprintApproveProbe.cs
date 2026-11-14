using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

internal static class SprintApproveProbe
{
    internal static async Task<int> RunAsync(string report)
    {
        if (File.Exists(report)) throw new IOException("Evidence already exists");
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var ct = budget.Token;
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        var fixture = await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/SprintApproveFixture.sql", ct);
        var method = typeof(RoomTypeCreationService).GetMethod("ApproveDomainInCallerTransactionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var checks = new List<object>();
        string? failure = null;
        foreach (var scenario in new[] { "execution-catalog", "approve", "duplicate", "mandatory-fail", "stale", "wrong-branch", "permission-revoked", "session-revoked", "cleaner-conflict", "invalid-state", "audit-failure",
            "recipient-eligible", "recipient-no-read", "recipient-inactive", "recipient-expired", "recipient-wrong-branch", "recipient-access-revoked", "recipient-missing-attribution",
            "recipient-revoked-after-stage", "recipient-retry", "recipient-stale-source", "recipient-delivery-conflict", "recipient-delivery-fault", "recipient-committed-delivery" })
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                // Fixture verifies exact instance, database GUID and owned run marker before any INSERT.
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.Transaction = tx.GetDbTransaction(); command.CommandText = fixture;
                long actor, branch, inspection, task, room;
                await using (var reader = await command.ExecuteReaderAsync(ct))
                {
                    if (!await reader.ReadAsync(ct)) throw new Exception("Missing fixture identity");
                    actor=reader.GetInt64(0); branch=reader.GetInt64(1); inspection=reader.GetInt64(2); task=reader.GetInt64(3); room=reader.GetInt64(4);
                }
                var passwords = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
                { CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3, IterationCount=PasswordCredentialService.BaselineIterationCount }));
                var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))+"Aa1!";
                var user = await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==actor,ct);
                var hash = passwords.HashPassword(user,password);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={hash} WHERE user_id={actor}",ct);
                var credentials = new CredentialLockoutService(db,passwords,TimeProvider.System);
                var sessions = new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System);
                var verified = await credentials.VerifyAsync(actor,password,(u,_)=>Task.FromResult(u.EmploymentStatus=="ACTIVE"&&u.AccountStatus=="ACTIVE"),ct);
                var login = await sessions.CreateSessionAsync(verified,ct);
                db.ChangeTracker.Clear();
                var current = new Identity(new(actor,login.Session.SessionId,false));
                var permission = new PermissionService(current,new EfPermissionDataSource(db),new UnresolvedPermissionResourceResolver(),TimeProvider.System);
                var service = new RoomTypeCreationService(db,current,permission,sessions,TimeProvider.System);
                async Task<string> Tag()=> '"'+Convert.ToHexString(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={inspection}").SingleAsync(ct))+'"';
                async Task<object> Apply(string tag,long scope)
                {
                    var pending=(Task)method.Invoke(service,[login.Ticket,login.Session.SessionId,scope,inspection,tag,ct])!;
                    await pending;
                    return pending.GetType().GetProperty("Result")!.GetValue(pending)!;
                }
                var tag=await Tag();
                var audits=await db.AuditLogs.CountAsync(ct);
                long? originalCleaner = null;
                if (scenario.StartsWith("recipient-"))
                {
                    // The recovered candidate is installed only in this rollback transaction.
                    await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(
                        "scripts/verification/CanonicalDatabase/ModelProbe/SprintCleanerAttribution.sql", ct), ct);
                    originalCleaner = await SeedCleanerAsync(db, actor, branch, task, scenario, ct);
                }
                if(scenario=="mandatory-fail")
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status='INSPECTING' WHERE inspection_id={inspection}; UPDATE dbo.inspection_results SET outcome='FAIL' WHERE inspection_id={inspection}; UPDATE dbo.room_inspections SET workflow_status='PENDING_CONCLUSION' WHERE inspection_id={inspection}",ct);
                    tag=await Tag();
                }
                if(scenario=="permission-revoked") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={actor}",ct);
                if(scenario=="session-revoked") await sessions.RevokeSessionAsync(login.Ticket,ct);
                if(scenario=="cleaner-conflict") await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status) VALUES({task},{actor},{actor},SYSUTCDATETIME(),'ACTIVE')",ct);
                if(scenario=="invalid-state")
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status='INSPECTING' WHERE inspection_id={inspection}",ct);
                    tag=await Tag();
                }
                audits=await db.AuditLogs.CountAsync(ct);
                if(scenario=="execution-catalog")
                {
                    await db.Database.ExecuteSqlRawAsync("""
                        IF NOT EXISTS(SELECT 1 FROM dbo.reference_values WHERE group_code='SEVERITY' AND value_code='MINOR')
                            INSERT dbo.reference_values(group_code,value_code,display_name,is_active) VALUES('SEVERITY','MINOR','Synthetic minor',1);
                        IF NOT EXISTS(SELECT 1 FROM dbo.reference_values WHERE group_code='SEVERITY' AND value_code='MAJOR')
                            INSERT dbo.reference_values(group_code,value_code,display_name,is_active) VALUES('SEVERITY','MAJOR','Synthetic major',1);
                        UPDATE dbo.reference_values SET is_active=1 WHERE group_code='SEVERITY' AND value_code IN('MINOR','MAJOR');
                        """,ct);
                    async Task<HotelOps.Api.DTOs.Housekeeping.InspectionExecutionDto> ReadExecution()
                    {
                        var stateTask = (Task<RoomTypeCreationService.HousekeepingState>)typeof(RoomTypeCreationService)
                            .GetMethod("InspectionState",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(service,[inspection,ct])!;
                        var state = await stateTask;
                        var entity = await db.RoomInspections.AsNoTracking().SingleAsync(i=>i.InspectionId==inspection,ct);
                        return await (Task<HotelOps.Api.DTOs.Housekeeping.InspectionExecutionDto>)typeof(RoomTypeCreationService)
                            .GetMethod("ExecutionDto",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(service,[entity,state,branch,ct])!;
                    }
                    var expectedCatalog=await db.ReferenceValues.AsNoTracking().Where(r=>r.GroupCode=="SEVERITY"&&(r.ValueCode=="MINOR"||r.ValueCode=="MAJOR"))
                        .OrderBy(r=>r.SortOrder).ThenBy(r=>r.ReferenceId).Select(r=>new HotelOps.Api.DTOs.Housekeeping.InspectionSeverityDto(r.ReferenceId,r.ValueCode,r.DisplayName)).ToArrayAsync(ct);
                    if(!(await ReadExecution()).Severities.SequenceEqual(expectedCatalog)) throw new Exception("Execution catalog did not preserve database IDs/labels/order");
                    await db.Database.ExecuteSqlRawAsync("UPDATE dbo.reference_values SET is_active=0 WHERE group_code='SEVERITY' AND value_code='MINOR'",ct);
                    var filtered=(await ReadExecution()).Severities;
                    if(filtered.Count!=1||filtered[0].Code!="MAJOR") throw new Exception("Inactive severity leaked into execution catalog");
                    if(await db.AuditLogs.CountAsync(ct)!=audits) throw new Exception("Catalog read wrote audit");
                }
                else if(scenario=="audit-failure")
                {
                    // Test-only trigger exists inside the same disposable transaction and is rolled back.
                    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_142A_AuditFault ON dbo.audit_logs AFTER INSERT AS BEGIN THROW 51994,'Synthetic audit fault',1; END",ct);
                    try { await Apply(tag,branch); throw new Exception("Audit fault was ignored"); }
                    catch(Microsoft.Data.SqlClient.SqlException error) when(error.Number==51994) { }
                }
                else if(scenario is "approve" or "duplicate" || scenario.StartsWith("recipient-"))
                {
                    var result=await Apply(tag,branch);
                    var row=await db.RoomInspections.AsNoTracking().SingleAsync(i=>i.InspectionId==inspection,ct);
                    var cleaning=await db.CleaningTasks.AsNoTracking().SingleAsync(t=>t.CleaningTaskId==task,ct);
                    if(row.Result!="PASS"||row.Status!="COMPLETED"||cleaning.Status!="COMPLETED"||await db.AuditLogs.CountAsync(ct)!=audits+1)
                        throw new Exception("Atomic inspection/task/audit mismatch");
                    if((bool)result.GetType().GetProperty("AuthoritativeCommitAccepted",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(result)!)
                        throw new Exception("Internal component claimed authoritative acceptance");
                    var recipient=result.GetType().GetProperty("NotificationRecipient")!.GetValue(result)!;
                    var expectedRecipient = scenario is "recipient-no-read" or "recipient-inactive" or "recipient-expired" or "recipient-wrong-branch" or "recipient-access-revoked"
                        ? "RECIPIENT_READ_DENIED" : scenario.StartsWith("recipient-") && scenario != "recipient-missing-attribution" ? "ELIGIBLE" : "ATTRIBUTION_UNAVAILABLE";
                    if ((string)recipient.GetType().GetProperty("Status")!.GetValue(recipient)! != expectedRecipient)
                        throw new Exception("Unexpected explicit-recipient authorization decision");
                    var cleaner=(HotelOps.Api.Services.Housekeeping.CleanerAttribution?)recipient.GetType().GetProperty("Cleaner")!.GetValue(recipient);
                    if (expectedRecipient != "ATTRIBUTION_UNAVAILABLE" && (cleaner?.CleanerUserId != originalCleaner || cleaner?.CleanerUserId == actor))
                        throw new Exception("Original cleaner binding was replaced by actor/current identity");
                    if (await db.Notifications.AnyAsync(ct) || await db.NotificationRecipients.AnyAsync(ct))
                        throw new Exception("Source resolution unexpectedly published a notification");
                    if(scenario=="duplicate") await Deny(()=>Apply(tag,branch),"Stale");
                    if(await db.AuditLogs.CountAsync(ct)!=audits+1) throw new Exception("Duplicate audit");
                    if(await db.Rooms.Where(r=>r.RoomId==room).Select(r=>r.OperationalStatus).SingleAsync(ct)!="INSPECTION")
                        throw new Exception("Domain participant changed room projection");
                    if (originalCleaner is { } receiver && expectedRecipient != "ATTRIBUTION_UNAVAILABLE")
                        await CheckDeliveryAsync(db, scenario, actor, receiver, inspection, ct);
                }
                else
                {
                    var expected=scenario switch {"mandatory-fail"=>"InvalidReference","stale"=>"Stale","session-revoked"=>"SessionInvalid","invalid-state"=>"Inactive",_=>"Forbidden"};
                    await Deny(()=>Apply(scenario=="stale"?"\"0000000000000000\"":tag,scenario=="wrong-branch"?branch+100000:branch),expected);
                    if(await db.AuditLogs.CountAsync(ct)!=audits || await db.RoomInspections.Where(i=>i.InspectionId==inspection).Select(i=>i.Result).SingleAsync(ct)!=null)
                        throw new Exception("Rejected operation mutated domain/audit");
                }
                if(scenario!="recipient-committed-delivery" && !ReferenceEquals(db.Database.CurrentTransaction?.GetDbTransaction(),tx.GetDbTransaction())) throw new Exception("Caller transaction replaced");
            }
            catch(Exception error){failure=scenario+": "+error.GetType().Name+": "+error.Message;}
            finally { if(tx.GetDbTransaction().Connection!=null) await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
            if(await db.Users.AnyAsync(ct)||await db.Branches.AnyAsync(ct)||await db.RoomInspections.AnyAsync(ct)||await db.UserSessions.AnyAsync(ct)||await db.AuditLogs.AnyAsync(ct)
                ||await db.Notifications.AnyAsync(ct)||await db.NotificationRecipients.AnyAsync(ct))
                throw new Exception("Synthetic rollback cleanup failed");
            if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.objects WHERE name IN('TR_DEV3_142A_AuditFault','CK_DEV3_142A_NotificationFault')").SingleAsync(ct)!=0)
                throw new Exception("Synthetic fault trigger survived rollback");
            if (scenario.StartsWith("recipient-") && await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.objects WHERE name IN('fe04_cleaner_attributions','fe04_capture_cleaner_attribution','TR_fe04_cleaner_immutable')").SingleAsync(ct)!=0)
                throw new Exception("Attribution rehearsal schema survived rollback");
            checks.Add(new {scenario,success=failure==null,rollbackVerified=scenario!="recipient-committed-delivery",cleanupVerified=true});
            Console.WriteLine((failure==null?"PASS ":"FAIL ")+scenario);
            if(failure!=null)break;
        }
        await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new {success=failure==null,checks,failure,
            scope="Real SQL approval/notification components; rollback cases plus committed delivery fault/retry/concurrency with cleanup. No public approval API, room projection or hosted worker acceptance."},new JsonSerializerOptions{WriteIndented=true}),ct);
        return failure==null?0:1;
    }
    private static async Task CheckDeliveryAsync(HotelOpsDbContext db, string scenario, long actor, long receiver, long inspection, CancellationToken ct)
    {
        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityType == "INSPECTION" && a.EntityId == inspection.ToString(), ct);
        using var json = JsonDocument.Parse(audit.NewValues!);
        if (json.RootElement.GetProperty("notification").GetProperty("recipientUserId").GetInt64() != receiver)
            throw new Exception("Durable intent lost original recipient");
        var type = typeof(RoomTypeCreationService).Assembly.GetType("HotelOps.Api.Services.Notifications.InspectionPassedNotificationDelivery")!;
        var delivery = Activator.CreateInstance(type, db, TimeProvider.System)!;
        var method = type.GetMethod("DeliverInCallerTransactionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        async Task<string> Send(long hint) => await (Task<string>)method.Invoke(delivery, [audit.AuditId, hint, ct])!;
        if (scenario == "recipient-committed-delivery")
        {
            await SprintNotificationCommitProbe.CheckAsync(db, audit, receiver, inspection, ct);
            return;
        }
        if (await Send(actor) != "SOURCE_DENIED") throw new Exception("Producer identity authorized as recipient");
        if (scenario is "recipient-revoked-after-stage" or "recipient-retry")
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={receiver}", ct);
        if (scenario == "recipient-stale-source")
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status='FAILED',result='FAIL' WHERE inspection_id={inspection}", ct);
        if (scenario == "recipient-delivery-conflict")
        {
            var wrong = new Notification { NotificationType="INSPECTION_PASSED",Title="Synthetic conflict",Message="Synthetic",Priority="NORMAL",SourceType="INSPECTION",SourceId=inspection,CreatedBy=actor,CreatedAt=DateTime.UtcNow };
            db.Notifications.Add(wrong); await db.SaveChangesAsync(ct);
            db.NotificationRecipients.Add(new() {NotificationId=wrong.NotificationId,UserId=actor,DeliveryChannel="IN_APP",DeliveryStatus="DELIVERED",DeliveredAt=DateTime.UtcNow});
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            try { await Send(receiver); } catch (InvalidOperationException e) when(e.Message=="INSPECTION_NOTIFICATION_RECEIPT_CONFLICT") { return; }
            throw new Exception("Conflicting persisted delivery receipt accepted");
        }
        if (scenario == "recipient-delivery-fault")
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.notification_recipients ADD CONSTRAINT CK_DEV3_142A_NotificationFault CHECK(user_id<0)", ct);
            try { await Send(receiver); }
            catch (DbUpdateException e) when(e.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number==547 && sql.Message.Contains("CK_DEV3_142A_NotificationFault")) { return; }
            throw new Exception("Notification insertion fault ignored");
        }
        var expected = scenario is "recipient-eligible" ? "DELIVERED" : scenario=="recipient-stale-source" ? "SOURCE_DENIED" : "RECIPIENT_READ_DENIED";
        if (await Send(receiver) != expected) throw new Exception("Unexpected delivery decision: "+scenario);
        if (scenario == "recipient-retry")
        {
            if (await db.Notifications.AnyAsync(ct)) throw new Exception("Denied recipient received notice");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={receiver}", ct);
            if (await Send(receiver) != "DELIVERED") throw new Exception("Durable event could not retry after access restored");
            expected = "DELIVERED";
        }
        if (expected == "DELIVERED")
        {
            if (await Send(receiver) != "ALREADY_DELIVERED" || await db.Notifications.CountAsync(ct)!=1 || await db.NotificationRecipients.CountAsync(ct)!=1)
                throw new Exception("Notification retry duplicated inbox rows");
        }
        else if (await db.Notifications.AnyAsync(ct) || await db.NotificationRecipients.AnyAsync(ct))
            throw new Exception("Denied delivery wrote inbox");
    }
    private static async Task<long> SeedCleanerAsync(HotelOpsDbContext db, long actor, long branch, long task, string scenario, CancellationToken ct)
    {
        // Separate historical cleaner. Existing catalog grants are fixture-only, never production seeds.
        var cleaner = await db.Database.SqlQuery<long>($"""
            INSERT dbo.users(employee_code,username,email,full_name,created_at)
            OUTPUT INSERTED.user_id AS Value
            VALUES('D3-CLEANER','D3-CLEANER','d3-cleaner@example.invalid','Synthetic original cleaner',SYSUTCDATETIME())
            """).ToArrayAsync(ct);
        var id = cleaner.Single();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @completed datetime2(7)=DATEADD(minute,-1,SYSUTCDATETIME());
            UPDATE dbo.cleaning_tasks SET actual_start_at=DATEADD(minute,-10,@completed),actual_end_at=@completed
              WHERE cleaning_task_id={task};
            INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status)
              VALUES({task},{id},{actor},DATEADD(minute,-11,@completed),'ACTIVE');
            DECLARE @assignment bigint=SCOPE_IDENTITY();
            INSERT dbo.cleaning_progress_logs(cleaning_task_id,logged_by,logged_at,status,progress_percent)
              VALUES({task},{id},@completed,'COMPLETED',100);
            DECLARE @progress bigint=SCOPE_IDENTITY();
            IF {scenario}<>'recipient-missing-attribution'
              EXEC dbo.fe04_capture_cleaner_attribution @task={task},@assignment=@assignment,@cleaner={id},@progress=@progress;
            INSERT dbo.user_branch_access(user_id,branch_id,granted_at) VALUES({id},{branch},DATEADD(minute,-1,SYSUTCDATETIME()));
            IF {scenario}<>'recipient-no-read'
              INSERT dbo.user_roles(user_id,role_id,branch_id,assigned_at)
              SELECT {id},role_id,{branch},DATEADD(minute,-2,SYSUTCDATETIME()) FROM dbo.roles WHERE role_code='HK_SUPERVISOR' AND is_active=1;
            """, ct);
        if (scenario == "recipient-inactive")
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET account_status='INACTIVE' WHERE user_id={id}", ct);
        if (scenario == "recipient-expired")
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET expires_at=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE user_id={id}", ct);
        if (scenario == "recipient-access-revoked")
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={id}", ct);
        if (scenario == "recipient-wrong-branch")
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.branches(branch_code,branch_name) VALUES('D3-OTHER','Synthetic other branch');
                DECLARE @other bigint=SCOPE_IDENTITY();
                INSERT dbo.user_branch_access(user_id,branch_id,granted_at) VALUES({id},@other,DATEADD(minute,-1,SYSUTCDATETIME()));
                UPDATE dbo.user_roles SET branch_id=@other WHERE user_id={id};
                """, ct);
        return id;
    }
    private static async Task Deny(Func<Task<object>> run,string status)
    {
        try {await run();} catch(InvalidOperationException error) when(error.Message=="FE05_APPROVE_SOURCE_"+status){return;}
        throw new Exception("Expected source denial: "+status);
    }
    private sealed class Identity(CurrentUserInfo value):ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user){user=value;return true;} }
}
