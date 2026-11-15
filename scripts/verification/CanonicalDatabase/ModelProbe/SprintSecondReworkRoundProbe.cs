using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal static class SprintSecondReworkRoundProbe
{
    // Invoked only by the identity-verified HTTPS fixture. Its parent owns both rounds and cleanup.
    internal static async Task VerifyAsync(HotelOpsDbContext db, long inspector, long branch, long firstRework, long secondRework,
        SessionReference executorTicket, long executorSid, string inspectorPassword, CancellationToken ct)
    {
        var executor = executorTicket.UserId;
        var firstInspection = await db.Database.SqlQuery<long>($"""
            SELECT b.inspection_id AS Value FROM dbo.fe05_reinspection_bindings_v1 b
            JOIN dbo.cleaning_reworks child ON child.inspection_id=b.inspection_id AND child.rework_id={secondRework}
              AND child.assignee_user_id={executor} AND child.cleaning_task_id=b.cleaning_task_id
            JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id AND i.workflow_status='FAILED'
            WHERE b.rework_id={firstRework} AND b.branch_id={branch} AND b.reinspector_user_id={inspector}
              AND b.submission_actor={executor}
            """).SingleAsync(ct);
        var firstAudit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityType == "REINSPECTION" && a.EntityId == firstInspection.ToString() && a.Action == "UPDATE", ct);
        var firstTag = await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={firstInspection}").SingleAsync(ct);
        var firstWorkTag = await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.cleaning_reworks WHERE rework_id={firstRework}").SingleAsync(ct);
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == executor, ct);
        var supervisor = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == inspector, ct);
        var room = await db.CleaningReworks.Where(w => w.ReworkId == secondRework).Select(w => w.CleaningTask.RoomId).SingleAsync(ct);
        var blocked = await db.RoomBlocks.AnyAsync(b => b.RoomId == room && b.ReleasedAt == null, ct);
        var day = await db.Workdays.AsNoTracking().SingleAsync(w => w.UserId == executor && w.BranchId == branch && w.Status == "WORKING", ct);
        if (day.DepartmentId != user.DepartmentId || await db.WorkIntervals.AnyAsync(w => w.UserId == executor && w.ClosedAt == null, ct))
            throw new Exception("Second round requires the existing authorized workday with no open interval");
        var notice = await db.Notifications.Where(n => n.NotificationType == "REWORK_ASSIGNED" && n.SourceType == "CLEANING_REWORK" && n.SourceId == secondRework)
            .Select(n => n.NotificationId).SingleAsync(ct);
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "Aa1!";
        var hash = new PasswordHasher<User>().HashPassword(user, password);
        await db.Database.ExecuteSqlRawAsync("""
            ALTER ROLE hotelops_rework_submission_writer ADD MEMBER DEV3_142A_ReinspectionHttps;
            GRANT INSERT,UPDATE ON dbo.work_intervals TO DEV3_142A_ReinspectionHttps;
            GRANT UPDATE ON dbo.cleaning_rework_defects TO DEV3_142A_ReinspectionHttps;
            GRANT INSERT ON dbo.fs016_rework_evidence TO DEV3_142A_ReinspectionHttps;
            """, ct);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={hash} WHERE user_id={executor}", ct);
            await Browser("sprintReworkReceiveBrowser.mjs", new { username = user.Username, password, branch, rework = secondRework,
                notification = notice, readiness = blocked ? "BLOCKED" : "REWORK_REQUIRED", submission = true }, ct);
            var interval = await db.WorkIntervals.AsNoTracking().SingleAsync(w => w.UserId == executor && w.WorkItemKind == "REWORK" && w.WorkItemId == secondRework, ct);
            if (interval.WorkdayId != day.WorkdayId || interval.ClosedAt == null || interval.EffectiveEndedAt < interval.EffectiveStartedAt
                || interval.EffectiveEndedAt == null || interval.ClosedBy != executor
                || await db.AuditLogs.CountAsync(a => a.ActorUserId == executor && a.EntityType == "cleaning_reworks" && a.EntityId == secondRework.ToString(), ct) != 3
                || await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.cleaning_reworks WHERE rework_id={secondRework}").SingleAsync(ct) != "PENDING_REINSPECTION")
                throw new Exception("Second round submission/interval/audit persistence mismatch");
            await SprintReworkSubmissionDeliveryProbe.VerifyAsync(db, executor, branch, secondRework, ct);
            var assigned = await Browser("sprintReinspectionAssignmentBrowser.mjs", new { username = supervisor.Username, password = inspectorPassword,
                actor = inspector, branch, rework = secondRework, conclude = false, outcome = "PASS", nextRound = false, executor }, ct);
            var secondInspection = assigned.GetProperty("inspection").GetInt64();
            await SprintReinspectionNoticeProbe.VerifyAsync(db, inspector, branch, secondInspection, inspectorPassword, ct, initial: true);
            await Browser("sprintReinspectionAssignmentBrowser.mjs", new { username = supervisor.Username, password = inspectorPassword,
                actor = inspector, branch, rework = secondRework, conclude = true, outcome = "PASS", nextRound = false,
                existingInspection = secondInspection, executor }, ct);
            if (await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_bindings_v1 b
                JOIN dbo.fe05_reinspection_bindings_v1 prior ON prior.inspection_id=b.original_inspection_id
                  AND prior.rework_id={firstRework} AND prior.cleaning_task_id=b.cleaning_task_id AND prior.version_id=b.version_id
                JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id AND i.workflow_status='PASSED' AND i.result='PASS'
                JOIN dbo.cleaning_reworks w ON w.rework_id=b.rework_id AND w.workflow_status='COMPLETED' AND w.completed_at=i.inspected_at
                JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=b.cleaning_task_id AND t.workflow_status='COMPLETED'
                WHERE b.rework_id={secondRework} AND b.inspection_id={secondInspection} AND b.branch_id={branch}
                  AND b.reinspector_user_id={inspector} AND b.submission_actor={executor}
                  AND (SELECT COUNT(*) FROM dbo.audit_logs WHERE entity_type='REINSPECTION' AND entity_id=CONVERT(nvarchar(50),b.inspection_id) AND action='UPDATE')=1
                  AND NOT EXISTS(SELECT 1 FROM dbo.fe05_reinspection_defect_results_v1 WHERE inspection_id=b.inspection_id AND (fixed IS NULL OR fixed=0))
                """).SingleAsync(ct) != 1
                || !(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={firstInspection}").SingleAsync(ct)).SequenceEqual(firstTag)
                || !(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.cleaning_reworks WHERE rework_id={firstRework}").SingleAsync(ct)).SequenceEqual(firstWorkTag)
                || await db.AuditLogs.Where(a => a.AuditId == firstAudit.AuditId).Select(a => a.NewValues).SingleAsync(ct) != firstAudit.NewValues)
                throw new Exception("Second Passed did not preserve the exact Failed history and cycle binding");
            await SprintReinspectionExecutorProbe.VerifyAsync(db, executorTicket, executorSid, branch, secondRework, ct, browser: true);
            await SprintReinspectionNoticeProbe.VerifyAsync(db, inspector, branch, secondInspection, inspectorPassword, ct, initial: false);
        }
        finally
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={user.PasswordHash} WHERE user_id={executor}", CancellationToken.None);
            await db.Database.ExecuteSqlRawAsync("ALTER ROLE hotelops_rework_submission_writer DROP MEMBER DEV3_142A_ReinspectionHttps", CancellationToken.None);
        }
    }

    private static async Task<JsonElement> Browser(string script, object fixture, CancellationToken ct)
    {
        var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("frontend/scripts/" + script);
        using var process = Process.Start(start) ?? throw new Exception("Second-round browser unavailable");
        try
        {
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(fixture).AsMemory(), ct); process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync(ct); var errors = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct); var result = JsonSerializer.Deserialize<JsonElement>(await output); await errors;
            if (process.ExitCode != 0 || !result.GetProperty("success").GetBoolean()) throw new Exception("Second-round " + script + ": " + result.GetRawText());
            return result;
        }
        finally { if (!process.HasExited) process.Kill(true); }
    }
}
