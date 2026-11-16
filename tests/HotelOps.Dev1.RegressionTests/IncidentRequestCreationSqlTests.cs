using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class IncidentLinkTrialFactAttribute : FactAttribute
{
    public IncidentLinkTrialFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("DEV3_INCIDENT_LINK_TRIAL") != "EXACT_DISPOSABLE_ROLLBACK")
            Skip = "Explicit disposable SQL identity and rollback trial required";
    }
}

[Collection("HotelOps isolated SQL")]
public sealed class IncidentRequestCreationSqlTests
{
    [IncidentLinkTrialFact]
    public async Task SourceAuthorityVersionAtomicLinkAndRollbackUseRealSql()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2)); var ct = timeout.Token;
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "HotelOps.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            // Exact server/name/GUID/run and empty database guards precede any mutation.
            await using var command = db.Database.GetDbConnection().CreateCommand(); command.Transaction = tx.GetDbTransaction();
            command.CommandText = await File.ReadAllTextAsync(Path.Combine(root!.FullName,
                "scripts/verification/CanonicalDatabase/ModelProbe/SprintApproveFixture.sql"), ct);
            long actor, branch;
            await using (var reader = await command.ExecuteReaderAsync(ct)) { Assert.True(await reader.ReadAsync(ct)); actor = reader.GetInt64(0); branch = reader.GetInt64(1); }
            command.CommandText = await File.ReadAllTextAsync(Path.Combine(root.FullName,
                "scripts/verification/CanonicalDatabase/InternalRequestIncidentLinksIsolated.sql"), ct);
            await command.ExecuteNonQueryAsync(ct);
            await command.ExecuteNonQueryAsync(ct); // Idempotent installer, still inside the outer rollback.
            Assert.True(await new InternalRequestProcessAuthority(db).InstalledAsync(ct));
            var now = DateTime.UtcNow; var prefix = "D3IC" + Guid.NewGuid().ToString("N")[..10];
            await db.Users.Where(u => u.UserId == actor).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "ACTIVE")
                .SetProperty(u => u.EmploymentStatus, "ACTIVE"), ct);
            var department = new Department { BranchId = branch, DepartmentCode = prefix, DepartmentName = "Synthetic", IsActive = true };
            var type = new InternalRequestType { TypeCode = prefix, TypeName = "Synthetic", IsActive = true };
            db.Departments.Add(department); db.InternalRequestTypes.Add(type); await db.SaveChangesAsync(ct);
            var role = await db.Roles.SingleAsync(r => r.RoleCode == "DEPT_MANAGER", ct);
            db.UserRoles.Add(new() { UserId = actor, RoleId = role.RoleId, BranchId = branch, IsActive = true, AssignedAt = now.AddMinutes(-1) });
            if (!await db.UserBranchAccesses.AnyAsync(a => a.UserId == actor && a.BranchId == branch, ct))
                db.UserBranchAccesses.Add(new() { UserId = actor, BranchId = branch, GrantedAt = now.AddMinutes(-1), AccessLevel = "STANDARD" });
            foreach (var code in new[] { "IR_VIEW", "IR_PROCESS" })
            {
                var permission = await db.Permissions.SingleOrDefaultAsync(p => p.PermissionCode == code, ct);
                if (permission is null)
                {
                    Assert.Equal("IR_VIEW", code);
                    var template = await db.Permissions.SingleAsync(p => p.PermissionCode == "IR_CREATE", ct);
                    permission = new() { PermissionCode = code, PermissionName = "View Internal Requests", ModuleCode = template.ModuleCode, ResourceCode = template.ResourceCode, ActionCode = "VIEW" };
                    db.Permissions.Add(permission); await db.SaveChangesAsync(ct);
                }
                var grant = await db.RolePermissions.SingleOrDefaultAsync(g => g.RoleId == role.RoleId && g.PermissionId == permission.PermissionId, ct);
                if (grant is null) db.RolePermissions.Add(new() { RoleId = role.RoleId, PermissionId = permission.PermissionId, DataScope = "BRANCH", GrantedAt = now });
                else Assert.Equal("BRANCH", grant.DataScope);
            }
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.department_management_access(user_id,branch_id,department_id,granted_at) VALUES({actor},{branch},{department.DepartmentId},{now})", ct);
            var source = new InternalRequest { RequestNo = prefix, BranchId = branch, RequestTypeId = type.RequestTypeId,
                RequesterId = actor, ReceivingDepartmentId = department.DepartmentId, Subject = "Synthetic source", Description = "Synthetic", Status = "IN_PROGRESS", CreatedAt = now };
            db.InternalRequests.Add(source); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            async Task<string> Version() => Convert.ToBase64String(await InternalRequestDecisionStore.RevisionAsync(db, source.InternalRequestId, ct));
            var original = await Version();
            CreateIncidentRequest Input(string version, long? targetBranch = null) => new() { BranchId = targetBranch ?? branch,
                InternalRequestId = source.InternalRequestId, RequestLink = new(version, true) };
            async Task Denied(int status, CreateIncidentRequest input)
            {
                var error = await Assert.ThrowsAsync<InternalRequestReadException>(() => IncidentRequestCreation.PrepareAsync(db, actor, input, ct));
                Assert.Equal(status, error.Status);
            }
            await Denied(404, Input(original, branch + 100000));
            await Denied(412, Input("AAAAAAAAAAA="));
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND department_id={department.DepartmentId}", ct);
            await Denied(403, Input(original));
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND department_id={department.DepartmentId}", ct);
            await db.UserRoles.Where(r => r.UserId == actor && r.RoleId == role.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), ct);
            await Denied(404, Input(original));
            await db.UserRoles.Where(r => r.UserId == actor && r.RoleId == role.RoleId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true), ct);
            var incidentCount = await db.Incidents.CountAsync(ct); var auditCount = await db.AuditLogs.CountAsync(ct);
            await tx.CreateSavepointAsync("before_link", ct);
            var binding = await IncidentRequestCreation.PrepareAsync(db, actor, Input(original), ct); Assert.NotNull(binding);
            var incident = new Incident { IncidentNo = prefix, BranchId = branch, ReportedBy = actor, IncidentTitle = "Synthetic linked incident",
                Description = "Synthetic", DiscoveredAt = now, ReportedAt = now, Status = "OPEN" };
            db.Incidents.Add(incident); await db.SaveChangesAsync(ct);
            await IncidentRequestCreation.WriteAsync(db, binding!, incident.IncidentId, ct);
            Assert.NotEqual(original, await Version());
            Assert.Equal("IN_PROGRESS", await db.InternalRequests.Where(r => r.InternalRequestId == source.InternalRequestId).Select(r => r.Status).SingleAsync(ct));
            Assert.Equal(1, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_incident_links l JOIN dbo.audit_logs a ON a.audit_id=l.audit_id WHERE l.internal_request_id={source.InternalRequestId} AND l.incident_id={incident.IncidentId} AND l.is_mandatory=1 AND a.actor_user_id={actor}").SingleAsync(ct));
            await Denied(412, Input(original));
            await tx.RollbackToSavepointAsync("before_link", ct); db.ChangeTracker.Clear();
            Assert.Equal(original, await Version()); Assert.Equal(incidentCount, await db.Incidents.CountAsync(ct));
            Assert.Equal(auditCount, await db.AuditLogs.CountAsync(ct));
            Assert.Equal(0, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_incident_links WHERE internal_request_id={source.InternalRequestId}").SingleAsync(ct));
            await db.InternalRequests.Where(r => r.InternalRequestId == source.InternalRequestId).ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "CLOSED"), ct);
            await Denied(409, Input(await Version()));
        }
        finally { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
        Assert.False(await db.Users.AnyAsync(ct)); Assert.False(await db.Branches.AnyAsync(ct)); Assert.False(await db.InternalRequests.AnyAsync(ct));
    }
}
