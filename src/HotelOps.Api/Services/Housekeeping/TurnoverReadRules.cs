using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;

namespace HotelOps.Api.Services.Housekeeping;

public static class TurnoverReadRules
{
    public static TurnoverQuery Validate(long room, long? cycle, TurnoverQuery q, long? attachment)
    {
        if (room <= 0 || cycle <= 0 || q.BranchId <= 0 || attachment <= 0 || attachment != null && cycle == null
            || q.From?.Kind is { } f && f != DateTimeKind.Utc || q.To?.Kind is { } t && t != DateTimeKind.Utc
            || q.From > q.To || q.Page < 1 || q.PageSize is < 1 or > 100 || q.Page > int.MaxValue / q.PageSize
            || q.TimelinePage < 1 || q.TimelinePageSize is < 1 or > 100 || q.TimelinePage > int.MaxValue / q.TimelinePageSize
            || q.Result?.Trim().ToUpperInvariant() is not (null or "" or "PASS" or "FAIL"))
            throw new ValidationException("Invalid turnover filters, identifiers or pagination; dates must be UTC.");
        return q with { Result = string.IsNullOrWhiteSpace(q.Result) ? null : q.Result.Trim().ToUpperInvariant() };
    }
    public static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    public static DateTime? Utc(DateTime? value) => value is { } date ? Utc(date) : null;
    public static TurnoverEvent[] Order(IEnumerable<TurnoverEvent> events) => events.OrderBy(e => e.AtUtc)
        .ThenBy(e => e.SourceType, StringComparer.Ordinal).ThenBy(e => e.SourceId).ThenBy(e => e.Event, StringComparer.Ordinal).ToArray();
    public static string? CancellationReason(string? json)
    {
        if (json == null) return null;
        try { using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && doc.RootElement.TryGetProperty("reason", out var v)
                && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null; }
        catch (System.Text.Json.JsonException) { return null; }
    }
}
