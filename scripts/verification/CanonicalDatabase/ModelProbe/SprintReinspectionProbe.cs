using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

internal static class SprintReinspectionProbe
{
    internal static async Task<int> RunAsync(string report)
    {
        if (File.Exists(report)) throw new IOException("Evidence already exists");
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var ct = budget.Token;
        var schema = (await File.ReadAllTextAsync("scripts/database/fe05-fast014/01_ReinspectionCandidate.sql", ct)).TrimStart('\uFEFF').Replace("\r\n", "\n");
        if (Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(schema))) != "B31B633A9E5832F79DDB01D2EDB26DBBB29DC3CBE15B591C995A0FD301B704B7")
            throw new InvalidOperationException("Unreviewed candidate script");
        // Reuse the pinned DDL body only after the sprint fixture's stronger exact GUID/owner guard.
        // Never execute the historical target wrapper, COMMIT, or a shared-database installer.
        var body = schema[(schema.IndexOf("BEGIN TRANSACTION;", StringComparison.Ordinal) + "BEGIN TRANSACTION;".Length)..schema.LastIndexOf("COMMIT;", StringComparison.Ordinal)];
        var fixture = await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/SprintApproveFixture.sql", ct);
        var reinspectionFixture = await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/SprintReinspectionFixture.sql", ct);
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        var serviceType = typeof(RoomTypeCreationService);
        var bind = serviceType.GetMethod("BindReinspectionComponentInCallerTransactionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var save = serviceType.GetMethod("SaveReinspectionComponentInCallerTransactionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var decisionType = serviceType.Assembly.GetType("HotelOps.Api.Services.Housekeeping.ReinspectionDefectDecision")!;
        var checks = new List<object>();
        string? failure = null;
        foreach (var scenario in new[] { "fixed", "not-fixed", "different-version", "missing-remediation", "cleaner-conflict", "duplicate-binding", "stale-binding", "stale-inspection", "stale-rework", "missing-decision", "foreign-defect", "duplicate-decision", "permission-revoked", "session-revoked", "wrong-branch", "cycle-changed", "trigger-disabled", "audit-failure" })
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.Transaction = tx.GetDbTransaction(); command.CommandText = fixture;
                long actor, branch, task, room;
                await using (var reader = await command.ExecuteReaderAsync(ct))
                {
                    if (!await reader.ReadAsync(ct)) throw new Exception("Fixture missing");
                    actor = reader.GetInt64(0); branch = reader.GetInt64(1); task = reader.GetInt64(3); room = reader.GetInt64(4);
                }
                if (await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.fe05_reinspection_bindings_v1') IS NULL AND COL_LENGTH('dbo.cleaning_rework_defects','remediation_note') IS NULL THEN 1 ELSE 0 END AS Value").SingleAsync(ct) != 1)
                    throw new Exception("Refuse overwrite of existing component schema");
                await db.Database.ExecuteSqlRawAsync(body, ct);
                command.CommandText = reinspectionFixture;
                command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@differentVersion",scenario=="different-version"));
                long inspection, rework, defect;
                await using (var reader = await command.ExecuteReaderAsync(ct))
                {
                    if (!await reader.ReadAsync(ct)) throw new Exception("Reinspection fixture missing");
                    inspection = reader.GetInt64(0); rework = reader.GetInt64(1); defect = reader.GetInt64(2);
                }
                var passwords = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
                { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
                var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) + "Aa1!";
                var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == actor, ct);
                var hash = passwords.HashPassword(user, password);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={hash} WHERE user_id={actor}", ct);
                var credentials = new CredentialLockoutService(db, passwords, TimeProvider.System);
                var sessions = new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System);
                var verified = await credentials.VerifyAsync(actor, password, (u, _) => Task.FromResult(u.EmploymentStatus == "ACTIVE" && u.AccountStatus == "ACTIVE"), ct);
                var login = await sessions.CreateSessionAsync(verified, ct); db.ChangeTracker.Clear();
                var current = new Identity(new(actor, login.Session.SessionId, false));
                var permission = new PermissionService(current, new EfPermissionDataSource(db), new UnresolvedPermissionResourceResolver(), TimeProvider.System);
                var service = new RoomTypeCreationService(db, current, permission, sessions, TimeProvider.System);
                async Task<object> Invoke(MethodInfo method, object?[] args)
                {
                    var pending = (Task)method.Invoke(service, args)!; await pending;
                    return pending.GetType().GetProperty("Result")!.GetValue(pending)!;
                }
                static string Property(object result, string name) => (string)result.GetType().GetProperty(name)!.GetValue(result)!;
                async Task<string> InspectionTag() => '"' + Convert.ToHexString(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={inspection}").SingleAsync(ct)) + '"';
                var inspectionTag = await InspectionTag();
                var reworkTag = '"' + Convert.ToHexString(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.cleaning_reworks WHERE rework_id={rework}").SingleAsync(ct)) + '"';
                var bound = await Invoke(bind, [login.Ticket, login.Session.SessionId, branch, inspection, rework, inspectionTag, reworkTag, ct]);
                inspectionTag = Property(bound, "InspectionETag");
                var bindingTag = Property(bound, "BindingETag");
                var audits = await db.AuditLogs.CountAsync(ct);
                if (scenario == "different-version" && await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_bindings_v1 WHERE inspection_id={inspection} AND original_version_id<>assigned_version_id").SingleAsync(ct) != 1)
                    throw new Exception("Original/assigned checklist versions were conflated");
                var count = scenario == "missing-decision" ? 0 : scenario == "duplicate-decision" ? 2 : 1;
                var decisions = Array.CreateInstance(decisionType, count);
                for (var index = 0; index < count; index++) decisions.SetValue(Activator.CreateInstance(decisionType, [scenario == "foreign-defect" ? defect + 100000 : defect, scenario != "not-fixed"]), index);
                if (scenario == "permission-revoked") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={actor}", ct);
                if (scenario == "session-revoked") await sessions.RevokeSessionAsync(login.Ticket, ct);
                if (scenario == "cycle-changed") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_cleaning_cycle_state SET cleaning_task_id=cleaning_task_id WHERE room_id={room}", ct);
                if (scenario == "missing-remediation")
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_rework_defects SET remediation_note=NULL,remediated_by=NULL,remediated_at=NULL WHERE rework_id={rework}", ct);
                    reworkTag='"'+Convert.ToHexString(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.cleaning_reworks WHERE rework_id={rework}").SingleAsync(ct))+'"';
                }
                if (scenario == "cleaner-conflict") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_reworks SET assignee_user_id={actor} WHERE rework_id={rework}", ct);
                if (scenario == "cleaner-conflict") reworkTag='"'+Convert.ToHexString(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.cleaning_reworks WHERE rework_id={rework}").SingleAsync(ct))+'"';
                if (scenario == "trigger-disabled") await db.Database.ExecuteSqlRawAsync("DISABLE TRIGGER dbo.TR_fe05_reinspection_result_revision_v1 ON dbo.fe05_reinspection_defect_results_v1", ct);
                if (scenario == "audit-failure") await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_142A_ReinspectionAuditFault ON dbo.audit_logs AFTER INSERT AS BEGIN THROW 51994,'Synthetic audit fault',1; END", ct);
                async Task<object> Apply() => await Invoke(save, [login.Ticket, login.Session.SessionId, scenario == "wrong-branch" ? branch + 100000 : branch,
                    inspection, rework, scenario == "stale-inspection" ? "\"0000000000000000\"" : inspectionTag,
                    scenario == "stale-rework" ? "\"0000000000000000\"" : reworkTag,
                    scenario == "stale-binding" ? "\"0000000000000000\"" : bindingTag, decisions, ct]);
                if (scenario == "duplicate-binding")
                {
                    try { await Invoke(bind,[login.Ticket,login.Session.SessionId,branch,inspection,rework,inspectionTag,reworkTag,ct]); throw new Exception("Duplicate binding admitted"); }
                    catch (Microsoft.Data.SqlClient.SqlException error) when(error.Number is 2601 or 2627) { }
                }
                else if (scenario is "fixed" or "not-fixed" or "different-version")
                {
                    var result = await Apply();
                    if (Property(result, "InspectionETag") == inspectionTag || Property(result, "BindingETag") == bindingTag)
                        throw new Exception("Aggregate revisions did not advance");
                    var saved = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_defect_results_v1 WHERE inspection_id={inspection} AND rework_id={rework} AND defect_id={defect} AND fixed={scenario != "not-fixed"} AND recorded_by={actor} AND recorded_at IS NOT NULL").SingleAsync(ct);
                    if (saved != 1 || await db.AuditLogs.CountAsync(ct) != audits + 1) throw new Exception("Result/audit persistence mismatch");
                    if ((bool)result.GetType().GetProperty("AuthoritativeOrCommitted", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(result)!) throw new Exception("Component claimed authority");
                    if (await db.CleaningTasks.Where(t => t.CleaningTaskId == task).Select(t => t.Status).SingleAsync(ct) != "IN_PROGRESS"
                        || await db.Rooms.Where(r => r.RoomId == room).Select(r => r.OperationalStatus).SingleAsync(ct) != "INSPECTION") throw new Exception("Per-defect save concluded lifecycle");
                }
                else if (scenario == "audit-failure")
                {
                    try { await Apply(); throw new Exception("Audit failure ignored"); }
                    catch (Microsoft.Data.SqlClient.SqlException error) when (error.Number == 51994) { }
                }
                else
                {
                    var expected = scenario switch {
                        "stale-inspection" => "REINSPECTION_SOURCE_STALE", "stale-rework" => "REINSPECTION_REWORK_STALE",
                        "missing-remediation" => "REINSPECTION_REMEDIATION_REQUIRED", "cleaner-conflict" => "INSPECTOR_CLEANER_CONFLICT",
                        "permission-revoked" or "session-revoked" or "wrong-branch" => "REINSPECTION_SOURCE_FORBIDDEN",
                        "trigger-disabled" => "REINSPECTION_ISOLATED_COMPONENT_SCHEMA_REQUIRED",
                        "stale-binding" or "cycle-changed" => "REINSPECTION_BINDING_STALE_OR_WRONG_SOURCE",
                        _ => "REINSPECTION_EXACT_COMPLETE_DECISIONS_REQUIRED" };
                    try { await Apply(); throw new Exception("Expected rejection: " + expected); }
                    catch (InvalidOperationException error) when (error.Message == expected) { }
                    if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_defect_results_v1 WHERE fixed IS NOT NULL").SingleAsync(ct) != 0)
                        throw new Exception("Denied save persisted an answer");
                }
            }
            catch (Exception error) { failure = scenario + ": " + error.GetBaseException().GetType().Name + ": " + error.GetBaseException().Message; }
            finally { if (tx.GetDbTransaction().Connection != null) await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
            var clean = !await db.Users.AnyAsync(ct) && !await db.Branches.AnyAsync(ct) && !await db.AuditLogs.AnyAsync(ct)
                && await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.fe05_reinspection_bindings_v1') IS NULL AND COL_LENGTH('dbo.cleaning_rework_defects','remediation_note') IS NULL AND OBJECT_ID('dbo.TR_DEV3_142A_ReinspectionAuditFault') IS NULL THEN 1 ELSE 0 END AS Value").SingleAsync(ct) == 1;
            if (!clean) throw new Exception("Synthetic/schema rollback cleanup failed");
            checks.Add(new { scenario, success = failure == null, schemaAndDataRollback = clean });
            Console.WriteLine((failure == null ? "PASS " : "FAIL ") + scenario);
            if (failure != null) break;
        }
        await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { success = failure == null, checks, failure,
            scope = "Actual internal reinspection binding/per-defect persistence, real SQL session/permission, schema and data rolled back; no public conclusion/notification/room/replay acceptance" }, new JsonSerializerOptions { WriteIndented = true }), ct);
        return failure == null ? 0 : 1;
    }
    private sealed class Identity(CurrentUserInfo value) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = value; return true; } }
}
