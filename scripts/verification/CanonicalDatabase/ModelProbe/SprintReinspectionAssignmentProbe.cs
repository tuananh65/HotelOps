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
        var distinctInspector = Environment.GetEnvironmentVariable("DEV3_REINSPECTION_DISTINCT_INSPECTOR") == "1";
        long? ownedInspector = null;
        var roomId = await db.CleaningTasks.Where(t => t.CleaningTaskId == source.CleaningTaskId).Select(t => t.RoomId).SingleAsync(ct);
        var originalRoomStatus = await db.Rooms.Where(r => r.RoomId == roomId).Select(r => r.OperationalStatus).SingleAsync(ct);
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == actor, ct);
        var original = await db.Database.SqlQueryRaw<string>("SELECT definition AS Value FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.cleaning_reworks') AND name='CK_rework_workflow_v1' AND is_disabled=0 AND is_not_trusted=0").SingleAsync(ct);
        var installed = false;
        var originalGuards = await db.Database.SqlQuery<string>($"""
            SELECT m.definition AS Value FROM sys.sql_modules m JOIN sys.triggers t ON t.object_id=m.object_id
            WHERE t.name IN('TR_hk_results_v1','TR_hk_defects_v1') AND t.is_disabled=0
            """).ToArrayAsync(ct);
        if (originalGuards.Length != 2) throw new Exception("Original projection guards required");
        async Task Install()
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'DEV3.SourceCommit',@value={commit}", ct);
            foreach (var file in new[] { "SprintReinspectionWorkflow.sql", "SprintReinspectionProjectionGuards.sql", "SprintReinspectionWriters.sql" })
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
                GRANT UPDATE ON dbo.fe05_reinspection_defect_results_v1 TO DEV3_Reinspector;
                GRANT INSERT ON dbo.fe05_reinspection_defect_attachments_v1 TO DEV3_Reinspector;
                GRANT INSERT ON dbo.inspection_results TO DEV3_Reinspector;
                GRANT INSERT,UPDATE ON dbo.inspection_defects TO DEV3_Reinspector;
                GRANT INSERT ON dbo.inspection_defect_attachments TO DEV3_Reinspector;
                GRANT INSERT ON dbo.attachments TO DEV3_Reinspector;
                GRANT UPDATE ON dbo.cleaning_reworks TO DEV3_Reinspector;
                GRANT UPDATE ON dbo.cleaning_tasks TO DEV3_Reinspector;
                GRANT UPDATE ON dbo.rooms TO DEV3_Reinspector;
                GRANT INSERT ON dbo.room_status_history TO DEV3_Reinspector;
                ALTER ROLE hotelops_reinspection_writer ADD MEMBER DEV3_Reinspector;
                """, ct);
        }
        async Task Cleanup()
        {
            var keys = await db.Database.SqlQuery<string>($"""
                SELECT a.storage_key AS Value FROM dbo.attachments a JOIN dbo.fe05_reinspection_bindings_v1 b ON b.inspection_id=a.entity_id
                WHERE b.rework_id={rework} AND a.entity_type='INSPECTION' AND a.uploaded_by={actor}
                  AND NOT EXISTS(SELECT 1 FROM dbo.cleaning_reworks child WHERE child.inspection_id=b.inspection_id)
                """).ToArrayAsync(CancellationToken.None);
            foreach (var definition in originalGuards)
                await db.Database.ExecuteSqlRawAsync(definition.Replace("CREATE TRIGGER", "ALTER TRIGGER"), CancellationToken.None);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                IF EXISTS(SELECT 1 FROM dbo.fe05_reinspection_bindings_v1 WHERE rework_id<>{rework})
                  THROW 51999,'Unexpected reinspection source; cleanup refused',1;
                DECLARE @owned TABLE(id bigint PRIMARY KEY);
                INSERT @owned SELECT inspection_id FROM dbo.fe05_reinspection_bindings_v1 WHERE rework_id={rework};
                DELETE r FROM dbo.notification_recipients r JOIN dbo.notifications n ON n.notification_id=r.notification_id
                  WHERE n.notification_type='REINSPECTION_ASSIGNED' AND n.source_type='INSPECTION' AND n.source_id IN(SELECT id FROM @owned) AND n.created_by={actor};
                DELETE dbo.notifications WHERE notification_type='REINSPECTION_ASSIGNED' AND source_type='INSPECTION' AND source_id IN(SELECT id FROM @owned) AND created_by={actor};
                DELETE r FROM dbo.notification_recipients r JOIN dbo.notifications n ON n.notification_id=r.notification_id
                  WHERE n.notification_type='REINSPECTION_PASSED' AND n.source_type='CLEANING_REWORK' AND n.source_id={rework} AND n.created_by={actor};
                DELETE dbo.notifications WHERE notification_type='REINSPECTION_PASSED' AND source_type='CLEANING_REWORK' AND source_id={rework} AND created_by={actor};
                -- A Failed reinspection may now own the next committed Request Rework.
                -- Retain that immutable source until the outer owned-authority cleanup drops
                -- its trial proof table and deletes the descendant rework before inspection.
                DECLARE @deletable TABLE(id bigint PRIMARY KEY);
                INSERT @deletable SELECT id FROM @owned o WHERE NOT EXISTS(SELECT 1 FROM dbo.cleaning_reworks w WHERE w.inspection_id=o.id);
                UPDATE dbo.room_inspections SET workflow_status='INSPECTING',status='IN_PROGRESS',result=NULL WHERE inspection_id IN(SELECT id FROM @deletable);
                DROP TABLE dbo.fe05_reinspection_defect_attachments_v1;
                DROP TABLE dbo.fe05_reinspection_defect_results_v1;
                DROP TABLE dbo.fe05_reinspection_bindings_v1;
                DELETE dbo.audit_logs WHERE entity_type='REINSPECTION' AND TRY_CONVERT(bigint,entity_id) IN(SELECT id FROM @owned);
                DELETE dbo.notification_operation_reservations WHERE actor_user_id={actor}
                  AND JSON_VALUE(result_json,'$.contract') IN('FE05_REINSPECTION_ASSIGNMENT_V1','FE05_REINSPECTION_CONCLUSION_V1')
                  AND TRY_CONVERT(bigint,JSON_VALUE(result_json,'$.reworkId'))={rework};
                DELETE dbo.room_status_history WHERE trigger_type='INSPECTION' AND trigger_id IN(SELECT id FROM @owned);
                DELETE l FROM dbo.inspection_defect_attachments l JOIN dbo.inspection_defects d ON d.defect_id=l.defect_id WHERE d.inspection_id IN(SELECT id FROM @deletable);
                DELETE dbo.attachments WHERE entity_type='INSPECTION' AND entity_id IN(SELECT id FROM @deletable);
                DELETE dbo.inspection_defects WHERE inspection_id IN(SELECT id FROM @deletable);
                DELETE dbo.inspection_results WHERE inspection_id IN(SELECT id FROM @deletable);
                DELETE dbo.room_inspections WHERE inspection_id IN(SELECT id FROM @deletable);
                UPDATE dbo.cleaning_reworks SET workflow_status='PENDING_REINSPECTION',status='IN_PROGRESS',completed_at=NULL
                  WHERE rework_id={rework} AND workflow_status IN('FAILED','COMPLETED');
                UPDATE dbo.cleaning_tasks SET workflow_status='REWORK_REQUIRED',status='IN_PROGRESS' WHERE cleaning_task_id={source.CleaningTaskId} AND (workflow_status<>'REWORK_REQUIRED' OR status<>'IN_PROGRESS');
                UPDATE dbo.rooms SET operational_status={originalRoomStatus} WHERE room_id={roomId} AND operational_status<>{originalRoomStatus};
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
            foreach (var name in keys)
            {
                if (Path.GetFileName(name) != name || name.Contains(':')) throw new Exception("Owned evidence filename required");
                File.Delete(Path.Combine(SprintReworkFixture.EvidenceRoot, name));
            }
        }
        try
        {
            if (distinctInspector)
            {
                await using var rehearsal = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var temporary = await SprintDistinctReinspectorFixture.CreateAsync(db, source.InspectorUserId, branch, ct);
                await SprintDistinctReinspectorFixture.CleanupAsync(db, temporary.UserId, branch, ct);
                await rehearsal.RollbackAsync(ct); db.ChangeTracker.Clear();
            }
            await using (var rehearsal = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
            { await Install(); await Cleanup(); await rehearsal.RollbackAsync(ct); }
            await using (var setup = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
            {
                await Install();
                if (distinctInspector)
                {
                    user = await SprintDistinctReinspectorFixture.CreateAsync(db, source.InspectorUserId, branch, ct);
                    actor = user.UserId;
                }
                await setup.CommitAsync(ct); installed = true;
                if (distinctInspector) ownedInspector = actor;
            }
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
            var assignedId = ((ReinspectionAssignmentResult)results[0].Value!).Id;
            async Task<RoomTypeBatchResult> Read(long readBranch, long readId, SessionReference readTicket, long readSid, long? file = null, bool original = false)
            {
                await using var context = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()).Options);
                await context.Database.OpenConnectionAsync(ct);
                await context.Database.ExecuteSqlRawAsync("EXECUTE AS USER='DEV3_Reinspector'", ct);
                try
                {
                    var identity = new Identity(new(readTicket.UserId, readSid, false));
                    var service = new RoomTypeCreationService(context, identity,
                        new PermissionService(identity, new EfPermissionDataSource(context), new UnresolvedPermissionResourceResolver(), TimeProvider.System),
                        new SqlSessionService(context, new SessionRevocationService(context), TimeProvider.System), TimeProvider.System);
                    return original
                        ? await service.ReadReinspectionOriginalAsync(readTicket, readSid, readBranch, readId, SprintReworkFixture.EvidenceRoot, ct, file)
                        : await service.ReadReinspectionExecutionAsync(readTicket, readSid, readBranch, readId, SprintReworkFixture.EvidenceRoot, ct, file);
                }
                finally { await context.Database.ExecuteSqlRawAsync("REVERT", CancellationToken.None); }
            }
            var detailRead = await Read(branch, assignedId, ticket, sid);
            if (detailRead.Status != RoomTypeBatchStatus.Success || detailRead.Value is not ReinspectionExecutionDto detail
                || detail.Id != assignedId || detail.ReworkId != rework || detail.InspectorId != actor
                || detail.Defects.Count == 0 || detail.Defects.Any(d => string.IsNullOrWhiteSpace(d.Remediation))
                || detailRead.ETag != detail.SourceETag || detail.State != "ASSIGNED")
                throw new Exception("Assigned reinspection detail unavailable: " + detailRead.Status);
            if ((await Read(branch + 100000, assignedId, ticket, sid)).Status == RoomTypeBatchStatus.Success
                || (await Read(branch, assignedId, executorTicket, executorSid)).Status == RoomTypeBatchStatus.Success
                || (await Read(branch, detail.OriginalInspectionId, ticket, sid)).Status == RoomTypeBatchStatus.Success
                || (await Read(branch, assignedId, ticket, sid, long.MaxValue)).Status != RoomTypeBatchStatus.NotFound)
                throw new Exception("Reinspection read/evidence scope leaked");
            var originalPhoto = detail.Defects.SelectMany(d => d.OriginalPhotos.Concat(d.RemediationPhotos)).First();
            var originalRead = await Read(branch, assignedId, ticket, sid, original: true);
            if (originalRead.Value is not ReinspectionOriginalDto originalSource || originalSource.Id != detail.OriginalInspectionId
                || originalSource.InspectorId != source.InspectorUserId || originalSource.ReinspectionId != assignedId
                || originalSource.Criteria.Count == 0 || originalSource.Defects.Count == 0 || originalSource.Evidence.Count == 0
                || originalSource.Evidence.Any(e => e.EntityType != "INSPECTION" || e.EntityId != originalSource.Id)
                || (distinctInspector && originalSource.InspectorId == actor)) throw new Exception("Exact original source read failed");
            foreach (var file in originalSource.Evidence)
                if ((await Read(branch, assignedId, ticket, sid, file.Id, original: true)).Value is not HotelOps.Api.DTOs.HotelStructure.PrivateHistoryFileDto)
                    throw new Exception("Original source evidence denied");
            var remediation = detail.Defects.SelectMany(d => d.RemediationPhotos).First();
            if ((await Read(branch, assignedId, ticket, sid, remediation, original: true)).Status != RoomTypeBatchStatus.NotFound
                || (await Read(branch, assignedId, executorTicket, executorSid, original: true)).Status == RoomTypeBatchStatus.Success
                || (await Read(branch + 100000, assignedId, ticket, sid, original: true)).Status == RoomTypeBatchStatus.Success
                || (await Read(branch, detail.OriginalInspectionId, ticket, sid, original: true)).Status == RoomTypeBatchStatus.Success)
                throw new Exception("Original source scope leaked");
            if ((await Read(branch, assignedId, ticket, sid, originalPhoto)).Value is not HotelOps.Api.DTOs.HotelStructure.PrivateHistoryFileDto)
                throw new Exception("Assigned inspector could not read linked evidence");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={branch}", ct);
            try
            {
                if ((await Read(branch, assignedId, ticket, sid)).Status == RoomTypeBatchStatus.Success
                    || (await Read(branch, assignedId, ticket, sid, originalPhoto)).Status == RoomTypeBatchStatus.Success
                    || (await Read(branch, assignedId, ticket, sid, original: true)).Status == RoomTypeBatchStatus.Success
                    || (await Read(branch, assignedId, ticket, sid, originalSource.Evidence[0].Id, original: true)).Status == RoomTypeBatchStatus.Success)
                    throw new Exception("Revoked inspector retained detail/evidence");
            }
            finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={branch}", ct); }
            var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityType == "REINSPECTION" && a.EntityId == assignedId.ToString() && a.Action == "CREATE", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values=N'{{}}' WHERE audit_id={audit.AuditId}", ct);
            try
            {
                if ((await Read(branch, assignedId, ticket, sid)).Status == RoomTypeBatchStatus.Success
                    || (await Read(branch, assignedId, ticket, sid, original: true)).Status == RoomTypeBatchStatus.Success)
                    throw new Exception("Unproven assignment receipt admitted to detail");
            }
            finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.audit_logs SET new_values={audit.NewValues} WHERE audit_id={audit.AuditId}", ct); }
            var finalRead = await Read(branch, assignedId, ticket, sid);
            if (finalRead.ETag != detail.SourceETag || finalRead.Value is not ReinspectionExecutionDto finalDetail
                || finalDetail.BindingETag != detail.BindingETag || finalDetail.ReworkETag != detail.ReworkETag)
                throw new Exception("Reinspection read mutated lifecycle revisions");
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
            await SprintReinspectionHttpsProbe.VerifyAsync(db, actor, branch, rework, executorTicket, executorSid, ct);
            if (Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BROWSER_CONCLUDE") != "1")
                await SprintReinspectionConclusionProbe.VerifyAsync(db, ticket, sid, branch, rework, ct);
            await SprintReinspectionTerminalReadProbe.VerifyAsync(db, ticket, sid, branch, rework, ct);
            if (Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BROWSER_CONCLUDE") != "1")
                await SprintReinspectionExecutorProbe.VerifyAsync(db, executorTicket, executorSid, branch, rework, ct);
            if (Environment.GetEnvironmentVariable("DEV3_REJECTION_ACCEPTANCE") == "1")
                await SprintRejectionSourceProbe.VerifyAfterLifecycleAsync(db, branch, rework, executorTicket.UserId, ct);
        }
        finally
        {
            if (installed)
            {
                await using var cleanup = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, CancellationToken.None);
                await Cleanup();
                if (ownedInspector is { } owned) await SprintDistinctReinspectorFixture.CleanupAsync(db, owned, branch, CancellationToken.None);
                await cleanup.CommitAsync(CancellationToken.None); db.ChangeTracker.Clear();
            }
        }
    }
    private sealed class Identity(CurrentUserInfo value) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = value; return true; } }
}
