using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal static class BoundaryProof
{
    internal static async Task<object> RunAsync(HotelOpsDbContext db, string connection, CancellationToken ct)
    {
        var actor = await db.Users.Where(x => x.Username == "fs011.admin").Select(x => x.UserId).SingleAsync(ct);
        var checks = new List<string>();
        foreach (var sql in new[] { "COMMIT TRANSACTION", "SELECT 1; ROLLBACK TRANSACTION", "BEGIN TRANSACTION", "SAVE TRANSACTION bypass" })
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            CandidateCommitGuard.Instance.Arm(tx.GetDbTransaction(), actor, Guid.NewGuid());
            var rejected = false;
            try { await db.Database.ExecuteSqlRawAsync(sql, ct); }
            catch (InvalidOperationException e) when (e.Message == "CANDIDATE_TRANSACTION_CONTROL_SQL_UNSUPPORTED") { rejected = true; }
            if (!rejected || tx.GetDbTransaction().Connection == null) throw new InvalidOperationException("EF_CONTROL_SQL_NOT_REJECTED");
            await tx.RollbackAsync(ct);
            checks.Add(sql + ":REJECTED_BEFORE_EXECUTION");
        }
        var assignment = await db.CleaningAssignments.AsNoTracking().OrderByDescending(x => x.AssignmentId).FirstAsync(ct);
        var counts = await Counts(db, ct);
        var marker = "FS011_EXTERNAL_UNAUDITED_" + Guid.NewGuid().ToString("N");
        await using (var outside = new SqlConnection(connection))
        {
            await outside.OpenAsync(ct);
            await using var command = outside.CreateCommand();
            command.CommandText = "UPDATE dbo.cleaning_assignments SET reason=@reason WHERE assignment_id=@id";
            command.Parameters.AddWithValue("@reason", marker); command.Parameters.AddWithValue("@id", assignment.AssignmentId);
            if (await command.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("EXTERNAL_WRITE_NOT_REPRODUCED");
        }
        var persisted = await db.CleaningAssignments.AsNoTracking().SingleAsync(x => x.AssignmentId == assignment.AssignmentId, ct);
        if (persisted.Reason != marker || !(await Counts(db, ct)).SequenceEqual(counts)) throw new InvalidOperationException("EXTERNAL_DURABLE_PROOF_MISMATCH");
        // The synthetic changed reason is retained. Never repair old evidence to make a case pass.
        return new { level = "COMPONENT_REAL_SQL", checks, externalAssignmentMutation = "CONFIRMED_UNAUDITED_BYPASS", assignment.AssignmentId, unchangedAuditOperationPublicationIntentCounts = counts, dbPermissionsChanged = false, residualRisk = "Trusted provider handles, dynamic SQL/procedures and other connections remain outside EF interception" };
    }
    private static async Task<int[]> Counts(HotelOpsDbContext db, CancellationToken ct) => [await db.AuditLogs.CountAsync(ct), await db.CandidateOperations.CountAsync(ct), await db.CandidatePublications.CountAsync(ct), await db.CandidateIntents.CountAsync(ct)];
}
