using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Housekeeping;

if (args.Length != 1 || File.Exists(args[0])) throw new ArgumentException("Unused receipt required.");
var cases = new List<string>();
var accessor = new NoIdentity();
// Null SQL collaborators deliberately prove these boundaries never reach persistence.
var service = new CleaningMonitorService(null!, accessor, null!, null!, TimeProvider.System);
foreach (var query in new[] { new CleaningMonitorTimelineQuery(0), new(-1), new(1, 0), new(1, -1),
    new(1, 101), new(int.MaxValue, 100), new(int.MaxValue, int.MaxValue) })
{
    try { await service.DetailAsync(null!, 1, 1, "cleaning", 1, query, default); }
    catch (ValidationException)
    {
        cases.Add($"Reject {query.TimelinePage}/{query.TimelinePageSize} before identity/SQL");
        continue;
    }
    throw new Exception("Invalid timeline accepted.");
}
if (accessor.Calls != 0) throw new Exception("Invalid query reached identity gate.");
cases.Add("Invalid pagination never reaches identity or SQL dependencies");
foreach (var query in new[] { new CleaningMonitorTimelineQuery(), new(2, 2), new(int.MaxValue / 100, 100), new(int.MaxValue, 1) })
{
    var result = await service.DetailAsync(null!, 1, 1, "cleaning", 1, query, default);
    if (result.Status != CleaningMonitorStatus.SessionInvalid || result.Value is not null)
        throw new Exception("Unauthenticated caller reached SQL or received data.");
    cases.Add($"Valid boundary {query.TimelinePage}/{query.TimelinePageSize} still rejects missing identity");
}
var legacy = await service.DetailAsync(null!, 1, 1, "cleaning", 1, default);
if (legacy.Status != CleaningMonitorStatus.SessionInvalid) throw new Exception("Legacy overload bypassed gate.");
cases.Add("Legacy service overload retains default query and session gate");
var defaults = new CleaningMonitorTimelineQuery();
if (defaults.TimelinePage != 1 || defaults.TimelinePageSize != 100) throw new Exception("Default contract changed.");
cases.Add("Backward compatible default latest-100 contract");
await File.WriteAllTextAsync(args[0], JsonSerializer.Serialize(new { success = true, checks = cases.Count,
    kind = "ISOLATED_SERVICE_BOUNDARIES_NO_SQL_NO_HTTPS_NOT_FRAMEWORK", cases }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"PASS {cases.Count} isolated boundary checks; SQL/HTTPS not exercised.");

sealed class NoIdentity : ICurrentUserAccessor
{
    public int Calls { get; private set; }
    public bool TryGetCurrentUser(out CurrentUserInfo user) { Calls++; user = default; return false; }
}
