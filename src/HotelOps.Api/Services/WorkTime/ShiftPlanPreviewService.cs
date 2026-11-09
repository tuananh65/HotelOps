using System.Data.Common;
using HotelOps.Api.DTOs.WorkTime;

namespace HotelOps.Api.Services.WorkTime;

public sealed record ShiftPlanScope(string? Timezone);
public sealed record ShiftPlanTemplate(long Id, long BranchId, bool IsActive,
    TimeOnly Start, TimeOnly End, bool CrossesMidnight);
public sealed record ShiftPlanEmployee(long Id, bool Active, bool HasBranchAccess, long? DepartmentId);

/// <summary>Server-trusted readers only. Caller must authorize every requested resource first.</summary>
public interface IShiftPlanPreviewReader
{
    Task<ShiftPlanScope?> ReadScopeAsync(long branchId, long departmentId, CancellationToken ct);
    Task<ShiftPlanTemplate?> ReadTemplateAsync(long templateId, CancellationToken ct);
    Task<ShiftPlanEmployee?> ReadEmployeeAsync(long employeeId, long branchId, CancellationToken ct);
    Task<bool> HasOverlapAsync(long employeeId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct);
}

/// <summary>New-assignment preview only. No mutation, version, authority or release approval.</summary>
public sealed class ShiftPlanPreviewService(IShiftPlanPreviewReader reader)
{
    public async Task<ShiftPlanPreview> PreviewAsync(IReadOnlyList<ShiftPlanDraft> source, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ct.ThrowIfCancellationRequested();
        if (source.Count > 500) throw new ArgumentException("Preview batch exceeds its technical bound.", nameof(source));
        var drafts = source.ToArray(); // Snapshot caller-owned collection before awaiting any read.
        var plans = new PlannedShiftRange?[drafts.Length];
        var issues = drafts.Select(_ => new HashSet<ShiftPlanIssue>()).ToArray();
        for (var i = 0; i < drafts.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var draft = drafts[i];
            if (draft is null || draft.EmployeeId <= 0 || draft.BranchId <= 0
                || draft.DepartmentId <= 0 || draft.TemplateId <= 0)
            {
                issues[i].Add(ShiftPlanIssue.InvalidDraft);
                continue;
            }
            try
            {
                var scope = await reader.ReadScopeAsync(draft.BranchId, draft.DepartmentId, ct);
                if (scope is null) { issues[i].Add(ShiftPlanIssue.ScopeUnavailable); continue; }
                TimeZoneInfo timezone;
                try
                {
                    if (string.IsNullOrWhiteSpace(scope.Timezone)) throw new TimeZoneNotFoundException();
                    timezone = TimeZoneInfo.FindSystemTimeZoneById(scope.Timezone);
                }
                catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
                { issues[i].Add(ShiftPlanIssue.TimezoneUnavailable); continue; }
                var template = await reader.ReadTemplateAsync(draft.TemplateId, ct);
                if (template is null || template.Id != draft.TemplateId || template.BranchId != draft.BranchId || !template.IsActive)
                { issues[i].Add(ShiftPlanIssue.TemplateUnavailable); continue; }
                var employee = await reader.ReadEmployeeAsync(draft.EmployeeId, draft.BranchId, ct);
                if (employee is null || employee.Id != draft.EmployeeId || !employee.Active || !employee.HasBranchAccess)
                { issues[i].Add(ShiftPlanIssue.EmployeeUnavailable); continue; }
                // Current schema fact only: do not invent multi-department eligibility/assignment policy.
                if (employee.DepartmentId != draft.DepartmentId)
                { issues[i].Add(ShiftPlanIssue.EmployeeDepartmentUnconfirmed); continue; }
                try { plans[i] = ShiftSchedulePlanner.Plan(draft.ShiftDate, template.Start, template.End, template.CrossesMidnight, timezone); }
                catch (ArgumentException) { issues[i].Add(ShiftPlanIssue.InvalidTime); continue; }
                var plan = plans[i]!;
                if (await reader.HasOverlapAsync(draft.EmployeeId, plan.StartUtc, plan.EndUtc, ct))
                    issues[i].Add(ShiftPlanIssue.ExistingOverlap);
            }
            catch (Exception e) when (e is DbException or InvalidOperationException)
            { issues[i].Add(ShiftPlanIssue.DataUnavailable); }
        }
        ct.ThrowIfCancellationRequested();
        for (var i = 0; i < drafts.Length; i++)
            for (var j = i + 1; j < drafts.Length; j++)
                if (plans[i] is { } a && plans[j] is { } b && drafts[i].EmployeeId == drafts[j].EmployeeId
                    && WorkIntervalMath.Overlaps(new(a.StartUtc, a.EndUtc), new(b.StartUtc, b.EndUtc)))
                { issues[i].Add(ShiftPlanIssue.ProposedOverlap); issues[j].Add(ShiftPlanIssue.ProposedOverlap); }
        return new(Array.AsReadOnly(drafts.Select((_, i) => new ShiftPlanPreviewRow(i,
            plans[i]?.StartUtc, plans[i]?.EndUtc, Array.AsReadOnly(issues[i].Order().ToArray()))).ToArray()));
    }
}