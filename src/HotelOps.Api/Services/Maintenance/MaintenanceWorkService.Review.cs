using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Dev2Authorization;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService
{
    private sealed record ReviewProof(MaintenanceReviewDto Review, ResolutionEvidenceProof[] Evidence);

    private async Task<ReviewProof> RequireReviewProofAsync(MaintenanceWork work, Incident incident, long auditId, CancellationToken ct)
    {
        if (!await MaintenanceReviewAccess.AllowsAsync(_dbContext, TrustedActorId, incident.BranchId, work.MaintenanceWorkId, ct))
            throw new Dev2AccessDenied(403);
        var workId = work.MaintenanceWorkId.ToString(CultureInfo.InvariantCulture);
        var audits = await _dbContext.AuditLogs.AsNoTracking().Where(a => a.EntityType == "maintenance_works"
            && a.EntityId == workId && a.OccurredAt == work.ResolvedAt && a.Action == "UPDATE")
            .ToArrayAsync(ct);
        var submissions = audits.Where(a => MaintenanceSubmissionSource.Parse(a) != null).ToArray();
        if (submissions.Length != 1 || submissions[0].AuditId != auditId
            || await MaintenanceSubmissionSource.ReadAsync(_dbContext, auditId, ct) == null)
            throw new ValidationException("The current submitted snapshot is unavailable or changed. Reload before reviewing.");
        ResolutionIssueProof[] materials;
        ResolutionEvidenceProof[] evidence;
        MaintenanceReviewDto review;
        try
        {
            using var doc = JsonDocument.Parse(submissions[0].NewValues!);
            var root = doc.RootElement;
            materials = root.GetProperty("submitted_materials").Deserialize<ResolutionIssueProof[]>()
                ?? throw new JsonException();
            evidence = root.GetProperty("submitted_evidence").Deserialize<ResolutionEvidenceProof[]>()
                ?? throw new JsonException();
            review = new(auditId, DateTime.SpecifyKind(submissions[0].OccurredAt, DateTimeKind.Utc),
                root.GetProperty("submitted_diagnosis").GetString()!, root.GetProperty("submitted_action_taken").GetString()!,
                evidence.Select(e => e.AttachmentId).ToArray());
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or NullReferenceException)
        { throw new ValidationException("Submitted material/evidence snapshot is unavailable."); }
        var current = await ValidateResolutionMaterialsAsync(work, incident, ct);
        // Other work may consume/return the same stock after this submission. Compare this
        // work's immutable usage lineage/quantities, not mutable aggregate consumption totals.
        if (materials.Length != current.Count || materials.Any(m => m == null || m.Usages == null || m.Usages.Any(u => u == null))
            || !materials.OrderBy(m => m.IssueLineId).Zip(current.OrderBy(m => m.IssueLineId)).All(pair =>
                pair.First.IssueLineId == pair.Second.IssueLineId && pair.First.DocumentId == pair.Second.DocumentId
                && pair.First.RequestId == pair.Second.RequestId && pair.First.FulfillmentId == pair.Second.FulfillmentId
                && pair.First.IssuedBaseQty == pair.Second.IssuedBaseQty
                && pair.First.Usages.OrderBy(u => u.UsageId).SequenceEqual(pair.Second.Usages.OrderBy(u => u.UsageId))))
            throw new ValidationException("Submitted material usage changed. Return the result for reconciliation.");
        await RequireReviewEvidenceAsync(review, evidence, incident, ct);
        return new(review, evidence);
    }

    private async Task RequireReviewEvidenceAsync(MaintenanceReviewDto review, IReadOnlyList<ResolutionEvidenceProof> evidence,
        Incident incident, CancellationToken ct)
    {
        var current = await ValidateResolutionEvidenceAsync(new SubmitMaintenanceResultRequest
            { ResultAttachmentIds = review.ResultAttachmentIds.ToArray() }, incident, ct);
        if (!evidence.SequenceEqual(current))
            throw new ValidationException("Submitted evidence changed or is unavailable. Return the result for reconciliation.");
    }
}
