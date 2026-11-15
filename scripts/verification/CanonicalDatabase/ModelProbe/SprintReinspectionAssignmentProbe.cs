using System.Data;
using System.Security.Cryptography;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

internal static class SprintReinspectionAssignmentProbe
{
    // Nested in the identity-guarded, committed UC036 fixture; never accepts an external database target.
    internal static async Task VerifyAsync(HotelOpsDbContext db, long branch, long rework, string commit,
        SessionReference executorTicket, long executorSid, CancellationToken ct)
    {
        if (await db.Database.SqlQueryRaw<int>("""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='meita'
              AND DB_NAME()='HotelOps_DEV3_142A_20261114_cc78c13f'
              AND EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID() AND database_guid='1D5A9B53-7D19-435A-96AB-9FDFF447B6E0')
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='DEV3.Sprint142A.Run' AND CONVERT(nvarchar(128),value)='cc78c13ffc124efcad67b3d804baa84c')
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) != 1) throw new Exception("Exact disposable reinspection identity required");
        var source = await db.CleaningReworks.AsNoTracking().Where(w => w.ReworkId == rework)
            .Select(w => new { w.Inspection.InspectorUserId, w.Inspection.VersionId, w.CleaningTaskId }).SingleAsync(ct);
        var actor = source.InspectorUserId;
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == actor, ct);
        var original = await db.Database.SqlQueryRaw<string>("SELECT definition AS Value FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.cleaning_reworks') AND name='CK_rework_workflow_v1' AND is_disabled=0 AND is_not_trusted=0").SingleAsync(ct);
        var installed = false;
        async Task Install()
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'DEV3.SourceCommit',@value={commit}", ct);
            foreach (var file in new[] { "SprintReinspectionWorkflow.sql", "SprintReinspectionWriters.sql" })
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
                command.CommandText = await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/" + file, ct);
                await command.ExecuteNonQueryAsync(ct);
            }
            await db.Database.ExecuteSqlRawAsync("""
                CREATE USER DEV3_Reinspector WITHOUT LOGIN;
                GRANT SELECT ON SCHEMA::dbo TO DEV3_Reinspector;
                GRANT VIEW DEFINITION TO DEV3_Reinspector;
                GRANT INSERT,UPDATE ON dbo.room_inspections TO DEV3_Reinspector;
                ALTER ROLE hotelops_reinspection_writer ADD MEMBER DEV3_Reinspector;
                """, ct);
        }
        async Task Cleanup()
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                IF EXISTS(SELECT 1 FROM dbo.fe05_reinspection_bindings_v1 WHERE rework_id<>{rework})
                  THROW 51999,'Unexpected reinspection source; cleanup refused',1;
                DECLARE @owned TABLE(id bigint PRIMARY KEY);
                INSERT @owned SELECT inspection_id FROM dbo.fe05_reinspection_bindings_v1 WHERE rework_id={rework};
                DROP TABLE dbo.fe05_reinspection_defect_attachments_v1;
                DROP TABLE dbo.fe05_reinspection_defect_results_v1;
                DROP TABLE dbo.fe05_reinspection_bindings_v1;
                DELETE dbo.audit_logs WHERE entity_type='REINSPECTION' AND TRY_CONVERT(bigint,entity_id) IN(SELECT id FROM @owned);
                DELETE dbo.notification_operation_reservations WHERE actor_user_id={actor}
                  AND JSON_VALUE(result_json,'$.contract')='FE05_REINSPECTION_ASSIGNMENT_V1'
                  AND TRY_CONVERT(bigint,JSON_VALUE(result_json,'$.reworkId'))={rework};
                DELETE dbo.room_inspections WHERE inspection_id IN(SELECT id FROM @owned);
                DROP PROCEDURE dbo.fe05_bind_reinspection;
                DROP PROCEDURE dbo.fe05_commit_reinspection;
                ALTER ROLE hotelops_reinspection_writer DROP MEMBER DEV3_Reinspector;
                DROP USER DEV3_Reinspector;
                DROP ROLE hotelops_reinspection_writer;
                DELETE dbo.fe04_rework_authority_issuers WHERE issuer IN('FE05.UC041','FE05.UC047') AND version=1;
                DECLARE @original nvarchar(max)={original};
                DECLARE @ddl nvarchar(max)=N'ALTER TABLE dbo.cleaning_reworks DROP CONSTRAINT CK_rework_workflow_v1; ALTER TABLE dbo.cleaning_reworks WITH CHECK ADD CONSTRAINT CK_rework_workflow_v1 CHECK '+@original;
                EXEC sys.sp_executesql @ddl;
                EXEC sys.sp_dropextendedproperty @name=N'HotelOps.Reinspection.V1';
                """, CancellationToken.None);
        }
        try
        {
            await using (var rehearsal = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
            { await Install(); await Cleanup(); await rehearsal.RollbackAsync(ct); }
            await using (var setup = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
            { await Install(); await setup.CommitAsync(ct); installed = true; }
            var credentials = new PasswordCredentialService(Options.Create(new PasswordHasherOptions
            { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = PasswordCredentialService.BaselineIterationCount }));
            var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) + "Aa1!";
            SessionReference ticket; long sid;
            await using (var loginTx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
            {
                var hash = credentials.HashPassword(user, password);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={hash} WHERE user_id={actor}", ct);
                var verified = await new CredentialLockoutService(db, credentials, TimeProvider.System).VerifyAsync(actor, password,
                    (u, _) => Task.FromResult(u.EmploymentStatus == "ACTIVE" && u.AccountStatus == "ACTIVE"), ct);
                var login = await new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System).CreateSessionAsync(verified, ct);
                ticket = login.Ticket; sid = login.Session.SessionId;
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={user.PasswordHash} WHERE user_id={actor}", ct);
                await loginTx.CommitAsync(ct); db.ChangeTracker.Clear();
            }
            var tag = '"' + Convert.ToHexString(await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.cleaning_reworks WHERE rework_id={rework}").SingleAsync(ct)) + '"';
            var input = new AssignInspectionRequest(actor, DateTime.UtcNow.AddHours(1), "Synthetic exact reinspection");
            async Task<RoomTypeBatchResult> Call(Guid operation, string? expected = null, AssignInspectionRequest? body = null)
            {
                await using var context = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
                await context.Database.OpenConnectionAsync(ct);
                await context.Database.ExecuteSqlRawAsync("EXECUTE AS USER='DEV3_Reinspector'", ct);
                try
                {
                    var identity = new Identity(new(actor, sid, false));
                    var service = new RoomTypeCreationService(context, identity,
                        new PermissionService(identity, new EfPermissionDataSource(context), new UnresolvedPermissionResourceResolver(), TimeProvider.System),
                        new SqlSessionService(context, new SessionRevocationService(context), TimeProvider.System), TimeProvider.System);
                    return await service.AssignReinspectionAsync(ticket, sid, branch, rework, expected ?? tag,
                        operation.ToString("D"), body ?? input, SprintReworkFixture.EvidenceRoot, ct);
                }
                finally { await context.Database.ExecuteSqlRawAsync("REVERT", CancellationToken.None); }
            }
            async Task Unchanged()
            {
                if (await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_bindings_v1").SingleAsync(ct) != 0
                    || await db.RoomInspections.AnyAsync(i => i.CleaningTaskId == source.CleaningTaskId && i.Status == "PENDING", ct))
                    throw new Exception("Assignment rollback leaked inspection/binding");
            }
            if ((await Call(Guid.NewGuid(), "\"FFFFFFFFFFFFFFFF\"")).Status != RoomTypeBatchStatus.Stale) throw new Exception("Stale assignment admitted");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={branch}", ct);
            try { if ((await Call(Guid.NewGuid())).Status == RoomTypeBatchStatus.Success) throw new Exception("Revoked assignment admitted"); }
            finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={branch}", ct); }
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs ADD CONSTRAINT CK_DEV3_ReinspectionAuditFault CHECK(entity_type<>'REINSPECTION')", ct);
            try
            {
                try { await Call(Guid.NewGuid()); throw new Exception("Expected assignment audit fault missing"); }
                catch (SqlException error) when (error.Number == 547) { }
                catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 547 }) { }
                await Unchanged();
            }
            finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.audit_logs DROP CONSTRAINT CK_DEV3_ReinspectionAuditFault", ct); }
            var operation = Guid.NewGuid();
            var results = await Task.WhenAll(Call(operation), Call(operation));
            if (results.Any(r => r.Status != RoomTypeBatchStatus.Success || r.Value is not ReinspectionAssignmentResult))
                throw new Exception("Concurrent assignment failed: " + string.Join(',', results.Select(r => r.Status)));
            if (((ReinspectionAssignmentResult)results[0].Value!).Id != ((ReinspectionAssignmentResult)results[1].Value!).Id
                || await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_bindings_v1").SingleAsync(ct) != 1)
                throw new Exception("Assignment replay created duplicate source");
            if ((await Call(operation, body: input with { Reason = "Changed body" })).Status != RoomTypeBatchStatus.Duplicate
                || (await Call(Guid.NewGuid())).Status != RoomTypeBatchStatus.Duplicate)
                throw new Exception("Assignment duplicated or reused changed operation");
            await using (var context = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options))
            {
                await context.Database.OpenConnectionAsync(ct);
                await context.Database.ExecuteSqlRawAsync("EXECUTE AS USER='DEV3_ReworkExecutor'", ct);
                try
                {
                    var current = new Identity(new(executorTicket.UserId, executorSid, false));
                    var service = new RoomTypeCreationService(context, current,
                        new PermissionService(current, new EfPermissionDataSource(context), new UnresolvedPermissionResourceResolver(), TimeProvider.System),
                        new SqlSessionService(context, new SessionRevocationService(context), TimeProvider.System), TimeProvider.System);
                    var read = await service.ReadAssignedReworkAsync(executorTicket, executorSid, branch, rework, SprintReworkFixture.EvidenceRoot, ct);
                    if (read.Status != RoomTypeBatchStatus.Success || read.Value is not ReworkExecutionDto { State: "PENDING_REINSPECTION", Capability: "READ_ONLY" })
                        throw new Exception("Reinspection assignment hid the executor's verified submission");
                }
                finally { await context.Database.ExecuteSqlRawAsync("REVERT", CancellationToken.None); }
            }
            // Restore only this synthetic assignment before the independent public UI trial.
            await using (var reset = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
            { await Cleanup(); await Install(); await reset.CommitAsync(ct); }
            await SprintReinspectionHttpsProbe.VerifyAsync(db, actor, branch, rework, ct);
        }
        finally
        {
            if (installed)
            {
                await using var cleanup = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, CancellationToken.None);
                await Cleanup(); await cleanup.CommitAsync(CancellationToken.None); db.ChangeTracker.Clear();
            }
        }
    }
    private sealed class Identity(CurrentUserInfo value) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = value; return true; } }
}
