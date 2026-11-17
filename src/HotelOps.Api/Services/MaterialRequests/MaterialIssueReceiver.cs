using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Entities;

namespace HotelOps.Api.Services.MaterialRequests;

// Receiver identity entered by the issuer is historical document metadata, not
// evidence that a user authenticated or acknowledged receipt (ReceivedBy/At).
internal static class MaterialIssueReceiver
{
    internal static string? Read(MaterialFulfillment fulfillment, IEnumerable<AuditLog> receipts)
    {
        var matches = receipts.Where(x => x.EntityId == fulfillment.FulfillmentId.ToString(CultureInfo.InvariantCulture)
            && x.ActorUserId == fulfillment.FulfilledBy && x.NewValues != null).ToArray();
        if (matches.Length != 1) return null;
        try
        {
            using var json = JsonDocument.Parse(matches[0].NewValues!);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("BusinessEvent", out var evt) || evt.ValueKind != JsonValueKind.String
                || evt.GetString() != "FULFILLMENT_ISSUED"
                || !root.TryGetProperty("State", out var state) || state.ValueKind != JsonValueKind.Object
                || !Identity(state, "FulfillmentId", fulfillment.FulfillmentId)
                || !Identity(state, "RequestId", fulfillment.RequestId)
                || !Identity(state, "WarehouseId", fulfillment.WarehouseId)
                || !Identity(state, "FulfilledBy", fulfillment.FulfilledBy)
                || fulfillment.StockDocumentId is not {} documentId || !Identity(state, "StockDocumentId", documentId)
                || !state.TryGetProperty("ReceiverName", out var name) || name.ValueKind != JsonValueKind.String)
                return null;
            var value = name.GetString()?.Trim();
            return !string.IsNullOrWhiteSpace(value) && value.Length <= 200 && !value.Any(char.IsControl) ? value : null;
        }
        catch (JsonException) { return null; }
    }

    private static bool Identity(JsonElement state, string name, long expected) =>
        state.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var actual) && actual == expected;
}
