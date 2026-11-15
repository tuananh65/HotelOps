using System.Text.Json;
using System.Text.Json.Nodes;

namespace HotelOps.Api.Services.Insights;

public static class AuditRedaction
{
    // Only reviewed business fields leave the service. Unknown keys and free-text
    // payloads are withheld, including credentials under unexpected key names.
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "status", "operationalStatus", "conditionStatus", "lifecycleStatus", "progressPercent",
        "branchId", "departmentId", "roomId", "areaId", "assetId", "itemId", "warehouseId",
        "assigneeId", "custodianUserId", "isActive", "isOpenCoverage", "approvedQty", "requestedQty",
        "fulfilledQty", "receivedBaseQty", "requestedBaseQty", "signedBaseQty", "minimumQty",
        "actualStartAt", "plannedStartAt", "plannedEndAt", "resolvedAt", "closedAt", "completedAt",
        "dueAt", "occurredAt", "fromStatus", "toStatus", "format", "rowCount", "snapshotId"
    };

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
                .Where(Allowed.Contains).Distinct(StringComparer.OrdinalIgnoreCase)
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
        if (node is not JsonObject obj) return JsonValue.Create("[withheld]");
        var result = new JsonObject();
        foreach (var pair in obj)
        {
            if (!Allowed.Contains(pair.Key)) continue;
            if (pair.Value is null) { result[pair.Key] = null; continue; }
            // Accepted fields contain scalar business facts only, never nested blobs.
            if (pair.Value is JsonValue scalar) result[pair.Key] = scalar.DeepClone();
        }
        return result;
    }
}
