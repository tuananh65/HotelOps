using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace UC012Fixtures;

public sealed record FixtureReceipt(string owner, Guid databaseGuid, string leaseId,
    long actorId, long deniedActorId, string codePrefix, bool approved,
    long branchId, long departmentId, string actorUsername, string deniedUsername,
    string runId, string planSha256, string manifestSha256);

public static class Seeder
{
    // The marker and rows commit together. Repetition is rejected without writes;
    // no repair, overwrite, rehash, session creation or unknown-outcome replay.
    public static async Task<FixtureReceipt> SeedAsync(FixturePlan plan, string planHash,
        string connection, string allowedPassword, string deniedPassword, CancellationToken ct)
    {
        PlanGuard.Validate(plan, DateTimeOffset.UtcNow, Environment.MachineName);
        PlanGuard.Require(Environment.GetEnvironmentVariable("UC012_FIXTURE_EXECUTE") == "APPROVED", "EXPLICIT_EXECUTION_SWITCH_REQUIRED");
        PlanGuard.Connection(plan, connection);
        PlanGuard.Passwords(allowedPassword, deniedPassword);
        PlanGuard.Artifact(plan.SchemaArtifact, plan.SchemaSha256);
        PlanGuard.Artifact(plan.ManifestArtifact, plan.ManifestSha256);
        PlanGuard.Artifact(typeof(HotelOpsDbContext).Assembly.Location, plan.ApiAssemblySha256);
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(plan.ManifestArtifact));
        PlanGuard.Manifest(plan, manifest.RootElement);

        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
        await db.Database.OpenConnectionAsync(ct);
        // Live identity is checked BEFORE any transaction or mutation.
        await using (var identity = db.Database.GetDbConnection().CreateCommand())
        {
            identity.CommandText = """
                SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),DB_NAME(),
                (SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()),
                (SELECT CONVERT(nvarchar(256),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UC012.Trial'),
                (SELECT CONVERT(nvarchar(256),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UC012.Schema')
                """;
            await using var r = await identity.ExecuteReaderAsync(ct);
            PlanGuard.Require(await r.ReadAsync(ct) && !r.IsDBNull(0) && r.GetString(0).Equals(plan.Server, StringComparison.OrdinalIgnoreCase)
                && r.GetString(1) == plan.Database && !r.IsDBNull(2) && r.GetGuid(2) == plan.DatabaseGuid
                && !r.IsDBNull(3) && r.GetString(3) == plan.IsolationMarker
                && !r.IsDBNull(4) && r.GetString(4) == "UC012.AuthBranchClosure.v1", "LIVE_DATABASE_MISMATCH");
        }
        await using (var schema = db.Database.GetDbConnection().CreateCommand())
        {
            using var source = new StreamReader(typeof(Seeder).Assembly.GetManifestResourceStream("UC012Fixtures.schema-precheck.sql")!);
            schema.CommandText = await source.ReadToEndAsync(ct);
            PlanGuard.Require(Convert.ToInt32(await schema.ExecuteScalarAsync(ct)) == 1, "BRANCH_SCHEMA_REQUIRED");
            schema.CommandText = "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.users') AND name='row_version' AND system_type_id=189 AND max_length=8 AND is_nullable=0";
            PlanGuard.Require(Convert.ToInt32(await schema.ExecuteScalarAsync(ct)) == 1, "USER_SCHEMA_REQUIRED");
        }
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(plan.EndUtc - DateTimeOffset.UtcNow);
        ct = limit.Token;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result=sys.sp_getapplock @Resource=N'UC012:FixtureProvision:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0;
            IF @result<0 THROW 51810,'Fixture provision lock unavailable',1;
            IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UC012.Fixture')
                THROW 51811,'Fixture already committed; no replay',1;
            """, ct);
        PlanGuard.Require(!await db.Users.AnyAsync(ct) && !await db.Branches.AnyAsync(ct)
            && !await db.Departments.AnyAsync(ct) && !await db.UserRoles.AnyAsync(ct)
            && !await db.UserBranchAccesses.AnyAsync(ct) && !await db.UserSessions.AnyAsync(ct)
            && !await db.AuditLogs.AnyAsync(ct), "EMPTY_OWNED_FIXTURE_DATABASE_REQUIRED");
        var role = await db.Roles.AsNoTracking().SingleOrDefaultAsync(x => x.RoleCode == "SYS_ADMIN", ct);
        PlanGuard.Require(role is { IsActive: true }, "CANONICAL_ROLE_REQUIRED");
        PlanGuard.Require(await db.Roles.CountAsync(ct) == 12 && await db.Permissions.CountAsync(ct) == 62
            && await db.RolePermissions.CountAsync(ct) == 140
            && await db.RolePermissions.AnyAsync(g => g.RoleId == role!.RoleId && g.Permission.PermissionCode == "MASTER_DATA_MANAGE" && g.DataScope == "ALL", ct), "CANONICAL_PERMISSION_PATH_REQUIRED");
        var now = DateTime.UtcNow;
        var branch = new Branch { BranchCode = "FX" + plan.RunId, BranchName = "UC012 fixture branch", Timezone = "Asia/Ho_Chi_Minh", IsActive = true, CreatedAt = now };
        db.Branches.Add(branch); await db.SaveChangesAsync(ct);
        var department = new Department { BranchId = branch.BranchId, DepartmentCode = "UC012", DepartmentName = "UC012 isolated fixture", IsActive = true };
        db.Departments.Add(department); await db.SaveChangesAsync(ct);
        var hasher = new PasswordCredentialService(Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
        User User(string suffix, string password)
        {
            var u = new User
            {
                EmployeeCode = "FX" + plan.RunId + suffix,
                Username = "uc012." + plan.RunId + "." + suffix,
                Email = "uc012." + plan.RunId + "." + suffix + "@example.invalid",
                FullName = "UC012 synthetic " + suffix,
                PrimaryBranchId = branch.BranchId,
                DepartmentId = department.DepartmentId,
                AccountStatus = "ACTIVE",
                EmploymentStatus = "ACTIVE",
                CreatedAt = now
            };
            u.PasswordHash = hasher.HashPassword(u, password); return u;
        }
        var allowed = User("allowed", allowedPassword); var denied = User("denied", deniedPassword);
        db.Users.AddRange(allowed, denied); await db.SaveChangesAsync(ct);
        db.UserRoles.AddRange(new UserRole { UserId = allowed.UserId, RoleId = role!.RoleId, AssignedAt = now, IsActive = true },
            new UserRole { UserId = denied.UserId, RoleId = role.RoleId, BranchId = branch.BranchId, AssignedAt = now, IsActive = true });
        foreach (var user in new[] { allowed, denied })
            db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.UserId, BranchId = branch.BranchId, AccessLevel = "STANDARD", IsPrimary = true, GrantedAt = now });
        await db.SaveChangesAsync(ct);
        PlanGuard.Require(await db.UserRoles.CountAsync(ct) == 2 && !await db.UserSessions.AnyAsync(ct)
            && !await db.Branches.AnyAsync(b => b.BranchCode.StartsWith(plan.CodePrefix), ct), "FIXTURE_POSTCONDITION_FAILED");
        // Constant identity profile + exact catalog path + only two assignments prove
        // scope construction; real effective admission remains an HTTPS acceptance case.
        await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_addextendedproperty @name=N'HotelOps.UC012.Fixture', @value={plan.RunId}", ct);
        PlanGuard.Validate(plan, DateTimeOffset.UtcNow, Environment.MachineName);
        await transaction.CommitAsync(ct);
        return new(plan.Owner, plan.DatabaseGuid, plan.LeaseId, allowed.UserId, denied.UserId,
            plan.CodePrefix, true, branch.BranchId, department.DepartmentId, allowed.Username, denied.Username,
            plan.RunId, planHash, plan.ManifestSha256);
    }
}
