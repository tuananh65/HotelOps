using System.Collections.Frozen;
namespace HotelOps.Api.Services.Dev2Authorization;

internal sealed record Dev2ExpandedAction(string Id, string Controller, string Action, string Method, string Kind, string? Argument, IReadOnlyList<string> Codes);

public static class Dev2ExpandedPolicyIdentity
{
    public const string Version="DEV2_DEMO_AUTHORITY_V2_61_ACTIONS";
    public const string Sha256="456249493fc4ee7eb81ba18ef18ff44a0b55a70064736fbdb78bae1784cf74be";
}

internal static class Dev2ExpandedCatalog
{
    internal static readonly FrozenDictionary<string, Dev2ExpandedAction> Actions = new Dev2ExpandedAction[]
    {
        new("A004A-001", "AssetsController", "CreateHandover", "POST", "asset", "assetId", Array.AsReadOnly(new[] { "ASSET_TRANSFER" })),
        new("A004A-002", "AssetsController", "GetHandovers", "GET", "asset", "assetId", Array.AsReadOnly(new[] { "ASSET_VIEW" })),
        new("A004A-003", "AssetsController", "GetHandover", "GET", "asset-handover", "handoverId", Array.AsReadOnly(new[] { "ASSET_VIEW" })),
        new("A004A-004", "AssetsController", "AcceptHandover", "POST", "asset-handover", "handoverId", Array.AsReadOnly(new[] { "ASSET_TRANSFER" })),
        new("A004A-005", "AssetsController", "RejectHandover", "POST", "asset-handover", "handoverId", Array.AsReadOnly(new[] { "ASSET_TRANSFER" })),
        new("A004A-006", "AssetsController", "RetireAsset", "POST", "asset", "assetId", Array.AsReadOnly(new[] { "ASSET_RETIRE" })),
        new("A004A-007", "AssetsController", "GetMaintenanceHistory", "GET", "asset", "assetId", Array.AsReadOnly(new[] { "ASSET_VIEW" })),
        new("A004A-008", "AssetsController", "GetStatistics", "GET", "branch-input", null, Array.AsReadOnly(new[] { "ASSET_VIEW" })),
        new("A004A-009", "AssetsController", "GetAssets", "GET", "branch-input", null, Array.AsReadOnly(new[] { "ASSET_VIEW" })),
        new("A004A-010", "AssetsController", "GetAsset", "GET", "asset", "id", Array.AsReadOnly(new[] { "ASSET_VIEW" })),
        new("A004A-011", "AssetsController", "GetAssetTypes", "GET", "branch-query", null, Array.AsReadOnly(new[] { "ASSET_VIEW" })),
        new("A004A-012", "AssetsController", "GetConditionHistory", "GET", "asset", "id", Array.AsReadOnly(new[] { "ASSET_VIEW" })),
        new("A004A-013", "AssetsController", "GetLocationHistory", "GET", "asset", "id", Array.AsReadOnly(new[] { "ASSET_VIEW" })),
        new("A004A-014", "AssetsController", "CreateAsset", "POST", "branch-input", null, Array.AsReadOnly(new[] { "ASSET_MANAGE" })),
        new("A004A-015", "AssetsController", "UpdateAsset", "PUT", "asset", "id", Array.AsReadOnly(new[] { "ASSET_MANAGE" })),
        new("A004A-016", "AssetsController", "AssignLocation", "POST", "asset", "id", Array.AsReadOnly(new[] { "ASSET_MANAGE" })),
        new("A004A-017", "AssetsController", "TransferLocation", "POST", "asset", "id", Array.AsReadOnly(new[] { "ASSET_TRANSFER" })),
        new("A004A-018", "AssetsController", "ChangeCondition", "POST", "asset", "id", Array.AsReadOnly(new[] { "ASSET_MANAGE" })),
        new("A004A-019", "IncidentsController", "ChangeIncidentStatus", "PATCH", "incident", "id", Array.AsReadOnly(new[] { "INCIDENT_MANAGE" })),
        new("A004A-020", "IncidentsController", "TriageIncident", "PATCH", "incident", "id", Array.AsReadOnly(new[] { "INCIDENT_TRIAGE" })),
        new("A004A-021", "IncidentsController", "CreateIncident", "POST", "branch-input", null, Array.AsReadOnly(new[] { "INCIDENT_CREATE" })),
        new("A004A-022", "IncidentsController", "GetIncidents", "GET", "branch-input", null, Array.AsReadOnly(new[] { "INCIDENT_VIEW" })),
        new("A004A-023", "IncidentsController", "GetIncident", "GET", "incident", "id", Array.AsReadOnly(new[] { "INCIDENT_VIEW" })),
        new("A004A-024", "MaintenanceWorksController", "CreateWork", "POST", "incident", "incidentId", Array.AsReadOnly(new[] { "MAINTENANCE_ASSIGN" })),
        new("A004A-025", "MaintenanceWorksController", "AssignWork", "PATCH", "maintenance-work", "id", Array.AsReadOnly(new[] { "MAINTENANCE_ASSIGN" })),
        new("A004A-026", "MaintenanceWorksController", "StartWork", "POST", "maintenance-work", "id", Array.AsReadOnly(new[] { "MAINTENANCE_EXECUTE" })),
        new("A004A-027", "MaintenanceWorksController", "HoldWork", "POST", "maintenance-work", "id", Array.AsReadOnly(new[] { "MAINTENANCE_EXECUTE" })),
        new("A004A-028", "MaintenanceWorksController", "ResumeWork", "POST", "maintenance-work", "id", Array.AsReadOnly(new[] { "MAINTENANCE_EXECUTE" })),
        new("A004A-029", "MaintenanceWorksController", "CreateLog", "POST", "maintenance-work", "id", Array.AsReadOnly(new[] { "MAINTENANCE_EXECUTE" })),
        new("A004A-030", "MaintenanceWorksController", "SubmitResult", "POST", "maintenance-work", "id", Array.AsReadOnly(new[] { "MAINTENANCE_EXECUTE" })),
        new("A004A-031", "MaintenanceWorksController", "VerifyWork", "POST", "maintenance-work", "id", Array.AsReadOnly(new[] { "MAINTENANCE_REVIEW" })),
        new("A004A-032", "MaintenanceWorksController", "ReturnWork", "POST", "maintenance-work", "id", Array.AsReadOnly(new[] { "MAINTENANCE_REVIEW" })),
        new("A004A-033", "MaintenanceWorksController", "CancelWork", "POST", "maintenance-work", "id", Array.AsReadOnly(new[] { "DEV2_DEMO_MAINTENANCEWORKS_CANCELWORK" })),
        new("A004A-034", "MaintenanceWorksController", "MaterialAvailability", "GET", "maintenance-work", "id", Array.AsReadOnly(new[] { "MAINTENANCE_VIEW", "INVENTORY_VIEW" })),
        new("A004A-035", "MaintenanceWorksController", "GetWorks", "GET", "incident-input", null, Array.AsReadOnly(new[] { "MAINTENANCE_VIEW" })),
        new("A004A-036", "MaintenanceWorksController", "GetWork", "GET", "maintenance-work", "id", Array.AsReadOnly(new[] { "MAINTENANCE_VIEW" })),
        new("A004A-037", "MaterialRequestsController", "List", "GET", "branch-input", null, Array.AsReadOnly(new[] { "DEV2_DEMO_MR_VIEW" })),
        new("A004A-038", "MaterialRequestsController", "Get", "GET", "material-request", "id", Array.AsReadOnly(new[] { "DEV2_DEMO_MR_VIEW" })),
        new("A004A-039", "MaterialRequestsController", "History", "GET", "material-request", "id", Array.AsReadOnly(new[] { "DEV2_DEMO_MR_VIEW" })),
        new("A004A-040", "MaterialRequestsController", "Availability", "GET", "material-request", "id", Array.AsReadOnly(new[] { "DEV2_DEMO_MR_VIEW", "INVENTORY_VIEW" })),
        new("A004A-041", "MaterialRequestsController", "Fulfillments", "GET", "material-request", "id", Array.AsReadOnly(new[] { "DEV2_DEMO_MR_VIEW" })),
        new("A004A-042", "MaterialRequestsController", "Fulfillment", "GET", "material-request", "id", Array.AsReadOnly(new[] { "DEV2_DEMO_MR_VIEW" })),
        new("A004A-043", "MaterialRequestsController", "Create", "POST", "branch-input", null, Array.AsReadOnly(new[] { "MR_CREATE" })),
        new("A004A-044", "MaterialRequestsController", "Edit", "PUT", "material-request", "id", Array.AsReadOnly(new[] { "MR_CREATE" })),
        new("A004A-045", "MaterialRequestsController", "Submit", "POST", "material-request", "id", Array.AsReadOnly(new[] { "MR_CREATE" })),
        new("A004A-046", "MaterialRequestsController", "Information", "POST", "material-request", "id", Array.AsReadOnly(new[] { "MR_APPROVE_L1" })),
        new("A004A-047", "MaterialRequestsController", "Approve", "POST", "material-request", "id", Array.AsReadOnly(new[] { "MR_APPROVE_L1" })),
        new("A004A-048", "MaterialRequestsController", "Reject", "POST", "material-request", "id", Array.AsReadOnly(new[] { "MR_APPROVE_L1" })),
        new("A004A-049", "MaterialRequestsController", "Cancel", "POST", "material-request", "id", Array.AsReadOnly(new[] { "MR_CREATE" })),
        new("A004A-050", "MaterialRequestsController", "Close", "POST", "material-request", "id", Array.AsReadOnly(new[] { "DEV2_DEMO_MATERIALREQUESTS_CLOSE" })),
        new("A004A-051", "MaterialRequestsController", "Issue", "POST", "material-request", "id", Array.AsReadOnly(new[] { "MR_FULFILL", "STOCK_ISSUE_POST" })),
        new("A004A-052", "ReplenishmentsController", "List", "GET", "branch-input", null, Array.AsReadOnly(new[] { "DEV2_DEMO_REPLENISHMENT_VIEW" })),
        new("A004A-053", "ReplenishmentsController", "Get", "GET", "replenishment", "id", Array.AsReadOnly(new[] { "DEV2_DEMO_REPLENISHMENT_VIEW" })),
        new("A004A-054", "ReplenishmentsController", "History", "GET", "replenishment", "id", Array.AsReadOnly(new[] { "DEV2_DEMO_REPLENISHMENT_VIEW" })),
        new("A004A-055", "ReplenishmentsController", "Receipts", "GET", "replenishment", "id", Array.AsReadOnly(new[] { "DEV2_DEMO_REPLENISHMENT_VIEW" })),
        new("A004A-056", "ReplenishmentsController", "Create", "POST", "warehouse-input", null, Array.AsReadOnly(new[] { "DEV2_DEMO_REPLENISHMENT_CREATE" })),
        new("A004A-057", "ReplenishmentsController", "Close", "POST", "replenishment", "id", Array.AsReadOnly(new[] { "DEV2_DEMO_REPLENISHMENT_CLOSE" })),
        new("A004A-058", "ReplenishmentsController", "Link", "POST", "replenishment", "id", Array.AsReadOnly(new[] { "DEV2_DEMO_REPLENISHMENT_RECEIVE" })),
        new("A004A-059", "ReplenishmentsController", "Post", "POST", "replenishment", "id", Array.AsReadOnly(new[] { "DEV2_DEMO_REPLENISHMENT_RECEIVE", "STOCK_RECEIPT_POST" })),
        new("A004A-060", "ReplenishmentsController", "Low", "GET", "warehouse-input", null, Array.AsReadOnly(new[] { "DEV2_DEMO_REPLENISHMENT_VIEW", "INVENTORY_VIEW" })),
        new("A004A-061", "ReplenishmentsController", "Material", "GET", "warehouse-input", null, Array.AsReadOnly(new[] { "DEV2_DEMO_REPLENISHMENT_VIEW", "INVENTORY_VIEW" })),
    }.ToFrozenDictionary(x=>x.Id, StringComparer.Ordinal);
}
