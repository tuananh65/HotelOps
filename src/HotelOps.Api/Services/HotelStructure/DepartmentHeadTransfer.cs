using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private sealed class HeadMapping
    {
        public long UserId { get; set; }
        public string Origin { get; set; } = "";
        public DateTime? RevokedAt { get; set; }
        public DateTime GrantedAt { get; set; }
    }

    private async Task TransferDepartmentHeadAsync(long actor, long branch, long department,
        long? previous, long? next, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == actor && r.IsActive
            && r.Role.IsActive && r.Role.RoleCode == "SYS_ADMIN" && r.AssignedAt <= now
            && (r.ExpiresAt == null || r.ExpiresAt > now) && (r.BranchId == null || r.BranchId == branch), ct))
            throw new DepartmentHeadTransferException("HEAD_ADMIN_REQUIRED");
        if (previous == next) return;
        if (!await new InternalRequestProcessAuthority(db).InstalledAsync(ct)
            || await db.Database.SqlQuery<int>($"""
                SELECT CASE WHEN COL_LENGTH('dbo.department_management_access','authority_origin')=24
                  AND COL_LENGTH('dbo.departments','head_transfer_version')=8
                  AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.department_management_access')
                    AND name='CK_department_authority_origin' AND is_disabled=0 AND is_not_trusted=0)
                  AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
                    AND name='HotelOps.DepartmentHeadTransfer.V1' AND CONVERT(nvarchar(10),value)='1')
                  THEN 1 ELSE 0 END AS Value
                """).SingleAsync(ct) != 1)
            throw new DepartmentHeadTransferException("HEAD_SCHEMA_REQUIRED");
        if (next is { } user)
        {
            if (!await EligibleDepartmentManager(branch, user, ct))
                throw new DepartmentHeadTransferException("HEAD_ROLE_OR_ACCESS_REQUIRED");
            if (!await db.Users.AsNoTracking().AnyAsync(u => u.UserId == user && u.DepartmentId == department, ct))
                throw new DepartmentHeadTransferException("HEAD_DEPARTMENT_REQUIRED");
            var required = new List<string> { "IR_PROCESS", "MR_APPROVE_L1" };
            if (await db.Warehouses.AsNoTracking().AnyAsync(w => w.DepartmentId == department && w.BranchId == branch && w.IsActive, ct))
                required.Add("MR_APPROVE_FALLBACK");
            foreach (var permission in required)
                if (!await (from r in db.UserRoles.AsNoTracking()
                    join p in db.RolePermissions.AsNoTracking() on r.RoleId equals p.RoleId
                    where r.UserId == user && r.BranchId == branch && r.IsActive && r.Role.IsActive
                        && r.Role.RoleCode == "DEPT_MANAGER" && r.AssignedAt <= now
                        && (r.ExpiresAt == null || r.ExpiresAt > now)
                        && p.Permission.PermissionCode == permission && (p.DataScope == "ALL" || p.DataScope == "BRANCH")
                    select r.UserId).AnyAsync(ct))
                    throw new DepartmentHeadTransferException("HEAD_MISSING_" + permission);
        }
        var mappings = await db.Database.SqlQuery<HeadMapping>($"""
            SELECT user_id AS UserId,authority_origin AS Origin,revoked_at AS RevokedAt,granted_at AS GrantedAt
            FROM dbo.department_management_access WITH(UPDLOCK,HOLDLOCK)
            WHERE branch_id={branch} AND department_id={department} AND (user_id={previous} OR user_id={next})
            """).ToArrayAsync(ct);
        if (mappings.Any(m => m.RevokedAt == null && m.Origin == "LEGACY_UNKNOWN"))
            throw new DepartmentHeadTransferException("HEAD_MAPPING_PROVENANCE_REQUIRED");
        if (mappings.Any(m => m.UserId == next && m.RevokedAt == null && m.GrantedAt > now))
            throw new DepartmentHeadTransferException("HEAD_MAPPING_NOT_EFFECTIVE");
        if (previous is { } old)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.department_management_access
                SET revoked_at=CASE WHEN authority_origin='DESIGNATION' THEN {now} ELSE revoked_at END,
                    authority_origin=CASE WHEN authority_origin='BOTH' THEN 'INDEPENDENT' ELSE authority_origin END
                WHERE user_id={old} AND branch_id={branch} AND department_id={department}
                  AND revoked_at IS NULL AND authority_origin IN ('DESIGNATION','BOTH')
                """, ct);
        if (next is { } head)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                IF EXISTS(SELECT 1 FROM dbo.department_management_access WITH(UPDLOCK,HOLDLOCK)
                    WHERE user_id={head} AND branch_id={branch} AND department_id={department})
                    UPDATE dbo.department_management_access
                    SET authority_origin=CASE WHEN revoked_at IS NULL AND authority_origin IN ('INDEPENDENT','BOTH')
                          THEN 'BOTH' ELSE 'DESIGNATION' END,
                        granted_at=CASE WHEN revoked_at IS NULL AND granted_at<={now} THEN granted_at ELSE {now} END,revoked_at=NULL
                    WHERE user_id={head} AND branch_id={branch} AND department_id={department};
                ELSE INSERT dbo.department_management_access(user_id,branch_id,department_id,granted_at,authority_origin)
                    VALUES({head},{branch},{department},{now},'DESIGNATION');
                """, ct);
        var after = await db.Database.SqlQuery<HeadMapping>($"""
            SELECT user_id AS UserId,authority_origin AS Origin,revoked_at AS RevokedAt,granted_at AS GrantedAt
            FROM dbo.department_management_access WHERE branch_id={branch} AND department_id={department}
              AND (user_id={previous} OR user_id={next})
            """).ToArrayAsync(ct);
        await BatchAuditAsync(actor, branch, "department_head_authority", department, mappings, after,
            ["managerUserId", "authorityOrigin", "revokedAt"], ct);
    }
}

internal sealed class DepartmentHeadTransferException(string code) : ValidationException(code);
