using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Controllers;
using HotelOps.Api.Controllers.Filters;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using HotelOps.Api.Security.Authorization;

namespace HotelOps.Api.Services.Dev2Authorization;

// Source-owned read contract only. Does not authorize any incident mutation or
// borrow Internal Request permissions as authority over a linked incident.
internal static class IncidentReadContract
{
    private static readonly object MaintenanceLinksKey = new();
    private static readonly object AssignmentKey = new();
    private static readonly object RoomBlocksKey = new();
    private static readonly object TriageOptionsKey = new();
    internal const string Reference = "DEV3_142A_INCIDENT_BRANCH_READ_V11";
    private const string Policy = "Related maintenance links require independent current MAINTENANCE_VIEW resource scope and final revalidation; detail endpoints independently authorize progress/history disclosure; Pending Verification projection requires unique current-cycle corrective work and proven committed submission/closed interval; reopened cycles require immutable closure/event/audit provenance and confirmed recurrence discovery for SLA options; prior cycles remain historical; Pending Triage cancellation availability requires escalation storage and native version and current scoped Lead INCIDENT_TRIAGE with final revalidation; Assignment availability requires independently scoped Lead MAINTENANCE_ASSIGN and MAINTENANCE_VIEW, installed receipt/version and final authority revalidation; Initial workflow derives from matching Created/Triage audit/SLA and current eligible assignment, inconsistent history is UNVERIFIED; GET api/incidents requires explicit branch; GET api/incidents/{id} derives branch from SQL; "
        + "Room block availability requires installed explicit scope, open source, native version, current Lead ROOM_BLOCK_MANAGE and independent ROOM_VIEW/INCIDENT_VIEW; current HTTPS session, active user/branch access and same-role INCIDENT_VIEW branch grant; GBR165 lead branch/staff reported or assigned; coherent location/asset branch; "
        + "serializable scoped reads and current authorization before disclosure; active INCIDENT_CATEGORY lookup requires the same branch read authority; "
        + "similar-location review issues a protected user/session/location receipt valid for ten minutes after scoped query; "
        + "evidence metadata and private download require current source visibility and exact attachment ownership; "
        + "UC051 source detail supports current eligible reporter/assignee or branch Lead/OM with independent INCIDENT_VIEW; UC143 list scope unchanged; recorded SLA only, missing or invalid deadlines remain unavailable without planned-time inference; triage options require independent current scoped Lead INCIDENT_TRIAGE with final revalidation; no mutation authority";
    internal static IEnumerable<Dev2ActionContract> Contracts => new[] { "A004A-022", "A004A-023", "INCIDENT-CATEGORIES-READ", "INCIDENT-EVIDENCE-LIST", "INCIDENT-EVIDENCE-DOWNLOAD" }.Select(id =>
        new Dev2ActionContract(id, Reference, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
            async (a, ct) => [new("INCIDENT_VIEW", IsSource(a)
                ? new(Dev2PermissionTargetKind.Resource, Resource: new("incident_source", Id(a)))
                : new(Dev2PermissionTargetKind.Branch, await BranchAsync(a, ct)))],
            Dev2PermissionCombination.All, ValidateAsync, (_, _) => false, (a, _) => new SourceResolver(a), ReadAsync,
            (_, _, _) => Task.FromResult(false), RequiresReadTransaction: true));

    private static bool IsSource(Dev2BoundAction a) => a.Identity.DecisionId is "A004A-023" or "INCIDENT-EVIDENCE-LIST" or "INCIDENT-EVIDENCE-DOWNLOAD";
    private sealed class SourceResolver(Dev2BoundAction a) : IPermissionResourceResolver
    {
        public async Task<VerifiedPermissionResource?> ResolveAsync(string permissionCode, PermissionResource resource, long currentUserId, CancellationToken ct)
        {
            if (!IsSource(a) || permissionCode != "INCIDENT_VIEW" || resource.ResourceType != "incident_source"
                || resource.ResourceId != Id(a) || currentUserId != a.Actor.UserId) return null;
            var row = await Db(a).Incidents.AsNoTracking().Where(i => i.IncidentId == Id(a) && i.Branch.IsActive)
                .Select(i => new { i.BranchId, Own = i.ReportedBy == currentUserId || i.MaintenanceWorks.Any(w => w.AssigneeId == currentUserId
                    && (w.Status == "PENDING" || w.Status == "IN_PROGRESS" || w.Status == "ON_HOLD")
                    && (w.Department == null || w.Department.BranchId == i.BranchId)) }).SingleOrDefaultAsync(ct);
            return row == null ? null : new(row.BranchId, row.Own);
        }
    }

    private static HotelOpsDbContext Db(Dev2BoundAction a) => a.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();
    private static IncidentQueryRequest Input(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("request") as IncidentQueryRequest
        ?? throw new Dev2AccessDenied(403);
    private static long Id(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("id") is long id && id > 0 ? id : throw new Dev2AccessDenied(403);
    private static async Task<long> BranchAsync(Dev2BoundAction a, CancellationToken ct)
    {
        if (a.Identity.DecisionId == "INCIDENT-CATEGORIES-READ")
            return a.Arguments.GetValueOrDefault("branchId") is long branch && branch > 0 ? branch : throw new Dev2AccessDenied(403);
        if (a.Identity.DecisionId == "A004A-022") return Input(a).BranchId is > 0 ? Input(a).BranchId!.Value : throw new Dev2AccessDenied(403);
        var id = Id(a);
        return await Db(a).Incidents.AsNoTracking().Where(i => i.IncidentId == id).Select(i => (long?)i.BranchId).SingleOrDefaultAsync(ct)
            ?? throw new Dev2AccessDenied(404);
    }

    private static async Task<bool> ValidateAsync(Dev2BoundAction a, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        if (a.Identity.ControllerType != typeof(IncidentsController) || a.Identity.HttpMethod != "GET"
            || a.Identity.DecisionId == "A004A-022" && a.Identity.ActionName != "GetIncidents"
            || a.Identity.DecisionId == "A004A-023" && a.Identity.ActionName != "GetIncident"
            || a.Identity.DecisionId == "INCIDENT-CATEGORIES-READ" && a.Identity.ActionName != "GetIncidentCategories"
            || a.Identity.DecisionId == "INCIDENT-EVIDENCE-LIST" && a.Identity.ActionName != "GetIncidentEvidence"
            || a.Identity.DecisionId == "INCIDENT-EVIDENCE-DOWNLOAD" && a.Identity.ActionName != "DownloadIncidentEvidence"
            || a.Identity.DecisionId is not ("A004A-022" or "A004A-023" or "INCIDENT-CATEGORIES-READ" or "INCIDENT-EVIDENCE-LIST" or "INCIDENT-EVIDENCE-DOWNLOAD") || !a.Http.Request.IsHttps) return false;
        var db = Db(a);
        if (phase == Dev2AuthorizationPhase.BeforeDisclosure && db.Database.CurrentTransaction == null) return false;
        // The admission guard first calls us before installing its read transaction.
        // Session validation always requires a caller-owned locking transaction.
        await using var admission = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        await new UserEligibilityLock(db).LockAsync([a.Actor.UserId], ct);
        if (!HotelOpsCookieTicket.TryRead(a.Http.User, out var ticket, out var sid) || ticket == null
            || sid != a.Actor.SessionId || ticket.UserId != a.Actor.UserId || ticket.IsRestricted) return false;
        var valid = await a.Http.RequestServices.GetRequiredService<ISqlSessionService>().ValidateSessionAsync(ticket, ct);
        if (!valid.IsValid || valid.Session?.SessionId != sid || valid.Session.IsRestricted) throw new Dev2AccessDenied(401);
        var now = DateTime.UtcNow; var branch = await BranchAsync(a, ct);
        if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(b => b.UserId == a.Actor.UserId && b.BranchId == branch
            && b.RevokedAt == null && b.GrantedAt <= now && b.Branch.IsActive, ct)) return false;
        var scope = await IncidentReadAccess.ReadAsync(db, a.Actor.UserId, branch, ct,
            sourceRecord: a.Identity.DecisionId is "A004A-023" or "INCIDENT-EVIDENCE-LIST" or "INCIDENT-EVIDENCE-DOWNLOAD");
        if (!scope.Lead && !scope.Staff) return false;
        if (a.Identity.DecisionId is "A004A-023" or "INCIDENT-EVIDENCE-LIST" or "INCIDENT-EVIDENCE-DOWNLOAD"
            && !await scope.Apply(db.Incidents.AsNoTracking()).AnyAsync(i => i.IncidentId == Id(a), ct))
            throw new Dev2AccessDenied(404);
        if (phase == Dev2AuthorizationPhase.BeforeDisclosure && a.Http.Items[MaintenanceLinksKey] is long[] linkedIds)
        {
            var maintenanceScope = await MaintenanceReadAccess.ReadAsync(db,a.Actor.UserId,branch,ct);
            var stillVisible = await maintenanceScope.Apply(db.MaintenanceWorks.AsNoTracking())
                .Where(w=>w.IncidentId==Id(a) && linkedIds.Contains(w.MaintenanceWorkId)).CountAsync(ct);
            if(stillVisible!=linkedIds.Length) return false;
        }
        if (phase == Dev2AuthorizationPhase.BeforeDisclosure && a.Http.Items.ContainsKey(TriageOptionsKey)
            && !await IncidentTriageAccess.AllowsAsync(db, a.Actor.UserId, branch, Id(a), ct)) return false;
        if (phase == Dev2AuthorizationPhase.BeforeDisclosure && a.Http.Items.ContainsKey(AssignmentKey)
            && !await MaintenanceAssignmentAccess.AllowsAsync(db, a.Actor.UserId, branch, Id(a), ct)) return false;
        if (phase == Dev2AuthorizationPhase.BeforeDisclosure && a.Http.Items.ContainsKey(RoomBlocksKey)
            && !await IncidentRoomBlockAccess.AllowsAsync(db,a.Actor.UserId,branch,Id(a),ct)) return false;
        return true;
    }

    private static async Task<IActionResult> ReadAsync(Dev2BoundAction a, CancellationToken ct)
    {
        if (Db(a).Database.CurrentTransaction == null) throw new Dev2AccessDenied(503);
        var db = Db(a);
        var scope = await IncidentReadAccess.ReadAsync(db, a.Actor.UserId, await BranchAsync(a, ct), ct,
            sourceRecord: a.Identity.DecisionId is "A004A-023" or "INCIDENT-EVIDENCE-LIST" or "INCIDENT-EVIDENCE-DOWNLOAD");
        var query = scope.Apply(db.Incidents.AsNoTracking());
        try
        {
            if (a.Identity.DecisionId == "INCIDENT-EVIDENCE-LIST")
                return await IncidentEvidenceRead.ListAsync(db, a.Actor.UserId, Id(a), a.Arguments.GetValueOrDefault("page") is int pageNumber ? pageNumber : 1, ct);
            if (a.Identity.DecisionId == "INCIDENT-EVIDENCE-DOWNLOAD")
                return a.Arguments.GetValueOrDefault("attachment") is long attachment && attachment > 0
                    ? await IncidentEvidenceRead.DownloadAsync(db, a.Http, Id(a), attachment, ct) : new BadRequestResult();
            if (a.Identity.DecisionId == "INCIDENT-CATEGORIES-READ")
                return new OkObjectResult(await db.ReferenceValues.AsNoTracking()
                    .Where(r => r.GroupCode == "INCIDENT_CATEGORY" && r.IsActive)
                    .OrderBy(r => r.SortOrder).ThenBy(r => r.DisplayName).ThenBy(r => r.ReferenceId)
                    .Select(r => new IncidentCategoryOptionDto(r.ReferenceId, r.ValueCode, r.DisplayName)).ToArrayAsync(ct));
            if (a.Identity.DecisionId == "A004A-022")
            {
                var input = Input(a);
                var page = await IncidentService.ListAsync(query, input, ct, db);
                if (input.SimilarOpenOnly)
                    page.SimilarReviewToken = IncidentReviewReceipt.Issue(a.Http.RequestServices.GetRequiredService<IDataProtectionProvider>(),
                        a.Http.RequestServices.GetRequiredService<TimeProvider>(), a.Actor.UserId, a.Actor.SessionId, input);
                return new OkObjectResult(page);
            }
            var result = await IncidentService.DetailAsync(query, Id(a), ct, db);
            if (result != null)
            {
                var maintenanceScope = await MaintenanceReadAccess.ReadAsync(db,a.Actor.UserId,result.BranchId,ct);
                result.MaintenanceWorks = await maintenanceScope.Apply(db.MaintenanceWorks.AsNoTracking())
                    .Where(w=>w.IncidentId==result.IncidentId).OrderBy(w=>w.CreatedAt).ThenBy(w=>w.MaintenanceWorkId)
                    .Select(w=>new IncidentMaintenanceLinkDto(w.MaintenanceWorkId,w.WorkNo,w.Status,w.CreatedAt)).ToArrayAsync(ct);
                a.Http.Items[MaintenanceLinksKey]=result.MaintenanceWorks.Select(w=>w.WorkId).ToArray();
                result.SubmissionWorkflow = await MaintenanceSubmissionWorkflow.ForIncidentAsync(db,result.IncidentId,ct);
                result.Sla = await IncidentSlaRead.ReadAsync(db, result.IncidentId, ct);
                result.AssignmentWorkflow = await IncidentAssignmentWorkflow.ReadAsync(db,
                    await query.SingleAsync(i => i.IncidentId == result.IncidentId, ct), ct);
                result.TriageETag = await IncidentTriageStore.TagAsync(db, result.IncidentId, ct);
                result.CanManageRoomBlocks = result.TriageETag != null && result.Status is not ("CLOSED" or "CANCELLED")
                    && await IncidentRoomBlockService.InstalledAsync(db,ct)
                    && await IncidentRoomBlockAccess.AllowsAsync(db,a.Actor.UserId,result.BranchId,result.IncidentId,ct);
                if(result.CanManageRoomBlocks) a.Http.Items[RoomBlocksKey]=true;
                result.CanCancelTriage = result.TriageETag != null && result.AssignmentWorkflow == "PENDING_TRIAGE"
                    && await IncidentEscalationClosure.InstalledAsync(db,ct)
                    && await IncidentTriageAccess.AllowsAsync(db,a.Actor.UserId,result.BranchId,result.IncidentId,ct);
                if(result.CanCancelTriage) a.Http.Items[TriageOptionsKey]=true;
                result.CanAssign = result.AssignmentWorkflow is "PENDING_ASSIGNMENT" or "ASSIGNED"
                    && result.TriageETag != null && await MaintenanceAssignmentStore.InstalledAsync(db,ct)
                    && await MaintenanceExecutionVersion.InstalledAsync(db,ct)
                    && await MaintenanceAssignmentAccess.AllowsAsync(db,a.Actor.UserId,result.BranchId,result.IncidentId,ct);
                if(result.CanAssign) a.Http.Items[AssignmentKey]=true;
                if (result.TriageETag != null && result.AssignmentWorkflow == "PENDING_TRIAGE" && result.CategoryId != null && result.SeverityId is > 0
                    && result.Sla.UnavailableReason == "INCIDENT_SLA_NOT_RECORDED" && result.Sla.ResponseDeadlineAt == null && result.Sla.ResolutionDeadlineAt == null
                    && await IncidentTriageAccess.AllowsAsync(db, a.Actor.UserId, result.BranchId, result.IncidentId, ct))
                {
                    var cycle = await IncidentReopenStore.ReadAsync(db, result.IncidentId, ct);
                    result.TriageOptions = await IncidentTriageStore.OptionsAsync(db, cycle?.DiscoveredAt ?? result.DiscoveredAt, ct);
                    result.CanTriage = result.TriageOptions.Count > 0;
                    a.Http.Items[TriageOptionsKey] = true;
                }
            }
            return result == null ? new NotFoundResult() : new OkObjectResult(result);
        }
        catch (ValidationException) { return new BadRequestObjectResult(new ProblemDetails { Status = 400, Title = "Invalid incident filters" }); }
    }
}
