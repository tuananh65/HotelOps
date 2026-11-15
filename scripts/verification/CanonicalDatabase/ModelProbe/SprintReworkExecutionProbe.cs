using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.EntityFrameworkCore;

internal static class SprintReworkExecutionProbe
{
    internal static async Task VerifyAsync(HotelOpsDbContext db,SessionReference ticket,long sid,long branch,long id,CancellationToken ct)
    {
        var actor=ticket.UserId;
        if(await db.Database.SqlQueryRaw<int>("""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita'
              AND DB_NAME()='HotelOps_DEV3_142A_20261114_cc78c13f'
              AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)!=1)throw new Exception("Exact disposable execution identity required");
        var original=await db.Database.SqlQueryRaw<string>("SELECT definition AS Value FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.cleaning_reworks') AND name='CK_rework_workflow_v1' AND is_disabled=0 AND is_not_trusted=0").SingleAsync(ct);
        if(original.Contains("ACCEPTED",StringComparison.Ordinal)||await db.Workdays.AnyAsync(w=>w.UserId==actor,ct))throw new Exception("Fresh execution trial required");
        var department=await db.Users.Where(u=>u.UserId==actor).Select(u=>u.DepartmentId!.Value).SingleAsync(ct);
        var template=new ShiftTemplate{BranchId=branch,ShiftCode="D3-REWORK",ShiftName="Synthetic rework shift",StartTime=new(0,0),EndTime=new(23,59),IsActive=true};
        var shift=new WorkShift{BranchId=branch,DepartmentId=department,ShiftTemplate=template,ShiftDate=DateOnly.FromDateTime(DateTime.UtcNow),
            ScheduledStartAt=DateTime.UtcNow.AddHours(-1),ScheduledEndAt=DateTime.UtcNow.AddHours(1),RequiredStaffCount=1,Status="PLANNED",CreatedBy=actor,CreatedAt=DateTime.UtcNow};
        var assignment=new ShiftAssignment{Shift=shift,UserId=actor,AssignedBy=actor,AssignedAt=DateTime.UtcNow.AddMinutes(-1),AssignmentStatus="ASSIGNED"};
        async Task RestoreSchema()=>await db.Database.ExecuteSqlInterpolatedAsync($"""
            DROP USER DEV3_ReworkExecutor;
            EXEC sys.sp_dropextendedproperty @name=N'HotelOps.ReworkExecution.V1';
            DECLARE @original nvarchar(max)={original};
            DECLARE @ddl nvarchar(max)=N'ALTER TABLE dbo.cleaning_reworks DROP CONSTRAINT CK_rework_workflow_v1; ALTER TABLE dbo.cleaning_reworks WITH CHECK ADD CONSTRAINT CK_rework_workflow_v1 CHECK '+@original;
            EXEC sys.sp_executesql @ddl;
            """,CancellationToken.None);
        for(var pass=0;pass<2;pass++)
        {
            await using var setup=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            // Preserve every original alternative; add only the SRS Accepted/Pending mapping.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @original nvarchar(max)={original};
                DECLARE @ddl nvarchar(max)=N'ALTER TABLE dbo.cleaning_reworks DROP CONSTRAINT CK_rework_workflow_v1; ALTER TABLE dbo.cleaning_reworks WITH CHECK ADD CONSTRAINT CK_rework_workflow_v1 CHECK ('+@original+N' OR (workflow_status=''ACCEPTED'' AND status=''PENDING''));';
                EXEC sys.sp_executesql @ddl;
                """,ct);
            await db.Database.ExecuteSqlRawAsync("""
                EXEC sys.sp_addextendedproperty @name=N'HotelOps.ReworkExecution.V1',@value=N'ACCEPT_START_V1';
                CREATE USER DEV3_ReworkExecutor WITHOUT LOGIN;
                GRANT SELECT ON SCHEMA::dbo TO DEV3_ReworkExecutor;
                GRANT VIEW DEFINITION TO DEV3_ReworkExecutor;
                GRANT UPDATE ON dbo.cleaning_reworks TO DEV3_ReworkExecutor;
                GRANT INSERT ON dbo.audit_logs TO DEV3_ReworkExecutor;
                GRANT INSERT ON dbo.work_intervals TO DEV3_ReworkExecutor;
                """,ct);
            if(pass==0){await RestoreSchema();await setup.RollbackAsync(ct);}
            else{db.ShiftAssignments.Add(assignment);await db.SaveChangesAsync(ct);await setup.CommitAsync(ct);db.ChangeTracker.Clear();}
        }
        async Task<string> Tag()=> '"'+Convert.ToHexString(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.cleaning_reworks WHERE rework_id={id}").SingleAsync(ct))+'"';
        async Task<RoomTypeBatchResult> Call(string action,string tag,Guid operation,string note="Synthetic receive rework")
        {
            await using var context=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
            await context.Database.OpenConnectionAsync(ct);await context.Database.ExecuteSqlRawAsync("EXECUTE AS USER='DEV3_ReworkExecutor'",ct);
            try
            {
                var identity=new Identity(new(actor,sid,false));
                var service=new RoomTypeCreationService(context,identity,new PermissionService(identity,new EfPermissionDataSource(context),new UnresolvedPermissionResourceResolver(),TimeProvider.System),
                    new SqlSessionService(context,new SessionRevocationService(context),TimeProvider.System),TimeProvider.System);
                return await service.ExecuteAssignedReworkAsync(ticket,sid,branch,id,action,tag,operation.ToString("D"),new(note),null,ct);
            }
            finally{await context.Database.ExecuteSqlRawAsync("REVERT",ct);await context.Database.CloseConnectionAsync();}
        }
        try
        {
            var before=await Tag();var acceptKey=Guid.NewGuid();
            if((await Call("start",before,Guid.NewGuid())).Status!=RoomTypeBatchStatus.Inactive)throw new Exception("Start bypassed acceptance");
            var accepted=await Call("accept",before,acceptKey);
            if(accepted.Status!=RoomTypeBatchStatus.Success||accepted.Value is not ReworkCommandResult {State:"ACCEPTED",IntervalId:null,Contract:"FE05_COMMITTED_V1"})throw new Exception("Committed accept failed: "+accepted.Status);
            if((await Call("accept",before,acceptKey)).Status!=RoomTypeBatchStatus.Success||await db.WorkIntervals.AnyAsync(w=>w.UserId==actor,ct))throw new Exception("Acceptance replay opened an interval");
            if((await Call("accept",before,acceptKey,"Changed request")).Status!=RoomTypeBatchStatus.Duplicate)throw new Exception("Operation key reused with different body");
            var startTag=await Tag();
            if((await Call("start",startTag,Guid.NewGuid())).Status!=RoomTypeBatchStatus.Inactive||await Tag()!=startTag)throw new Exception("No-workday start did not roll back");
            var day=new Workday{UserId=actor,BranchId=branch,DepartmentId=department,WorkDate=DateOnly.FromDateTime(DateTime.UtcNow),IsUnscheduled=true,
                UnscheduledReason="Synthetic receive rework fixture",Status="WORKING",StartedAt=DateTime.UtcNow.AddMinutes(-5)};
            db.Workdays.Add(day);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            var startKey=Guid.NewGuid();var results=await Task.WhenAll(Call("start",startTag,startKey),Call("start",startTag,startKey));
            if(results.Any(r=>r.Status!=RoomTypeBatchStatus.Success)||await db.WorkIntervals.CountAsync(w=>w.UserId==actor&&w.WorkItemKind=="REWORK"&&w.WorkItemId==id&&w.ClosedAt==null,ct)!=1)
                throw new Exception("Concurrent identical start failed to produce one interval: "+string.Join(',',results.Select(r=>r.Status)));
            if((await Call("start",startTag,Guid.NewGuid())).Status!=RoomTypeBatchStatus.Stale)throw new Exception("Stale start was accepted");
            if(await db.AuditLogs.CountAsync(a=>a.ActorUserId==actor&&a.EntityType=="cleaning_reworks"&&a.EntityId==id.ToString(),ct)!=2)
                throw new Exception("Accept/start audits duplicated or missing");
        }
        finally
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.work_intervals WHERE user_id={actor} AND work_item_kind='REWORK' AND work_item_id={id}; DELETE dbo.workdays WHERE user_id={actor}; UPDATE dbo.cleaning_reworks SET status='PENDING',workflow_status='PENDING_ACCEPTANCE',note=NULL WHERE rework_id={id}",CancellationToken.None);
            await RestoreSchema();
        }
    }
    private sealed class Identity(CurrentUserInfo value):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo user){user=value;return true;}}
}
