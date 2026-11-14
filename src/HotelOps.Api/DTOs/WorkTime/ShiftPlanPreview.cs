namespace HotelOps.Api.DTOs.WorkTime;

// Internal preview contracts, not an approved public HTTP/write contract.
public sealed record ShiftPlanDraft(long EmployeeId, long BranchId, long DepartmentId,
    long TemplateId, DateOnly ShiftDate);
public enum ShiftPlanIssue
{
    InvalidDraft, ScopeUnavailable, TemplateUnavailable, EmployeeUnavailable,
    EmployeeDepartmentUnconfirmed, TimezoneUnavailable, InvalidTime,
    ExistingOverlap, ProposedOverlap, DataUnavailable
}
public sealed record ShiftPlanPreviewRow(int Index, DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc, IReadOnlyList<ShiftPlanIssue> Issues);
public sealed record ShiftPlanPreview(IReadOnlyList<ShiftPlanPreviewRow> Rows)
{
    // Never permission to save, a version token or an atomic snapshot.
    public bool RequiresServerRevalidation => true;
}