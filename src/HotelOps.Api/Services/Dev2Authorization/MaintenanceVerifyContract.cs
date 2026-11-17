using System.Data;
using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Dev2Authorization;

internal static class MaintenanceVerifyContract
{
    private const string Policy = "UC059 Verify: HTTPS current unrestricted SQL session; current scoped Maintenance Lead with MAINTENANCE_REVIEW and independent MAINTENANCE_VIEW; performer separation; exact request/root and submitted audit binding; catalog lock before user/source; native If-Match and Serializable transaction; independent material/evidence read authority; persisted closure, block release, audit and retryable notification intents atomically; capability gaps fail closed";
    internal static IEnumerable<Dev2ActionContract> Contracts => [new("A004A-031", "DEV3_142A_MAINTENANCE_VERIFY_V1",
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
        (a, ct) => Task.FromResult<IReadOnlyList<Dev2PermissionRequirement>>([
            new("MAINTENANCE_REVIEW", new(Dev2PermissionTargetKind.Resource, Resource: new("maintenance_work", Id(a)))),
            new("MAINTENANCE_VIEW", new(Dev2PermissionTargetKind.Resource, Resource: new("maintenance_work", Id(a))))]),
        Dev2PermissionCombination.All, Validate,
        (a, call) => call.Controller == "MaintenanceWorks" && call.Action == "VerifyWork" && call.RootId == Id(a)
            && ReferenceEquals(call.Input, Input(a)), (a, _) => new Resolver(a), null, Validate, RequiresActionTransaction: true)];

    private static HotelOpsDbContext Db(Dev2BoundAction a) => a.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();
    private static long Id(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("id") is long id && id > 0 ? id : throw new Dev2AccessDenied(404);
    private static VerifyMaintenanceWorkRequest Input(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("request") as VerifyMaintenanceWorkRequest ?? throw new Dev2AccessDenied(403);
    private static Task<long?> Branch(Dev2BoundAction a, CancellationToken ct) => Db(a).MaintenanceWorks.AsNoTracking()
        .Where(w => w.MaintenanceWorkId == Id(a) && w.Incident != null).Select(w => (long?)w.Incident!.BranchId).SingleOrDefaultAsync(ct);
    private sealed class Resolver(Dev2BoundAction a) : IPermissionResourceResolver
    {
        public async Task<VerifiedPermissionResource?> ResolveAsync(string permission, PermissionResource resource, long user, CancellationToken ct)
        {
            if (user != a.Actor.UserId || permission is not ("MAINTENANCE_REVIEW" or "MAINTENANCE_VIEW")
                || resource.ResourceType != "maintenance_work" || resource.ResourceId != Id(a)) return null;
            var branch = await Branch(a, ct);
            return branch == null ? null : new(branch.Value, false);
        }
    }
    private static async Task<bool> Validate(Dev2BoundAction a, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        if (!a.Http.Request.IsHttps || a.Identity.DecisionId != "A004A-031" || a.Identity.ControllerType != typeof(MaintenanceWorksController)
            || a.Identity.HttpMethod != "POST" || a.Identity.ActionName != "VerifyWork" || Input(a).ActorUserId != a.Actor.UserId || Input(a).SubmissionAuditId <= 0) return false;
        var db = Db(a);
        if (phase != Dev2AuthorizationPhase.Admission && db.Database.CurrentTransaction == null) return false;
        await using var admission = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return false;
        await a.Http.RequestServices.GetRequiredService<HotelOps.Api.Services.Inventory.IInventoryIssueCapacity>().AcquireCatalog(ct);
        var owner = await db.MaintenanceWorks.AsNoTracking().Where(w => w.MaintenanceWorkId == Id(a)).Select(w => w.AssigneeId).SingleOrDefaultAsync(ct);
        if (owner is not > 0) return false;
        await new UserEligibilityLock(db).LockAsync([a.Actor.UserId, owner.Value], ct);
        if (!await db.MaintenanceWorks.AsNoTracking().AnyAsync(w => w.MaintenanceWorkId == Id(a) && w.AssigneeId == owner, ct)) return false;
        if (!HotelOpsCookieTicket.TryRead(a.Http.User, out var ticket, out var sid) || ticket == null || ticket.IsRestricted
            || ticket.UserId != a.Actor.UserId || sid != a.Actor.SessionId) return false;
        var session = await Dev2SessionValidation.ValidateAsync(db, ticket, a.Http.RequestServices.GetRequiredService<TimeProvider>(), ct);
        if (!session.IsValid || session.Session?.SessionId != sid || session.Session.IsRestricted) throw new Dev2AccessDenied(401);
        if (!await MaintenanceExecutionVersion.InstalledAsync(db, ct)) throw new Dev2AccessDenied(503);
        var branch = await Branch(a, ct);
        if (branch == null || !await MaintenanceReviewAccess.AllowsAsync(db, a.Actor.UserId, branch.Value, Id(a), ct)
            || !await MaintenanceVerifyAvailability.AllowsAsync(db, a.Actor.UserId, branch.Value, Id(a), ct)) return false;
        // Service rebinds the snapshot under source locks before mutation. Disclosure
        // runs after closure and must not require the previous COMPLETED state.
        if (phase == Dev2AuthorizationPhase.Admission)
        {
            var submitted = await MaintenanceReviewSnapshot.ReadAsync(db, a.Actor.UserId, branch.Value, Id(a), ct);
            if (submitted?.SubmissionAuditId != Input(a).SubmissionAuditId) return false;
        }
        return true;
    }
}
