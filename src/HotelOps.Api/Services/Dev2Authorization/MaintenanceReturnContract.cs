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

internal static class MaintenanceReturnContract
{
    private const string Policy = "UC059 Return only: HTTPS current unrestricted SQL session; current branch Maintenance Lead with MAINTENANCE_REVIEW and independent MAINTENANCE_VIEW; never current assignee, performer or previous submitter; exact request/root binding; Serializable caller transaction, native If-Match and COMPLETED source; restore eligible executor responsibility without starting effort; audit and notification intent atomically; no Verify admission";
    internal static IEnumerable<Dev2ActionContract> Contracts => [new("A004A-032", "DEV3_142A_MAINTENANCE_RETURN_V1",
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
        (a, ct) => Task.FromResult<IReadOnlyList<Dev2PermissionRequirement>>([
            new("MAINTENANCE_REVIEW", new(Dev2PermissionTargetKind.Resource, Resource: new("maintenance_work", Id(a)))),
            new("MAINTENANCE_VIEW", new(Dev2PermissionTargetKind.Resource, Resource: new("maintenance_work", Id(a))))]),
        Dev2PermissionCombination.All, Validate,
        (a, call) => call.Controller == "MaintenanceWorks" && call.Action == "ReturnWork" && call.RootId == Id(a)
            && ReferenceEquals(call.Input, Input(a)), (a, _) => new Resolver(a), null, Validate, RequiresActionTransaction: true)];

    private static HotelOpsDbContext Db(Dev2BoundAction a) => a.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();
    private static long Id(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("id") is long id && id > 0 ? id : throw new Dev2AccessDenied(404);
    private static ReturnMaintenanceWorkRequest Input(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("request") as ReturnMaintenanceWorkRequest ?? throw new Dev2AccessDenied(403);
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
        if (!a.Http.Request.IsHttps || a.Identity.DecisionId != "A004A-032" || a.Identity.ControllerType != typeof(MaintenanceWorksController)
            || a.Identity.HttpMethod != "POST" || a.Identity.ActionName != "ReturnWork" || Input(a).ActorUserId != a.Actor.UserId) return false;
        var db = Db(a);
        if (phase != Dev2AuthorizationPhase.Admission && db.Database.CurrentTransaction == null) return false;
        await using var admission = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return false;
        var owner = await db.MaintenanceWorks.AsNoTracking().Where(w => w.MaintenanceWorkId == Id(a)).Select(w => w.AssigneeId).SingleOrDefaultAsync(ct);
        if (owner is not > 0) return false;
        await new UserEligibilityLock(db).LockAsync([a.Actor.UserId, owner.Value], ct);
        if (!await db.MaintenanceWorks.AsNoTracking().AnyAsync(w => w.MaintenanceWorkId == Id(a) && w.AssigneeId == owner, ct)) return false;
        if (!HotelOpsCookieTicket.TryRead(a.Http.User, out var ticket, out var sid) || ticket == null || ticket.IsRestricted
            || ticket.UserId != a.Actor.UserId || sid != a.Actor.SessionId) return false;
        var session = await a.Http.RequestServices.GetRequiredService<ISqlSessionService>().ValidateSessionAsync(ticket, ct);
        if (!session.IsValid || session.Session?.SessionId != sid || session.Session.IsRestricted) throw new Dev2AccessDenied(401);
        if (!await MaintenanceExecutionVersion.InstalledAsync(db, ct)) throw new Dev2AccessDenied(503);
        var branch = await Branch(a, ct);
        return branch != null && await MaintenanceReviewAccess.AllowsAsync(db, a.Actor.UserId, branch.Value, Id(a), ct);
    }
}
