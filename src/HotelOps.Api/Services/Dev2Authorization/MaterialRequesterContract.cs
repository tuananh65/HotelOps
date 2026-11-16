using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.MaterialRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Dev2Authorization;

internal static class MaterialRequesterContract
{
    private static readonly IReadOnlyDictionary<string, string> Actions = new Dictionary<string, string>
    { ["A004A-044"] = "Edit", ["A004A-045"] = "Submit", ["A004A-049"] = "Cancel" };
    private const string Policy = "HTTPS current unrestricted SQL session; principal is existing requester; "
        + "current approved creator role MR_CREATE and independent MR_VIEW with branch access; "
        + "ordered responsibility locks; exact action/input/root binding; Serializable through disclosure; "
        + "existing state/version/no-issue and reapproval rules; current approver mapping on submission";
    internal static IEnumerable<Dev2ActionContract> Contracts => Actions.Keys.Select(id => new Dev2ActionContract(id,
        "DEV3_142A_MATERIAL_REQUESTER_V1", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
        (a, _) => Task.FromResult<IReadOnlyList<Dev2PermissionRequirement>>(
            [new("MR_CREATE", new(Dev2PermissionTargetKind.Resource, Resource: new("material_request", Id(a)))),
             new("MR_VIEW", new(Dev2PermissionTargetKind.Resource, Resource: new("material_request", Id(a))))]),
        Dev2PermissionCombination.All, ValidateAsync,
        (a, invocation) => invocation.Controller == "MaterialRequests" && invocation.Action == a.Identity.ActionName
            && invocation.RootId == Id(a) && ReferenceEquals(invocation.Input, Input(a)),
        (a, _) => new Resolver(a), null, ValidateAsync, RequiresActionTransaction: true));

    private static HotelOpsDbContext Db(Dev2BoundAction a) => a.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();
    private static long Id(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("id") is long id && id > 0 ? id : throw new Dev2AccessDenied(404);
    private static object Input(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("input") ?? throw new Dev2AccessDenied(400);
    private sealed class Resolver(Dev2BoundAction a) : IPermissionResourceResolver
    {
        public async Task<VerifiedPermissionResource?> ResolveAsync(string permission, PermissionResource resource, long user, CancellationToken ct)
        {
            if (permission is not ("MR_CREATE" or "MR_VIEW") || user != a.Actor.UserId
                || resource.ResourceType != "material_request" || resource.ResourceId != Id(a)) return null;
            var row = await Db(a).MaterialRequests.AsNoTracking().Where(r => r.RequestId == Id(a))
                .Select(r => new { r.BranchId, r.RequesterId }).SingleOrDefaultAsync(ct);
            return row?.RequesterId == user ? new(row.BranchId, true) : null;
        }
    }

    private static async Task<bool> ValidateAsync(Dev2BoundAction a, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        if (!a.Http.Request.IsHttps || a.Identity.ControllerType != typeof(MaterialRequestsController)
            || !Actions.TryGetValue(a.Identity.DecisionId, out var action) || a.Identity.ActionName != action
            || a.Identity.HttpMethod != (action == "Edit" ? "PUT" : "POST")) return false;
        var input = Input(a);
        var claim = input switch { MaterialRequestWrite w when action == "Edit" => w.ActorUserId,
            MaterialCommand c when action != "Edit" => c.ActorUserId, _ => 0 };
        if (claim != a.Actor.UserId) return false;
        var db = Db(a); var id = Id(a);
        if (phase != Dev2AuthorizationPhase.Admission && db.Database.CurrentTransaction == null) return false;
        var hint = await db.MaterialRequests.AsNoTracking().SingleOrDefaultAsync(r => r.RequestId == id, ct) ?? throw new Dev2AccessDenied(404);
        if (hint.RequesterId != a.Actor.UserId) return false;
        var selected = input switch { MaterialRequestWrite w => w.AssignedApproverUserId,
            MaterialCommand c when action == "Submit" => c.AssignedApproverUserId, _ => null };
        if (selected is <= 0) return false;
        if (action == "Edit" && selected == null)
            selected = await db.MaterialRequestApprovalCycles.AsNoTracking().Where(c => c.RequestId == id)
                .OrderByDescending(c => c.RevisionNo).Select(c => (long?)c.AssignedApproverId).FirstOrDefaultAsync(ct);
        await using var admission = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return false;
        var users = await new UserEligibilityLock(db).LockAsync(selected is long approver ? [a.Actor.UserId, approver] : [a.Actor.UserId], ct);
        if (!users.TryGetValue(a.Actor.UserId, out var actor) || actor == null || actor.DeletedAt != null
            || actor.AccountStatus != "ACTIVE" || actor.EmploymentStatus != "ACTIVE") return false;
        if (!HotelOpsCookieTicket.TryRead(a.Http.User, out var ticket, out var sid) || ticket == null || ticket.IsRestricted
            || ticket.UserId != a.Actor.UserId || sid != a.Actor.SessionId) return false;
        var session = await a.Http.RequestServices.GetRequiredService<ISqlSessionService>().ValidateSessionAsync(ticket, ct);
        if (!session.IsValid || session.Session?.SessionId != sid || session.Session.IsRestricted) throw new Dev2AccessDenied(401);
        var request = await db.MaterialRequests.AsNoTracking().SingleOrDefaultAsync(r => r.RequestId == id, ct);
        if (request == null || request.RequesterId != a.Actor.UserId || request.BranchId != hint.BranchId) return false;
        // The fallback may have changed before the ordered user locks were acquired.
        // Never let the service acquire a different approver lock out of order.
        if (input is MaterialRequestWrite { AssignedApproverUserId: null })
        {
            var lockedFallback = await db.MaterialRequestApprovalCycles.AsNoTracking().Where(c => c.RequestId == id)
                .OrderByDescending(c => c.RevisionNo).Select(c => (long?)c.AssignedApproverId).FirstOrDefaultAsync(ct);
            if (lockedFallback != selected) throw new Dev2AccessDenied(409);
        }
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!(await MaterialCreationAccess.BranchesAsync(db, a.Actor.UserId, now, ct)).Any(b => b.BranchId == request.BranchId)) return false;
        var scope = await MaterialRequestReadAccess.ReadAsync(db, a.Actor.UserId, request.BranchId, now, ct);
        if (!await scope.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(r => r.RequestId == id, ct)
            || !await MaterialReadProvenance.ValidAsync(db, id, request.BranchId, ct)) return false;
        if (input is MaterialRequestWrite write)
        {
            if (write.BranchId != request.BranchId || write.InternalRequestId != null || write.RequestLink != null) return false;
            if (write.IncidentId is long incident)
            {
                var incidents = await IncidentReadAccess.ReadAsync(db, a.Actor.UserId, request.BranchId, ct);
                if (!await incidents.Apply(db.Incidents.AsNoTracking()).AnyAsync(i => i.IncidentId == incident, ct)) return false;
            }
        }
        if (phase == Dev2AuthorizationPhase.BeforeDisclosure && action != "Cancel" && request.Status == "PENDING_APPROVAL")
        {
            var currentApprover = await db.MaterialRequestApprovalCycles.AsNoTracking().Where(c => c.RequestId == id)
                .OrderByDescending(c => c.RevisionNo).Select(c => (long?)c.AssignedApproverId).FirstOrDefaultAsync(ct);
            if (currentApprover == null || currentApprover != selected) return false;
            try { await MaterialApprovalAuthority.ValidateAsync(db, request, currentApprover.Value, ct); }
            catch (ValidationException) { return false; }
        }
        return true;
    }
}
