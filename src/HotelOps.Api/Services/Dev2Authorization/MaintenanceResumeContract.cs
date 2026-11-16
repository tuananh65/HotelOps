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

internal static class MaintenanceResumeContract
{
    private const string Policy = "UC054/055/058 Start/Hold/Resume/Submit: HTTPS current unrestricted SQL session, active assigned MAINTENANCE_STAFF, same-role MAINTENANCE_EXECUTE plus independent MAINTENANCE_VIEW, current branch and corrective source scope; exact request/root binding; trusted rowversion If-Match under source locks; Serializable transaction; Working workday and single interval; no material disclosure or other action admission";
    internal static IEnumerable<Dev2ActionContract> Contracts => new[] { "A004A-026", "A004A-027", "A004A-028", "A004A-030" }.Select(id => new Dev2ActionContract(id, "DEV3_142A_MAINTENANCE_EXECUTION_V4",
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
        (a, ct) => Task.FromResult<IReadOnlyList<Dev2PermissionRequirement>>([
            new("MAINTENANCE_EXECUTE", new(Dev2PermissionTargetKind.Resource, Resource: new("maintenance_work", Id(a)))),
            new("MAINTENANCE_VIEW", new(Dev2PermissionTargetKind.Resource, Resource: new("maintenance_work", Id(a))))]),
        Dev2PermissionCombination.All, Validate,
        (a, call) => call.Controller == "MaintenanceWorks" && call.Action == Action(a) && call.RootId == Id(a)
            && ReferenceEquals(call.Input, Input(a)), (a, _) => new Resolver(a), null, Validate, RequiresActionTransaction: true));
    private sealed class Resolver(Dev2BoundAction a) : IPermissionResourceResolver
    {
        public async Task<VerifiedPermissionResource?> ResolveAsync(string permission, PermissionResource resource, long user, CancellationToken ct)
        {
            if (user != a.Actor.UserId || permission is not ("MAINTENANCE_EXECUTE" or "MAINTENANCE_VIEW")
                || resource.ResourceType != "maintenance_work" || resource.ResourceId != Id(a)) return null;
            var owner = await Db(a).MaintenanceWorks.AsNoTracking().Where(w => w.MaintenanceWorkId == Id(a)).Select(w => w.AssigneeId).SingleOrDefaultAsync(ct);
            return new(await Branch(a, ct), owner == user);
        }
    }
    private static HotelOpsDbContext Db(Dev2BoundAction a) => a.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();
    private static long Id(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("id") is long id && id > 0 ? id : throw new Dev2AccessDenied(404);
    private static string Action(Dev2BoundAction a) => a.Identity.DecisionId switch
    { "A004A-026" => "StartWork", "A004A-027" => "HoldWork", "A004A-028" => "ResumeWork", "A004A-030" => "SubmitResult", _ => throw new Dev2AccessDenied(403) };
    private static MaintenanceActorRequest Input(Dev2BoundAction a)
        => (a.Identity.DecisionId, a.Arguments.GetValueOrDefault("request")) switch
        {
            ("A004A-026", StartMaintenanceWorkRequest input) => input,
            ("A004A-027", HoldMaintenanceWorkRequest input) => input,
            ("A004A-028", ResumeMaintenanceWorkRequest input) => input,
            ("A004A-030", SubmitMaintenanceResultRequest input) => input,
            _ => throw new Dev2AccessDenied(403)
        };
    private static async Task<long> Branch(Dev2BoundAction a, CancellationToken ct)
        => await Db(a).MaintenanceWorks.AsNoTracking().Where(w => w.MaintenanceWorkId == Id(a) && w.Incident != null)
            .Select(w => (long?)w.Incident!.BranchId).SingleOrDefaultAsync(ct) ?? throw new Dev2AccessDenied(404);
    private static async Task<bool> Validate(Dev2BoundAction a, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        if (!a.Http.Request.IsHttps || a.Identity.ControllerType != typeof(MaintenanceWorksController)
            || a.Identity.HttpMethod != "POST" || a.Identity.ActionName != Action(a)
            || Input(a).ActorUserId != a.Actor.UserId) return false;
        var db = Db(a);
        if (phase != Dev2AuthorizationPhase.Admission && db.Database.CurrentTransaction == null) return false;
        await using var admission = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return false;
        if (a.Identity.DecisionId == "A004A-030")
            await a.Http.RequestServices.GetRequiredService<HotelOps.Api.Services.Inventory.IInventoryIssueCapacity>().AcquireCatalog(ct);
        await new UserEligibilityLock(db).LockAsync([a.Actor.UserId], ct);
        if (!HotelOpsCookieTicket.TryRead(a.Http.User, out var ticket, out var sid) || ticket == null || ticket.IsRestricted
            || ticket.UserId != a.Actor.UserId || sid != a.Actor.SessionId) return false;
        var session = await a.Http.RequestServices.GetRequiredService<ISqlSessionService>().ValidateSessionAsync(ticket, ct);
        if (!session.IsValid || session.Session?.SessionId != sid || session.Session.IsRestricted) throw new Dev2AccessDenied(401);
        if (!await MaintenanceExecutionVersion.InstalledAsync(db, ct)) throw new Dev2AccessDenied(503);
        return await MaintenanceExecutionAccess.AllowsAsync(db, a.Actor.UserId, await Branch(a, ct), Id(a), ct);
    }
}
