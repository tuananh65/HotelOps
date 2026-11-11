using HotelOps.Api.Services.Dependencies;

namespace HotelOps.Api.Services.Users;

public sealed record UserResponsibilityAssessment(IReadOnlyList<string> Blockers, IReadOnlyList<string> Unresolved);

/// <summary>Consumes DEV2 facts without granting mutation authority or certifying writer coverage.</summary>
public static class UserResponsibilitySafety
{
    public static UserResponsibilityAssessment Assess(long targetId, GlobalUserDependencySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var blockers = new SortedSet<string>(StringComparer.Ordinal);
        var gaps = new SortedSet<string>(StringComparer.Ordinal);
        if (targetId <= 0 || snapshot.UserId != targetId || !Digest(snapshot.Fingerprint)
            || snapshot.Branches.Count == 0 || snapshot.Branches.Select(b => b.BranchId).Distinct().Count() != snapshot.Branches.Count)
            gaps.Add("DEV2_TARGET_COVERAGE_INVALID");
        foreach (var branch in snapshot.Branches)
        {
            if (branch.Resource != "USER" || branch.ResourceId != targetId || branch.BranchId <= 0 || !Digest(branch.Fingerprint))
                gaps.Add("DEV2_BRANCH_COVERAGE_INVALID");
            foreach (var gap in branch.Unresolved) gaps.Add(gap);
            if (branch.Facts.Count(f => f.Category == "TARGET_USER" && f.RecordId == targetId) != 1
                || branch.Facts.Count(f => f.Category == "TARGET_BRANCH" && f.RecordId == branch.BranchId) != 1)
                gaps.Add("DEV2_TARGET_METADATA_MISSING");
            foreach (var fact in branch.Facts)
            {
                if (fact.BranchId != branch.BranchId || fact.RecordId <= 0 || fact.SourceFingerprint is not null && !Digest(fact.SourceFingerprint))
                    gaps.Add("DEV2_FACT_INVALID");
                // These are fingerprinted context, not a responsibility or clearance.
                if (fact.Category is "TARGET_USER" or "TARGET_BRANCH")
                {
                    if (fact.RecordId != (fact.Category == "TARGET_USER" ? targetId : branch.BranchId)) gaps.Add("DEV2_TARGET_METADATA_INVALID");
                    continue;
                }
                switch (fact.Classification)
                {
                    case DependencyClassification.BLOCKING:
                    case DependencyClassification.TRANSFER_REQUIRED: blockers.Add(fact.Category); break;
                    case DependencyClassification.HISTORICAL: break;
                    default: gaps.Add("DEV2_RESPONSIBILITY_POLICY_UNRESOLVED"); break;
                }
            }
        }
        if (snapshot.Unscoped.Count != 0) gaps.Add("DEV2_UNSCOPED_RESPONSIBILITY");
        return new(Array.AsReadOnly(blockers.ToArray()), Array.AsReadOnly(gaps.ToArray()));
    }

    // Excludes only the User context row changed by the owner mutation itself.
    // The reader's exact full fingerprint is still checked before the first write.
    public static string ResponsibilityFingerprint(GlobalUserDependencySnapshot snapshot) =>
        Dev2DependencyReader.SourceFingerprint(new
        {
            snapshot.UserId,
            Branches = snapshot.Branches.OrderBy(b => b.BranchId).Select(b => new
            {
                b.Resource,
                b.ResourceId,
                b.BranchId,
                Facts = b.Facts.Where(f => f.Category != "TARGET_USER").OrderBy(f => f.Category, StringComparer.Ordinal)
                    .ThenBy(f => f.RecordId).ThenBy(f => f.SourceFingerprint, StringComparer.Ordinal),
                Unresolved = b.Unresolved.Order(StringComparer.Ordinal)
            }),
            Unscoped = snapshot.Unscoped.OrderBy(f => f.Category, StringComparer.Ordinal).ThenBy(f => f.RecordId)
        });

    private static bool Digest(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
