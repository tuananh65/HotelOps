using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

public enum AccountCreationStatus { Success, Forbidden, InvalidReference, Duplicate }
public sealed record AccountCreationResult(AccountCreationStatus Status, CreatedEmployeeAccountDto? User = null,
    IssuedTemporaryCredential? Credential = null, IReadOnlyList<string>? DuplicateFields = null) : IDisposable
{
    public void Dispose() => Credential?.Dispose();
}

public sealed class UserAccountCreationService(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, ITemporaryCredentialService credentials,
    IInitialCredentialTransactionParticipant participant, TimeProvider clock)
{
    // UC-004 business allowlist explicitly approved in Task 30; not a grant/catalog edit.
    private static readonly IReadOnlyDictionary<string, string> Departments = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["HK_STAFF"] = "HK", ["HK_SUPERVISOR"] = "HK", ["MAINTENANCE_STAFF"] = "MT",
        ["MAINTENANCE_LEAD"] = "MT", ["STOREKEEPER"] = "WH", ["RECEPTION"] = "FO"
    };

    public async Task<AccountCreationResult> CreateAsync(CreateEmployeeAccountRequest input, CancellationToken ct)
    {
        var request = input.Normalize();
        if (db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Creation requires a clean scoped context.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (!current.TryGetCurrentUser(out var actor) || actor.IsRestricted
            || !(await permissions.EvaluateGlobalAsync("USER_MANAGE", ct)).IsGranted
            || !(await permissions.EvaluateGlobalAsync("ROLE_MANAGE", ct)).IsGranted)
            return new(AccountCreationStatus.Forbidden);
        if (!Departments.TryGetValue(request.RoleCode, out var departmentCode)) return new(AccountCreationStatus.InvalidReference);

        // Serialize master deactivation with validation/insert; no assumption from client scope.
        var branch = await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH (UPDLOCK,HOLDLOCK) WHERE branch_id={request.BranchId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        var department = await db.Departments.FromSqlInterpolated($"SELECT * FROM dbo.departments WITH (UPDLOCK,HOLDLOCK) WHERE department_id={request.DepartmentId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        var role = await db.Roles.FromSqlInterpolated($"SELECT * FROM dbo.roles WITH (UPDLOCK,HOLDLOCK) WHERE role_code={request.RoleCode}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (branch is not { IsActive: true } || department is not { IsActive: true } || role is not { IsActive: true }
            || department.BranchId != branch.BranchId || department.DepartmentCode != departmentCode || role.RoleCode != request.RoleCode)
            return new(AccountCreationStatus.InvalidReference);

        // Enforce lossless varchar storage under the actual database collation, not a new HR code format.
        var lossy = await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*) AS [Value] FROM (VALUES
              (CAST(@code AS nvarchar(max)),40),(CAST(@username AS nvarchar(max)),100),
              (CAST(@email AS nvarchar(max)),150),(CAST(@phone AS nvarchar(max)),20)) AS fields([value],[max_bytes])
            WHERE DATALENGTH(CONVERT(varchar(max),[value])) > [max_bytes]
               OR CONVERT(varbinary(max),CONVERT(nvarchar(max),CONVERT(varchar(max),[value]))) <> CONVERT(varbinary(max),[value])
            """, new SqlParameter("@code", request.EmployeeCode), new SqlParameter("@username", request.Username),
            new SqlParameter("@email", request.Email), new SqlParameter("@phone", (object?)request.Phone ?? DBNull.Value)).SingleAsync(ct);
        if (lossy != 0) throw new ValidationException("A field cannot be stored losslessly within the database limits.");

        var now = clock.GetUtcNow().UtcDateTime;
        var user = new User { EmployeeCode = request.EmployeeCode, Username = request.Username, FullName = request.FullName,
            Email = request.Email, Phone = request.Phone, PrimaryBranchId = branch.BranchId, DepartmentId = department.DepartmentId,
            AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", CreatedAt = now };
        db.Users.Add(user);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(user).State = EntityState.Detached;
            await transaction.RollbackAsync(ct);
            await transaction.DisposeAsync();
            var duplicates = new List<string>();
            if (await db.Users.AsNoTracking().AnyAsync(u => u.EmployeeCode == request.EmployeeCode, ct)) duplicates.Add("employeeCode");
            if (await db.Users.AsNoTracking().AnyAsync(u => u.Username == request.Username, ct)) duplicates.Add("username");
            if (await db.Users.AsNoTracking().AnyAsync(u => u.Email == request.Email, ct)) duplicates.Add("email");
            return new(AccountCreationStatus.Duplicate, DuplicateFields: duplicates);
        }
        db.Entry(user).State = EntityState.Detached;
        var access = new UserBranchAccess { UserId = user.UserId, BranchId = branch.BranchId, IsPrimary = true,
            AccessLevel = "STANDARD", GrantedBy = actor.UserId, GrantedAt = now };
        var assignment = new UserRole { UserId = user.UserId, RoleId = role.RoleId, BranchId = branch.BranchId,
            AssignedBy = actor.UserId, AssignedAt = now, IsActive = true };
        var audit = new AuditLog { ActorUserId = actor.UserId, BranchId = branch.BranchId, EntityType = "users",
            EntityId = user.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "CREATE", OccurredAt = now,
            NewValues = JsonSerializer.Serialize(new { employeeCode = request.EmployeeCode, username = request.Username,
                fullName = request.FullName, email = request.Email, phone = request.Phone, branchId = branch.BranchId,
                departmentId = department.DepartmentId, roleCode = role.RoleCode, accountStatus = "ACTIVE" }) };
        db.AddRange(access, assignment, audit);
        try { await db.SaveChangesAsync(ct); }
        finally
        {
            db.Entry(access).State = EntityState.Detached; db.Entry(assignment).State = EntityState.Detached;
            db.Entry(audit).State = EntityState.Detached;
        }
        using var prepared = credentials.Prepare(user.UserId);
        using var pending = await participant.StageInitialAsync(prepared, actor.UserId, ct);
        await transaction.CommitAsync(ct);
        var issued = pending.ReleaseAfterCommit();
        return new(AccountCreationStatus.Success, new(user.UserId, request.EmployeeCode, request.Username, request.FullName,
            request.Email, request.Phone, branch.BranchId, department.DepartmentId, role.RoleCode, "ACTIVE"), issued);
    }
}
