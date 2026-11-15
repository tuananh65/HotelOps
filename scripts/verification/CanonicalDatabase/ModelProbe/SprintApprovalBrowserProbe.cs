using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal static class SprintApprovalBrowserProbe
{
    internal static async Task<int> RunAsync(string report, bool retry = false, bool concurrentBlock = false, bool approvalRace = false, bool rejection = false, bool rework = false)
    {
        if(File.Exists(report)) throw new IOException("Evidence already exists");
        var rejectionSource=Environment.GetEnvironmentVariable("DEV3_REJECTION_SOURCE")=="1";
        var rejectionAcceptance=Environment.GetEnvironmentVariable("DEV3_REJECTION_ACCEPTANCE")=="1";
        if(rejectionAcceptance&&(!rework||!rejectionSource))throw new InvalidOperationException("Reject acceptance requires owned rework and source fixture");
        if(rejectionSource&&!rework)throw new InvalidOperationException("Rejection source requires owned rework/evidence fixture");
        if(Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BLOCK_RACE")=="1"
            &&Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BROWSER_CONCLUDE")=="1")
            throw new InvalidOperationException("SQL block-race mode and browser-conclusion mode are mutually exclusive");
        if(Environment.GetEnvironmentVariable("DEV3_REINSPECTION_RETIRE_AFTER_ASSIGNMENT")=="1"
            &&Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BROWSER_CONCLUDE")!="1")
            throw new InvalidOperationException("Retirement trial requires browser conclusion mode");
        using var budget=new CancellationTokenSource(TimeSpan.FromMinutes(
            Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BROWSER_CONCLUDE")=="1"
            ||Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BLOCK_RACE")=="1"||rejectionSource||rejectionAcceptance?8:4)); var ct=budget.Token;
        await using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(
            "Server=localhost;Database=HotelOps_DEV3_142A_20261114_cc78c13f;Integrated Security=true;Encrypt=true;TrustServerCertificate=true").Options);
        var checks=new List<object>(); string? failure=null;
        foreach(var blocked in new[]{false,true})
        {
            long actor=0,branch=0,inspection=0,task=0,room=0,cleaner=0,incidentId=0; bool committed=false;
            var password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))+"Aa1!";
            async Task Seed()
            {
                // Exact server/database/GUID/run guard runs before any mutation.
                await using var command=db.Database.GetDbConnection().CreateCommand();
                command.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
                command.CommandText=await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/SprintApproveFixture.sql",ct);
                await using(var reader=await command.ExecuteReaderAsync(ct))
                { if(!await reader.ReadAsync(ct)) throw new Exception("Fixture missing"); actor=reader.GetInt64(0); branch=reader.GetInt64(1); inspection=reader.GetInt64(2);task=reader.GetInt64(3);room=reader.GetInt64(4); }
                await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/ModelProbe/SprintCleanerAttribution.sql",ct),ct);
                cleaner=await SprintApproveProbe.SeedCleanerAsync(db,actor,branch,task,"recipient-eligible",ct);
                if(rework) await SprintReworkFixture.SeedAsync(db,actor,cleaner,branch,ct);
                if(rejection)
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE dbo.room_inspections SET workflow_status='INSPECTING' WHERE inspection_id={inspection};
                        UPDATE dbo.inspection_results SET outcome='FAIL' WHERE inspection_id={inspection};
                        IF {(!rejectionAcceptance ? 1 : 0)}=1 INSERT dbo.inspection_defects(inspection_id,checklist_item_id,description,status)
                          SELECT inspection_id,checklist_item_id,'Synthetic rejection defect','OPEN' FROM dbo.inspection_results WHERE inspection_id={inspection};
                        UPDATE dbo.room_inspections SET workflow_status='PENDING_CONCLUSION' WHERE inspection_id={inspection};
                        """,ct);
                if(rework&&!rejectionAcceptance)await SprintReworkFixture.SeedEvidenceAsync(db,actor,inspection,ct);
                var user=await db.Users.AsNoTracking().SingleAsync(u=>u.UserId==actor,ct);
                var hash=new PasswordHasher<User>().HashPassword(user,password);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET password_hash={hash} WHERE user_id={actor}",ct);
                if(retry) await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.notification_recipients ADD CONSTRAINT CK_DEV3_142A_NotificationFault CHECK(user_id<0)",ct);
                if(approvalRace && blocked)
                {
                    // Narrow approved index trial, with original definition restored during cleanup.
                    command.CommandText=await File.ReadAllTextAsync("scripts/verification/CanonicalDatabase/RoomBlockPairIsolated.sql",ct);
                    await command.ExecuteNonQueryAsync(ct);
                }
                if(blocked || concurrentBlock)
                {
                    var incident=new Incident {IncidentNo="D3-APPROVE-BLOCK",BranchId=branch,RoomId=room,IncidentTitle="Synthetic approval block",Description="Synthetic",Status="OPEN",ReportedBy=actor,ReportedAt=DateTime.UtcNow,DiscoveredAt=DateTime.UtcNow};
                    db.Incidents.Add(incident);await db.SaveChangesAsync(ct);
                    incidentId=incident.IncidentId;
                    if(blocked) db.RoomBlocks.Add(new RoomBlock{RoomId=room,IncidentId=incidentId,Reason="Synthetic block",BlockedBy=actor,BlockedAt=DateTime.UtcNow.AddMinutes(-1)});
                    await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
                    if(approvalRace && blocked)
                    {
                        var second=new Incident{IncidentNo="D3-APPROVE-BLOCK-2",BranchId=branch,RoomId=room,IncidentTitle="Second synthetic block",Description="Synthetic",Status="OPEN",ReportedBy=actor,ReportedAt=DateTime.UtcNow,DiscoveredAt=DateTime.UtcNow};
                        db.Incidents.Add(second);await db.SaveChangesAsync(ct);
                        db.RoomBlocks.Add(new RoomBlock{RoomId=room,IncidentId=second.IncidentId,Reason="Second synthetic block",BlockedBy=actor,BlockedAt=DateTime.UtcNow.AddMinutes(-1)});
                        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
                    }
                }
                if(concurrentBlock)
                    await db.Database.ExecuteSqlRawAsync("""
                        CREATE TRIGGER dbo.TR_DEV3_142A_ApprovalDelay ON dbo.room_inspections AFTER UPDATE AS
                        BEGIN
                          IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON i.inspection_id=d.inspection_id
                            WHERE i.workflow_status='PASSED' AND d.workflow_status='PENDING_CONCLUSION')
                          BEGIN
                            DECLARE @r int;
                            EXEC @r=sys.sp_getapplock @Resource='DEV3:ApprovalDelay',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=1000;
                            IF @r<0 THROW 51994,'Synthetic approval synchronization failed',1;
                            WAITFOR DELAY '00:00:04';
                          END
                        END
                        """,ct);
            }
            async Task Cleanup(CancellationToken token)
            {
                if(rework) await SprintReworkFixture.CleanupAsync(db,cleaner,branch,inspection,token);
                if(concurrentBlock) await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.TR_DEV3_142A_ApprovalDelay",token);
                if(rejection) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status='INSPECTING',status='IN_PROGRESS',result=NULL WHERE inspection_id={inspection}; DELETE dbo.inspection_defects WHERE inspection_id={inspection}",token);
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.room_blocks WHERE room_id={room} AND blocked_by={actor}; DELETE dbo.incidents WHERE room_id={room} AND reported_by={actor} AND incident_no IN('D3-APPROVE-BLOCK','D3-APPROVE-BLOCK-2')",token);
                if(approvalRace && blocked)
                    await db.Database.ExecuteSqlRawAsync("DROP INDEX UX_room_blocks_incident_room_active ON dbo.room_blocks; CREATE UNIQUE INDEX UX_room_blocks_active ON dbo.room_blocks(room_id) WHERE released_at IS NULL",token);
                await SprintNotificationCommitProbe.CleanupAsync(db,actor,cleaner,branch,inspection,token);
            }
            try
            {
                // Prove complete cleanup against this schema in a rollback transaction first.
                await using(var rehearsal=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
                {await Seed();await Cleanup(ct);await rehearsal.RollbackAsync(ct);}
                await using(var setup=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
                {await Seed();await setup.CommitAsync(ct);committed=true;}
                if(rejectionAcceptance)
                {
                    await SprintRejectionAcceptanceProbe.VerifyAsync(db,actor,branch,inspection,password,ct);
                    checks.Add(new{blocked,rejectMissingDefectHttps=true,rejectMissingDefectMessage=true,rejectReasonLengthHttps=true,rejectAuditFaultHttps=true,rejectFullSnapshotRollback=true,rejectNoAutomaticRetry=true});
                }
                var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
                start.ArgumentList.Add(rejection ? "frontend/scripts/sprintRejectionBrowser.mjs" : "frontend/scripts/sprintApprovalBrowser.mjs");
                using var process=Process.Start(start)??throw new Exception("Browser unavailable");
                try
                {
                    await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username="D3-EVIDENCE",password,branch,inspection,blocked,retry,concurrentBlock,approvalRace,rework}).AsMemory(),ct);process.StandardInput.Close();
                    var output=process.StandardOutput.ReadToEndAsync(ct);var errors=process.StandardError.ReadToEndAsync(ct);
                    if(concurrentBlock)
                    {
                        var deadline=DateTime.UtcNow.AddSeconds(30);bool observed=false;
                        while(DateTime.UtcNow<deadline && !process.HasExited)
                        {
                            if(await db.Database.SqlQueryRaw<int>("SELECT APPLOCK_TEST('public','DEV3:ApprovalDelay','Shared','Session') AS Value").SingleAsync(ct)==0)
                            {observed=true;break;}
                            await Task.Delay(50,ct);
                        }
                        if(!observed)throw new Exception("Approval did not reach synchronized locked domain write");
                        // This connection races the API's retained range/row locks. It must wait
                        // for approval to commit, then the next API read must reflect the new block facts.
                        var elapsed=Stopwatch.StartNew();
                        if(blocked)
                            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_blocks SET released_at=SYSUTCDATETIME() WHERE room_id={room} AND blocked_by={actor}",ct);
                        else
                            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_blocks(room_id,incident_id,reason,blocked_by,blocked_at) VALUES({room},{incidentId},'Synthetic concurrent block',{actor},SYSUTCDATETIME())",ct);
                        if(elapsed.Elapsed<TimeSpan.FromSeconds(1))throw new Exception("Concurrent block write bypassed retained approval locks");
                        checks.Add(new{blocked,concurrentBlockWrite=blocked?"release":"insert",waitedMilliseconds=elapsed.ElapsedMilliseconds});
                    }
                    await process.WaitForExitAsync(ct);var result=await output;await errors;
                    using var json=JsonDocument.Parse(result);checks.Add(new{blocked,browser=json.RootElement.Clone()});
                    if(!json.RootElement.GetProperty("success").GetBoolean())throw new Exception("Browser failed at "+json.RootElement.GetProperty("stage").GetString());
                }
                finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}
                if(rejection)
                {
                    await SprintRejectionDeliveryProbe.VerifyAsync(db,actor,cleaner,branch,inspection,ct);
                    if(rejectionSource)
                    {
                        await SprintRejectionSourceProbe.VerifyAsync(db,actor,cleaner,branch,inspection,password,ct);
                        checks.Add(new{blocked,rejectionSourceBrowser=true,supervisorAndOfficialCleaner=true,historyPhotoReload=true,sourceRoleBranchRevocation=true,sourceAuditTamperDenied=true,sourceReceiptMembership=true,storedActionUrlIgnored=true});
                    }
                    if(await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.cleaning_tasks WHERE cleaning_task_id={task}").SingleAsync(ct)!="REWORK_REQUIRED"
                        ||await db.RoomInspections.Where(i=>i.InspectionId==inspection).Select(i=>i.Result).SingleAsync(ct)!="FAIL"
                        ||await db.Rooms.Where(r=>r.RoomId==room).Select(r=>r.OperationalStatus).SingleAsync(ct)!="INSPECTION")
                        throw new Exception("Rejected lifecycle/physical status mismatch");
                    checks.Add(new{blocked,rejection=true,atomicLifecycle=true,deduplicatedFanout=true,revocation=true,retry=true});
                    if(rework)
                    {
                        var created=await db.CleaningReworks.AsNoTracking().SingleAsync(r=>r.InspectionId==inspection,ct);
                        if(created.AssigneeUserId!=cleaner || created.Status!="PENDING"
                            || await db.Database.SqlQuery<string>($"SELECT workflow_status AS Value FROM dbo.cleaning_reworks WHERE rework_id={created.ReworkId}").SingleAsync(ct)!="PENDING_ACCEPTANCE"
                            || await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.cleaning_rework_defects WHERE rework_id={created.ReworkId}").SingleAsync(ct)!=1
                            || !await db.AuditLogs.AnyAsync(a=>a.EntityType=="cleaning_reworks"&&a.EntityId==created.ReworkId.ToString(),ct))
                            throw new Exception("Persisted rework/defect/assignee/audit mismatch");
                        await SprintReworkAuthorityProbe.VerifyAsync(db,cleaner,branch,created.ReworkId,ct);
                        await SprintReworkDeliveryProbe.VerifyAsync(db,cleaner,branch,created.ReworkId,ct);
                        await SprintAssignedReworkProbe.VerifyAsync(db,cleaner,branch,created.ReworkId,ct);
                        checks.Add(new{blocked,submissionSql=true,submissionRemediationAttribution=true,submissionIntervalClosed=true,
                            submissionUploadRollback=true,submissionAuditRollback=true,concurrentSubmissionOneReceipt=true,
                            submissionChangedRequestDenied=true,submissionStaleDenied=true,submissionRevocationDenied=true,
                            submissionDisabledIssuerDenied=true,submissionSchemaRestored=true,submissionHttps=true,
                            submissionPerDefectForm=true,submissionPhotoHttps=true,submissionReload=true,submissionCsrfEtag=true,
                            submissionDeliveryRollback=true,submissionDeliveryRevocation=true,submissionDeliveryDeduplicated=true,
                            submissionSupervisorInboxHttps=true,submissionSupervisorSourceVerified=true,submissionSourceRevocationTamperDenied=true});
                        checks.Add(new{blocked,assignmentNoticeDeliveryAndBrowser=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_ASSIGNMENT_NOTICE")=="1",
                            assignmentNoticeTerminalBrowser=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_ASSIGNMENT_NOTICE")=="1",
                            exactReinspectionAssignmentSql=true,assignmentStaleDenied=true,
                            assignmentBranchRevocationDenied=true,assignmentAuditRollback=true,
                            assignmentConcurrentReplayOneBinding=true,assignmentChangedRequestDenied=true,
                            assignmentDuplicateDenied=true,assignmentSchemaRollbackAndCleanup=true,
                            reinspectionConclusionVerified=false,reinspectionConclusionSqlVerified=true,
                            reinspectionConclusionOutcome=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_OUTCOME")??"PASS",
                            reinspectionConclusionStaleRollbackConcurrency=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BROWSER_CONCLUDE")!="1",
                            reinspectionConclusionBrowserVerified=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BROWSER_CONCLUDE")=="1",
                            terminalReinspectionReadEvidenceSql=true,terminalReadRevocationAuditTamperDenied=true,
                            terminalReadRecomputesReadiness=true,terminalReadDoesNotReopenMutation=true,
                            executorTerminalReadSql=true,executorTerminalEvidenceSql=true,executorTerminalRevocationTamperDenied=true,
                            executorTerminalBrowser=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BROWSER_CONCLUDE")=="1",
                            nextRoundCommittedBrowser=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_NEXT_ROUND")=="1",
                            nextRoundNotificationExecutorSource=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_NEXT_ROUND")=="1",
                            secondRoundAcceptStartSubmitBrowser=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_COMPLETE_NEXT_ROUND")=="1",
                            secondRoundReinspectionPassedBrowser=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_COMPLETE_NEXT_ROUND")=="1",
                            secondRoundClosedIntervalAndAudit=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_COMPLETE_NEXT_ROUND")=="1",
                            secondRoundNotificationsAndTerminalBrowser=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_COMPLETE_NEXT_ROUND")=="1",
                            submittedInboxToAssignmentBrowser=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_COMPLETE_NEXT_ROUND")=="1",
                            submittedSourceDeniedAfterConclusion=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_COMPLETE_NEXT_ROUND")=="1",
                            firstFailedHistoryPreservedAfterSecondPassed=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_COMPLETE_NEXT_ROUND")=="1",
                            originalInspectionScopedRead=true,
                            rejectSourceAfterTerminalLifecycle=rejectionAcceptance,
                            originalInspectionBrowserLink=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BROWSER_CONCLUDE")=="1",
                            distinctReinspector=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_DISTINCT_INSPECTOR")=="1",
                            reinspectionConcurrentBlockSql=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BLOCK_RACE")=="1",
                            assignedVersionRetiredBrowserVerified=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_RETIRE_AFTER_ASSIGNMENT")=="1",
                            reinspectionPassedDeliveryRollbackRetryDedup=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_OUTCOME")!="FAIL",
                            reinspectionPassedInboxSourceBrowser=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BROWSER_CONCLUDE")=="1"&&Environment.GetEnvironmentVariable("DEV3_REINSPECTION_OUTCOME")!="FAIL",
                            terminalReloadBrowserVerified=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_BROWSER_CONCLUDE")=="1",
                            reinspectionAssignmentBrowserVerified=true,
                            reinspectionAssignmentCsrfEtag=true,reinspectionAssignmentReplay=true,
                            reinspectionAssignmentReload=true,executorSubmissionReadAfterAssignment=true,
                            assignedReinspectionDetailSqlHttps=true,assignedReinspectionEvidenceHttps=true,
                            reinspectionReadRevocationDenied=true,reinspectionReadAuditTamperDenied=true,
                            reinspectionReadForeignSourceDenied=true,reinspectionReadDoesNotMutate=true,
                            genericInspectionMutationDenied=true});
                        checks.Add(new{blocked,committedAcceptStart=true,receiveReplay=true,changedRequestKeyDenied=true,
                            noWorkdayRollback=true,busyStartDenied=true,startAuditRollback=true,concurrentStartOneInterval=true,executionSchemaRestored=true});
                        checks.Add(new{blocked,restrictedReceiveBrowser=true,defectPhotoHttps=true,receiveCsrfEtag=true,receiveAcceptStartHttps=true,
                            receiveReload=true,receiveReplayHttps=true,receiveEffectiveReadiness=true,restrictedRoleCleanup=true});
                        checks.Add(new{blocked,assigneeInboxBrowser=true,inboxReloadPersistence=true,notificationSourceHttps=true,privilegedDetailFailClosed=true});
                        // A committed next round makes the first Failed inspection historical.
                        // Exercise audit rollback/concurrent creation against the current Failed
                        // source, after the browser has verified its real Pending Acceptance handoff.
                        var creationInspection=Environment.GetEnvironmentVariable("DEV3_REINSPECTION_NEXT_ROUND")=="1"
                            ?await db.RoomInspections.AsNoTracking().Where(i=>i.CleaningTaskId==task&&i.Result=="FAIL")
                                .OrderByDescending(i=>i.InspectionId).Select(i=>i.InspectionId).FirstAsync(ct):inspection;
                        var creationRework=await db.CleaningReworks.AsNoTracking().Where(w=>w.InspectionId==creationInspection)
                            .Select(w=>w.ReworkId).SingleAsync(ct);
                        await SprintReworkCreationProbe.VerifyAsync(db,actor,password,branch,creationInspection,cleaner,creationRework,ct);
                        checks.Add(new{blocked,reworkCreateAuditRollback=true,reworkCreateConcurrency=true,oneOpenRework=true});
                        checks.Add(new{blocked,reworkPersisted=true,selectedDefects=true,assignee=cleaner,audit=true,assignedReadWithRealSession=true,sourceResolution=true,revokedSourceDenied=true,foreignEvidenceDenied=true,reworkDelivery=true,reworkDeliveryRollback=true,reworkDeliveryRevocation=true,reworkConcurrentRetry=true,committedAuthority=true,leastPrivilegeReader=true,sourceTamperDenied=true,disabledIssuerDenied=true,unauthorizedDenied=true,noShiftAssignee=true,ineligibleAssigneesDenied=true});
                    }
                }
                else
                {
                if(retry)
                {
                    if(await db.Notifications.AnyAsync(ct)||await db.NotificationRecipients.AnyAsync(ct)
                        ||await db.AuditLogs.CountAsync(a=>a.EntityType=="INSPECTION"&&a.EntityId==inspection.ToString(),ct)!=1)
                        throw new Exception("Delivery fault lost durable event or published partial inbox");
                    // Revocation is synthetic and precedes fault removal. Hosted retry must recheck
                    // current access, not trust the eligible recipient captured during approval.
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={cleaner}",ct);
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.notification_recipients DROP CONSTRAINT CK_DEV3_142A_NotificationFault",ct);
                    await Task.Delay(TimeSpan.FromSeconds(3),ct);
                    if(await db.NotificationRecipients.AnyAsync(ct))throw new Exception("Worker bypassed revoked recipient access");
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=NULL WHERE user_id={cleaner}",ct);
                    var deadline=DateTime.UtcNow.AddSeconds(15);
                    while(!await db.NotificationRecipients.AnyAsync(n=>n.UserId==cleaner&&n.DeliveryStatus=="DELIVERED",ct)&&DateTime.UtcNow<deadline)
                        await Task.Delay(250,ct);
                    await Task.Delay(TimeSpan.FromSeconds(3),ct);
                    checks.Add(new{blocked,hostedRetry=true,revokedRecipientDenied=true,restoredAccessRetried=true,noApprovalReplay=true});
                }
                if(!await db.RoomInspections.AnyAsync(i=>i.InspectionId==inspection&&i.Status=="COMPLETED"&&i.Result=="PASS",ct)
                    || !await db.CleaningTasks.AnyAsync(t=>t.CleaningTaskId==task&&t.Status=="COMPLETED",ct)
                    || await db.Rooms.Where(r=>r.RoomId==room).Select(r=>r.OperationalStatus).SingleAsync(ct)!=(blocked?"INSPECTION":"READY")
                    || await db.RoomStatusHistories.CountAsync(h=>h.RoomId==room&&h.TriggerId==inspection,ct)!=(blocked?0:1)
                    || await db.AuditLogs.CountAsync(a=>a.EntityType=="INSPECTION"&&a.EntityId==inspection.ToString(),ct)!=1
                    || await db.NotificationRecipients.CountAsync(n=>n.UserId==cleaner&&n.DeliveryStatus=="DELIVERED",ct)!=1)
                    throw new Exception("Persisted approval/room/history/audit/original-cleaner notification mismatch");
                checks.Add(new{blocked,persistedBusinessAndNotification=true});
                if(approvalRace && blocked)
                {
                    if(await db.RoomBlocks.CountAsync(b=>b.RoomId==room&&b.ReleasedAt==null,ct)!=2)throw new Exception("Expected two effective incident blocks");
                    checks.Add(new{twoEffectiveBlocks=true,approvalRemainedBlocked=true});
                }
                }
            }
            catch(Exception error){failure=error.GetType().Name+": "+error.Message;
                if(error.InnerException is Microsoft.Data.SqlClient.SqlException sql) failure+=" SQL "+sql.Number+": "+sql.Message;}
            finally
            {
                try
                {
                    if(committed){await using var cleanup=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,CancellationToken.None);await Cleanup(CancellationToken.None);await cleanup.CommitAsync(CancellationToken.None);}
                }
                catch(Exception cleanupError)
                {
                    failure=(failure??"Business checks finished")+"; CLEANUP_FAILED: "+cleanupError.Message;
                    await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{success=false,checks,failure,cleanupFailed=true},new JsonSerializerOptions{WriteIndented=true}));
                    throw;
                }
                db.ChangeTracker.Clear();
                if(rework)SprintReworkFixture.CleanupEvidenceFiles();
            }
            if(await db.Users.AnyAsync(CancellationToken.None)||await db.Rooms.AnyAsync(CancellationToken.None)||await db.RoomBlocks.AnyAsync(CancellationToken.None)||await db.AuditLogs.AnyAsync(CancellationToken.None)||await db.Notifications.AnyAsync(CancellationToken.None))
                throw new Exception("Approval fixture cleanup failed");
            checks.Add(new{blocked,cleanup=true});if(failure!=null)break;
        }
        await File.WriteAllTextAsync(report,JsonSerializer.Serialize(new{success=failure==null,checks,failure},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(failure??"PASS approval browser HTTPS SQL, cleanup verified");return failure==null?0:1;
    }
}
