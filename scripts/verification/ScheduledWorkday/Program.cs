using HotelOps.Api.Entities;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using HotelOps.Api.Controllers;
using HotelOps.Api.DTOs.WorkTime;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
var count = 0; void Check(bool v, string name) { if (!v) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
Check(typeof(WorkdaysController).IsDefined(typeof(NonControllerAttribute)), "mutation controller remains inactive");
Check(typeof(WorkdaysController).GetMethod("Start")!.GetCustomAttribute<HttpPostAttribute>()?.Template == null, "scheduled start POST template");
Check(typeof(WorkdaysController).GetMethod("End")!.GetCustomAttribute<HttpPostAttribute>()?.Template == "{workdayId:long}/end", "end POST route");
foreach (var restricted in new[] { false, true })
{
    var actor = new Actor(restricted); var service = new WorkdayService(null!, actor, new HttpContextAccessor(), null!, null!, null!, null!);
    var start = await service.StartAsync(1, new() { ShiftAssignmentId = 10 }, default); Check(start.Status == (restricted ? 403 : 401), "start early identity gate " + restricted);
    var end = await service.EndAsync(1, 20, new byte[8], false, default); Check(end.Status == (restricted ? 403 : 401), "end early identity gate " + restricted);
}
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
Check(JsonSerializer.Deserialize<StartWorkdayRequest>("{\"shiftAssignmentId\":10}", options)!.ShiftAssignmentId == 10, "start payload");
foreach (var json in new[] { "{\"shiftAssignmentId\":10,\"userId\":7}", "{\"shiftAssignmentId\":10,\"branchId\":1}" }) { var denied = false; try { JsonSerializer.Deserialize<StartWorkdayRequest>(json, options); } catch (JsonException) { denied = true; } Check(denied, "caller identity body rejected"); }
Check(JsonSerializer.Deserialize<EndWorkdayRequest>("{\"pauseAll\":false}", options)!.PauseAll == false, "explicit no pause-all");
foreach (var tag in new[] { "*", "W/\"AAAAAAAAAAE=\"", "\"bad\"", "\"AAAAAAAAAAF=\"" })
{
    var controller = new WorkdaysController(null!) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } }; controller.Request.Headers.IfMatch = tag;
    var result = await controller.End(1, 20, new() { PauseAll = false }, default); Check(result is ObjectResult { StatusCode: 400 }, "invalid ETag rejected " + tag);
}
var http = new WorkdaysController(null!) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } }; http.Request.Scheme = "http"; Check(await http.Start(1, new() { ShiftAssignmentId = 10 }, default) is ObjectResult { StatusCode: 400 }, "HTTPS before scheduled service");
// Explicit source assertions complement executable guards; NOT SQL behavioral/race evidence.
var serviceSource = File.ReadAllText("src/HotelOps.Api/Services/WorkTime/WorkdayService.cs");
var participant = File.ReadAllText("src/HotelOps.Api/Services/WorkTime/SqlWorkTimeTransactionParticipant.cs");
foreach (var token in new[] { "IsolationLevel.Serializable", "participant.LockUsersAsync([actor.UserId]", "sessions.ValidateSessionAsync(ticket", "ticket.UserId != actor.UserId", "Authorize(\"ATTENDANCE_RECORD\"", "a.UserId == actor.UserId && a.Shift.BranchId == branchId", "TIME_STALE", "MSG31_PAUSE_ALL_REQUIRED", "UNSCHEDULED_NOTIFICATION_UNAVAILABLE", "PAUSE_ALL_UNAVAILABLE" }) Check(serviceSource.Contains(token), "service boundary source: " + token);
foreach (var token in new[] { "new UserEligibilityLock(db).LockAsync", "sp_getapplock", "RequireLocks(actorUserId)", "Working(actorUserId).AnyAsync", "assignment.UserId != actorUserId", "assignment.AssignmentStatus != \"ASSIGNED\"", "shift.BranchId != branchId", "x.IsActive", "day.RowVersion.AsSpan().SequenceEqual(expectedRowVersion)", "Open(actorUserId).AnyAsync", "x.RowVersion == expectedRowVersion", "SetProperty(x => x.Status, \"ENDED\")", "\"CREATE\"", "\"UPDATE\"", "db.SaveChangesAsync" }) Check(participant.Contains(token), "participant invariant source: " + token);
Check(serviceSource.IndexOf("participant.LockUsersAsync", StringComparison.Ordinal) < serviceSource.IndexOf("sessions.ValidateSessionAsync", StringComparison.Ordinal), "user-first source ordering");
Check(serviceSource.Contains("await using var tx") && serviceSource.Contains("if (result.Status is >= 200 and < 300) await tx.CommitAsync(ct)"), "transaction disposal/commit boundary source");

// Executed business query over mutable history, not source-string assertions.
var assignments = new[] { new ShiftAssignment { ShiftAssignmentId = 10, UserId = 7 }, new ShiftAssignment { ShiftAssignmentId = 11, UserId = 7 }, new ShiftAssignment { ShiftAssignmentId = 12, UserId = 8 } }.AsQueryable();
var history = new List<Workday>();
long[] Available() => ScheduledWorkdayClaims.Unclaimed(assignments, history.AsQueryable()).Select(a => a.ShiftAssignmentId).ToArray();
Check(Available().SequenceEqual(new long[] { 10, 11, 12 }), "claim behavior: fresh assignments eligible");
history.Add(new() { WorkdayId = 1, UserId = 7, ShiftAssignmentId = 10, Status = "WORKING" });
Check(Available().SequenceEqual(new long[] { 11, 12 }), "claim behavior: working claim omitted");
history[0].Status = "ENDED";
Check(Available().SequenceEqual(new long[] { 11, 12 }), "claim behavior: End never releases assignment claim");
history.Add(new() { WorkdayId = 2, UserId = 7, ShiftAssignmentId = null, Status = "ENDED", IsUnscheduled = true });
Check(Available().SequenceEqual(new long[] { 11, 12 }), "claim behavior: unscheduled history does not consume unrelated assignment");
history.Add(new() { WorkdayId = 3, UserId = 8, ShiftAssignmentId = 11, Status = "ENDED" });
Check(Available().SequenceEqual(new long[] { 12 }), "claim behavior: inconsistent ownership history fails closed");
history.Add(new() { WorkdayId = 4, UserId = 7, ShiftAssignmentId = 10, Status = "ENDED" });
Check(Available().SequenceEqual(new long[] { 12 }), "claim behavior: duplicate history does not multiply eligible rows");
Check(!ScheduledWorkdayClaims.Unclaimed(assignments.Where(a => a.ShiftAssignmentId == 10), history.AsQueryable()).Any(), "claim behavior: replay after End denied by writer query");
using (var queryDb = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer("Server=offline.invalid;Database=OFFLINE_QUERY_TRANSLATION;Integrated Security=true;Encrypt=true;TrustServerCertificate=false").Options))
{
    var sql = ScheduledWorkdayClaims.Unclaimed(queryDb.ShiftAssignments.AsNoTracking(), queryDb.Workdays.AsNoTracking()).Select(a => a.ShiftAssignmentId).ToQueryString();
    Check(sql.Contains("NOT EXISTS") && sql.Contains("[workdays]") && sql.Contains("[shift_assignment_id]"), "SQL adapter translates claim anti-join without connection; execution NOT_RUN");
}
Console.WriteLine($"RESULT: {count} PASS. SQL success/race/cancellation/rollback acceptance NOT RUN.");
sealed class Actor(bool restricted) : ICurrentUserAccessor { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(7, 9, restricted); return restricted; } }
