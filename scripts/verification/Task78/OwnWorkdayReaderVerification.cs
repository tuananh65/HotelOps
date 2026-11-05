using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.WorkTime;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Read-only component runner. Identity is a test collaborator, not HTTPS authentication evidence.
var trial = "HotelOps_Canonical_20261103_162910_76025447_Fresh";
var source = Environment.GetEnvironmentVariable("TASK78_TRIAL_SQL_SOURCE")
    ?? throw new InvalidOperationException("Explicit trial SQL source required");
if (args.Length != 1 || File.Exists(args[0])) throw new ArgumentException("One unused receipt required");
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
var ct = timeout.Token;
var cs = new SqlConnectionStringBuilder { DataSource = source, InitialCatalog = trial,
    IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10,
    ApplicationName = "HotelOps Task78 read-only component" }.ConnectionString;
await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(cs).Options);
var marker = await db.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(100),value) AS [Value] FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.WorkTime.Contract'").SingleAsync(ct);
if (marker != "Task77-v1") throw new InvalidOperationException("Owned A1 marker missing");
var cases = new List<string>();
void Check(bool ok, string label) { if (!ok) throw new Exception(label); cases.Add(label); Console.WriteLine("PASS " + label); }
async Task Reject(OwnWorkdaySnapshotReader reader, long branch, string code, string label)
{
    try { await reader.ReadAsync(branch, ct); }
    catch (WorkTimeContractException e) when (e.Code == code) { Check(true, label); return; }
    throw new Exception("Missing rejection " + label);
}
var access = await db.UserBranchAccesses.AsNoTracking().Where(a => a.RevokedAt == null
    && a.User.AccountStatus == "ACTIVE" && a.User.EmploymentStatus == "ACTIVE"
    && a.User.DeletedAt == null && a.Branch.IsActive && a.GrantedAt <= DateTime.UtcNow)
    .Select(a => new { a.UserId, a.BranchId }).FirstAsync(ct);
var identity = new Identity(new(access.UserId, 1, false));
var reader = new OwnWorkdaySnapshotReader(db, identity);
var before = await db.Database.SqlQuery<string>($"SELECT (SELECT * FROM dbo.workdays ORDER BY workday_id FOR JSON PATH) AS [Value]").SingleAsync(ct);
await Reject(new(db, new Identity(null)), access.BranchId, "TIME_SESSION_INVALID", "Unauthenticated identity refused");
await Reject(new(db, new Identity(new(access.UserId, 1, true))), access.BranchId, "TIME_RESTRICTED", "Restricted identity refused");
await Reject(reader, 0, "TIME_SCOPE_DENIED", "Invalid branch refused");
await Reject(reader, long.MaxValue, "TIME_SCOPE_DENIED", "Inaccessible branch refused");
await Reject(new(db, new Identity(new(long.MaxValue, 1, false))), access.BranchId,
    "TIME_ACTOR_INVALID", "Unknown account refused");
var result = await reader.ReadAsync(access.BranchId, ct);
var expected = await db.Workdays.AsNoTracking().SingleOrDefaultAsync(d => d.UserId == access.UserId
    && d.BranchId == access.BranchId && d.Status == "WORKING", ct);
Check(result?.WorkdayId == expected?.WorkdayId, "Own current result matches independent SQL query (including no Working day)");
Check(!db.ChangeTracker.Entries().Any(), "Read query tracks no entities");
var after = await db.Database.SqlQuery<string>($"SELECT (SELECT * FROM dbo.workdays ORDER BY workday_id FOR JSON PATH) AS [Value]").SingleAsync(ct);
Check(before == after, "Workday contents unchanged by component checks");
await File.WriteAllTextAsync(args[0], JsonSerializer.Serialize(new { trial, marker, checks=cases.Count,
    cases, scope="READ_ONLY_SQL_COMPONENT_IDENTITY_DOUBLE_NOT_HTTPS_OR_PERMISSION_ACCEPTANCE",
    populatedWorkingDayCovered=expected is not null, finishedUtc=DateTimeOffset.UtcNow }, new JsonSerializerOptions { WriteIndented=true }), ct);
sealed class Identity(CurrentUserInfo? actor) : ICurrentUserAccessor
{
    public bool TryGetCurrentUser(out CurrentUserInfo user) { user=actor.GetValueOrDefault(); return actor.HasValue; }
}
