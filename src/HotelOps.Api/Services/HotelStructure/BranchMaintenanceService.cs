using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> MaintainBranchAsync(SessionReference ticket, long sid, long? id,
        string? expected, CreateBranchRequest? create, UpdateBranchRequest? update, CancellationToken ct)
    {
        if (id <= 0 || create != null && id != null || update != null && id == null)
            throw new ValidationException("Invalid branch operation.");
        if (update != null && !HotelStructureUpdateService.IsStrongTag(expected))
            throw new ValidationException("A single strong If-Match is required.");
        var name = create != null || update != null ? BranchText(create?.Name ?? update?.Name, 200, true) : null;
        var address = create != null || update != null ? BranchText(create?.Address ?? update?.Address, 4000, true) : null;
        var phone = BranchText(create?.Phone ?? update?.Phone, 20, false);
        var email = BranchText(create?.Email ?? update?.Email, 150, false);
        if (phone?.Any(c => !char.IsAsciiDigit(c) && !"+()- .".Contains(c)) == true
            || email != null && (!new EmailAddressAttribute().IsValid(email) || email.Any(c => c > 127)))
            throw new ValidationException("Invalid contact information.");
        var code = create?.Code?.Trim().ToUpperInvariant();
        if (create != null && (string.IsNullOrEmpty(code) || code.Length > 30
            || !char.IsAsciiLetterOrDigit(code[0]) || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-')))
            throw new ValidationException("Invalid branch code.");
        var reason = update == null ? null : BranchText(update.Reason, 500, true);
        if (update != null && reason!.Length < 10) throw new ValidationException("A reason of at least ten characters is required.");
        // UC012 is administrator master data: only an effective SQL global grant qualifies.
        // This retains actor/session/catalog locks without requiring the optional room-type schema.
        return BatchAsync(ticket, sid, null, async (actor, token) =>
        {
            Branch? row;
            if (create != null)
            {
                if (await db.Branches.AnyAsync(b => b.BranchCode.Trim().ToUpper() == code, token))
                    return new(RoomTypeBatchStatus.Duplicate);
                row = new Branch { BranchCode = code!, BranchName = name!, Address = address, Phone = phone,
                    Email = email, Timezone = "Asia/Ho_Chi_Minh", IsActive = true, CreatedAt = clock.GetUtcNow().UtcDateTime };
                db.Add(row); await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
                var created = BranchDto(row);
                await BatchAuditAsync(actor, row.BranchId, "branches", row.BranchId, null, created,
                    ["code", "name", "address", "phone", "email", "timezone", "isActive"], token);
                return new(RoomTypeBatchStatus.Success, created, await BranchTag(created, token));
            }
            row = await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH(UPDLOCK,HOLDLOCK) WHERE branch_id={id}")
                .AsNoTracking().SingleOrDefaultAsync(token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            var before = BranchDto(row); var tag = await BranchTag(before, token);
            if (update == null) return new(RoomTypeBatchStatus.Success, before, tag);
            if (tag != expected) return new(RoomTypeBatchStatus.Stale);
            // Metadata update preserves code, timezone and activation state, including legacy records.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.branches SET branch_name={name},address={address},phone={phone},email={email},updated_at={clock.GetUtcNow().UtcDateTime} WHERE branch_id={id}", token);
            var after = before with { Name = name!, Address = address, Phone = phone, Email = email };
            await BatchAuditAsync(actor, row.BranchId, "branches", row.BranchId, before,
                new { reason, entity = after }, ["name", "address", "phone", "email"], token);
            return new(RoomTypeBatchStatus.Success, after, await BranchTag(after, token));
        }, ct, requireRoomTypeSchema: false, eligibility: async token =>
        {
            // Global master-data scope still follows User -> Branch -> catalog order.
            // Metadata maintenance may read/update inactive branches without reactivating them.
            if (id != null) await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH(UPDLOCK,HOLDLOCK) WHERE branch_id={id}")
                .AsNoTracking().ToArrayAsync(token);
            return true;
        });
    }

    private static string? BranchText(string? input, int max, bool required)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            if (required) throw new ValidationException("Required branch field is empty.");
            return null;
        }
        if (text.Length > max || text.Any(char.IsControl)) throw new ValidationException("Invalid branch field.");
        return text;
    }
    private static EditableBranchDto BranchDto(Branch row) => new(row.BranchId, row.BranchCode,
        row.BranchName, row.Address, row.Phone, row.Email, row.Timezone, row.IsActive);
    private async Task<string> BranchTag(EditableBranchDto row, CancellationToken ct)
    {
        var key = row.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var revision = await db.AuditLogs.Where(a => a.EntityType == "branches" && a.EntityId == key)
            .Select(a => (long?)a.AuditId).MaxAsync(ct) ?? 0;
        return '"' + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { row, revision }, Json))) + '"';
    }
}
