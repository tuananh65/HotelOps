using HotelOps.Api.Services.Dependencies;
using HotelOps.Api.Services.Users;

internal static class ResponsibilityIntegrationChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var digest = new string('A', 64);
        DependencyFact Fact(string category, DependencyClassification classification, long branch = 1) => new(category, 20, branch, "STATE", classification, digest);
        DependencySnapshot Branch(long id, params DependencyFact[] facts) => new("USER", 8, id,
            new[] { new DependencyFact("TARGET_USER", 8, id, "ACTIVE", DependencyClassification.UNKNOWN_POLICY, digest),
                new DependencyFact("TARGET_BRANCH", id, id, "INACTIVE", DependencyClassification.UNKNOWN_POLICY, digest) }.Concat(facts).ToArray(), [], digest);
        GlobalUserDependencySnapshot Snapshot(params DependencySnapshot[] branches) => new(8, branches, [], digest);
        var clear = Snapshot(Branch(1));
        var assessment = UserResponsibilitySafety.Assess(8, clear);
        check(assessment.Blockers.Count == 0 && assessment.Unresolved.Count == 0, "complete synthetic DEV2 facts classify clear only, never release");
        foreach (var category in new[] { "MAINTENANCE", "ASSET", "ASSET_HANDOVER", "MATERIAL_REQUEST", "MATERIAL_REQUEST_APPROVER", "WAREHOUSE" })
        {
            assessment = UserResponsibilitySafety.Assess(8, Snapshot(Branch(1, Fact(category, DependencyClassification.BLOCKING))));
            check(assessment.Blockers.SequenceEqual([category]) && assessment.Unresolved.Count == 0, category + " responsibility blocks even in inactive branch");
        }
        assessment = UserResponsibilitySafety.Assess(8, Snapshot(Branch(1, Fact("ASSET", DependencyClassification.TRANSFER_REQUIRED))));
        check(assessment.Blockers.Count == 1, "transfer required cannot silently release responsibility");
        assessment = UserResponsibilitySafety.Assess(8, Snapshot(Branch(1, Fact("HISTORICAL_ACTOR", DependencyClassification.HISTORICAL) with { SourceFingerprint = null })));
        check(assessment.Blockers.Count == 0 && assessment.Unresolved.Count == 0, "historical reporter with optional fingerprint is not current assignee");
        assessment = UserResponsibilitySafety.Assess(8, Snapshot(Branch(1, Fact("MATERIAL_FULFILLMENT", DependencyClassification.UNKNOWN_POLICY))));
        check(assessment.Unresolved.Count != 0, "unknown receipt policy remains unresolved");
        assessment = UserResponsibilitySafety.Assess(8, Snapshot(Branch(1, Fact("UNKNOWN", (DependencyClassification)99))));
        check(assessment.Unresolved.Count != 0, "unsupported classification fails closed");
        foreach (var invalid in new[] {
            clear with { UserId = 9 }, clear with { Fingerprint = "invalid" }, Snapshot(), Snapshot(Branch(1), Branch(1)),
            Snapshot(Branch(1) with { ResourceId = 9 }), Snapshot(Branch(1) with { Resource = "ROOM" }),
            Snapshot(Branch(1) with { Facts = [] }), Snapshot(Branch(1, Fact("ASSET", DependencyClassification.BLOCKING, 2))),
            clear with { Unscoped = [new("MAINTENANCE_UNRESOLVED_SCOPE", 20, "OPEN", digest)] },
            Snapshot(Branch(1) with { Unresolved = ["WRITER_COVERAGE_REQUIRED"] }),
            Snapshot(Branch(1, Fact("ASSET", DependencyClassification.BLOCKING) with { SourceFingerprint = "invalid" })) })
            check(UserResponsibilitySafety.Assess(8, invalid).Unresolved.Count != 0, "malformed, incomplete or unscoped coverage fails closed");
        var first = UserResponsibilitySafety.ResponsibilityFingerprint(clear);
        var ownWrite = Snapshot(Branch(1) with { Facts = Branch(1).Facts.Select(f => f.Category == "TARGET_USER" ? f with { State = "INACTIVE", SourceFingerprint = new string('B', 64) } : f).ToArray() });
        check(first == UserResponsibilitySafety.ResponsibilityFingerprint(ownWrite), "owner User status/version change does not invalidate responsibility-only fingerprint");
        check(first != UserResponsibilitySafety.ResponsibilityFingerprint(Snapshot(Branch(1, Fact("ASSET", DependencyClassification.BLOCKING)))), "late new custody changes final fingerprint");
        check(first != UserResponsibilitySafety.ResponsibilityFingerprint(Snapshot(Branch(1), Branch(2))), "branch universe replacement changes final fingerprint");
        check(first != UserResponsibilitySafety.ResponsibilityFingerprint(clear with { Unscoped = [new("MAINTENANCE", 20, "OPEN", digest)] }), "late unscoped work changes final fingerprint");
        var blocked = UserResponsibilitySafety.Assess(8, Snapshot(Branch(1, Fact("ASSET", DependencyClassification.BLOCKING)) with { Unresolved = ["WRITER_PENDING"] }));
        check(UserRoleRevocationService.ValidateResponsibilities(blocked.Blockers, blocked.Unresolved, [], []) == HotelOps.Api.DTOs.Users.RevokeRoleStatus.ResponsibilitiesUnresolved, "known blocker never masks incomplete coverage");
    }
}
