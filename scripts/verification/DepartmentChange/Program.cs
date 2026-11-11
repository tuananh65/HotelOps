using System.Text.Json;
using HotelOps.Api.Entities;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Controllers;
using HotelOps.Api.Services.Users;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
int count = 0; void Check(bool v, string name) { if (!v) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); count++; }
var now = DateTime.UtcNow;
User Target() => new() { UserId = 8, PrimaryBranchId = 1, DepartmentId = 2, AccountStatus = "ACTIVE", EmploymentStatus = "ACTIVE", RowVersion = new byte[8] };
Branch Branch() => new() { BranchId = 1, IsActive = true };
Department Dept(long id) => new() { DepartmentId = id, BranchId = 1, IsActive = true };
UserBranchAccess Membership() => new() { UserId = 8, BranchId = 1, IsPrimary = true, AccessLevel = "STANDARD", GrantedAt = now.AddDays(-1) };
DepartmentChangeStatus? Validate(User? t = null, Branch? b = null, Department? old = null, Department? dest = null, IReadOnlyList<UserBranchAccess>? members = null, IReadOnlyList<UserRole>? roles = null, byte[]? version = null)
 => UserDepartmentChangeService.ValidateState(t ?? Target(), b ?? Branch(), old ?? Dept(2), dest ?? Dept(3), members ?? [Membership()], roles ?? [], version ?? new byte[8], now);
Check(Validate() == null, "role-less employee may move between distinct departments inside primary branch (facts only)");
var t = Target(); var m = Membership(); var old = Dept(2); var dest = Dept(3); Check(Validate(t, old: old, dest: dest, members: [m]) == null && t.DepartmentId == 2 && t.PrimaryBranchId == 1 && m.IsPrimary, "validation never mutates user or membership");
Check(Validate(dest: Dept(2)) == DepartmentChangeStatus.NoChange, "same destination rejected without mutation");
Check(Validate(version: [1, 0, 0, 0, 0, 0, 0, 0]) == DepartmentChangeStatus.Stale, "stale User version");
t = Target(); t.AccountStatus = "INACTIVE"; Check(Validate(t) == DepartmentChangeStatus.InvalidScope, "inactive target");
t = Target(); t.EmploymentStatus = "INACTIVE"; Check(Validate(t) == DepartmentChangeStatus.InvalidScope, "inactive employment");
t = Target(); t.DeletedAt = now; Check(Validate(t) == DepartmentChangeStatus.NotFound, "deleted target");
t = Target(); t.PrimaryBranchId = 9; Check(Validate(t) == DepartmentChangeStatus.InvalidScope, "primary branch cannot change");
dest = Dept(3); dest.BranchId = 9; Check(Validate(dest: dest) == DepartmentChangeStatus.InvalidScope, "cross-branch destination");
dest = Dept(3); dest.IsActive = false; Check(Validate(dest: dest) == DepartmentChangeStatus.InvalidScope, "inactive destination");
old = Dept(2); old.BranchId = 9; Check(Validate(old: old) == DepartmentChangeStatus.InvalidScope, "old department must belong to primary branch");
var branch = Branch(); branch.IsActive = false; Check(Validate(b: branch) == DepartmentChangeStatus.InvalidScope, "inactive primary branch");
foreach (var active in new[] { false, true }) foreach (var expires in new DateTime?[] { null, now.AddDays(-1), now.AddDays(1) })
    Check(Validate(roles: [new() { UserId = 8, UserRoleId = 10, RoleId = 4, IsActive = active, ExpiresAt = expires }]) == DepartmentChangeStatus.RoleConflict, "all active/inactive/expiry role history preserved and denied");
Check(Validate(members: []) == DepartmentChangeStatus.InvalidScope, "missing primary membership");
m = Membership(); m.RevokedAt = now; Check(Validate(members: [m]) == DepartmentChangeStatus.InvalidScope, "revoked primary membership");
m = Membership(); m.UserId = 9; Check(Validate(members: [m]) == DepartmentChangeStatus.InvalidScope, "foreign user membership");
m = Membership(); m.GrantedAt = now.AddHours(1); Check(Validate(members: [m]) == DepartmentChangeStatus.InvalidScope, "future membership");
Check(Validate(members: [Membership(), Membership()]) == DepartmentChangeStatus.InvalidScope, "ambiguous primary history");
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
Check(JsonSerializer.Deserialize<ChangeDepartmentRequest>("{\"departmentId\":3}", jsonOptions)!.DepartmentId == 3, "exact request field");
foreach (var field in new[] { "branchId", "userId", "roleId", "isPrimary" }) { try { JsonSerializer.Deserialize<ChangeDepartmentRequest>("{\"departmentId\":3,\"" + field + "\":1}", jsonOptions); throw new Exception("accepted extra field"); } catch (JsonException) { Check(true, "unknown request field rejected " + field); } }
var manager = new ApplicationPartManager(); manager.ApplicationParts.Add(new AssemblyPart(typeof(UserDepartmentChangeController).Assembly)); manager.FeatureProviders.Add(new ControllerFeatureProvider()); var feature = new ControllerFeature(); manager.PopulateFeature(feature); Check(!feature.Controllers.Any(x => x.AsType() == typeof(UserDepartmentChangeController)), "new controller not publicly mounted");
using var connection = new SqlConnection(); using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
var actor = new Actor(); var service = new UserDepartmentChangeService(db, actor, null!, null!, null!, TimeProvider.System); var ticket = SessionReference.FromProtectedTicket(7, new string('A', 43), false, now);
Check((await service.ChangeAsync(ticket, 70, 8, new(3), new byte[8], default)).Status == DepartmentChangeStatus.ReleaseRequired, "direct service cannot bypass release");
Check(connection.State == System.Data.ConnectionState.Closed && !db.ChangeTracker.Entries().Any(), "unreleased call performs no SQL or tracked mutation");
Check((await service.ChangeAsync(ticket, 70, 7, new(3), new byte[8], default)).Status == DepartmentChangeStatus.Forbidden, "self change denied");
actor.Value = new(7, 70, true); Check((await service.ChangeAsync(ticket, 70, 8, new(3), new byte[8], default)).Status == DepartmentChangeStatus.Forbidden, "restricted actor denied");
actor.Value = new(9, 70, false); Check((await service.ChangeAsync(ticket, 70, 8, new(3), new byte[8], default)).Status == DepartmentChangeStatus.SessionInvalid, "identity replacement denied");
try { ((IList<string>)UserDepartmentChangeService.PendingReleaseGates).Clear(); throw new Exception("mutable gate"); } catch (NotSupportedException) { Check(true, "immutable release evidence"); }
Check(UserRoleRevocationService.ValidateResponsibilities([], [], [], ["MISSING_WRITER"]) == RevokeRoleStatus.ResponsibilitiesUnresolved, "empty reader cannot clear missing writer participation");
Check(UserRoleRevocationService.ValidateResponsibilities(["OPEN_WORK"], [], [], []) == RevokeRoleStatus.ResponsibilitiesBlocked, "open work blocks change");
var action = typeof(UserDepartmentChangeController).GetMethod("Change")!;
Check(action.GetCustomAttributes(typeof(HttpPutAttribute), false).Length == 1, "only conditional PUT mutation contract");
Check(action.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), false).Length == 1, "explicit CSRF on mutation");
var context = new Microsoft.AspNetCore.Http.DefaultHttpContext(); var controller = new UserDepartmentChangeController(service) { ControllerContext = new() { HttpContext = context } };
Check(((ObjectResult)await controller.Change(8, new(3), default)).StatusCode == 400, "HTTP rejected before service");
context.Request.Scheme = "https"; Check(((ObjectResult)await controller.Change(8, new(3), default)).StatusCode == 401, "missing cookie rejected");
context.User = (System.Security.Claims.ClaimsPrincipal)typeof(HotelOps.Api.Security.HotelOpsCookieTicket).GetMethod("BuildPrincipal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, [ticket, 70L])!;
Check(((ObjectResult)await controller.Change(8, new(3), default)).StatusCode == 428, "missing If-Match maps428");
context.Request.Headers.IfMatch = "*"; Check(((ObjectResult)await controller.Change(8, new(3), default)).StatusCode == 400, "wildcard If-Match rejected");
context.Request.Headers.IfMatch = "\"0011223344556677\""; actor.Value = new(7, 70, false);
Check(((ObjectResult)await controller.Change(8, new(3), default)).StatusCode == 503, "unreleased HTTP direct invocation fail-closed");
Check(connection.State == System.Data.ConnectionState.Closed, "direct controller gate does not open SQL");
Console.WriteLine($"RESULT {count}/{count} PASS; SQL transaction/rollback/HTTPS NOT_RUN.");
sealed class Actor : ICurrentUserAccessor { public CurrentUserInfo Value = new(7, 70, false); public bool TryGetCurrentUser(out CurrentUserInfo value) { value = Value; return true; } }