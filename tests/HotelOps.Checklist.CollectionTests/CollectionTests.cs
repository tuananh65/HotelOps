using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Checklist.CollectionTests;

public sealed class CollectionTests
{
    private static readonly ChecklistScopeV1[] Scopes = [
        new() { ChecklistId = 1, BranchId = 7 }, new() { ChecklistId = 2, BranchId = 7 },
        new() { ChecklistId = 3, BranchId = 8 }, new() { ChecklistId = 4, BranchId = null }];
    private static readonly ChecklistRoomTypeV1[] Types = [
        new() { ChecklistId = 1, RoomTypeId = 11 }, new() { ChecklistId = 2, RoomTypeId = 12 }];
    private static InspectionChecklist[] Roots() => [
        new() { ChecklistId = 1, ChecklistCode = "A", ChecklistName = "Bedroom", IsActive = true,
            InspectionChecklistVersions = [new() { VersionId = 10, VersionNo = 1, Status = "RETIRED", RoomInspections = [new()] },
                new() { VersionId = 11, VersionNo = 2, Status = "PUBLISHED" }, new() { VersionId = 12, VersionNo = 3, Status = "DRAFT" }] },
        new() { ChecklistId = 2, ChecklistCode = "B", ChecklistName = "Bathroom", IsActive = false,
            InspectionChecklistVersions = [new() { VersionId = 20, VersionNo = 1, Status = "RETIRED" }] },
        new() { ChecklistId = 3, ChecklistCode = "FOREIGN", ChecklistName = "Hidden", IsActive = true },
        new() { ChecklistId = 4, ChecklistCode = "LEGACY", ChecklistName = "Hidden", IsActive = true }];

    private static ChecklistListItemDto[] List(long branch = 7, ChecklistCollectionRequest? request = null)
    {
        var filter = ChecklistCollectionQuery.Validate(branch, request ?? new());
        var rows = ChecklistCollectionQuery.Rows(Scopes.AsQueryable(), Roots().AsQueryable(), Types.AsQueryable(), branch, filter);
        return ChecklistCollectionQuery.Page(rows, Types.AsQueryable(), branch, filter).ToArray();
    }

    [Fact] public void Branch_owned_list_excludes_foreign_and_unowned_legacy()
    {
        var items = List(); Assert.Equal(new long[] { 1, 2 }, items.Select(x => x.Id));
        Assert.All(items, item => Assert.Equal(7, item.BranchId));
        Assert.Equal(new long[] { 11 }, items[0].RoomTypeIds);
    }
    [Fact] public void Empty_branch_returns_empty_collection() => Assert.Empty(List(99));
    [Fact] public void Historical_versions_are_preserved_and_usage_is_not_rewritten()
    {
        var item = List()[0]; Assert.Equal(new[] { "RETIRED", "PUBLISHED", "DRAFT" }, item.Versions.Select(v => v.Status));
        Assert.True(item.Versions[0].UsedByInspection); Assert.False(item.Versions[1].UsedByInspection);
        Assert.Equal(new[] { 1, 2, 3 }, item.Versions.Select(v => v.Number));
    }
    [Theory] [InlineData("DRAFT", 1)] [InlineData("PUBLISHED", 1)] [InlineData("RETIRED", 2)]
    public void Status_matches_any_version_and_returns_all_history(string status, int count)
    {
        var result = List(request: new() { Status = status }); Assert.Equal(count, result.Length);
        Assert.Equal(3, result[0].Versions.Count);
    }
    [Fact] public void Active_search_and_type_filters_compose()
    {
        Assert.Equal(1, Assert.Single(List(request: new() { Search = "  Bed  ", IsActive = true, RoomTypeId = 11 })).Id);
        Assert.Empty(List(request: new() { RoomTypeId = 12, IsActive = true }));
        Assert.Equal(2, Assert.Single(List(request: new() { IsActive = false })).Id);
    }
    [Fact] public void Stable_pagination_has_no_duplicate_or_missing_items()
    {
        Assert.Equal(1, Assert.Single(List(request: new() { PageSize = 1 })).Id);
        Assert.Equal(2, Assert.Single(List(request: new() { PageSize = 1, Page = 2 })).Id);
        Assert.Empty(List(request: new() { PageSize = 1, Page = 3 }));
    }
    [Theory] [InlineData(0, 20)] [InlineData(-1, 20)] [InlineData(1, 0)] [InlineData(1, 101)] [InlineData(int.MaxValue, 100)]
    public void Invalid_pagination_is_rejected(int page, int size)
        => Assert.Throws<ValidationException>(() => ChecklistCollectionQuery.Validate(7, new() { Page = page, PageSize = size }));
    [Theory] [InlineData(0)] [InlineData(-1)]
    public void Invalid_branch_is_rejected(long branch)
        => Assert.Throws<ValidationException>(() => ChecklistCollectionQuery.Validate(branch, new()));
    [Theory] [InlineData(0)] [InlineData(-1)]
    public void Invalid_type_is_rejected(long id)
        => Assert.Throws<ValidationException>(() => ChecklistCollectionQuery.Validate(7, new() { RoomTypeId = id }));
    [Theory] [InlineData("draft")] [InlineData("UNKNOWN")] [InlineData("")]
    public void Unknown_status_is_rejected(string status)
        => Assert.Throws<ValidationException>(() => ChecklistCollectionQuery.Validate(7, new() { Status = status }));
    [Fact] public void Search_boundaries_and_defaults()
    {
        Assert.Throws<ValidationException>(() => ChecklistCollectionQuery.Validate(7, new() { Search = new('A', 201) }));
        Assert.Throws<ValidationException>(() => ChecklistCollectionQuery.Validate(7, new() { Search = "x\0y" }));
        var filter = ChecklistCollectionQuery.Validate(7, new() { Search = "  " });
        Assert.Null(filter.Search); Assert.Equal(1, filter.Page); Assert.Equal(20, filter.PageSize);
        Assert.Equal(200, ChecklistCollectionQuery.Validate(7, new() { Search = new('A', 200), PageSize = 100 }).Search!.Length);
    }
    [Fact] public void SqlServer_translates_scoped_filtered_summary_without_executing_SQL()
    {
        // No server/connection string. ToQueryString only compiles relational expressions.
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer().Options);
        var filter = new ChecklistCollectionRequest { Status = "RETIRED", RoomTypeId = 11, IsActive = true, Search = "Bed", PageSize = 1 };
        var rows = ChecklistCollectionQuery.Rows(db.ChecklistScopes.AsNoTracking(), db.InspectionChecklists.AsNoTracking(), db.ChecklistRoomTypes.AsNoTracking(), 7, filter);
        var sql = ChecklistCollectionQuery.Page(rows, db.ChecklistRoomTypes.AsNoTracking(), 7, filter).AsSplitQuery().ToQueryString();
        Assert.Contains("[branch_id]", sql); Assert.Contains("EXISTS", sql); Assert.Contains("OFFSET", sql); Assert.Contains("FETCH NEXT", sql);
        Assert.Empty(db.ChangeTracker.Entries());
    }
    [Fact] public void Collection_route_reuses_existing_cookie_authorization()
    {
        var type = typeof(InspectionChecklistsController);
        Assert.Equal(HotelOpsCookieTicket.Scheme, Assert.Single(type.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).AuthenticationSchemes);
        Assert.Null(Assert.Single(type.GetMethod("List")!.GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>()).Template);
    }
    [Theory] [InlineData(false, 400)] [InlineData(true, 401)]
    public async Task Http_guard_rejects_http_or_missing_ticket(bool https, int expected)
    {
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer().Options);
        var controller = new InspectionChecklistsController(new(db, new Actor(), new Permission(), new Sessions(), TimeProvider.System));
        var http = new DefaultHttpContext(); http.Request.Scheme = https ? "https" : "http";
        controller.ControllerContext = new() { HttpContext = http };
        Assert.Equal(expected, Assert.IsType<ObjectResult>(await controller.List(7, new(), default)).StatusCode);
    }
    [Theory] [InlineData(PermissionDecision.Denied, false, 403)] [InlineData(PermissionDecision.Unavailable, false, 503)] [InlineData(PermissionDecision.Granted, true, 401)]
    public async Task Existing_service_gate_fails_closed_before_collection_queries(PermissionDecision decision, bool expired, int expected)
    {
        // Fake ADO supports ONLY the existing UserEligibilityLock SELECT; it cannot reach a real DB.
        using var connection = new LockOnlyConnection();
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
        var permission = new Permission { Decision = decision };
        var service = new RoomTypeCreationService(db, new Actor(), permission, new Sessions { Expired = expired }, TimeProvider.System);
        var controller = new InspectionChecklistsController(service);
        var http = new DefaultHttpContext(); http.Request.Scheme = "https";
        var ticket = SessionReference.FromProtectedTicket(5, SessionCredential.Generate(), false, DateTime.UtcNow);
        http.User = HotelOpsCookieTicket.BuildPrincipal(ticket, 9); controller.ControllerContext = new() { HttpContext = http };
        var result = Assert.IsType<ObjectResult>(await controller.List(7, new(), default));
        Assert.Equal(expected, result.StatusCode); Assert.IsType<ProblemDetails>(result.Value);
        Assert.True(expired || permission.Calls.SequenceEqual(new[] { ("CHECKLIST_MANAGE", 7L) }));
        Assert.Equal(1, connection.Reads); Assert.False(connection.Committed);
    }
    private sealed class Actor : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(5, 9, false); return true; } }
    private sealed class Permission : IPermissionService
    {
        public PermissionDecision Decision { get; init; } = PermissionDecision.Denied;
        public List<(string, long)> Calls { get; } = [];
        public Task<PermissionEvaluation> EvaluateBranchAsync(string code, long branch, CancellationToken ct) { Calls.Add((code, branch)); return Task.FromResult(new PermissionEvaluation(Decision)); }
        public Task<PermissionEvaluation> EvaluateGlobalAsync(string code, CancellationToken ct) => throw new InvalidOperationException("No global fallback allowed.");
        public Task<PermissionEvaluation> EvaluateResourceAsync(string code, PermissionResource resource, CancellationToken ct) => throw new InvalidOperationException("No parallel authority allowed.");
    }
    private sealed class Sessions : ISqlSessionService
    {
        public bool Expired { get; init; }
        public Task<SessionValidationResult> ValidateSessionAsync(SessionReference ticket, CancellationToken ct = default)
            => Task.FromResult(new SessionValidationResult(!Expired, Expired ? null : new(9, 5, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5), DateTime.UtcNow.AddMinutes(10), false)));
        public Task<CreatedSqlSession> CreateSessionAsync(CredentialLockoutResult credential, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SessionValidationResult> TouchSessionAsync(SessionReference ticket, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> RevokeSessionAsync(SessionReference ticket, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> RevokeAllSessionsAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}

// Test-only in-memory ADO boundary, not a SQL simulator or integration receipt.
internal sealed class LockOnlyConnection : DbConnection
{
    private ConnectionState state;
    public int Reads { get; private set; }
    public bool Committed { get; set; }
    [AllowNull] public override string ConnectionString { get; set; } = "";
    public override string Database => "NO_DATABASE";
    public override string DataSource => "NO_SERVER";
    public override string ServerVersion => "16.0.0";
    public override ConnectionState State => state;
    public override void Open() => state = ConnectionState.Open;
    public override void Close() => state = ConnectionState.Closed;
    public override void ChangeDatabase(string name) => throw new NotSupportedException();
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolation) => new Transaction(this, isolation);
    protected override DbCommand CreateDbCommand() => new Command(this);
    private sealed class Transaction(LockOnlyConnection connection, IsolationLevel isolation) : DbTransaction
    {
        public override IsolationLevel IsolationLevel => isolation;
        protected override DbConnection DbConnection => connection;
        public override void Commit() => connection.Committed = true;
        public override void Rollback() { }
    }
    private sealed class Command(LockOnlyConnection connection) : DbCommand
    {
        private readonly Microsoft.Data.SqlClient.SqlCommand parameters = new();
        [AllowNull] public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; } = connection;
        protected override DbTransaction? DbTransaction { get; set; }
        protected override DbParameterCollection DbParameterCollection => parameters.Parameters;
        protected override DbParameter CreateDbParameter() => new Microsoft.Data.SqlClient.SqlParameter();
        public override void Cancel() { }
        public override void Prepare() { }
        public override int ExecuteNonQuery() => throw new InvalidOperationException("Mutations forbidden.");
        public override object ExecuteScalar() => throw new NotSupportedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            Assert.Contains("dbo.users WITH (UPDLOCK, HOLDLOCK)", CommandText);
            connection.Reads++;
            var table = new DataTable(); table.Columns.Add("user_id", typeof(long)); table.Columns.Add("deleted_at", typeof(DateTime));
            table.Columns.Add("account_status", typeof(string)); table.Columns.Add("employment_status", typeof(string));
            table.Rows.Add(5L, DBNull.Value, "ACTIVE", "ACTIVE"); return table.CreateDataReader();
        }
    }
}