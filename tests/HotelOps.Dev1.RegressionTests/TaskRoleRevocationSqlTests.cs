using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class TaskRoleTrialFactAttribute : FactAttribute
{
    public TaskRoleTrialFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("DEV3_TASK_ROLE_TRIAL") != "EXACT_DISPOSABLE_ROLLBACK")
            Skip = "Explicit exact disposable SQL identity and rollback trial required";
    }
}

[Collection("HotelOps isolated SQL")]
public sealed class TaskRoleRevocationSqlTests
{
    [TaskRoleTrialFact]
    public async Task CurrentTaskCapabilityLossAlternativeGrantHistoryAndUnknownWorkflowUseRealSql()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = budget.Token;
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        Assert.Equal(1, await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita'
              AND DB_NAME()='HotelOps_DEV3_142A_20261114_cc78c13f'
              AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
                AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run'
                AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct));
        Assert.False(await db.Users.AnyAsync(ct)); Assert.False(await db.Branches.AnyAsync(ct));
        Assert.True(await ProgressSchema.InstalledAsync(db, ct));
        var now = DateTime.UtcNow;
        var prefix = "D3ROLE" + Guid.NewGuid().ToString("N")[..10];
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var branch = new Branch { BranchCode=prefix,BranchName="Synthetic revoke dependency",IsActive=true };
            db.Branches.Add(branch); await db.SaveChangesAsync(ct);
            var department = new Department { BranchId=branch.BranchId,DepartmentCode=prefix,DepartmentName="Synthetic",IsActive=true };
            db.Departments.Add(department); await db.SaveChangesAsync(ct);
            var user = new User { EmployeeCode=prefix,Username=prefix,FullName="Synthetic dependency executor",Email=prefix+"@example.invalid",
                PasswordHash="NO_LOGIN_SYNTHETIC_HASH",EmploymentStatus="ACTIVE",AccountStatus="ACTIVE",CreatedAt=now,
                PrimaryBranchId=branch.BranchId,DepartmentId=department.DepartmentId };
            db.Users.Add(user); await db.SaveChangesAsync(ct);
            await new UserEligibilityLock(db).LockAsync([user.UserId],ct);
            db.UserBranchAccesses.Add(new(){UserId=user.UserId,BranchId=branch.BranchId,GrantedAt=now.AddMinutes(-1),AccessLevel="STANDARD"});
            var roles = await db.Roles.Where(r=>r.IsActive&&db.RolePermissions.Any(p=>p.RoleId==r.RoleId
                    &&p.Permission.PermissionCode=="TASK_EXECUTE"&&p.DataScope=="OWN"))
                .OrderBy(r=>r.RoleCode).Select(r=>r.RoleId).Take(2).ToArrayAsync(ct);
            Assert.Equal(2,roles.Length);
            var selected = new UserRole { UserId=user.UserId,RoleId=roles[0],BranchId=branch.BranchId,AssignedAt=now.AddMinutes(-1),IsActive=true };
            db.UserRoles.Add(selected);
            var task = new OperationalTask { TaskCode=prefix,Title="Synthetic assigned work",BranchId=branch.BranchId,DepartmentId=department.DepartmentId,
                Status="IN_PROGRESS",ProgressPercent=1,CreatedBy=user.UserId,CreatedAt=now.AddMinutes(-2),ActualStartAt=now.AddMinutes(-1) };
            db.OperationalTasks.Add(task);await db.SaveChangesAsync(ct);
            var assignment = new OperationalTaskAssignment { TaskId=task.TaskId,AssigneeId=user.UserId,AssignedBy=user.UserId,AssignedAt=now.AddMinutes(-1),AssignmentStatus="ACTIVE" };
            db.OperationalTaskAssignments.Add(assignment);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_progress_versions(task_id,workflow_status) VALUES({task.TaskId},'IN_PROGRESS')",ct);
            async Task<Dev1ResponsibilityReview> Read()=>await new RevokeRoleResponsibilities(db,TimeProvider.System).ReadAsync(user.UserId,selected.UserRoleId,false,ct);
            var label=$"TASK_EXECUTOR:{task.TaskId}";
            Assert.Contains(label,(await Read()).Blockers);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status='PAUSED' WHERE task_id={task.TaskId}",ct);
            Assert.Contains(label,(await Read()).Blockers);
            var alternate = new UserRole { UserId=user.UserId,RoleId=roles[1],BranchId=branch.BranchId,AssignedAt=now.AddMinutes(-1),IsActive=true };
            db.UserRoles.Add(alternate);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            var preserved=await Read();Assert.DoesNotContain(label,preserved.Blockers);
            Assert.Contains("USER_FIRST_WRITER_FINAL_REREAD_ACCEPTANCE_PENDING",preserved.Unresolved);
            await db.UserRoles.Where(r=>r.UserRoleId==alternate.UserRoleId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ExpiresAt,now),ct);
            Assert.Contains(label,(await Read()).Blockers);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.task_progress_versions WHERE task_id={task.TaskId}",ct);
            Assert.Contains(label+"_UNKNOWN",(await Read()).Unresolved);
            await db.OperationalTaskAssignments.Where(a=>a.AssignmentId==assignment.AssignmentId)
                .ExecuteUpdateAsync(s=>s.SetProperty(a=>a.AssignmentStatus,"CANCELLED").SetProperty(a=>a.EndedAt,now),ct);
            var historical=await Read();Assert.DoesNotContain(label,historical.Blockers);
            Assert.DoesNotContain(label+"_UNKNOWN",historical.Unresolved);
            Assert.NotEmpty(UserRoleRevocationService.PendingReleaseGates);
        }
        finally { await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear(); }
        Assert.False(await db.Users.AnyAsync(ct));Assert.False(await db.Branches.AnyAsync(ct));
        Assert.False(await db.OperationalTasks.AnyAsync(ct));Assert.False(await db.UserRoles.AnyAsync(ct));
    }
}
