using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintDistinctReinspectorFixture
{
    // Caller has verified the disposable server/database/GUID/run and owns this transaction.
    internal static async Task<User> CreateAsync(HotelOpsDbContext db, long original, long branch, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null || Environment.GetEnvironmentVariable("DEV3_REINSPECTION_NEXT_ROUND") == "1")
            throw new Exception("Distinct inspector fixture requires an owned transaction and no descendant rework");
        var source = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == original && u.Username == "D3-EVIDENCE"
            && db.UserBranchAccesses.Any(a => a.UserId == u.UserId && a.BranchId == branch && a.RevokedAt == null && a.Branch.BranchCode == "D3-EVIDENCE"), ct)
            ?? throw new Exception("Original synthetic inspector with branch access required");
        if (await db.Users.AnyAsync(u => u.Username == "D3-REINSPECTOR", ct)) throw new Exception("Unexpected distinct inspector fixture");
        var user = new User { Username = "D3-REINSPECTOR", EmployeeCode = "D3-REINSPECTOR", Email = "reinspector@example.invalid",
            FullName = "Synthetic distinct reinspector", AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE",
            PrimaryBranchId = branch, DepartmentId = source.DepartmentId, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user); await db.SaveChangesAsync(ct);
        var roles = await db.UserRoles.AsNoTracking().Where(r => r.UserId == original && r.IsActive).ToArrayAsync(ct);
        db.UserRoles.AddRange(roles.Select(r => new UserRole { UserId = user.UserId, RoleId = r.RoleId, BranchId = r.BranchId,
            AssignedBy = original, AssignedAt = r.AssignedAt, ExpiresAt = r.ExpiresAt, IsActive = true }));
        var access = await db.UserBranchAccesses.AsNoTracking().SingleAsync(a => a.UserId == original && a.BranchId == branch && a.RevokedAt == null, ct);
        db.UserBranchAccesses.Add(new UserBranchAccess { UserId = user.UserId, BranchId = branch, AccessLevel = access.AccessLevel,
            IsPrimary = true, GrantedBy = original, GrantedAt = access.GrantedAt });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); return user;
    }

    internal static async Task CleanupAsync(HotelOpsDbContext db, long actor, long branch, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null || !await db.Users.AsNoTracking().AnyAsync(u => u.UserId == actor
            && u.Username == "D3-REINSPECTOR" && u.PrimaryBranchId == branch && u.PrimaryBranch!.BranchCode == "D3-EVIDENCE", ct))
            throw new Exception("Owned distinct inspector cleanup required");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            IF EXISTS(SELECT 1 FROM dbo.room_inspections WHERE inspector_user_id={actor})
              OR EXISTS(SELECT 1 FROM dbo.attachments WHERE uploaded_by={actor})
              THROW 51999,'Inspector sources must be cleaned first',1;
            DELETE dbo.user_roles WHERE user_id={actor};
            DELETE dbo.user_branch_access WHERE user_id={actor};
            DELETE dbo.user_sessions WHERE user_id={actor};
            DELETE dbo.audit_logs WHERE actor_user_id={actor} OR(entity_type='users' AND entity_id=CONVERT(nvarchar(50),{actor}));
            DELETE dbo.users WHERE user_id={actor} AND username='D3-REINSPECTOR';
            """, ct);
    }
}
