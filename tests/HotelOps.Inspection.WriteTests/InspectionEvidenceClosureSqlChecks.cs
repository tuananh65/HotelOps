using System.Data;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

internal static class InspectionEvidenceClosureSqlChecks
{
    internal static async Task RunAsync(HotelOpsDbContext db,string environment,string output,Func<Task> guard,NotificationCommitGuard commitGuard)
    {
        var results=new List<object>();await guard();
        var inspection=await db.RoomInspections.AsNoTracking().OrderBy(i=>i.InspectionId).FirstAsync();
        var actor=inspection.InspectorUserId;var other=await db.Users.Where(u=>u.Username=="int.denied").Select(u=>u.UserId).SingleAsync();
        var branch=await db.Rooms.Where(r=>r.RoomId==inspection.RoomId).Select(r=>r.BranchId).SingleAsync();
        using var secret=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(environment))!,"runtime.private.json")));
        CreatedSqlSession login;
        await using(var setup=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            await guard();var passwords=new PasswordCredentialService(Options.Create(new PasswordHasherOptions{CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount}));
            var credential=await new CredentialLockoutService(db,passwords,TimeProvider.System).VerifyAsync(actor,secret.RootElement.GetProperty("password").GetString()!,
                (u,_)=>Task.FromResult(u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"));
            login=await new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System).CreateSessionAsync(credential);
            await setup.CommitAsync();db.ChangeTracker.Clear();
        }
        var current=new Identity(new(actor,login.Session.SessionId,false));
        var permissions=new PermissionService(current,new EfPermissionDataSource(db),new UnresolvedPermissionResourceResolver(),TimeProvider.System);
        var fileRoot=Path.GetFullPath(Path.Combine(output,"files",Guid.NewGuid().ToString("N")));Directory.CreateDirectory(fileRoot);
        async Task<string> State()=>await db.Database.SqlQuery<string>($"SELECT (SELECT (SELECT inspection_id,row_version FROM dbo.room_inspections ORDER BY inspection_id FOR JSON PATH) AS roots,(SELECT attachment_id,entity_type,entity_id,storage_key,file_size_bytes,deleted_at FROM dbo.attachments ORDER BY attachment_id FOR JSON PATH) AS attachments,(SELECT cleaning_task_id,status,workflow_status,row_version FROM dbo.cleaning_tasks ORDER BY cleaning_task_id FOR JSON PATH) AS tasks,(SELECT rework_id,inspection_id,assignee_user_id,row_version FROM dbo.cleaning_reworks ORDER BY rework_id FOR JSON PATH) AS reworks,(SELECT role_id,permission_id,data_scope FROM dbo.role_permissions ORDER BY role_id,permission_id FOR JSON PATH) AS grants FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS Value").SingleAsync();
        async Task Check(string title,Func<RoomTypeCreationService,InspectionUsersPrelock,long,string,InspectionPreparedEvidence,Task> action,bool attach=true)
        {
            await guard();var before=await State();var audits=await db.AuditLogs.LongCountAsync();
            var reader=new InspectionRecipientRosterReader(db,new MissingInspectionOriginalCleanerSource());
            var advisory=await reader.DiscoverAsync(actor,branch,inspection.InspectionId,other,Fe05NotificationKind.CleaningReworkRequested,default);
            var users=new InspectionUsersPrelock(db,reader,commitGuard);var owner=new InspectionOwnerOperation();
            var service=new RoomTypeCreationService(db,current,permissions,new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System),TimeProvider.System,
                inspectionOwner:owner,inspectionUsers:users,inspectionRoster:reader);
            await using var prepared=await InspectionVersionedEvidenceCandidate.PrepareAsync(fileRoot,Encoding.ASCII.GetBytes("%PDF-1.4 synthetic versioned evidence"),default);
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var locked=await new UserEligibilityLock(db).LockAsync(advisory.Users,default);
                await users.RetainLockedUsersAsync(advisory,locked,default);
                await db.Database.SqlQuery<int>($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction'; SELECT @r AS Value").ToArrayAsync();
                long attachment=0;
                if(attach)
                {
                    await guard();var row=new Attachment{EntityType="INSPECTION",EntityId=inspection.InspectionId,FileName=prepared.Version.Key,StorageKey=prepared.Version.Key,
                        OriginalFileName="versioned.pdf",MimeType="application/pdf",FileSizeBytes=prepared.Version.Length,UploadedBy=actor,UploadedAt=DateTime.UtcNow};
                    db.Add(row);await db.SaveChangesAsync();db.ChangeTracker.Clear();attachment=row.AttachmentId;
                }
                var version=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={inspection.InspectionId}").SingleAsync();
                await action(service,users,attachment,'"'+Convert.ToHexString(version)+'"',prepared);
                if(!ReferenceEquals(tx.GetDbTransaction(),db.Database.CurrentTransaction!.GetDbTransaction()))throw new Exception("Caller transaction replaced");
            }
            finally{if(tx.GetDbTransaction().Connection!=null)await tx.RollbackAsync();db.ChangeTracker.Clear();}
            if(await State()!=before||await db.AuditLogs.LongCountAsync()!=audits)throw new Exception("SQL evidence rollback escaped");
            results.Add(new{title,result="PASS",rollback=true});Console.WriteLine("PASS "+title);
        }
        async Task Deny(Func<Task> action)
        {try{await action();}catch(Exception e)when(e is Fe05NotificationUnavailableException or ObjectDisposedException or InvalidOperationException){return;}throw new Exception("Expected fail-closed denial");}
        Task<InspectionEvidenceReadLease> Pin(RoomTypeCreationService service,long id,string tag)=>service.PinVersionedInspectionEvidenceInCallerTransactionAsync(login.Ticket,login.Session.SessionId,branch,
            inspection.InspectionId,id,tag,fileRoot,commitGuard,default);
        try
        {
            await Check("Persisted version key/length and real owner session pin file in exact guarded transaction",async(service,users,id,tag,prepared)=>{
                await using var lease=await Pin(service,id,tag);if(lease.Version!=prepared.Version)throw new Exception("Persisted key changed");
                await commitGuard.VerifyBeforeCommitAsync(db,default);
            });
            await Check("Source stale ETag cannot pin evidence",async(service,users,id,tag,prepared)=>{
                await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET summary_note=summary_note WHERE inspection_id={inspection.InspectionId}");
                await Deny(()=>Pin(service,id,tag));
            });
            await Check("Attachment rebind invalidates pinned metadata and before-commit guard",async(service,users,id,tag,prepared)=>{
                await using var lease=await Pin(service,id,tag);await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.attachments SET storage_key='fixture.pdf' WHERE attachment_id={id}");
                await Deny(()=>commitGuard.VerifyBeforeCommitAsync(db,default));
                try{await db.Database.CurrentTransaction!.CommitAsync();throw new Exception("Guarded bad rebind committed");}catch(InvalidOperationException e)when(e.Message.Contains("roll back")){}
            });
            await Check("Premature lease disposal blocks guarded commit verification",async(service,users,id,tag,prepared)=>{
                var lease=await Pin(service,id,tag);await lease.DisposeAsync();await Deny(()=>commitGuard.VerifyBeforeCommitAsync(db,default));
            });
            await Check("Legacy mutable storage key rejected without rewriting existing evidence",async(service,users,id,tag,prepared)=>{
                await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.attachments SET storage_key='fixture.pdf' WHERE attachment_id={id}");
                var revision=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={inspection.InspectionId}").SingleAsync();
                await Deny(()=>Pin(service,id,'"'+Convert.ToHexString(revision)+'"'));
            });
            await Check("Revoked owner SQL session cannot pin evidence",async(service,users,id,tag,prepared)=>{
                await new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System).RevokeSessionAsync(login.Ticket);
                await Deny(()=>Pin(service,id,tag));
            });
            await Check("Soft-deleted attachment rejected by source pin",async(service,users,id,tag,prepared)=>{
                await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.attachments SET deleted_at=SYSUTCDATETIME() WHERE attachment_id={id}");
                await Deny(()=>Pin(service,id,tag));
            });
            await Check("Replacement after file lease release invalidates persisted digest identity",async(service,users,id,tag,prepared)=>{
                await File.WriteAllBytesAsync(Path.Combine(fileRoot,prepared.Version.Key),Encoding.ASCII.GetBytes(new string('x',(int)prepared.Version.Length)));
                await Deny(()=>Pin(service,id,tag));
            });
            await Check("Unknown-commit observation retains actual unaccepted N01 SQL lane gate",async(service,users,id,tag,prepared)=>{
                var request=new Fe05OperationRequest(Guid.NewGuid(),branch,inspection.InspectionId,new string('A',64),[Fe05NotificationKind.CleaningReworkRequested]);
                var coord=new InspectionOwnerSourceCoordination(db,current,new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System),permissions,
                    // Test obtains the exact owner injected above through the service seam, not a second operation authority.
                    (InspectionOwnerOperation)typeof(RoomTypeCreationService).GetFields(System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
                        .Single(f=>f.FieldType==typeof(InspectionOwnerOperation)).GetValue(service)!,users);
                var store=new Fe05Fs013CandidateStore(db,new InspectionClosedTrialExecution());
                try{await service.ObserveInspectionOutcomeInCallerTransactionAsync(login.Ticket,login.Session.SessionId,request,store,coord,commitGuard,default,fileRoot);throw new Exception("Unaccepted SQL lane observed success");}
                catch(Fe05NotificationUnavailableException e)when(e.Message=="N01_SQL_LANE_NOT_ACCEPTED"){}
            },attach:false);
            await Check("Exact Rework assignee current grant matches and revoked grant denies in same caller transaction",async(service,users,id,tag,prepared)=>{
                await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET status='COMPLETED',result='FAIL',workflow_status='FAILED',inspected_at=SYSUTCDATETIME() WHERE inspection_id={inspection.InspectionId}; UPDATE dbo.cleaning_tasks SET workflow_status='REWORK_REQUIRED' WHERE cleaning_task_id={inspection.CleaningTaskId}");
                var created=await db.Database.SqlQuery<long>($"""
                    INSERT dbo.cleaning_reworks(cleaning_task_id,inspection_id,assignee_user_id,reason,status,workflow_status,requested_at,due_at)
                    VALUES({inspection.CleaningTaskId},{inspection.InspectionId},{other},'Synthetic scoped read contract','PENDING','PENDING_ACCEPTANCE',SYSUTCDATETIME(),DATEADD(hour,2,SYSUTCDATETIME()));
                    SELECT CONVERT(bigint,SCOPE_IDENTITY()) AS Value;
                    """).ToArrayAsync();
                var rework=created.Single();var row=await db.CleaningReworks.AsNoTracking().SingleAsync(w=>w.ReworkId==rework);
                var rootRevision=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={inspection.InspectionId}").SingleAsync();
                var reworkRevision=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.cleaning_reworks WHERE rework_id={rework}").SingleAsync();
                var source=new Fe05NotificationSource(actor,branch,inspection.RoomId,inspection.InspectionId,users.Roster.TaskId,inspection.VersionId,rework,
                    '"'+Convert.ToHexString(rootRevision)+'"',users.Roster.TaskId,"COMPLETED","FAIL","FAILED",new DateTimeOffset(DateTime.SpecifyKind(row.RequestedAt,DateTimeKind.Utc)),[1],'"'+Convert.ToHexString(reworkRevision)+'"');
                var policy=new InspectionRecipientSourceReadCandidate(db,users,TimeProvider.System);
                var match=await policy.AssessAsync(source,other,InspectionRecipientRelationship.ReworkAssignee,default);
                if(!match.ExistingGrantAndRelationshipMatch||match.CanRead||match.CanReadPrivateEvidence)throw new Exception("Exact Rework candidate failed or escaped authority boundary");
                await guard();await db.Database.ExecuteSqlInterpolatedAsync($"DELETE rp FROM dbo.role_permissions rp JOIN dbo.permissions p ON p.permission_id=rp.permission_id JOIN dbo.roles r ON r.role_id=rp.role_id WHERE r.role_code='HK_STAFF' AND p.permission_code='CLEANING_VIEW'");
                if((await policy.AssessAsync(source,other,InspectionRecipientRelationship.ReworkAssignee,default)).ExistingGrantAndRelationshipMatch)throw new Exception("Revoked Rework read grant accepted");
            },attach:false);
            async Task RecipientCheck(string title,Func<InspectionRecipientSourceReadCandidate,Fe05NotificationSource,Task> run)
            {
                await guard();var before=await State();var audits=await db.AuditLogs.LongCountAsync();
                RoomTypeCreationService? service=null;
                var source=new InspectionR05CleanerSource(db,current,permissions,()=>service!);source.BindCaller(login.Ticket,login.Session.SessionId);
                var reader=new InspectionRecipientRosterReader(db,source);var users=new InspectionUsersPrelock(db,reader,commitGuard);
                service=new(db,current,permissions,new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System),TimeProvider.System,inspectionUsers:users,inspectionRoster:reader);
                var advisory=await reader.DiscoverAsync(actor,branch,inspection.InspectionId,null,Fe05NotificationKind.InspectionFailed,default);
                await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    var locked=await new UserEligibilityLock(db).LockAsync(advisory.Users,default);await users.RetainLockedUsersAsync(advisory,locked,default);
                    await db.Database.SqlQuery<int>($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction'; SELECT @r AS Value").ToArrayAsync();
                    await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET status='COMPLETED',result='FAIL',workflow_status='FAILED',inspected_at=SYSUTCDATETIME() WHERE inspection_id={inspection.InspectionId}; UPDATE dbo.cleaning_tasks SET workflow_status='REWORK_REQUIRED' WHERE cleaning_task_id={inspection.CleaningTaskId}");
                    var version=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={inspection.InspectionId}").SingleAsync();
                    var observed=new Fe05NotificationSource(actor,branch,inspection.RoomId,inspection.InspectionId,advisory.TaskId,inspection.VersionId,null,
                        '"'+Convert.ToHexString(version)+'"',advisory.TaskId,"COMPLETED","FAIL","FAILED",DateTimeOffset.UtcNow,[1]);
                    await run(new(db,users,TimeProvider.System),observed);
                }
                finally{if(tx.GetDbTransaction().Connection!=null)await tx.RollbackAsync();db.ChangeTracker.Clear();}
                if(await State()!=before||await db.AuditLogs.LongCountAsync()!=audits)throw new Exception("Recipient case rollback escaped");
                results.Add(new{title,result="PASS",rollback=true});Console.WriteLine("PASS "+title);
            }
            await RecipientCheck("Exact supervisor and real R05 original-cleaner candidate grants match without business acceptance",async(policy,source)=>{
                foreach(var role in new[]{InspectionRecipientRelationship.ResponsibleSupervisor,InspectionRecipientRelationship.OriginalCleaner})
                {
                    var r=await policy.AssessAsync(source,role==InspectionRecipientRelationship.ResponsibleSupervisor?actor:other,role,default);
                    if(!r.ExistingGrantAndRelationshipMatch||r.AcceptedConcreteBinding!=null||r.CanRead||r.CanReadPrivateEvidence)throw new Exception("Recipient grant candidate boundary");
                }
            });
            await RecipientCheck("R05 relationship alone cannot authorize recipient after existing grant revocation",async(policy,source)=>{
                await guard();await db.Database.ExecuteSqlInterpolatedAsync($"DELETE rp FROM dbo.role_permissions rp JOIN dbo.permissions p ON p.permission_id=rp.permission_id JOIN dbo.roles r ON r.role_id=rp.role_id WHERE r.role_code='HK_STAFF' AND p.permission_code='CLEANING_VIEW'");
                if((await policy.AssessAsync(source,other,InspectionRecipientRelationship.OriginalCleaner,default)).ExistingGrantAndRelationshipMatch)throw new Exception("Revoked recipient grant accepted");
            });
            await RecipientCheck("Other retained User cannot borrow supervisor relationship",async(policy,source)=>{
                if((await policy.AssessAsync(source,other,InspectionRecipientRelationship.ResponsibleSupervisor,default)).ExistingGrantAndRelationshipMatch)throw new Exception("Wrong recipient relationship accepted");
            });
            await File.WriteAllTextAsync(Path.Combine(output,"evidence-sql.json"),JsonSerializer.Serialize(new{success=true,componentOnly=true,acceptedN01Lane=false,
                publicMutation=false,durableImmutabilityAccepted=false,realOwnerSession=true,cases=results},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception e){await File.WriteAllTextAsync(Path.Combine(output,"evidence-sql.json"),JsonSerializer.Serialize(new{success=false,cases=results,error=e.GetType().Name+": "+e.Message},new JsonSerializerOptions{WriteIndented=true}));throw;}
    }
    private sealed class Identity(CurrentUserInfo actor):ICurrentUserAccessor{public bool TryGetCurrentUser(out CurrentUserInfo user){user=actor;return true;}}
}
