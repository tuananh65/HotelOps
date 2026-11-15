using System.Diagnostics;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintInspectionHistoryProbe
{
    // Invoked within the exact GUID/ownership-checked, cleanup-rehearsed FE05 fixture,
    // after the real public Rework/Reinspection flow, while its HTTPS host is alive.
    internal static async Task VerifyAsync(HotelOpsDbContext db, long actor, string password, long branch,
        long rework, CancellationToken ct)
    {
        var source = await db.CleaningReworks.AsNoTracking().Where(w => w.ReworkId == rework)
            .Select(w => new { w.InspectionId, w.CleaningTaskId, w.CleaningTask.RoomId, w.AssigneeUserId }).SingleAsync(ct);
        var username = await db.Users.Where(u => u.UserId == actor).Select(u => u.Username).SingleAsync(ct);
        // A historical cancelled assignee did not clean the room: the old Any(assignment)
        // filter incorrectly included this inspector. Fixture only; remove in finally.
        var assignment = new CleaningAssignment { CleaningTaskId = source.CleaningTaskId, AssigneeUserId = actor,
            AssignedBy = actor, AssignedAt = DateTime.UtcNow.AddDays(-2), EndedAt = DateTime.UtcNow.AddDays(-1),
            AssignmentStatus = "CANCELLED", Reason = "Synthetic history filter negative" };
        db.CleaningAssignments.Add(assignment); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        async Task<string> Fingerprint() => await db.Database.SqlQuery<string>($"""
            SELECT (SELECT (SELECT COUNT(*) FROM dbo.audit_logs WHERE entity_type IN('INSPECTION','cleaning_reworks','room_inspections')) AS Audits,
                (SELECT COUNT(*) FROM dbo.notifications) AS Notifications,
                (SELECT COUNT(*) FROM dbo.room_status_history) AS RoomHistory,
                (SELECT * FROM dbo.room_inspections WHERE cleaning_task_id={source.CleaningTaskId} ORDER BY inspection_id FOR JSON PATH) AS Inspections,
                (SELECT * FROM dbo.inspection_results WHERE inspection_id IN(SELECT inspection_id FROM dbo.room_inspections WHERE cleaning_task_id={source.CleaningTaskId}) ORDER BY result_id FOR JSON PATH) AS Results,
                (SELECT * FROM dbo.cleaning_reworks WHERE cleaning_task_id={source.CleaningTaskId} ORDER BY rework_id FOR JSON PATH) AS Reworks
                FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS Value
            """).SingleAsync(ct);
        async Task Browser(string mode)
        {
            var start = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("frontend/scripts/sprintInspectionHistoryBrowser.mjs");
            using var process = Process.Start(start) ?? throw new Exception("History browser unavailable");
            try
            {
                await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { username, password, branch, actor,
                    inspection = source.InspectionId, room = source.RoomId, cleaner = source.AssigneeUserId, mode }).AsMemory(), ct);
                process.StandardInput.Close();
                var output = process.StandardOutput.ReadToEndAsync(ct); var errors = process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct); var result = JsonSerializer.Deserialize<JsonElement>(await output); await errors;
                SprintReinspectionReassignmentBrowserProbe.Evidence.Add(new { inspectionHistory = result });
                if (process.ExitCode != 0 || !result.GetProperty("success").GetBoolean())
                    throw new Exception("Inspection history browser: " + result.GetRawText());
            }
            finally { if (!process.HasExited) process.Kill(true); }
        }
        long? opsAssignment = null;
        try
        {
            var before = await Fingerprint();
            await Browser("supervisor");
            if (await Fingerprint() != before) throw new Exception("Read-only history changed business records");
            // Advance only this disposable room's pointer to a synthetic later cancelled
            // cycle. This is a history regression fixture, not acceptance of cycle creation.
            var nextTask = await db.Database.SqlQuery<long>($"""
                INSERT dbo.cleaning_tasks(task_no,room_id,cleaning_type_id,status,workflow_status,created_by,created_at)
                SELECT CONCAT('D3-HISTORY-',LEFT(REPLACE(CONVERT(varchar(36),NEWID()),'-',''),24)),room_id,cleaning_type_id,'CANCELLED','CANCELLED',{actor},SYSUTCDATETIME()
                FROM dbo.cleaning_tasks WHERE cleaning_task_id={source.CleaningTaskId};
                SELECT CONVERT(bigint,SCOPE_IDENTITY()) AS Value;
                """).ToArrayAsync(ct);
            if (nextTask.Length != 1 || nextTask[0] <= 0) throw new Exception("Later cycle fixture missing");
            try
            {
                if (await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_cleaning_cycle_state SET cleaning_task_id={nextTask[0]} WHERE room_id={source.RoomId} AND cleaning_task_id={source.CleaningTaskId}", ct) != 1)
                    throw new Exception("Current cycle fixture changed unexpectedly");
                await Browser("later-cycle");
                if (await Fingerprint() != before) throw new Exception("Historical read changed the old cycle");
            }
            finally
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_cleaning_cycle_state SET cleaning_task_id={source.CleaningTaskId} WHERE room_id={source.RoomId} AND cleaning_task_id={nextTask[0]}", CancellationToken.None);
                await db.CleaningTasks.Where(t => t.CleaningTaskId == nextTask[0] && t.CreatedBy == actor && t.TaskNo.StartsWith("D3-HISTORY-")).ExecuteDeleteAsync(CancellationToken.None);
            }
            var role = await db.Roles.SingleAsync(r => r.RoleCode == "OPERATIONS_MANAGER" && r.IsActive, ct);
            var granted = new UserRole { UserId = actor, RoleId = role.RoleId, BranchId = branch,
                IsActive = true, AssignedAt = DateTime.UtcNow.AddMinutes(-1) };
            db.UserRoles.Add(granted); await db.SaveChangesAsync(ct); opsAssignment = granted.UserRoleId; db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={actor} AND user_role_id<>{opsAssignment}", ct);
            try { await Browser("operations"); }
            finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=1 WHERE user_id={actor} AND user_role_id<>{opsAssignment}", CancellationToken.None); }
            if (await Fingerprint() != before) throw new Exception("Operations history changed business records");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={branch}", ct);
            try { await Browser("revoked"); }
            finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={branch}", CancellationToken.None); }
        }
        finally
        {
            if (opsAssignment != null) await db.UserRoles.Where(r => r.UserRoleId == opsAssignment).ExecuteDeleteAsync(CancellationToken.None);
            await db.CleaningAssignments.Where(a => a.AssignmentId == assignment.AssignmentId).ExecuteDeleteAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
        }
    }
}
