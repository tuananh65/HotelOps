using System.Data.Common;
using System.Text.Json.Serialization;
using HotelOps.Api.DTOs.WorkTime;
using HotelOps.Api.Security.Authorization;
namespace HotelOps.Api.Services.WorkTime;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ShiftPreviewInput(long EmployeeId, long TemplateId, DateOnly ShiftDate);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ShiftPreviewRequest(IReadOnlyList<ShiftPreviewInput> Drafts);
public sealed record SecurePreviewResult(int Status, ShiftPlanPreview? Value = null);
public interface IShiftPreviewScopeData
{
    Task<bool> ActorScopeAsync(long actor, long branch, long department, CancellationToken ct);
    Task<bool> EmployeeScopeAsync(long employee, long branch, long department, CancellationToken ct);
    Task<bool> TemplateScopeAsync(long template, long branch, CancellationToken ct);
    Task<bool> HiddenOverlapAsync(long employee, long branch, long department, DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
}
public interface IShiftPreviewSessionAdmission { Task<bool> ValidateAsync(CancellationToken ct); }
/// <summary>Real permission evaluator plus server-resolved resource scope, not a role-name bypass.</summary>
public sealed class SecureShiftPlanPreviewService(ICurrentUserAccessor current, IPermissionDataSource paths,
 IShiftPreviewScopeData scope, IShiftPlanPreviewReader reader, IShiftPreviewSessionAdmission session, TimeProvider clock)
{
    public async Task<SecurePreviewResult> PreviewAsync(long branch, long department, ShiftPreviewRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId <= 0 || actor.SessionId <= 0) return new(401);
        if (actor.IsRestricted) return new(403);
        if (branch <= 0 || department <= 0 || request?.Drafts == null || request.Drafts.Count is < 1 or > 500) return new(400);
        var input = request.Drafts.ToArray();
        if (input.Any(d => d == null || d.EmployeeId <= 0 || d.TemplateId <= 0 || d.ShiftDate == default)) return new(400);
        try
        {
            var admitted = await AuthorizeAsync(branch, department, new(input), ct); if (admitted != 200) return new(admitted);
            var drafts = input.Select(d => new ShiftPlanDraft(d.EmployeeId, branch, department, d.TemplateId, d.ShiftDate)).ToArray();
            var protectedReader = new PrivateReader(reader, scope, branch, department);
            var preview = await new ShiftPlanPreviewService(protectedReader).PreviewAsync(drafts, ct);
            var final = await AuthorizeAsync(branch, department, new(input), ct); if (final != 200) return new(final);
            // Do not project hidden resource facts or a reusable authorization/version token.
            var rows = preview.Rows.Select(r => r.Issues.Contains(ShiftPlanIssue.DataUnavailable)
             ? new ShiftPlanPreviewRow(r.Index, null, null, Array.AsReadOnly(new[] { ShiftPlanIssue.DataUnavailable })) : r).ToArray();
            return new(200, new(Array.AsReadOnly(rows)));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) when (e is DbException or TimeoutException or InvalidOperationException or HotelOps.Api.Services.Authentication.SessionStoreUnavailableException) { return new(503); }
    }
    public async Task<int> AuthorizeAsync(long branch, long department, ShiftPreviewRequest request, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId <= 0 || actor.SessionId <= 0) return 401;
        if (actor.IsRestricted) return 403;
        if (branch <= 0 || department <= 0 || request?.Drafts == null || request.Drafts.Count is < 1 or > 500) return 400;
        var input = request.Drafts.ToArray();
        if (input.Any(d => d == null || d.EmployeeId <= 0 || d.TemplateId <= 0 || d.ShiftDate == default)) return 400;
        var permission = new PermissionService(current, paths, new ScopeResolver(scope, branch, department), clock);
        if (!await session.ValidateAsync(ct)) return 401;
        foreach (var resource in new[] { new PermissionResource("shift-preview-branch", branch), new PermissionResource("shift-preview-department", department) }
         .Concat(input.Select(x => new PermissionResource("shift-preview-employee", x.EmployeeId))).Concat(input.Select(x => new PermissionResource("shift-preview-template", x.TemplateId))).Distinct())
        {
            var result = await permission.EvaluateResourceAsync("SHIFT_MANAGE", resource, ct);
            if (result.Decision == PermissionDecision.Unavailable) return 503;
            if (!result.IsGranted) return 403;
        }
        return 200;
    }
    private sealed class ScopeResolver(IShiftPreviewScopeData scope, long branch, long department) : IPermissionResourceResolver
    {
        public async Task<VerifiedPermissionResource?> ResolveAsync(string code, PermissionResource resource, long actor, CancellationToken ct)
        {
            if (code != "SHIFT_MANAGE" || !await scope.ActorScopeAsync(actor, branch, department, ct)) return null;
            var allowed = resource.ResourceType switch
            {
                "shift-preview-branch" => resource.ResourceId == branch,
                "shift-preview-department" => resource.ResourceId == department,
                "shift-preview-employee" => await scope.EmployeeScopeAsync(resource.ResourceId, branch, department, ct),
                "shift-preview-template" => await scope.TemplateScopeAsync(resource.ResourceId, branch, ct),
                _ => false
            };
            return allowed ? new(branch, false) : null; // OWN never grants other-employee roster access.
        }
    }
    private sealed class PrivateReader(IShiftPlanPreviewReader reader, IShiftPreviewScopeData scope, long branch, long department) : IShiftPlanPreviewReader
    {
        public Task<ShiftPlanScope?> ReadScopeAsync(long b, long d, CancellationToken ct) => reader.ReadScopeAsync(b, d, ct);
        public Task<ShiftPlanTemplate?> ReadTemplateAsync(long t, CancellationToken ct) => reader.ReadTemplateAsync(t, ct);
        public Task<ShiftPlanEmployee?> ReadEmployeeAsync(long e, long b, CancellationToken ct) => reader.ReadEmployeeAsync(e, b, ct);
        public async Task<bool> HasOverlapAsync(long e, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
        {
            if (await scope.HiddenOverlapAsync(e, branch, department, start, end, ct)) throw new InvalidOperationException("Unavailable");
            return await reader.HasOverlapAsync(e, start, end, ct);
        }
    }
}