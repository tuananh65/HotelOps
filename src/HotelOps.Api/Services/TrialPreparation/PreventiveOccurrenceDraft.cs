using System.Collections.ObjectModel;

namespace HotelOps.Api.Services.TrialPreparation;

// C13/C14 in-memory design only. No schedule/run entity, status, DDL or worker is changed.
public sealed record ChecklistStepDraft(string Reference, string Instruction);

public sealed class PreventiveOccurrenceDraft
{
    public long ScheduleId { get; }
    public string ScheduleVersion { get; }
    public DateTimeOffset PlannedAtUtc { get; }
    public ReadOnlyCollection<ChecklistStepDraft> Checklist { get; }

    public PreventiveOccurrenceDraft(long scheduleId, string scheduleVersion, DateTimeOffset plannedAt,
        IEnumerable<ChecklistStepDraft> checklist)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scheduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scheduleVersion);
        var copy = checklist.ToArray();
        if (copy.Length == 0 || copy.Any(s => string.IsNullOrWhiteSpace(s.Reference) ||
            string.IsNullOrWhiteSpace(s.Instruction)) || copy.Select(s => s.Reference).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("A nonempty, uniquely referenced checklist snapshot is required.", nameof(checklist));
        ScheduleId = scheduleId;
        ScheduleVersion = scheduleVersion;
        PlannedAtUtc = plannedAt.ToUniversalTime();
        Checklist = Array.AsReadOnly(copy);
    }

    // Proposed identity components, not an approved durable DB key or duplicate reservation.
    public string ProposedIdentity => $"{ScheduleId}:{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(ScheduleVersion))}:{PlannedAtUtc.UtcTicks}";
}

public static class PreventiveClockDraft
{
    // Caller supplies a policy-selected local occurrence. Ambiguous/invalid DST requires an explicit decision.
    public static DateTimeOffset ResolveLocal(DateTime local, TimeZoneInfo zone)
    {
        if (local.Kind != DateTimeKind.Unspecified) throw new ArgumentException("Explicit local wall time required.", nameof(local));
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
            throw new ArgumentException("DST policy decision required; no silent occurrence shift.", nameof(local));
        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }
}
