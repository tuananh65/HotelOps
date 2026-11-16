using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.MaterialRequests;

// Source-side participant only, called inside the admitted MaterialRequests.Create
// transaction. It never creates target admission, selects approvers or commits.
internal static class MaterialRequestCreation
{
    internal sealed record Binding(long Actor, long Branch, long Request, byte[] Version, bool Mandatory);

    internal static byte[]? Validate(long? existingMaterialId, MaterialRequestWrite input)
    {
        if (input.InternalRequestId is null && input.RequestLink is null) return null;
        if (existingMaterialId is not null) throw new ValidationException("Use Link Existing to attach an existing material request.");
        if (input.InternalRequestId is not > 0 || input.RequestLink is null)
            throw new ValidationException("Internal Request and explicit link details are required together.");
        byte[] version;
        try { version = Convert.FromBase64String(input.RequestLink.Version ?? ""); }
        catch (FormatException) { throw new ValidationException("Loaded Internal Request version required."); }
        if (version.Length != 8 || Convert.ToBase64String(version) != input.RequestLink.Version)
            throw new ValidationException("Loaded Internal Request version required.");
        return version;
    }

    internal static async Task<Binding?> PrepareAsync(HotelOpsDbContext db, long actor, MaterialRequestWrite input, CancellationToken ct)
    {
        var version = Validate(null, input);
        if (version is null) return null;
        var binding = new Binding(actor, input.BranchId, input.InternalRequestId!.Value, version, input.RequestLink!.Mandatory);
        await AuthorizeAsync(db, binding, ct);
        if (!(await InternalRequestDecisionStore.RevisionAsync(db, binding.Request, ct)).SequenceEqual(version))
            throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
        return binding;
    }

    private static async Task AuthorizeAsync(HotelOpsDbContext db, Binding binding, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Linked creation requires the existing Serializable material transaction.");
        await new UserEligibilityLock(db).LockAsync([binding.Actor], ct);
        if (!await InternalRequestDecisionStore.InstalledAsync(db, ct) || !await InternalRequestCompletionService.InstalledAsync(db, ct))
            throw new InternalRequestReadException(503, "IR_MATERIAL_LINK_SCHEMA_REQUIRED");
        // User locks are acquired by the writer first. Retain source state/version
        // before inventory catalog locks, number allocation and target persistence.
        await db.InternalRequests.FromSqlInterpolated($"SELECT * FROM dbo.internal_requests WITH(UPDLOCK,HOLDLOCK) WHERE internal_request_id={binding.Request}")
            .AsNoTracking().ToArrayAsync(ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var scope = await new InternalRequestReadAccess(db).ReadAsync(binding.Actor, now, ct);
        var source = await scope.Apply(db.InternalRequests.AsNoTracking(), now)
            .SingleOrDefaultAsync(r => r.InternalRequestId == binding.Request && r.BranchId == binding.Branch, ct)
            ?? throw new InternalRequestReadException(404, "IR_NOT_FOUND");
        if (!await new InternalRequestProcessAuthority(db).AllowsCurrentRequestAsync(binding.Actor, binding.Request, ct))
            throw new InternalRequestReadException(403, "IR_PROCESS_REQUIRED");
        if (source.Status is not ("ACCEPTED" or "IN_PROGRESS")) throw new InternalRequestReadException(409, "IR_LINK_STATE_INVALID");
    }

    internal static async Task WriteAsync(HotelOpsDbContext db, Binding binding, long material, CancellationToken ct)
    {
        await AuthorizeAsync(db, binding, ct);
        if (!await db.MaterialRequests.AsNoTracking().AnyAsync(m => m.RequestId == material
            && m.BranchId == binding.Branch && m.RequesterId == binding.Actor
            && m.Department.BranchId == binding.Branch && (m.Status == "DRAFT" || m.Status == "PENDING_APPROVAL"), ct))
            throw new InvalidOperationException("Created material does not match the linked command.");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_requests SET updated_at={now} WHERE internal_request_id={binding.Request} AND row_version={binding.Version} AND status IN('ACCEPTED','IN_PROGRESS')", ct) != 1)
            throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
        var audit = new AuditLog { ActorUserId = binding.Actor, BranchId = binding.Branch, EntityType = "internal_requests",
            EntityId = binding.Request.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "UPDATE", OccurredAt = now,
            NewValues = JsonSerializer.Serialize(new { operation = "CREATE_LINKED_MATERIAL", materialRequestId = material, binding.Mandatory }),
            ChangedFields = "[\"linked_work\"]" };
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_material_links(internal_request_id,material_request_id,is_mandatory,audit_id) VALUES({binding.Request},{material},{binding.Mandatory},{audit.AuditId})", ct);
        await AuthorizeAsync(db, binding, ct);
        // The material writer rechecks target permission and owns commit/rollback.
    }
}
