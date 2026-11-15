using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class InternalRequestReadAccessTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
    private static PermissionGrantPath Path() => new("IR_VIEW", "BRANCH", 7, true, true, Now.AddDays(-1), null, true, true);
    private static InternalRequest Request(long id, long branch = 7, long requester = 90) => new()
        { InternalRequestId = id, BranchId = branch, RequesterId = requester, Branch = new() { IsActive = true } };
    private static InternalRequestReadAccess.Scope Scope(string role, PermissionGrantPath? path = null) =>
        InternalRequestReadAccess.Resolve(10, [new(role, path ?? Path())], Now);

    [Theory]
    [InlineData("OPERATIONS_MANAGER", true)]
    [InlineData("DEPT_MANAGER", true)]
    [InlineData("SYS_ADMIN", true)]
    [InlineData("HK_STAFF", false)]
    [InlineData("OWNER", false)]
    public void BranchReadRequiresApprovedActorOnGrantPath(string role, bool allowed)
    {
        var rows = Scope(role).Apply(new[] { Request(1), Request(2, 8) }.AsQueryable(), Now).ToArray();
        Assert.Equal(allowed ? new long[] { 1 } : [], rows.Select(r => r.InternalRequestId));
    }

    [Fact]
    public void RequesterAndOnlyUnambiguousCurrentHandlerAreRelated()
    {
        var own = Request(1, requester: 10);
        var current = Request(2);
        current.InternalRequestAssignments.Add(new() { AssigneeId = 10, Status = "ACTIVE", AssignedAt = Now });
        var ended = Request(3);
        ended.InternalRequestAssignments.Add(new() { AssigneeId = 10, Status = "ACTIVE", AssignedAt = Now.AddDays(-1), EndedAt = Now });
        var future = Request(4);
        future.InternalRequestAssignments.Add(new() { AssigneeId = 10, Status = "ACTIVE", AssignedAt = Now.AddTicks(1) });
        var duplicate = Request(5);
        duplicate.InternalRequestAssignments.Add(new() { AssigneeId = 10, Status = "ACTIVE", AssignedAt = Now });
        duplicate.InternalRequestAssignments.Add(new() { AssigneeId = 11, Status = "ACTIVE", AssignedAt = Now });
        var rows = new[] { own, current, ended, future, duplicate, Request(6, 8, 10) }.AsQueryable();
        Assert.Equal(new long[] { 1, 2 }, Scope("HK_STAFF", Path() with { DataScope = "OWN" })
            .Apply(rows, Now).Select(r => r.InternalRequestId));
        current.ReceivingDepartmentId = 222;
        Assert.Equal(new long[] { 1, 2 }, Scope("HK_STAFF").Apply(rows, Now).Select(r => r.InternalRequestId));
        current.InternalRequestAssignments.Single().EndedAt = Now;
        Assert.Single(Scope("HK_STAFF").Apply(rows, Now));
    }

    [Fact]
    public void InvalidRevokedExpiredFutureAndUnscopedPathsDenyEvenRequester()
    {
        foreach (var path in new[] { Path() with { PermissionCode = "IR_PROCESS" }, Path() with { AssignmentActive = false },
            Path() with { RoleActive = false }, Path() with { BranchActive = false }, Path() with { BranchAccessActive = false },
            Path() with { AssignedAt = Now.AddTicks(1) }, Path() with { ExpiresAt = Now },
            Path() with { DataScope = "DEPARTMENT" }, Path() with { BranchId = null } })
            Assert.Empty(Scope("DEPT_MANAGER", path).Apply(new[] { Request(1, requester: 10) }.AsQueryable(), Now));
    }

    [Fact]
    public void AdministratorNeedsExplicitScopeAndCannotBorrowAnotherRolesGrant()
    {
        Assert.Empty(InternalRequestReadAccess.Resolve(10, [], Now).Apply(new[] { Request(1) }.AsQueryable(), Now));
        var global = Path() with { BranchId = null, DataScope = "ALL" };
        Assert.Single(Scope("SYS_ADMIN", global).Apply(new[] { Request(1, 8) }.AsQueryable(), Now));
        Assert.Empty(Scope("DEPT_MANAGER", global).Apply(new[] { Request(1, 8) }.AsQueryable(), Now));
        Assert.Empty(Scope("SYS_ADMIN", Path() with { DataScope = "OWN" }).Apply(new[] { Request(1) }.AsQueryable(), Now));
        var inactive = Request(1); inactive.Branch.IsActive = false;
        Assert.Empty(Scope("SYS_ADMIN", global).Apply(new[] { inactive }.AsQueryable(), Now));
    }

    [Fact]
    public void ScopeTranslatesToSqlBeforePaginationWithoutDepartmentOwnershipInference()
    {
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true").Options);
        var sql = Scope("HK_STAFF").Apply(db.InternalRequests, Now).OrderBy(r => r.InternalRequestId).Take(25).ToQueryString();
        Assert.Contains("internal_request_assignments", sql);
        Assert.Contains("requester_id", sql);
        Assert.Contains("ended_at", sql);
        Assert.Contains("is_active", sql);
        Assert.DoesNotContain("manager_user_id", sql);
    }
}
