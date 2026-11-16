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
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

namespace HotelOps.Api.Services.Dev2Authorization;

// Source-owned read contract only. Does not authorize any incident mutation or
// borrow Internal Request permissions as authority over a linked incident.
internal static class IncidentReadContract
{
    internal const string Reference = "DEV3_142A_INCIDENT_BRANCH_READ_V1";
    private const string Policy = "GET api/incidents requires explicit branch; GET api/incidents/{id} derives branch from SQL; "
        + "current HTTPS session, active user/branch access and same-role INCIDENT_VIEW branch grant; GBR165 lead branch/staff reported or assigned; coherent location/asset branch; "
        + "serializable scoped reads and current authorization before disclosure; active INCIDENT_CATEGORY lookup requires the same branch read authority; "
        + "similar-location review issues a protected user/session/location receipt valid for ten minutes after scoped query; "
        + "evidence metadata and private download require current source visibility and exact attachment ownership; no mutation authority";
    internal static IEnumerable<Dev2ActionContract> Contracts => new[] { "A004A-022", "A004A-023", "INCIDENT-CATEGORIES-READ", "INCIDENT-EVIDENCE-LIST", "INCIDENT-EVIDENCE-DOWNLOAD" }.Select(id =>
        new Dev2ActionContract(id, Reference, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
            async (a, ct) => [new("INCIDENT_VIEW", new(Dev2PermissionTargetKind.Branch, await BranchAsync(a, ct)))],
            Dev2PermissionCombination.All, ValidateAsync, (_, _) => false, null, ReadAsync,
            (_, _, _) => Task.FromResult(false), RequiresReadTransaction: true));

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
        var scope = await IncidentReadAccess.ReadAsync(db, a.Actor.UserId, branch, ct);
        if (!scope.Lead && !scope.Staff) return false;
        if (a.Identity.DecisionId is "A004A-023" or "INCIDENT-EVIDENCE-LIST" or "INCIDENT-EVIDENCE-DOWNLOAD"
            && !await scope.Apply(db.Incidents.AsNoTracking()).AnyAsync(i => i.IncidentId == Id(a), ct))
            throw new Dev2AccessDenied(404);
        return true;
    }

    private static async Task<IActionResult> ReadAsync(Dev2BoundAction a, CancellationToken ct)
    {
        if (Db(a).Database.CurrentTransaction == null) throw new Dev2AccessDenied(503);
        var db = Db(a);
        var scope = await IncidentReadAccess.ReadAsync(db, a.Actor.UserId, await BranchAsync(a, ct), ct);
        var query = scope.Apply(db.Incidents.AsNoTracking());
        try
        {
            if (a.Identity.DecisionId == "INCIDENT-EVIDENCE-LIST")
                return await IncidentEvidenceRead.ListAsync(db, Id(a), a.Arguments.GetValueOrDefault("page") is int pageNumber ? pageNumber : 1, ct);
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
            return result == null ? new NotFoundResult() : new OkObjectResult(result);
        }
        catch (ValidationException) { return new BadRequestObjectResult(new ProblemDetails { Status = 400, Title = "Invalid incident filters" }); }
    }
}
