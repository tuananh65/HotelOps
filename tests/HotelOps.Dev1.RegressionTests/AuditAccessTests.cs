using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Insights;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class AuditAccessTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);
    private static PermissionGrantPath Grant() => new("AUDIT_VIEW", "BRANCH", 7, true, true, Now.AddDays(-1), null, true, true);

    [Theory]
    [InlineData("SYS_ADMIN", true)]
    [InlineData("OWNER", false)]
    [InlineData("OPERATIONS_MANAGER", false)]
    [InlineData("DEPT_MANAGER", false)]
    public void AuditActorIsSeparateFromReportActors(string role, bool expected)
        => Assert.Equal(expected, AuditAccess.Allows(role, Grant(), 7, Now));

    [Fact]
    public void BranchPermissionAndRoleMustBelongToTheSameEligiblePath()
    {
        Assert.False(AuditAccess.Allows("SYS_ADMIN", Grant() with { PermissionCode = "REPORT_VIEW" }, 7, Now));
        Assert.False(AuditAccess.Allows("SYS_ADMIN", Grant(), 8, Now));
        Assert.False(AuditAccess.Allows("SYS_ADMIN", Grant(), null, Now));
        Assert.True(AuditAccess.Allows("SYS_ADMIN", Grant() with { DataScope = "ALL", BranchId = null }, null, Now));
        foreach (var path in new[] { Grant() with { AssignmentActive = false }, Grant() with { RoleActive = false },
            Grant() with { BranchActive = false }, Grant() with { BranchAccessActive = false },
            Grant() with { AssignedAt = Now.AddSeconds(1) }, Grant() with { ExpiresAt = Now }, Grant() with { DataScope = "OWN" } })
            Assert.False(AuditAccess.Allows("SYS_ADMIN", path, 7, Now));
    }
}
