using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

[Collection("HotelOps isolated SQL")]
public sealed class RevokeMappingLockSqlTests
{
    private const string Connection = "Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true";
    private static HotelOpsDbContext Context() => new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(Connection).Options);

    [TaskRoleTrialFact]
    public async Task ReadCommittedResponsibilityReadRetainsExistingAndMissingMappingUntilTransactionEnds()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = budget.Token;
        await using var db = Context();
        Assert.Equal(1, await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita'
              AND DB_NAME()='HotelOps_DEV3_142A_20261114_cc78c13f'
              AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
                AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run'
                AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct));
        Assert.False(await db.Users.AnyAsync(ct));
        Assert.False(await db.Branches.AnyAsync(ct));
        Assert.True(await new InternalRequestProcessAuthority(db).InstalledAsync(ct));
        var prefix = "D3LOCK" + Guid.NewGuid().ToString("N")[..10];
        var now = DateTime.UtcNow.AddMinutes(-1);
        var branch = new Branch { BranchCode=prefix, BranchName="Synthetic mapping lock", IsActive=true };
        var department = new Department { DepartmentCode=prefix, DepartmentName="Synthetic mapping lock", IsActive=true };
        var user = new User { EmployeeCode=prefix, Username=prefix, Email=prefix+"@example.invalid", FullName="Synthetic mapping lock",
            PasswordHash="NO_LOGIN_SYNTHETIC_HASH", EmploymentStatus="ACTIVE", AccountStatus="ACTIVE", CreatedAt=now };
        var type = new InternalRequestType { TypeCode=prefix, TypeName="Synthetic mapping lock", IsActive=true };
        var request = new InternalRequest { RequestNo=prefix, Subject="Synthetic mapping lock", Description="Synthetic", Status="IN_PROGRESS", CreatedAt=now };
        var role = new UserRole { AssignedAt=now, IsActive=true };
        var committed = false;
        try
        {
            // Commit only the synthetic fixture, so an uncommitted INSERT cannot
            // masquerade as a lock acquired by the responsibility reader.
            await using (var setup = await db.Database.BeginTransactionAsync(ct))
            {
                db.Branches.Add(branch); await db.SaveChangesAsync(ct);
                department.BranchId=branch.BranchId; db.Departments.Add(department); await db.SaveChangesAsync(ct);
                user.PrimaryBranchId=branch.BranchId; db.Users.Add(user); db.InternalRequestTypes.Add(type); await db.SaveChangesAsync(ct);
                role.UserId=user.UserId; role.BranchId=branch.BranchId;
                role.RoleId=await db.Roles.Where(r=>r.RoleCode=="DEPT_MANAGER"&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
                db.UserRoles.Add(role);
                db.UserBranchAccesses.Add(new() { UserId=user.UserId, BranchId=branch.BranchId, GrantedAt=now, AccessLevel="STANDARD" });
                request.BranchId=branch.BranchId; request.RequesterId=user.UserId; request.ReceivingDepartmentId=department.DepartmentId; request.RequestTypeId=type.RequestTypeId;
                db.InternalRequests.Add(request); await db.SaveChangesAsync(ct);
                db.InternalRequestAssignments.Add(new() { InternalRequestId=request.InternalRequestId, AssigneeId=user.UserId, AssignedBy=user.UserId, AssignedAt=now, Status="ACTIVE" });
                await db.SaveChangesAsync(ct);
                await setup.CommitAsync(ct); committed=true; db.ChangeTracker.Clear();
            }
            foreach (var scenario in new[] { "mapping-revoke", "mapping-insert", "request-route", "assignment-end" })
            {
                var exists = scenario != "mapping-insert";
                if (exists)
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.department_management_access(user_id,branch_id,department_id,granted_at) VALUES({user.UserId},{branch.BranchId},{department.DepartmentId},{now})",ct);
                await using var reader = Context();
                await using var tx = await reader.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
                await new RevokeRoleResponsibilities(reader,TimeProvider.System).ReadAsync(user.UserId,role.UserRoleId,false,ct);
                await using var competitor = new SqlConnection(Connection); await competitor.OpenAsync(ct);
                await using var competingTx = (SqlTransaction)await competitor.BeginTransactionAsync(ct);
                await using var command = competitor.CreateCommand(); command.Transaction=competingTx;
                command.CommandText = "SET LOCK_TIMEOUT 700; " + (scenario switch
                {
                    "mapping-revoke" => "UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id=@user AND branch_id=@branch AND department_id=@department",
                    "mapping-insert" => "INSERT dbo.department_management_access(user_id,branch_id,department_id,granted_at) VALUES(@user,@branch,@department,SYSUTCDATETIME())",
                    "request-route" => "UPDATE dbo.internal_requests SET receiving_department_id=NULL WHERE internal_request_id=@request",
                    "assignment-end" => "UPDATE dbo.internal_request_assignments SET status='REASSIGNED',ended_at=SYSUTCDATETIME() WHERE internal_request_id=@request AND assignee_id=@user AND status='ACTIVE'",
                    _ => throw new InvalidOperationException("Unknown scenario")
                });
                command.Parameters.AddWithValue("@user",user.UserId); command.Parameters.AddWithValue("@branch",branch.BranchId); command.Parameters.AddWithValue("@department",department.DepartmentId);
                command.Parameters.AddWithValue("@request",request.InternalRequestId);
                var blocked = await Assert.ThrowsAsync<SqlException>(()=>command.ExecuteNonQueryAsync(ct));
                Assert.Equal(1222,blocked.Number);
                await tx.RollbackAsync(ct);
                // Same writer succeeds after the reader releases its locks.
                Assert.Equal(1,await command.ExecuteNonQueryAsync(ct));
                await competingTx.RollbackAsync(ct);
                Assert.Equal(exists ? 1 : 0, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.department_management_access WHERE user_id={user.UserId} AND branch_id={branch.BranchId} AND department_id={department.DepartmentId} AND revoked_at IS NULL").SingleAsync(ct));
                Assert.Equal(department.DepartmentId,await db.InternalRequests.Where(r=>r.InternalRequestId==request.InternalRequestId).Select(r=>r.ReceivingDepartmentId).SingleAsync(ct));
                Assert.True(await db.InternalRequestAssignments.AnyAsync(a=>a.InternalRequestId==request.InternalRequestId&&a.AssigneeId==user.UserId&&a.Status=="ACTIVE"&&a.EndedAt==null,ct));
                if (exists)
                    await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.department_management_access WHERE user_id={user.UserId} AND branch_id={branch.BranchId} AND department_id={department.DepartmentId}",ct);
            }
            Assert.NotEmpty(UserRoleRevocationService.PendingReleaseGates);
        }
        finally
        {
            db.ChangeTracker.Clear();
            if (committed)
            {
                // Delete only this fixture's recorded IDs, never a table-wide cleanup.
                await using var cleanup = await db.Database.BeginTransactionAsync(CancellationToken.None);
                await db.InternalRequestAssignments.Where(a=>a.InternalRequestId==request.InternalRequestId).ExecuteDeleteAsync(CancellationToken.None);
                await db.InternalRequests.Where(r=>r.InternalRequestId==request.InternalRequestId).ExecuteDeleteAsync(CancellationToken.None);
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.department_management_access WHERE user_id={user.UserId} AND branch_id={branch.BranchId} AND department_id={department.DepartmentId}",CancellationToken.None);
                await db.UserRoles.Where(r=>r.UserRoleId==role.UserRoleId).ExecuteDeleteAsync(CancellationToken.None);
                await db.UserBranchAccesses.Where(a=>a.UserId==user.UserId&&a.BranchId==branch.BranchId).ExecuteDeleteAsync(CancellationToken.None);
                await db.Users.Where(u=>u.UserId==user.UserId).ExecuteDeleteAsync(CancellationToken.None);
                await db.Departments.Where(d=>d.DepartmentId==department.DepartmentId).ExecuteDeleteAsync(CancellationToken.None);
                await db.Branches.Where(b=>b.BranchId==branch.BranchId).ExecuteDeleteAsync(CancellationToken.None);
                await db.InternalRequestTypes.Where(t=>t.RequestTypeId==type.RequestTypeId).ExecuteDeleteAsync(CancellationToken.None);
                await cleanup.CommitAsync(CancellationToken.None);
            }
        }
        Assert.False(await db.Users.AnyAsync(ct)); Assert.False(await db.Branches.AnyAsync(ct));
        Assert.False(await db.InternalRequests.AnyAsync(ct));
    }
}
