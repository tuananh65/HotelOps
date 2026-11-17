using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Assets;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.DTOs.Replenishments;
using HotelOps.Api.Services.Assets;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.MaterialRequests;
using HotelOps.Api.Services.Replenishments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Dev2Authorization;

public static partial class Dev2DemoBindings
{
    internal static bool ExpandedEnabled => string.Equals(Environment.GetEnvironmentVariable("DEV2_DEMO_AUTHORITY_V1"), Dev2ExpandedPolicyIdentity.Sha256, StringComparison.Ordinal);
    public static IReadOnlyCollection<string> BoundDecisionIds => Dev2ExpandedCatalog.Actions.Keys;
    public static IReadOnlyList<string> PermissionCodes(string id) => Dev2ExpandedCatalog.Actions.TryGetValue(id, out var plan) ? plan.Codes : throw new Dev2AccessDenied(403);
    internal static bool DemoReference(string reference) => reference is Dev2DemoPolicyIdentity.Version or Dev2ExpandedPolicyIdentity.Version;
    private static Dev2ExpandedAction Plan(Dev2BoundAction a) => Dev2ExpandedCatalog.Actions.TryGetValue(a.Identity.DecisionId, out var p) ? p : throw new Dev2AccessDenied(403);
    internal static object? BoundInput(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("request") ?? a.Arguments.GetValueOrDefault("input") ?? a.Arguments.GetValueOrDefault("page");
    private static long? Field(object? input, string name) => input is null ? null : Number(input, name);
    private static long Arg(Dev2BoundAction a, string name) => a.Arguments.GetValueOrDefault(name) is long id && id > 0 ? id : throw new Dev2AccessDenied(403);
    private static long Required(long? value) => value is > 0 ? value.Value : throw new Dev2AccessDenied(403);

    internal static Dev2ActionContract? FindExpanded(Controllers.Filters.Dev2KnownAction identity, HttpContext http)
    {
        if (!ExpandedEnabled || !Eligible(http) || !Dev2ExpandedCatalog.Actions.TryGetValue(identity.DecisionId, out var p)
            || p.Controller != identity.ControllerType.Name || p.Action != identity.ActionName || p.Method != identity.HttpMethod) return null;
        return new(p.Id, Dev2ExpandedPolicyIdentity.Version, Dev2ExpandedPolicyIdentity.Sha256,
            async (a, ct) =>
            {
                var branch = await ExpandedBranchAsync(a, ct);
                return p.Codes.Select(code => new Dev2PermissionRequirement(code, new(Dev2PermissionTargetKind.Branch, branch))).ToArray();
            },
            Dev2PermissionCombination.All, ValidateExpandedAsync,
            (a, call) => call.Controller + "Controller" == p.Controller && call.Action == p.Action
                && call.RootId == (p.Argument is null ? null : Arg(a, p.Argument)) && ReferenceEquals(call.Input, BoundInput(a)),
            null, p.Method == "GET" ? ReadExpandedAsync : null, TransactionAsync);
    }

    private static async Task<long> ExpandedBranchAsync(Dev2BoundAction a, CancellationToken ct)
    {
        var p = Plan(a); var input = BoundInput(a); var db = Db(a);
        if (p.Kind == "branch-input") return Required(Field(input, "BranchId"));
        if (p.Kind == "branch-query")
        {
            var raw = a.Http.Request.Query["branchId"];
            return raw.Count == 1 && long.TryParse(raw[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var branch) && branch > 0 ? branch : throw new Dev2AccessDenied(403);
        }
        if (p.Kind == "warehouse-input")
        {
            var warehouse = Required(Field(input, "WarehouseId"));
            return await db.Warehouses.AsNoTracking().Where(x => x.WarehouseId == warehouse && x.IsActive).Select(x => (long?)x.BranchId).SingleOrDefaultAsync(ct) ?? throw new Dev2AccessDenied(403);
        }
        var kind = p.Kind == "incident-input" ? "incident" : p.Kind;
        var id = p.Kind == "incident-input" ? Required(Field(input, "IncidentId")) : Arg(a, p.Argument!);
        return (await new Dev2ResourceFactReader(db).ReadAsync(kind, id, null, ct))?.BranchId ?? throw new Dev2AccessDenied(403);
    }

    private static async Task<bool> ValidateExpandedAsync(Dev2BoundAction a, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        if (!ExpandedEnabled || !Eligible(a.Http)) return false;
        var db = Db(a); await VerifyDatabaseAsync(db, ct);
        var p = Plan(a); var input = BoundInput(a); var branch = await ExpandedBranchAsync(a, ct);
        if (p.Method != "GET" && Field(input, p.Id == "A004A-021" ? "ReportedBy" : "ActorUserId") != a.Actor.UserId) return false;
        if (Field(input, "BranchId") is long claimed && claimed != branch) return false;
        if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(x => x.UserId == a.Actor.UserId && x.BranchId == branch && x.RevokedAt == null && x.Branch.IsActive, ct)) return false;
        foreach (var name in new[] { "RoomId", "ReceivingRoomId" })
            if (Field(input, name) is long room && !await db.Rooms.AsNoTracking().AnyAsync(x => x.RoomId == room && x.BranchId == branch && x.IsActive && x.Floor.IsActive && x.Floor.BranchId == branch && (x.AreaId == null || x.Area != null && x.Area.BranchId == branch && x.Area.IsActive), ct)) return false;
        foreach (var name in new[] { "AreaId", "ReceivingAreaId" })
            if (Field(input, name) is long area && !await db.Areas.AsNoTracking().AnyAsync(x => x.AreaId == area && x.BranchId == branch && x.IsActive, ct)) return false;
        foreach (var name in new[] { "DepartmentId", "CurrentDepartmentId", "ToDepartmentId" })
            if (Field(input, name) is long department && !await db.Departments.AsNoTracking().AnyAsync(x => x.DepartmentId == department && x.BranchId == branch && x.IsActive, ct)) return false;
        foreach (var name in new[] { "WarehouseId", "SourceWarehouseId" })
            if (Field(input, name) is long warehouse && !await db.Warehouses.AsNoTracking().AnyAsync(x => x.WarehouseId == warehouse && x.BranchId == branch && x.IsActive, ct)) return false;
        if (Field(input, "IncidentId") is long incident && !await db.Incidents.AsNoTracking().AnyAsync(x => x.IncidentId == incident && x.BranchId == branch, ct)) return false;
        if (Field(input, "AssetId") is long asset && !await db.Assets.AsNoTracking().AnyAsync(x => x.AssetId == asset && x.BranchId == branch && (x.LifecycleStatus == "IN_USE" || x.LifecycleStatus == "UNDER_REPAIR")
            && (Field(input, "RoomId") == null || x.CurrentRoomId == Field(input, "RoomId")) && (Field(input, "AreaId") == null || x.CurrentAreaId == Field(input, "AreaId")), ct)) return false;
        if (p.Method != "GET")
            foreach (var name in new[] { "AssigneeId", "AssignedApproverUserId", "ToUserId", "CustodianUserId" })
                if (Field(input, name) is long target && !await db.UserBranchAccesses.AsNoTracking().AnyAsync(x => x.UserId == target && x.BranchId == branch && x.RevokedAt == null && x.User.DeletedAt == null && x.User.AccountStatus == "ACTIVE" && x.User.EmploymentStatus == "ACTIVE", ct)) return false;
        if (!await ConsistentGraphAsync(a, branch, ct)) return false;
        if (p.Argument is null) return true;
        var facts = await new Dev2ResourceFactReader(db).ReadAsync(p.Kind, Arg(a, p.Argument), null, ct);
        if (facts is null || facts.BranchId != branch) return false;
        if (p.Id is "A004A-004" or "A004A-005") return facts.TargetUserId == a.Actor.UserId;
        if (p.Id is "A004A-026" or "A004A-027" or "A004A-028" or "A004A-029" or "A004A-030") return facts.OwnerUserId == a.Actor.UserId;
        if (p.Id is "A004A-031" or "A004A-032") return facts.OwnerUserId != a.Actor.UserId;
        if (p.Id is "A004A-044" or "A004A-045" or "A004A-049") return facts.OwnerUserId == a.Actor.UserId;
        if (p.Id is "A004A-046" or "A004A-047" or "A004A-048" or "A004A-050")
        {
            var submitter = await db.MaterialRequestApprovalCycles.AsNoTracking().Where(x => x.RequestId == facts.Id).OrderByDescending(x => x.RevisionNo).Select(x => (long?)x.SubmittedBy).FirstOrDefaultAsync(ct);
            return facts.TargetUserId == a.Actor.UserId && facts.OwnerUserId != a.Actor.UserId && submitter is not null && submitter != a.Actor.UserId;
        }
        if (p.Id == "A004A-051") return facts.TargetUserId != a.Actor.UserId;
        return true;
    }

    private static async Task<bool> ConsistentGraphAsync(Dev2BoundAction a, long branch, CancellationToken ct)
    {
        var db = Db(a); var p = Plan(a); var input = BoundInput(a);
        // List/statistics services expose location names: reject inconsistent branch ancestry before querying their DTOs.
        if (p.Kind == "asset" || p.Id is "A004A-008" or "A004A-009")
        {
            var id = p.Kind == "asset" ? Arg(a, p.Argument!) : (long?)null;
            if (await db.Assets.AsNoTracking().Where(x => x.BranchId == branch && (id == null || x.AssetId == id)).AnyAsync(x => x.CurrentRoom != null && x.CurrentRoom.BranchId != branch || x.CurrentArea != null && x.CurrentArea.BranchId != branch || x.CurrentDepartment != null && x.CurrentDepartment.BranchId != branch, ct)) return false;
        }
        if (p.Kind == "asset-handover" || p.Id == "A004A-002")
        {
            var id = Arg(a, p.Argument!);
            if (await db.AssetHandovers.AsNoTracking().Where(x => p.Id == "A004A-002" ? x.AssetId == id : x.HandoverId == id).AnyAsync(x => x.ToDepartment != null && x.ToDepartment.BranchId != branch || x.FromDepartment != null && x.FromDepartment.BranchId != branch, ct)) return false;
        }
        if (p.Id == "A004A-007")
        {
            var id = Arg(a, "assetId");
            if (await db.MaintenanceWorks.AsNoTracking().AnyAsync(x => x.Incident != null && x.Incident.AssetId == id && (x.Incident.BranchId != branch || x.Department != null && x.Department.BranchId != branch), ct)) return false;
            if (await db.PreventiveMaintenanceRuns.AsNoTracking().Where(x => x.Schedule.AssetId == id).AnyAsync(x => x.Schedule.Room != null && x.Schedule.Room.BranchId != branch || x.Schedule.Area != null && x.Schedule.Area.BranchId != branch || x.Schedule.AssignedDepartment != null && x.Schedule.AssignedDepartment.BranchId != branch || x.MaintenanceWork != null && (x.MaintenanceWork.IncidentId == null || x.MaintenanceWork.Incident!.BranchId != branch), ct)) return false;
        }
        if (p.Id is "A004A-007" or "A004A-012")
        {
            var id = Arg(a, p.Argument!);
            if (await db.AssetConditionHistories.AsNoTracking().AnyAsync(x => x.AssetId == id && x.RelatedIncident != null && x.RelatedIncident.BranchId != branch, ct)) return false;
        }
        if (p.Kind == "incident" || p.Kind == "maintenance-work" || p.Kind == "incident-input" || p.Id == "A004A-022")
        {
            var id = p.Id == "A004A-022" ? (long?)null : p.Kind == "incident-input" ? Required(Field(input, "IncidentId")) : Arg(a, p.Argument!);
            var incident = p.Kind == "maintenance-work" ? await db.MaintenanceWorks.Where(x => x.MaintenanceWorkId == id).Select(x => x.IncidentId).SingleOrDefaultAsync(ct) : id;
            if (p.Kind == "maintenance-work" && incident is null) return false;
            if (await db.Incidents.AsNoTracking().Where(x => x.BranchId == branch && (incident == null || x.IncidentId == incident)).AnyAsync(x => x.Room != null && x.Room.BranchId != branch || x.Area != null && x.Area.BranchId != branch || x.Asset != null && x.Asset.BranchId != branch, ct)) return false;
            if (p.Kind is "maintenance-work" or "incident-input")
                if (await db.MaintenanceWorks.AsNoTracking().Where(x => x.IncidentId == incident && (p.Kind != "maintenance-work" || x.MaintenanceWorkId == id)).AnyAsync(x => x.DepartmentId != null && x.Department!.BranchId != branch, ct)) return false;
        }
        if (p.Kind == "material-request")
        {
            var id = Arg(a, p.Argument!);
            if (await db.MaterialRequests.AsNoTracking().Where(x => x.RequestId == id).AnyAsync(x => x.Department.BranchId != branch || x.SourceWarehouse != null && x.SourceWarehouse.BranchId != branch || x.ReceivingRoom != null && x.ReceivingRoom.BranchId != branch || x.ReceivingArea != null && x.ReceivingArea.BranchId != branch || x.Incident != null && x.Incident.BranchId != branch, ct)) return false;
            if (await db.MaterialFulfillments.AsNoTracking().Where(x => x.RequestId == id).AnyAsync(x => x.Warehouse.BranchId != branch || x.StockDocument != null && x.StockDocument.SourceWarehouse != null && x.StockDocument.SourceWarehouse.BranchId != branch, ct)) return false;
            if (await db.MaterialFulfillmentLines.AsNoTracking().Where(x => x.Fulfillment.RequestId == id).AnyAsync(x => x.RequestLine.RequestId != id || x.StockDocumentLine != null && (x.StockDocumentLine.DocumentId != x.Fulfillment.StockDocumentId || x.StockDocumentLine.Document.SourceWarehouseId != x.Fulfillment.WarehouseId), ct)) return false;
            var issueDocs = db.MaterialFulfillments.Where(x => x.RequestId == id && x.StockDocumentId != null).Select(x => x.StockDocumentId!.Value);
            if (await db.StockReturnLineDetails.AsNoTracking().Where(x => issueDocs.Contains(x.OriginalIssueLine.DocumentId)).AnyAsync(x => x.StockDocumentLine.Document.DestinationWarehouse == null || x.StockDocumentLine.Document.DestinationWarehouse.BranchId != branch, ct)) return false;
            if (p.Id == "A004A-039")
            {
                var batchIds = db.MaterialFulfillments.Where(x => x.RequestId == id).Select(x => x.FulfillmentId.ToString());
                var docIds = issueDocs.Select(x => x.ToString());
                if (await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "material_requests" && x.EntityId == id.ToString() || x.EntityType == "material_fulfillments" && batchIds.Contains(x.EntityId) || x.EntityType == "stock_documents" && docIds.Contains(x.EntityId)).AnyAsync(x => x.BranchId != branch, ct)) return false;
            }
            if (p.Id == "A004A-042" && await new Dev2ResourceFactReader(db).ReadAsync("material-fulfillment", Arg(a, "fulfillmentId"), id, ct) is null) return false;
            var lines = input?.GetType().GetProperty("Lines")?.GetValue(input) as System.Collections.IEnumerable;
            if (lines is not null) foreach (var line in lines)
                if (Field(line, "RequestLineId") is long lineId && !await db.MaterialRequestLines.AsNoTracking().AnyAsync(x => x.LineId == lineId && x.RequestId == id, ct)) return false;
        }
        if (p.Kind == "replenishment")
        {
            var id = Arg(a, p.Argument!);
            var warehouse = await db.MaterialReplenishments.Where(x => x.ReplenishmentId == id).Select(x => x.WarehouseId).SingleAsync(ct);
            if (await db.MaterialReplenishmentLines.AsNoTracking().Where(x => x.ReplenishmentId == id).AnyAsync(x => x.WarehouseId != warehouse || x.SourceMaterialRequestLine != null && x.SourceMaterialRequestLine.Request.BranchId != branch, ct)) return false;
            if (await db.MaterialReplenishmentReceiptLines.AsNoTracking().Where(x => x.ReplenishmentLine.ReplenishmentId == id).AnyAsync(x => x.StockDocumentLine.Document.DestinationWarehouseId != warehouse, ct)) return false;
            if (p.Id == "A004A-054")
            {
                var originals = db.MaterialReplenishmentReceiptLines.Where(x => x.ReplenishmentLine.ReplenishmentId == id).Select(x => x.StockDocumentLine.DocumentId);
                var relatedDocs = db.StockDocuments.Where(x => originals.Contains(x.DocumentId) || x.ReversalOfDocumentId != null && originals.Contains(x.ReversalOfDocumentId.Value) && x.DocumentType == "REVERSAL" && x.Status == "POSTED");
                if (await relatedDocs.AnyAsync(x => x.SourceWarehouse != null && x.SourceWarehouse.BranchId != branch || x.DestinationWarehouse != null && x.DestinationWarehouse.BranchId != branch, ct)) return false;
                var docIds = relatedDocs.Select(x => x.DocumentId.ToString());
                if (await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "material_replenishments" && x.EntityId == id.ToString() || x.EntityType == "stock_documents" && docIds.Contains(x.EntityId)).AnyAsync(x => x.BranchId != branch, ct)) return false;
            }
            var mappings = input?.GetType().GetProperty("Lines")?.GetValue(input) as System.Collections.IEnumerable;
            if (p.Method != "GET" && mappings is not null) foreach (var mapping in mappings)
            {
                var lineId = Required(Field(mapping, "ReplenishmentLineId")); var stockLineId = Required(Field(mapping, "StockDocumentLineId"));
                if (!await db.MaterialReplenishmentLines.AsNoTracking().AnyAsync(x => x.ReplenishmentLineId == lineId && x.ReplenishmentId == id, ct)
                    || !await db.StockDocumentLines.AsNoTracking().AnyAsync(x => x.LineId == stockLineId && x.Document.DestinationWarehouseId == warehouse && x.Document.DocumentType == "RECEIPT", ct)) return false;
            }
            if (p.Id == "A004A-059")
            {
                var document = Required(Field(input, "ReceiptDocumentId"));
                if (!await db.StockDocuments.AsNoTracking().AnyAsync(x => x.DocumentId == document && x.DestinationWarehouseId == warehouse && x.DocumentType == "RECEIPT" && x.ApprovedBy != null && x.ApprovedBy != a.Actor.UserId, ct)) return false;
            }
        }
        return true;
    }

    private static async Task<IActionResult> ReadExpandedAsync(Dev2BoundAction a, CancellationToken ct)
    {
        var services = a.Http.RequestServices; var p = Plan(a); var input = BoundInput(a);
        IAssetService AssetService() => services.GetRequiredService<IAssetService>();
        try
        {
            object? result = p.Id switch
            {
                "A004A-002" => await AssetService().GetHandoversAsync(Arg(a,"assetId"),(AssetPageRequest)input!,ct),
                "A004A-003" => await AssetService().GetHandoverAsync(Arg(a,"handoverId"),ct),
                "A004A-007" => await AssetService().GetMaintenanceHistoryAsync(Arg(a,"assetId"),(AssetMaintenanceHistoryRequest)input!,ct),
                "A004A-008" => await AssetService().GetStatisticsAsync((AssetStatisticsRequest)input!,ct),
                "A004A-009" => await AssetService().GetAssetsAsync((AssetQueryRequest)input!,ct),
                "A004A-010" => await AssetService().GetAssetAsync(Arg(a,"id"),ct),
                "A004A-011" => await AssetService().GetAssetTypesAsync(ct),
                "A004A-012" => await AssetService().GetConditionHistoryAsync(Arg(a,"id"),(AssetPageRequest)input!,ct),
                "A004A-013" => await AssetService().GetLocationHistoryAsync(Arg(a,"id"),(AssetPageRequest)input!,ct),
                "A004A-022" => await services.GetRequiredService<IIncidentService>().GetIncidentsAsync((IncidentQueryRequest)input!,ct),
                "A004A-023" => await services.GetRequiredService<IIncidentService>().GetIncidentAsync(Arg(a,"id"),ct),
                // MVC applies optional parameter defaults when invoking the action, after this filter.
                "A004A-034" => await services.GetRequiredService<IMaintenanceWorkService>().MaterialAvailabilityAsync(Arg(a,"id"),a.Arguments.TryGetValue("page",out var page)?(int)page!:1,a.Arguments.TryGetValue("pageSize",out var pageSize)?(int)pageSize!:20,ct),
                "A004A-035" => await services.GetRequiredService<IMaintenanceWorkService>().GetWorksAsync((MaintenanceQueryRequest)input!,ct),
                "A004A-036" => await services.GetRequiredService<IMaintenanceWorkService>().GetWorkAsync(Arg(a,"id"),ct),
                "A004A-037" => await services.GetRequiredService<MaterialRequestService>().List((MaterialPage)input!,ct),
                "A004A-038" => await services.GetRequiredService<MaterialRequestService>().Get(Arg(a,"id"),ct),
                "A004A-039" => await services.GetRequiredService<MaterialRequestService>().History(Arg(a,"id"),ct),
                "A004A-040" => await services.GetRequiredService<MaterialRequestService>().Availability(Arg(a,"id"),ct),
                "A004A-041" => await services.GetRequiredService<MaterialRequestService>().Fulfillments(Arg(a,"id"),ct),
                "A004A-042" => await services.GetRequiredService<MaterialRequestService>().Fulfillment(Arg(a,"id"),Arg(a,"fulfillmentId"),ct),
                "A004A-052" => await services.GetRequiredService<ReplenishmentService>().List((ReplenishmentPage)input!,ct),
                "A004A-053" => await services.GetRequiredService<ReplenishmentService>().Get(Arg(a,"id"),ct),
                "A004A-054" => await services.GetRequiredService<ReplenishmentService>().History(Arg(a,"id"),ct),
                "A004A-055" => await services.GetRequiredService<ReplenishmentService>().Receipts(Arg(a,"id"),ct),
                "A004A-060" => await services.GetRequiredService<ReplenishmentService>().LowStock((ReplenishmentPage)input!,ct),
                "A004A-061" => await services.GetRequiredService<ReplenishmentService>().MaterialShortages((ReplenishmentPage)input!,ct),
                _ => throw new Dev2AccessDenied(403)
            };
            return result is null ? new NotFoundResult() : new OkObjectResult(result);
        }
        catch (ValidationException) { return new BadRequestObjectResult(new ProblemDetails { Status=400,Title="Invalid scoped read request" }); }
    }
}
