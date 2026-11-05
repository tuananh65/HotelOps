using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private static (string Name, string? Description) DepartmentInput(string? name, string? description, long? manager)
    {
        name = name?.Trim(); description = description?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 150 || name.Any(char.IsControl) || manager <= 0
            || description?.Length > 4000 || description?.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t') == true)
            throw new ValidationException("Invalid department fields.");
        return (name, string.IsNullOrEmpty(description) ? null : description);
    }
    private async Task<bool> EligibleDepartmentManager(long branch, long? manager, CancellationToken ct)
    {
        if (manager == null) return true;
        var now = clock.GetUtcNow().UtcDateTime;
        // Branch DH designation is not an RBAC grant. Both role assignment and branch
        // membership must remain effective, including the final commit-time reread.
        return await db.Users.AsNoTracking().AnyAsync(u => u.UserId == manager && u.DeletedAt == null
            && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE"
            && db.UserRoles.Any(r => r.UserId == u.UserId && r.BranchId == branch && r.IsActive && r.Role.IsActive && r.Role.RoleCode == "DEPT_MANAGER"
                && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now))
            && u.UserBranchAccessUsers.Any(a => a.BranchId == branch && a.RevokedAt == null && a.GrantedAt <= now), ct);
    }
    public Task<RoomTypeBatchResult> CreateDepartmentAsync(SessionReference ticket, long sid, long branch, CreateDepartmentRequest input, CancellationToken ct)
    {
        var (name, description) = DepartmentInput(input.Name, input.Description, input.ManagerUserId);
        var code = input.Code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code) || code.Length > 30 || !char.IsAsciiLetterOrDigit(code[0])
            || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-')) throw new ValidationException("Invalid code.");
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (await db.Departments.AsNoTracking().AnyAsync(x => x.BranchId == branch && x.DepartmentCode.Trim().ToUpper() == code, token)) return new(RoomTypeBatchStatus.Duplicate);
            var row = new Department { BranchId = branch, DepartmentCode = code, DepartmentName = name, ScopeDescription = description, ManagerUserId = input.ManagerUserId, IsActive = true };
            db.Add(row); await db.SaveChangesAsync(token);
            var value = DepartmentDto(row);
            await BatchAuditAsync(actor, branch, "departments", row.DepartmentId, null, value, ["code", "name", "description", "managerUserId", "isActive"], token);
            return new(RoomTypeBatchStatus.Success, value, await DepartmentTag(value, token));
        }, ct, additionalUsers: input.ManagerUserId is { } manager ? [manager] : [], eligibility: token => EligibleDepartmentManager(branch, input.ManagerUserId, token), requireRoomTypeSchema: false);
    }
    public Task<RoomTypeBatchResult> ReadDepartmentAsync(SessionReference ticket, long sid, long branch, long id, CancellationToken ct)
        => EditDepartmentAsync(ticket, sid, branch, id, null, null, ct);
    public Task<RoomTypeBatchResult> UpdateDepartmentAsync(SessionReference ticket, long sid, long branch, long id, string tag, UpdateDepartmentRequest input, CancellationToken ct)
    {
        Batch2Tag(tag); var (name, description) = DepartmentInput(input.Name, input.Description, input.ManagerUserId);
        return EditDepartmentAsync(ticket, sid, branch, id, tag, input with { Name = name, Description = description }, ct);
    }
    private Task<RoomTypeBatchResult> EditDepartmentAsync(SessionReference ticket, long sid, long branch, long id, string? expected, UpdateDepartmentRequest? input, CancellationToken ct)
    {
        if (id <= 0) throw new ValidationException("Id must be positive.");
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            var row = await db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.DepartmentId == id && x.BranchId == branch, token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            var before = DepartmentDto(row); var tag = await DepartmentTag(before, token);
            if (input == null) return new(RoomTypeBatchStatus.Success, before, tag);
            if (!row.IsActive) return new(RoomTypeBatchStatus.Inactive);
            if (tag != expected) return new(RoomTypeBatchStatus.Stale);
            await db.Database.ExecuteSqlInterpolatedAsync($"/* UC025:Update */ UPDATE dbo.departments SET department_name={input.Name},scope_description={input.Description},manager_user_id={input.ManagerUserId} WHERE department_id={id} AND branch_id={branch}", token);
            var after = before with { Name = input.Name, Description = input.Description, ManagerUserId = input.ManagerUserId };
            await BatchAuditAsync(actor, branch, "departments", id, before, after, ["name", "description", "managerUserId"], token);
            return new(RoomTypeBatchStatus.Success, after, await DepartmentTag(after, token));
        }, ct, additionalUsers: input?.ManagerUserId is { } manager ? [manager] : [],
            eligibility: input == null ? null : token => EligibleDepartmentManager(branch, input.ManagerUserId, token), requireRoomTypeSchema: false);
    }
    private static DepartmentInformationDto DepartmentDto(Department row) => new(row.DepartmentId, row.BranchId, row.DepartmentCode, row.DepartmentName, row.ScopeDescription, row.ManagerUserId, row.IsActive);
    private async Task<string> DepartmentTag(DepartmentInformationDto value, CancellationToken ct)
    {
        var key = value.Id.ToString(CultureInfo.InvariantCulture);
        var revision = await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "departments" && x.EntityId == key).Select(x => (long?)x.AuditId).MaxAsync(ct) ?? 0;
        return '"' + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { value, revision }, Json))) + '"';
    }
}
