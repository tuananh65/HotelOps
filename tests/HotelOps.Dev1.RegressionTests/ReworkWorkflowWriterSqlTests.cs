using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Notifications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

[Collection("Transfer access database")]
public sealed class ReworkWorkflowWriterSqlTests
{
    private sealed class Actor(long id, long sid) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(id, sid, false); return true; } }

    [HousekeepingWriterSqlFact]
    public Task CommittedReworkAndReinspectionStayScopedAtomicAndReplaySafe() => RunAsync(true);

    [HousekeepingWriterSqlFact]
    public Task FailedReinspectionRetainsDefectsAndRequiresAnotherRework() => RunAsync(false);

    [HousekeepingWriterSqlFact]
    public Task LanExistingPermissionsCanExecuteNewReworkWithoutChangingOldRecords() => RunAsync(true, "lan.dang");

    private async Task RunAsync(bool passes, string? existingUser = null)
    {
        var primary = Environment.GetEnvironmentVariable("TRANSFER_REQUEST_TEST_CONNECTION")!;
        var restricted = Environment.GetEnvironmentVariable("INSPECTION_WRITER_TEST_CONNECTION")!;
        Assert.Matches("^HotelOps_TransferTest_[a-f0-9]{32}$", new SqlConnectionStringBuilder(primary).InitialCatalog);
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(primary).Options);
        var mai = await db.Users.AsNoTracking().SingleAsync(u => u.Username == "mai.tran");
        var branch = mai.PrimaryBranchId!.Value;
        var now = DateTime.UtcNow; var raw = SessionCredential.Generate();
        var session = new UserSession { UserId = mai.UserId, SessionKeyHash = SessionCredential.Hash(raw), CreatedAt = now, LastUsedAt = now, ExpiresAt = now.AddMinutes(20) };
        db.UserSessions.Add(session); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var ticket = SessionReference.FromProtectedTicket(mai.UserId, raw, false, now);
        var actor = new Actor(mai.UserId, session.SessionId);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:HotelOpsDb"] = primary, ["ConnectionStrings:InspectionAssignmentDb"] = restricted }).Build();
        var writer = new InitialInspectionWriter(config, actor, TimeProvider.System);
        var workflowConnection=Environment.GetEnvironmentVariable("REWORK_WRITER_TEST_CONNECTION")!;
        config["ConnectionStrings:ReworkWorkflowDb"]=workflowConnection;
        var pipeline=new ReworkWorkflowWriter(config,actor,TimeProvider.System);
        var root=Path.Combine(Path.GetTempPath(),"HotelOps-ReworkEvidence-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
        var token = Guid.NewGuid().ToString("N")[..10];
        var task = await db.Database.SqlQuery<long>($"""
            SET NOCOUNT ON;
            DECLARE @list bigint,@version bigint,@type bigint,@room bigint,@task bigint;
            INSERT dbo.inspection_checklists(checklist_code,checklist_name) VALUES({token},N'Writer isolated checklist'); SET @list=SCOPE_IDENTITY();
            INSERT dbo.inspection_checklist_versions(checklist_id,version_no,status) VALUES(@list,1,'DRAFT'); SET @version=SCOPE_IDENTITY();
            INSERT dbo.inspection_checklist_items(version_id,criterion,is_required,sort_order) VALUES(@version,N'Isolated criterion',1,1);
            UPDATE dbo.inspection_checklist_versions SET status='PUBLISHED',published_by={mai.UserId},effective_at=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE version_id=@version;
            INSERT dbo.room_types(type_code,type_name,standard_capacity,branch_id,standard_cleaning_minutes,default_checklist_id)
            VALUES({token},N'Writer isolated type',1,{branch},30,@list); SET @type=SCOPE_IDENTITY();
            INSERT dbo.rooms(branch_id,floor_id,room_type_id,room_number,operational_status)
            SELECT TOP(1) {branch},floor_id,@type,{token},'INSPECTION' FROM dbo.floors WHERE branch_id={branch} AND is_active=1; SET @room=SCOPE_IDENTITY();
            INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,status,created_by,workflow_status,created_at)
            SELECT TOP(1) {token},@room,cleaning_type_id,'IN_PROGRESS',{mai.UserId},'PENDING_INSPECTION',SYSUTCDATETIME() FROM dbo.cleaning_tasks; SET @task=SCOPE_IDENTITY();
            INSERT dbo.room_cleaning_cycle_state(room_id,cleaning_task_id) VALUES(@room,@task);
            SET NOCOUNT OFF; SELECT @task AS Value;
            """).ToArrayAsync();
        var id = Assert.Single(task);
        T Success<T>(RoomTypeBatchResult r) { Assert.True(r.Status == RoomTypeBatchStatus.Success, $"{typeof(T).Name}: {r.Status} {System.Text.Json.JsonSerializer.Serialize(r.Value)}"); return Assert.IsType<T>(r.Value); }
        async Task<string> Tag(long inspection) => await db.Database.SqlQuery<string>($"SELECT CHAR(34)+CONVERT(varchar(16),CONVERT(binary(8),row_version),2)+CHAR(34) AS Value FROM dbo.room_inspections WHERE inspection_id={inspection}").SingleAsync();
        var read=await writer.ExecuteAsync(s=>s.ReadInitialInspectionOptionsAsync(ticket,session.SessionId,branch,id,default),default);
        var initial=Success<InitialInspectionOptions>(read);
        var assigned=Success<InitialInspectionAssignmentResult>(await writer.ExecuteAsync(s=>s.AssignInitialInspectionAsync(ticket,session.SessionId,branch,id,read.ETag,Guid.NewGuid().ToString(),new(mai.UserId,initial.ChecklistVersionId!.Value,initial.CycleETag,DateTime.UtcNow.AddHours(1),"Initial fixture"),default),default));
        var inspection=assigned.Id;
        var other=initial.Inspectors.First(u=>u.Id!=mai.UserId).Id;
        var ro=Success<InspectionReassignmentOptions>(await writer.ExecuteAsync(s=>s.ReadInspectionReassignmentAsync(ticket,session.SessionId,branch,inspection,default),default));
        var change=Guid.NewGuid().ToString();
        var changeBody=new InspectionReassignmentRequest(other,ro.CycleETag,"Switch inspector");
        Assert.Equal(RoomTypeBatchStatus.Stale,(await writer.ExecuteAsync(s=>s.ReassignInspectionAsync(ticket,session.SessionId,branch,inspection,"\"FFFFFFFFFFFFFFFF\"",change,changeBody,default),default)).Status);
        Success<InspectionReassignmentResult>(await writer.ExecuteAsync(s=>s.ReassignInspectionAsync(ticket,session.SessionId,branch,inspection,ro.SourceETag,change,changeBody,default),default));
        Success<InspectionReassignmentResult>(await writer.ExecuteAsync(s=>s.ReassignInspectionAsync(ticket,session.SessionId,branch,inspection,ro.SourceETag,change,changeBody,default),default));
        ro=Success<InspectionReassignmentOptions>(await writer.ExecuteAsync(s=>s.ReadInspectionReassignmentAsync(ticket,session.SessionId,branch,inspection,default),default));
        Success<InspectionReassignmentResult>(await writer.ExecuteAsync(s=>s.ReassignInspectionAsync(ticket,session.SessionId,branch,inspection,ro.SourceETag,Guid.NewGuid().ToString(),new(mai.UserId,ro.CycleETag,"Return inspector"),default),default));

        // Isolated failed-inspection fixture. All new rework transitions below run through production services.
        var cleaner=existingUser != null ? await db.Users.AsNoTracking().SingleAsync(u=>u.Username==existingUser) : new User { EmployeeCode=token+"c",Username=token+"c",Email=token+"@example.test",FullName="Isolated cleaner",PrimaryBranchId=branch,DepartmentId=mai.DepartmentId,AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=now };
        if(existingUser == null) { db.Users.Add(cleaner);await db.SaveChangesAsync();db.ChangeTracker.Clear(); }
        Assert.Equal(branch,cleaner.PrimaryBranchId);
        var originalReworks = await db.CleaningReworks.AsNoTracking().Where(r=>r.AssigneeUserId==cleaner.UserId)
            .Select(r=>new { r.ReworkId,r.Status,r.CompletedAt,r.Note,r.Reason }).ToArrayAsync();
        var originalIds = originalReworks.Select(r=>r.ReworkId).ToArray();
        var originalGrants = await db.UserRoles.AsNoTracking().Where(r=>r.UserId==cleaner.UserId)
            .OrderBy(r=>r.UserRoleId).Select(r=>new { r.UserRoleId,r.IsActive,r.RoleId,r.BranchId,r.ExpiresAt }).ToArrayAsync();
        if(existingUser == null)
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.user_branch_access(user_id,branch_id,granted_at) VALUES({cleaner.UserId},{branch},DATEADD(minute,-1,SYSUTCDATETIME()));
            INSERT dbo.user_roles(user_id,role_id,branch_id,assigned_at) SELECT {cleaner.UserId},role_id,{branch},DATEADD(minute,-1,SYSUTCDATETIME()) FROM dbo.roles WHERE role_code='HK_STAFF';
            INSERT dbo.employee_department_memberships(user_id,branch_id,department_id,granted_at,recorded_by,provenance) VALUES({cleaner.UserId},{branch},{mai.DepartmentId},DATEADD(minute,-1,SYSUTCDATETIME()),{mai.UserId},N'Isolated test');
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.room_inspections SET workflow_status='INSPECTING',status='IN_PROGRESS' WHERE inspection_id={inspection};
            INSERT dbo.inspection_results(inspection_id,checklist_item_id,outcome,inspected_at) SELECT {inspection},item_id,'FAIL',SYSUTCDATETIME() FROM dbo.inspection_checklist_items WHERE version_id={initial.ChecklistVersionId};
            INSERT dbo.inspection_defects(inspection_id,checklist_item_id,severity_id,description,status)
            SELECT TOP(1) {inspection},i.item_id,r.reference_id,N'Fixture dust','OPEN' FROM dbo.inspection_checklist_items i CROSS JOIN dbo.reference_values r WHERE i.version_id={initial.ChecklistVersionId} AND r.group_code='SEVERITY' AND r.value_code='MINOR';
            UPDATE dbo.room_inspections SET workflow_status='FAILED',status='COMPLETED',result='FAIL',inspected_at=SYSUTCDATETIME() WHERE inspection_id={inspection};
            UPDATE dbo.cleaning_tasks SET workflow_status='REWORK_REQUIRED' WHERE cleaning_task_id={id};
            """);
        var defect=await db.InspectionDefects.AsNoTracking().SingleAsync(d=>d.InspectionId==inspection);
        const string png="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aS1cAAAAASUVORK5CYII=";
        var name=Guid.NewGuid().ToString("N")+".png";await File.WriteAllBytesAsync(Path.Combine(root,name),Convert.FromBase64String(png));
        var photo=new Attachment {EntityType="INSPECTION",EntityId=inspection,FileName="fixture.png",OriginalFileName="fixture.png",MimeType="image/png",FileSizeBytes=Convert.FromBase64String(png).Length,StorageKey=name,UploadedBy=mai.UserId,UploadedAt=DateTime.UtcNow};
        db.Attachments.Add(photo);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.inspection_defect_attachments(defect_id,attachment_id) VALUES({defect.DefectId},{photo.AttachmentId})");
        var tag=await Tag(inspection);
        var create=new RequestReworkRequest(cleaner.UserId,DateTime.UtcNow.AddHours(2),[defect.DefectId],"Remove dust");
        Task<RoomTypeBatchResult> Create(long scope,string etag)=>pipeline.ExecuteAsync(s=>s.RequestCleaningReworkAsync(ticket,session.SessionId,scope,inspection,etag,create,default,root),default);
        Assert.Equal(RoomTypeBatchStatus.Forbidden,(await Create(branch+100000,tag)).Status);
        Assert.Equal(RoomTypeBatchStatus.Stale,(await Create(branch,"\"FFFFFFFFFFFFFFFF\"")).Status);
        var rework=Success<HousekeepingWriteDto>(await Create(branch,tag)).Id;
        Assert.Equal(RoomTypeBatchStatus.Duplicate,(await Create(branch,tag)).Status);
        var cleanerRaw=SessionCredential.Generate();var cs=new UserSession{UserId=cleaner.UserId,SessionKeyHash=SessionCredential.Hash(cleanerRaw),CreatedAt=now,LastUsedAt=now,ExpiresAt=now.AddMinutes(25)};
        db.UserSessions.Add(cs);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var cticket=SessionReference.FromProtectedTicket(cleaner.UserId,cleanerRaw,false,now);
        var cw=new ReworkWorkflowWriter(config,new Actor(cleaner.UserId,cs.SessionId),TimeProvider.System);
        Task<RoomTypeBatchResult> ReadWork()=>cw.ExecuteAsync(s=>s.ReadAssignedReworkAsync(cticket,cs.SessionId,branch,rework,root,default),default);
        var work=Success<ReworkExecutionDto>(await ReadWork());
        Assert.NotEqual(RoomTypeBatchStatus.Success,(await pipeline.ExecuteAsync(s=>s.ExecuteAssignedReworkAsync(ticket,session.SessionId,branch,rework,"accept",work.SourceETag,Guid.NewGuid().ToString(),new("Wrong owner"),root,default),default)).Status);
        var accept=Guid.NewGuid().ToString();
        if(existingUser == null) Assert.Equal(RoomTypeBatchStatus.InvalidReference,(await cw.ExecuteAsync(s=>s.ExecuteAssignedReworkAsync(cticket,cs.SessionId,branch,rework,"accept",work.SourceETag,accept,new("Received"),root,default),default)).Status);
        var template=new ShiftTemplate{BranchId=branch,ShiftCode=token,ShiftName="Isolated shift",StartTime=new(0,0),EndTime=new(23,59),IsActive=true};
        var shift=new WorkShift{BranchId=branch,DepartmentId=mai.DepartmentId!.Value,ShiftTemplate=template,ShiftDate=DateOnly.FromDateTime(now),ScheduledStartAt=DateTime.UtcNow.AddHours(-1),ScheduledEndAt=DateTime.UtcNow.AddHours(2),RequiredStaffCount=1,Status="PLANNED",CreatedBy=mai.UserId,CreatedAt=now};
        db.ShiftAssignments.Add(new ShiftAssignment{Shift=shift,UserId=cleaner.UserId,AssignedBy=mai.UserId,AssignedAt=now,AssignmentStatus="ASSIGNED"});await db.SaveChangesAsync();db.ChangeTracker.Clear();
        Success<ReworkCommandResult>(await cw.ExecuteAsync(s=>s.ExecuteAssignedReworkAsync(cticket,cs.SessionId,branch,rework,"accept",work.SourceETag,accept,new("Received"),root,default),default));
        Success<ReworkCommandResult>(await cw.ExecuteAsync(s=>s.ExecuteAssignedReworkAsync(cticket,cs.SessionId,branch,rework,"accept",work.SourceETag,accept,new("Received"),root,default),default));
        work=Success<ReworkExecutionDto>(await ReadWork());
        if(!await db.Workdays.AsNoTracking().AnyAsync(w=>w.UserId==cleaner.UserId&&w.BranchId==branch&&w.Status=="WORKING")) db.Workdays.Add(new Workday {UserId=cleaner.UserId,BranchId=branch,DepartmentId=mai.DepartmentId!.Value,WorkDate=DateOnly.FromDateTime(now),IsUnscheduled=true,UnscheduledReason="Isolated workflow",Status="WORKING",StartedAt=DateTime.UtcNow.AddMinutes(-1)});await db.SaveChangesAsync();db.ChangeTracker.Clear();
        Success<ReworkCommandResult>(await cw.ExecuteAsync(s=>s.ExecuteAssignedReworkAsync(cticket,cs.SessionId,branch,rework,"start",work.SourceETag,Guid.NewGuid().ToString(),new("Started"),root,default),default));
        work=Success<ReworkExecutionDto>(await ReadWork());
        var submit=new ReworkActionRequest("Fixed",[new(defect.DefectId,"Dust removed",[new("fixed.png","image/png",png)])]);var submitOp=Guid.NewGuid().ToString();
        Success<ReworkCommandResult>(await cw.ExecuteAsync(s=>s.SubmitAssignedReworkAsync(cticket,cs.SessionId,branch,rework,work.SourceETag,submitOp,submit,root,default),default));
        Success<ReworkCommandResult>(await cw.ExecuteAsync(s=>s.SubmitAssignedReworkAsync(cticket,cs.SessionId,branch,rework,work.SourceETag,submitOp,submit,root,default),default));
        var options=Success<ReinspectionAssignmentOptionsDto>(await pipeline.ExecuteAsync(s=>s.ReadReinspectionAssignmentAsync(ticket,session.SessionId,branch,rework,root,default),default));
        var assignmentOp=Guid.NewGuid().ToString();var assignment=new AssignInspectionRequest(other,DateTime.UtcNow.AddHours(2),"Check correction");
        var reins=Success<ReinspectionAssignmentResult>(await pipeline.ExecuteAsync(s=>s.AssignReinspectionAsync(ticket,session.SessionId,branch,rework,options.ReworkETag,assignmentOp,assignment,root,default),default));
        Success<ReinspectionAssignmentResult>(await pipeline.ExecuteAsync(s=>s.AssignReinspectionAsync(ticket,session.SessionId,branch,rework,options.ReworkETag,assignmentOp,assignment,root,default),default));
        var rr=Success<ReinspectionReassignmentOptions>(await pipeline.ExecuteAsync(s=>s.ReadReinspectionReassignmentAsync(ticket,session.SessionId,branch,reins.Id,root,default),default));
        var reassignOp=Guid.NewGuid().ToString();var reassignBody=new ReinspectionReassignmentRequest(mai.UserId,rr.CycleETag,rr.ReworkETag,rr.BindingETag,"Mai verifies correction");
        // The receipt includes its real audit identity while the audit remains append-only.
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_ReworkTest_AppendOnly ON dbo.audit_logs AFTER UPDATE,DELETE AS BEGIN IF EXISTS(SELECT 1 FROM deleted) THROW 51997,'Audit immutable test',1; END");
        Success<ReinspectionReassignmentResult>(await pipeline.ExecuteAsync(s=>s.ReassignReinspectionAsync(ticket,session.SessionId,branch,reins.Id,rr.SourceETag,reassignOp,reassignBody,root,default),default));
        Success<ReinspectionReassignmentResult>(await pipeline.ExecuteAsync(s=>s.ReassignReinspectionAsync(ticket,session.SessionId,branch,reins.Id,rr.SourceETag,reassignOp,reassignBody,root,default),default));
        var reassignmentAudit=await db.AuditLogs.AsNoTracking().SingleAsync(a=>a.EntityType=="REINSPECTION_ASSIGNMENT"&&a.EntityId==reins.Id.ToString());
        db.ChangeTracker.Clear();
        var delivery=new ReinspectionReassignedNotificationDelivery(db,TimeProvider.System);
        Assert.Equal("DELIVERED",await delivery.DeliverAsync(reassignmentAudit.AuditId,default));
        Assert.Equal("DELIVERED",await delivery.DeliverAsync(reassignmentAudit.AuditId,default));
        db.ChangeTracker.Clear();
        Assert.Equal(1,await db.Notifications.CountAsync(n=>n.SourceType=="REINSPECTION_ASSIGNMENT"&&n.SourceId==reassignmentAudit.AuditId));
        var execution=Success<ReinspectionExecutionDto>(await pipeline.ExecuteAsync(s=>s.ReadReinspectionExecutionAsync(ticket,session.SessionId,branch,reins.Id,root,default),default));
        var conclude=new ConcludeReinspectionRequest(execution.ReworkETag,execution.BindingETag,"Correction verified",[new(defect.DefectId,passes,passes?"Clean":"Still dusty",[new("check.png","image/png",png)])]);var concludeOp=Guid.NewGuid().ToString();
        Assert.Equal(RoomTypeBatchStatus.Stale,(await pipeline.ExecuteAsync(s=>s.ConcludeReinspectionAsync(ticket,session.SessionId,branch,reins.Id,"\"FFFFFFFFFFFFFFFF\"",concludeOp,conclude,root,default),default)).Status);
        if(passes)
        {
            var filesBefore=Directory.GetFiles(root).Order().ToArray();
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_ReworkTest_AuditFault ON dbo.audit_logs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE entity_type='REINSPECTION' AND action='UPDATE') THROW 51998,'Isolated audit failure',1; END");
            try { Assert.Equal(51998,(await Assert.ThrowsAsync<SqlException>(()=>pipeline.ExecuteAsync(s=>s.ConcludeReinspectionAsync(ticket,session.SessionId,branch,reins.Id,execution.SourceETag,concludeOp,conclude,root,default),default))).Number); }
            finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_ReworkTest_AuditFault"); }
            Assert.Equal(execution.SourceETag,await Tag(reins.Id));
            Assert.Equal(filesBefore,Directory.GetFiles(root).Order().ToArray());
            Assert.Equal(0,await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations WHERE operation_key={Guid.Parse(concludeOp)}").SingleAsync());
        }
        var ended=Success<ReinspectionConclusionResult>(await pipeline.ExecuteAsync(s=>s.ConcludeReinspectionAsync(ticket,session.SessionId,branch,reins.Id,execution.SourceETag,concludeOp,conclude,root,default),default));
        Assert.Equal(passes?"PASSED":"FAILED",ended.State);Assert.Equal(passes?"COMPLETED":"FAILED",ended.ReworkState);
        Success<ReinspectionConclusionResult>(await pipeline.ExecuteAsync(s=>s.ConcludeReinspectionAsync(ticket,session.SessionId,branch,reins.Id,execution.SourceETag,concludeOp,conclude,root,default),default));
        if(passes)
        {
            var audit=await db.AuditLogs.AsNoTracking().SingleAsync(a=>a.EntityType=="REINSPECTION"&&a.Action=="UPDATE"&&a.EntityId==reins.Id.ToString());
            db.ChangeTracker.Clear();var passedDelivery=new ReinspectionPassedNotificationDelivery(db,TimeProvider.System);
            Assert.Equal("DELIVERED",await passedDelivery.DeliverAsync(audit.AuditId,default));
            Assert.Equal("DELIVERED",await passedDelivery.DeliverAsync(audit.AuditId,default));db.ChangeTracker.Clear();
            Assert.Equal(1,await db.Notifications.CountAsync(n=>n.NotificationType=="REINSPECTION_PASSED"&&n.SourceId==rework));
        }
        else
        {
            var remaining=await db.InspectionDefects.AsNoTracking().Where(d=>d.InspectionId==reins.Id).ToArrayAsync();Assert.Single(remaining);
            Success<HousekeepingWriteDto>(await pipeline.ExecuteAsync(s=>s.RequestCleaningReworkAsync(ticket,session.SessionId,branch,reins.Id,ended.SourceETag,new(cleaner.UserId,DateTime.UtcNow.AddHours(2),[remaining[0].DefectId],"Another correction required"),default,root),default));
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={mai.UserId} AND branch_id={branch} AND role_id IN(SELECT role_id FROM dbo.roles WHERE role_code='HK_SUPERVISOR')");
        try { Assert.NotEqual(RoomTypeBatchStatus.Success,(await pipeline.ExecuteAsync(s=>s.ConcludeReinspectionAsync(ticket,session.SessionId,branch,reins.Id,execution.SourceETag,concludeOp,conclude,root,default),default)).Status); }
        finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=1 WHERE user_id={mai.UserId} AND branch_id={branch} AND role_id IN(SELECT role_id FROM dbo.roles WHERE role_code='HK_SUPERVISOR')"); }
        work=Success<ReworkExecutionDto>(await ReadWork());Assert.Equal(passes?"COMPLETED":"FAILED",work.State);Assert.NotNull(work.Conclusion);
        if(existingUser != null) {
            Assert.Equal(originalReworks,await db.CleaningReworks.AsNoTracking().Where(r=>originalIds.Contains(r.ReworkId))
                .Select(r=>new { r.ReworkId,r.Status,r.CompletedAt,r.Note,r.Reason }).ToArrayAsync());
            Assert.Equal(originalGrants,await db.UserRoles.AsNoTracking().Where(r=>r.UserId==cleaner.UserId)
                .OrderBy(r=>r.UserRoleId).Select(r=>new { r.UserRoleId,r.IsActive,r.RoleId,r.BranchId,r.ExpiresAt }).ToArrayAsync());
        }
        await using var limited=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(workflowConnection).Options);
        foreach(var table in new[]{"notification_operation_reservations","fs016_rework_source_proofs","fe04_rework_authority_issuers","fe05_reinspection_bindings_v1","user_roles"})
            foreach(var permission in new[]{"INSERT","UPDATE","DELETE"}) Assert.Equal(0,await limited.Database.SqlQuery<int>($"SELECT HAS_PERMS_BY_NAME({"dbo."+table},'OBJECT',{permission}) AS Value").SingleAsync());
        } finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_ReworkTest_AppendOnly"); Directory.Delete(root,true); }
    }
}
