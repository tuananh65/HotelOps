using System.Collections.Frozen;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace HotelOps.Api.Controllers.Filters;

/// <summary>
/// Reviewed action identity, not an accepted permission or a business grant.
/// Controller type, reflected method, HTTP method and MVC route must all match.
/// </summary>
public sealed record Dev2KnownAction(string DecisionId, Type ControllerType,
    string ActionName, string HttpMethod, string RouteTemplate);

public static class Dev2ActionAdmissionRegistry
{
    // Only source-owned contracts are admitted. Never populate this from request
    // data, configuration, role labels or the historical candidate matrix.
    public static IReadOnlySet<string> ApprovedDecisionIds =>
        HotelOps.Api.Services.Dev2Authorization.Dev2ActionContracts.DecisionIds;

    public static IReadOnlyList<Dev2KnownAction> KnownActions { get; } = Array.AsReadOnly(new Dev2KnownAction[]
    {
        new("A004A-001", typeof(AssetsController), "CreateHandover", "POST", "api/assets/{assetId:long}/handovers"),
        new("A004A-002", typeof(AssetsController), "GetHandovers", "GET", "api/assets/{assetId:long}/handovers"),
        new("A004A-003", typeof(AssetsController), "GetHandover", "GET", "api/asset-handovers/{handoverId:long}"),
        new("A004A-004", typeof(AssetsController), "AcceptHandover", "POST", "api/asset-handovers/{handoverId:long}/accept"),
        new("A004A-005", typeof(AssetsController), "RejectHandover", "POST", "api/asset-handovers/{handoverId:long}/reject"),
        new("A004A-006", typeof(AssetsController), "RetireAsset", "POST", "api/assets/{assetId:long}/retire"),
        new("A004A-007", typeof(AssetsController), "GetMaintenanceHistory", "GET", "api/assets/{assetId:long}/maintenance-history"),
        new("A004A-008", typeof(AssetsController), "GetStatistics", "GET", "api/assets/statistics/by-location"),
        new("A004A-009", typeof(AssetsController), "GetAssets", "GET", "api/assets"),
        new("A004A-010", typeof(AssetsController), "GetAsset", "GET", "api/assets/{id:long}"),
        new("A004A-011", typeof(AssetsController), "GetAssetTypes", "GET", "api/asset-types"),
        new("A004A-012", typeof(AssetsController), "GetConditionHistory", "GET", "api/assets/{id:long}/condition-history"),
        new("A004A-013", typeof(AssetsController), "GetLocationHistory", "GET", "api/assets/{id:long}/location-history"),
        new("A004A-014", typeof(AssetsController), "CreateAsset", "POST", "api/assets"),
        new("A004A-015", typeof(AssetsController), "UpdateAsset", "PUT", "api/assets/{id:long}"),
        new("A004A-016", typeof(AssetsController), "AssignLocation", "POST", "api/assets/{id:long}/location"),
        new("A004A-017", typeof(AssetsController), "TransferLocation", "POST", "api/assets/{id:long}/location-transfers"),
        new("A004A-018", typeof(AssetsController), "ChangeCondition", "POST", "api/assets/{id:long}/condition"),
        new("A004A-019", typeof(IncidentsController), "ChangeIncidentStatus", "PATCH", "api/incidents/{id:long}/status"),
        new("A004A-020", typeof(IncidentsController), "TriageIncident", "PATCH", "api/incidents/{id:long}/triage"),
        new("A004A-021", typeof(IncidentsController), "CreateIncident", "POST", "api/incidents"),
        new("A004A-022", typeof(IncidentsController), "GetIncidents", "GET", "api/incidents"),
        new("INCIDENT-CATEGORIES-READ", typeof(IncidentsController), "GetIncidentCategories", "GET", "api/incidents/categories/{branchId:long}"),
        new("A004A-023", typeof(IncidentsController), "GetIncident", "GET", "api/incidents/{id:long}"),
        new("A004A-024", typeof(MaintenanceWorksController), "CreateWork", "POST", "api/incidents/{incidentId:long}/maintenance-works"),
        new("A004A-025", typeof(MaintenanceWorksController), "AssignWork", "PATCH", "api/maintenance-works/{id:long}/assignment"),
        new("A004A-026", typeof(MaintenanceWorksController), "StartWork", "POST", "api/maintenance-works/{id:long}/start"),
        new("A004A-027", typeof(MaintenanceWorksController), "HoldWork", "POST", "api/maintenance-works/{id:long}/hold"),
        new("A004A-028", typeof(MaintenanceWorksController), "ResumeWork", "POST", "api/maintenance-works/{id:long}/resume"),
        new("A004A-029", typeof(MaintenanceWorksController), "CreateLog", "POST", "api/maintenance-works/{id:long}/logs"),
        new("A004A-030", typeof(MaintenanceWorksController), "SubmitResult", "POST", "api/maintenance-works/{id:long}/submit-result"),
        new("A004A-031", typeof(MaintenanceWorksController), "VerifyWork", "POST", "api/maintenance-works/{id:long}/verify"),
        new("A004A-032", typeof(MaintenanceWorksController), "ReturnWork", "POST", "api/maintenance-works/{id:long}/return"),
        new("A004A-033", typeof(MaintenanceWorksController), "CancelWork", "POST", "api/maintenance-works/{id:long}/cancel"),
        new("A004A-034", typeof(MaintenanceWorksController), "MaterialAvailability", "GET", "api/maintenance-works/{id:long}/material-availability"),
        new("A004A-035", typeof(MaintenanceWorksController), "GetWorks", "GET", "api/maintenance-works"),
        new("A004A-036", typeof(MaintenanceWorksController), "GetWork", "GET", "api/maintenance-works/{id:long}"),
        new("A004A-037", typeof(MaterialRequestsController), "List", "GET", "api/material-requests"),
        new("A004A-038", typeof(MaterialRequestsController), "Get", "GET", "api/material-requests/{id:long}"),
        new("A004A-039", typeof(MaterialRequestsController), "History", "GET", "api/material-requests/{id:long}/history"),
        new("A004A-040", typeof(MaterialRequestsController), "Availability", "GET", "api/material-requests/{id:long}/availability"),
        new("A004A-041", typeof(MaterialRequestsController), "Fulfillments", "GET", "api/material-requests/{id:long}/fulfillments"),
        new("A004A-042", typeof(MaterialRequestsController), "Fulfillment", "GET", "api/material-requests/{id:long}/fulfillments/{fulfillmentId:long}"),
        new("A004A-043", typeof(MaterialRequestsController), "Create", "POST", "api/material-requests"),
        new("MATERIAL-CREATE-APPROVERS", typeof(MaterialRequestsController), "Approvers", "GET", "api/material-requests/creation-approvers"),
        new("A004A-044", typeof(MaterialRequestsController), "Edit", "PUT", "api/material-requests/{id:long}"),
        new("A004A-045", typeof(MaterialRequestsController), "Submit", "POST", "api/material-requests/{id:long}/submit"),
        new("A004A-046", typeof(MaterialRequestsController), "Information", "POST", "api/material-requests/{id:long}/request-information"),
        new("A004A-047", typeof(MaterialRequestsController), "Approve", "POST", "api/material-requests/{id:long}/approve"),
        new("A004A-048", typeof(MaterialRequestsController), "Reject", "POST", "api/material-requests/{id:long}/reject"),
        new("A004A-049", typeof(MaterialRequestsController), "Cancel", "POST", "api/material-requests/{id:long}/cancel"),
        new("A004A-050", typeof(MaterialRequestsController), "Close", "POST", "api/material-requests/{id:long}/close-remainder"),
        new("A004A-051", typeof(MaterialRequestsController), "Issue", "POST", "api/material-requests/{id:long}/fulfillments"),
        new("A004A-052", typeof(ReplenishmentsController), "List", "GET", "api/replenishments"),
        new("A004A-053", typeof(ReplenishmentsController), "Get", "GET", "api/replenishments/{id:long}"),
        new("A004A-054", typeof(ReplenishmentsController), "History", "GET", "api/replenishments/{id:long}/history"),
        new("A004A-055", typeof(ReplenishmentsController), "Receipts", "GET", "api/replenishments/{id:long}/receipts"),
        new("A004A-056", typeof(ReplenishmentsController), "Create", "POST", "api/replenishments"),
        new("A004A-057", typeof(ReplenishmentsController), "Close", "POST", "api/replenishments/{id:long}/close"),
        new("A004A-058", typeof(ReplenishmentsController), "Link", "POST", "api/replenishments/{id:long}/receipts/link"),
        new("A004A-059", typeof(ReplenishmentsController), "Post", "POST", "api/replenishments/{id:long}/receipts/post"),
        new("A004A-060", typeof(ReplenishmentsController), "Low", "GET", "api/replenishment-shortages/low-stock"),
        new("A004A-061", typeof(ReplenishmentsController), "Material", "GET", "api/replenishment-shortages/material-requests"),
    });

    public static Dev2KnownAction? Resolve(ControllerActionDescriptor descriptor, string requestMethod)
    {
        var controllerType = descriptor.ControllerTypeInfo.AsType();
        if (descriptor.MethodInfo.DeclaringType != controllerType
            || descriptor.ActionName != descriptor.MethodInfo.Name
            || descriptor.AttributeRouteInfo?.Template is not string route)
            return null;

        var methods = descriptor.ActionConstraints?.OfType<HttpMethodActionConstraint>()
            .SelectMany(c => c.HttpMethods).ToArray();
        if (methods is not { Length: 1 }
            || !string.Equals(methods[0], requestMethod, StringComparison.OrdinalIgnoreCase))
            return null;

        route = route.TrimStart('~', '/');
        return KnownActions.SingleOrDefault(action => action.ControllerType == controllerType
            && action.ActionName == descriptor.MethodInfo.Name
            && action.HttpMethod == methods[0]
            && action.RouteTemplate == route);
    }
}
