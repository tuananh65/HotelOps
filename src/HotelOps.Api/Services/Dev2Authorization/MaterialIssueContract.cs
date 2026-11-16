using System.Data;
using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.MaterialRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Dev2Authorization;

internal static class MaterialIssueContract
{
    private const string Policy = "HTTPS unrestricted SQL session; active STOREKEEPER in actual warehouse branch; "
        + "MR_FULFILL plus STOCK_ISSUE_POST plus INVENTORY_VIEW and independent MR_VIEW; "
        + "issuer differs from requester and current approved-cycle approver; exact input/principal/request binding; "
        + "Serializable action through stock posting and disclosure with version/stock/reconciliation/audit guards";
    internal static IEnumerable<Dev2ActionContract> Contracts => [new("A004A-051", "DEV3_142A_MATERIAL_ISSUE_V1",
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))), async (a, ct) =>
        {
            var branch = await Db(a).MaterialRequests.AsNoTracking().Where(r => r.RequestId == Id(a)).Select(r => r.BranchId).SingleAsync(ct);
            return new Dev2PermissionRequirement[] {
                new("MR_FULFILL", new(Dev2PermissionTargetKind.Branch, branch)),
                new("STOCK_ISSUE_POST", new(Dev2PermissionTargetKind.Branch, branch)),
                new("INVENTORY_VIEW", new(Dev2PermissionTargetKind.Branch, branch)),
                new("MR_VIEW", new(Dev2PermissionTargetKind.Resource, Resource: new("material_request", Id(a)))) };
        }, Dev2PermissionCombination.All, ValidateAsync,
        (a, call) => call.Controller == "MaterialRequests" && call.Action == "Issue" && call.RootId == Id(a) && ReferenceEquals(call.Input, Input(a)),
        (a, _) => new Resolver(a), null, ValidateAsync, RequiresActionTransaction: true)];

    private static HotelOpsDbContext Db(Dev2BoundAction a) => a.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();
    private static long Id(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("id") is long id && id > 0 ? id : throw new Dev2AccessDenied(404);
    private static MaterialIssue Input(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("input") as MaterialIssue ?? throw new Dev2AccessDenied(400);
    private sealed class Resolver(Dev2BoundAction a) : IPermissionResourceResolver
    {
        public async Task<VerifiedPermissionResource?> ResolveAsync(string code, PermissionResource resource, long user, CancellationToken ct)
        {
            if (code != "MR_VIEW" || user != a.Actor.UserId || resource.ResourceType != "material_request" || resource.ResourceId != Id(a)) return null;
            var request = await Db(a).MaterialRequests.AsNoTracking().Where(r => r.RequestId == Id(a)).Select(r => new { r.BranchId, r.RequesterId }).SingleOrDefaultAsync(ct);
            return request == null ? null : new(request.BranchId, request.RequesterId == user);
        }
    }

    private static async Task<bool> ValidateAsync(Dev2BoundAction a, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        if (!a.Http.Request.IsHttps || a.Identity.ControllerType != typeof(MaterialRequestsController)
            || a.Identity.DecisionId != "A004A-051" || a.Identity.ActionName != "Issue" || a.Identity.HttpMethod != "POST"
            || Input(a).ActorUserId != a.Actor.UserId) return false;
        var db = Db(a); var id = Id(a);
        if (phase != Dev2AuthorizationPhase.Admission && db.Database.CurrentTransaction == null) return false;
        var hint = await db.MaterialRequests.AsNoTracking().Where(r => r.RequestId == id).Select(r => new { r.RequesterId, r.BranchId }).SingleOrDefaultAsync(ct)
            ?? throw new Dev2AccessDenied(404);
        await using var admission = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return false;
        var users = await new UserEligibilityLock(db).LockAsync([a.Actor.UserId, hint.RequesterId], ct);
        if (!users.TryGetValue(a.Actor.UserId, out var actor) || actor is not { DeletedAt: null, AccountStatus: "ACTIVE", EmploymentStatus: "ACTIVE" }) return false;
        if (!HotelOpsCookieTicket.TryRead(a.Http.User, out var ticket, out var sid) || ticket == null || ticket.IsRestricted
            || ticket.UserId != a.Actor.UserId || sid != a.Actor.SessionId) return false;
        var session = await Dev2SessionValidation.ValidateAsync(db, ticket,
            a.Http.RequestServices.GetRequiredService<TimeProvider>(), ct);
        if (!session.IsValid || session.Session?.SessionId != sid || session.Session.IsRestricted) throw new Dev2AccessDenied(401);
        var request = await db.MaterialRequests.AsNoTracking().SingleOrDefaultAsync(r => r.RequestId == id, ct);
        if (request == null || request.RequesterId != hint.RequesterId || request.BranchId != hint.BranchId || request.RequesterId == a.Actor.UserId) return false;
        if (!await db.Warehouses.AsNoTracking().AnyAsync(w => w.WarehouseId == request.SourceWarehouseId && w.IsActive && w.BranchId == request.BranchId, ct)) return false;
        var cycle = await db.MaterialRequestApprovalCycles.AsNoTracking().Where(c => c.RequestId == id).OrderByDescending(c => c.RevisionNo).FirstOrDefaultAsync(ct);
        if (cycle == null || cycle.Status != "APPROVED" || cycle.AssignedApproverId == a.Actor.UserId) return false;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == a.Actor.UserId && r.Role.RoleCode == "STOREKEEPER"
            && r.IsActive && r.Role.IsActive && r.BranchId == request.BranchId && r.Branch!.IsActive
            && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && db.UserBranchAccesses.Any(b => b.UserId == a.Actor.UserId && b.BranchId == request.BranchId && b.GrantedAt <= now && b.RevokedAt == null), ct)) return false;
        var scope = await MaterialRequestReadAccess.ReadAsync(db, a.Actor.UserId, request.BranchId, now, ct);
        return await scope.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(r => r.RequestId == id, ct)
            && await MaterialReadProvenance.ValidAsync(db, id, request.BranchId, ct);
    }
}
