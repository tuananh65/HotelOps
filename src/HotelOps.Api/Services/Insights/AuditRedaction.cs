using System.Text.Json;
using System.Text.Json.Nodes;

namespace HotelOps.Api.Services.Insights;

public static class AuditRedaction
{
    // Only reviewed business fields leave the service. Unknown payloads and
    // credential properties are never disclosed through the audit reader.
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "status", "operationalStatus", "conditionStatus", "lifecycleStatus", "progressPercent",
        "branchId", "departmentId", "roomId", "areaId", "assetId", "itemId", "warehouseId",
        "assigneeId", "custodianUserId", "isActive", "isOpenCoverage", "approvedQty", "requestedQty",
        "fulfilledQty", "receivedBaseQty", "requestedBaseQty", "signedBaseQty", "minimumQty",
        "actualStartAt", "plannedStartAt", "plannedEndAt", "resolvedAt", "closedAt", "completedAt",
        "dueAt", "occurredAt", "fromStatus", "toStatus", "format", "rowCount", "snapshotId",
        "notificationId", "readAt", "acknowledgedAt"
    };
    private static readonly HashSet<string> Containers = new(StringComparer.OrdinalIgnoreCase)
    { "entity", "value", "state", "before", "after", "items", "lines", "permissions", "grants", "assignments", "membership" };
    private static string Canonical(string key) => key.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
    private static readonly HashSet<string> Reviewed = Allowed.Select(Canonical).Concat(new[]
    {
        "id", "userid", "roleid", "rolecode", "permissionid", "permissioncode", "datascope", "accesslevel",
        "isprimary", "assignedat", "expiresat", "revokedat", "departmentids", "taskid", "inspectionid",
        "reworkid", "inspectorid", "previousinspectorid", "handler", "handlerid", "receivingdepartmentid",
        "severityid", "priorityid", "categoryid", "materialrequestid", "incidentid", "workflow", "workflowstatus",
        "state", "progress", "percent", "mandatory", "previousrequirement", "deadline", "scheduledstartat",
        "scheduledendat", "responsedeadlineat", "resolutiondeadlineat", "discoveredat", "submittedat",
        "createdat", "updatedat", "eventid", "assignmenteventid", "assignmentid", "closureeventid", "auditid",
        "fulfillmentstatus", "requeststatus", "currentassigneeid", "defaultchecklistid", "standardcleaningminutes",
        "capacity", "floorid", "roomtypeid", "branchcode", "branchname", "departmentcode", "departmentname",
        "roomcode", "roomnumber", "roomtypename", "roomtypecode", "areacode", "areaname", "floorcode", "floorname",
        "fullname", "email", "phone", "accountstatus", "employmentstatus", "requiredstaffcount",
        "requestno", "requesterid", "sourcewarehouseid", "neededat", "receivingroomid", "receivingareaid",
        "remainderclosedat", "remainderclosedby", "lineid", "lineno", "uomid",
        "reason", "note", "description", "purpose", "cancellationreason", "remainderclosereason",
        "deadlinereason", "title", "result", "rolename", "floororder", "standardcapacity", "maxcapacity"
    }).ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyList<string>? Fields(string? json)
    {
        if (json is null || json.Length > 100_000) return null;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Array) return null;
            return document.RootElement.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString()!)
                .Where(key => Reviewed.Contains(Canonical(key)) || Containers.Contains(key)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (JsonException) { return null; }
    }

    public static object? Read(string? json)
    {
        if (json is null) return null;
        try
        {
            if (json.Length > 1_000_000) return new { withheld = true };
            return Sanitize(JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 32 }));
        }
        catch (JsonException) { return new { withheld = true }; }
    }

    private static JsonNode? Sanitize(JsonNode? node)
    {
        if (node is JsonArray array)
            return new JsonArray(array.Select(item => item is JsonObject or JsonArray ? Sanitize(item)
                : item is null ? null : JsonValue.Create("[withheld]")).ToArray());
        if (node is not JsonObject obj) return JsonValue.Create("[withheld]");
        var result = new JsonObject();
        foreach (var pair in obj)
        {
            if (Containers.Contains(pair.Key) && pair.Value is JsonObject or JsonArray)
            {
                result[pair.Key] = Sanitize(pair.Value);
                continue;
            }
            if (!Reviewed.Contains(Canonical(pair.Key))) continue;
            if (pair.Value is null) { result[pair.Key] = null; continue; }
            // Only named business envelopes may recurse. Unknown blobs, credentials,
            // private attachment payloads and storage keys remain withheld.
            if (Canonical(pair.Key) == "departmentids" && pair.Value is JsonArray ids
                && ids.All(id => id is JsonValue number && number.TryGetValue<long>(out var value) && value > 0))
                result[pair.Key] = ids.DeepClone();
            if (pair.Value is JsonValue scalar && ReviewedNotificationValue(pair.Key, scalar)) result[pair.Key] = scalar.DeepClone();
        }
        return result;
    }

    // Notification acknowledgement writers persist identifiers and UTC instants,
    // never arbitrary text. Do not open a new free-text channel through these keys.
    private static bool ReviewedNotificationValue(string key, JsonValue value)
    {
        if (Canonical(key) == "notificationid")
            return value.TryGetValue<long>(out var id) && id > 0;
        if (Canonical(key) is "readat" or "acknowledgedat")
            return value.TryGetValue<string>(out var text) && text.Length is >=20 and <=33
                && (text.EndsWith('Z') || text.EndsWith("+00:00", StringComparison.Ordinal))
                && DateTimeOffset.TryParseExact(text, ["yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"],
                    System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)
                && date.Offset == TimeSpan.Zero;
        return true;
    }
}
