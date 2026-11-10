using System.Data;
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

internal static class InspectionResponsibilityChecks
{
    internal static void Unit()
    {
        var count=0;void Check(string name,bool condition){if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
        foreach(var match in new[]{false,true})
        {
            var result=new InspectionRecipientReadAssessment(match,"candidate");
            Check("Candidate match="+match+" cannot authorize source",!result.CanRead);
            Check("Candidate match="+match+" cannot authorize bytes",!result.CanReadPrivateEvidence);
            Check("Candidate match="+match+" retains null approved binding",result.AcceptedConcreteBinding==null);
        }
        Check("Reject dependency gates remain nonempty",RoomTypeCreationService.RejectWriteDependencies.Length>0);
        Check("Rework dependency gates remain nonempty",RoomTypeCreationService.ReworkWriteDependencies.Length>0);
        Check("Explicit assignment candidate is not public",typeof(RoomTypeCreationService).GetMethod("AssignInspectionWithSupervisorCandidateAsync")==null);
        Check("Binding writer is not public",typeof(RoomTypeCreationService).GetMethod("BindSupervisorInAssignmentAsync")==null);
        Console.WriteLine(count+"/"+count+" PASS");
    }

    internal static async Task SqlAsync(HotelOpsDbContext db,string environment,string output,Func<Task> guard,NotificationCommitGuard commitGuard)
    {
        SupervisorBindingUsers? expiredBinding=null;
        var results=new List<object>();var root=await db.RoomInspections.AsNoTracking().OrderBy(x=>x.InspectionId).FirstAsync();
        var actor=root.InspectorUserId;var branch=await db.Rooms.Where(x=>x.RoomId==root.RoomId).Select(x=>x.BranchId).SingleAsync();
        var foreign=await db.Branches.Where(x=>x.BranchCode=="BR002").Select(x=>x.BranchId).SingleAsync();
        var other=await db.Users.Where(x=>x.Username=="int.denied").Select(x=>x.UserId).SingleAsync();
        using var secret=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(environment))!,"runtime.private.json")));
        var passwords=new PasswordCredentialService(Options.Create(new PasswordHasherOptions{CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount}));
        var logins=new Dictionary<long,CreatedSqlSession>();
        await guard();await using(var setup=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            foreach(var id in new[]{actor,other})
            {
                var verified=await new CredentialLockoutService(db,passwords,TimeProvider.System).VerifyAsync(id,secret.RootElement.GetProperty("password").GetString()!,
                    (u,_)=>Task.FromResult(u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"));
                logins[id]=await new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System).CreateSessionAsync(verified);
            }
            await setup.CommitAsync();db.ChangeTracker.Clear();
        }
        RoomTypeCreationService Service(long user=0)
        {
            if(user==0)user=actor;var identity=new Identity(new(user,logins[user].Session.SessionId,false));
            return new(db,identity,new PermissionService(identity,new EfPermissionDataSource(db),new UnresolvedPermissionResourceResolver(),TimeProvider.System),
                new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System),TimeProvider.System,inspectionCommitGuard:commitGuard);
        }
        async Task Check(string title,Func<Task> run)
        {
            await guard();var before=await State();var audits=await db.AuditLogs.CountAsync();
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try{await run();if(!ReferenceEquals(tx.GetDbTransaction(),db.Database.CurrentTransaction!.GetDbTransaction()))throw new Exception("Physical caller transaction changed");}
            finally{if(tx.GetDbTransaction().Connection!=null)await tx.RollbackAsync();db.ChangeTracker.Clear();}
            if(await State()!=before||await db.AuditLogs.CountAsync()!=audits)throw new Exception("Business mutation escaped rollback");
            results.Add(new{title,result="PASS",rollback=true});Console.WriteLine("PASS "+title);
        }
        async Task<string> State()=>await db.Database.SqlQuery<string>($"SELECT (SELECT (SELECT inspection_id,status,result,workflow_status,row_version FROM dbo.room_inspections ORDER BY inspection_id FOR JSON PATH) AS roots,(SELECT * FROM dbo.inspection_supervisor_bindings ORDER BY inspection_id FOR JSON PATH) AS bindings,(SELECT cleaning_task_id,status,workflow_status,row_version FROM dbo.cleaning_tasks ORDER BY cleaning_task_id FOR JSON PATH) AS tasks,(SELECT room_id,cleaning_task_id,row_version FROM dbo.room_cleaning_cycle_state ORDER BY room_id FOR JSON PATH) AS cycles,(SELECT rework_id,inspection_id,cleaning_task_id,assignee_user_id,status,workflow_status,row_version FROM dbo.cleaning_reworks ORDER BY rework_id FOR JSON PATH) AS reworks,(SELECT user_role_id,user_id,role_id,branch_id,is_active FROM dbo.user_roles ORDER BY user_role_id FOR JSON PATH) AS roles,(SELECT access_id,user_id,branch_id,revoked_at FROM dbo.user_branch_access ORDER BY access_id FOR JSON PATH) AS access,(SELECT user_id,account_status FROM dbo.users ORDER BY user_id FOR JSON PATH) AS users FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS Value").SingleAsync();
        async Task Deny(Func<Task> run,string? contains=null)
        {
            try{await run();}catch(Fe05NotificationUnavailableException e)when(contains==null||e.Message.Contains(contains)){return;}
            throw new Exception("Expected closed rejection: "+contains);
        }
        async Task<SupervisorBindingUsers> Lock(long supervisor=0)
        {
            if(supervisor==0)supervisor=actor;
            var actual=await new UserEligibilityLock(db).LockAsync(new[]{actor,other,supervisor},default);
            // Actor and inspector deliberately differ for identity separation coverage.
            return SupervisorBindingUsers.Retain(db,actual,actor,other,supervisor);
        }
        async Task Catalog()=>_ = await db.Database.SqlQuery<int>($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction'; SELECT @r AS Value").ToArrayAsync();
        async Task<RoomTypeCreationService.SupervisorAssignmentSource> Assigned()
        {
            await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET status='PENDING',workflow_status='ASSIGNED',inspector_user_id={other} WHERE inspection_id={root.InspectionId}");
            return await Service().ReadSupervisorAssignmentSourceAsync(branch,root.InspectionId,default);
        }
        Task<string> Bind(SupervisorBindingUsers locks,RoomTypeCreationService.SupervisorAssignmentSource source,long supervisor=0,long user=0)
        {
            if(supervisor==0)supervisor=actor;if(user==0)user=actor;
            return Service(user).BindSupervisorInAssignmentAsync(logins[user].Ticket,logins[user].Session.SessionId,branch,root.InspectionId,supervisor,
                source.RootRevision,source.CycleRevision,"Explicit synthetic UC041 selection",locks,default);
        }
        async Task FixtureRole(long user,string code)
        {
            var role=await db.Roles.Where(r=>r.RoleCode==code).Select(r=>r.RoleId).SingleAsync();
            if(!await db.UserRoles.AnyAsync(r=>r.UserId==user&&r.RoleId==role&&r.BranchId==branch))
                db.Add(new UserRole{UserId=user,RoleId=role,BranchId=branch,AssignedAt=DateTime.UtcNow.AddHours(-1),IsActive=true});
            if(!await db.UserBranchAccesses.AnyAsync(a=>a.UserId==user&&a.BranchId==branch&&a.RevokedAt==null))
                db.Add(new UserBranchAccess{UserId=user,BranchId=branch,GrantedAt=DateTime.UtcNow.AddHours(-1),AccessLevel="STANDARD"});
            await guard();await db.SaveChangesAsync();db.ChangeTracker.Clear();
        }
        IReadOnlyDictionary<long,UserEligibilityLock.LockedUser?>? lockedResults=null;
        async Task<(InspectionUsersPrelock Users,InspectionRecipientSourceReadCandidate Policy,Fe05NotificationSource Source)> Failed()
        {
            var locks=await InitialLock();await Catalog();var src=await Assigned();await Bind(locks,src);
            await guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET inspector_user_id={actor},status='COMPLETED',result='FAIL',workflow_status='FAILED',inspected_at=SYSUTCDATETIME() WHERE inspection_id={root.InspectionId}; UPDATE dbo.cleaning_tasks SET status='IN_PROGRESS',workflow_status='REWORK_REQUIRED' WHERE cleaning_task_id={root.CleaningTaskId}");
            var reader=new InspectionRecipientRosterReader(db,new CleanerFixture(other));
            var roster=await reader.ReadAsync(actor,branch,root.InspectionId,null,Fe05NotificationKind.InspectionFailed,true,default);
            var users=new InspectionUsersPrelock(db,reader,commitGuard);
            // Same actual locks already held at transaction start; no late lock call.
            await users.RetainLockedUsersAsync(roster,lockedResults!,default);
            var source=new Fe05NotificationSource(actor,branch,root.RoomId,root.InspectionId,root.CleaningTaskId!.Value,root.VersionId,null,
                '"'+roster.InspectionRevision+'"',roster.TaskId,"COMPLETED","FAIL","FAILED",DateTimeOffset.UtcNow,[1]);
            return(users,new(db,users,TimeProvider.System),source);
        }

        // Keep real results for the source-read candidate; never acquire a second/late User lock.
        async Task<SupervisorBindingUsers> InitialLock()
        {
            lockedResults=await new UserEligibilityLock(db).LockAsync([actor,other],default);
            return SupervisorBindingUsers.Retain(db,lockedResults,actor,other,actor);
        }
        async Task<(InspectionUsersPrelock Users,InspectionRecipientSourceReadCandidate Policy,Fe05NotificationSource Source)> Rework()
        {
            await InitialLock();await Catalog();await guard();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET status='COMPLETED',result='FAIL',workflow_status='FAILED',inspected_at=SYSUTCDATETIME() WHERE inspection_id={root.InspectionId}; UPDATE dbo.cleaning_tasks SET status='IN_PROGRESS',workflow_status='REWORK_REQUIRED' WHERE cleaning_task_id={root.CleaningTaskId}");
            var ids=await db.Database.SqlQuery<long>($"""
                INSERT dbo.cleaning_reworks(inspection_id,cleaning_task_id,assignee_user_id,reason,status,workflow_status,due_at,requested_at)
                VALUES({root.InspectionId},{root.CleaningTaskId},{other},'Synthetic resource-read candidate','PENDING','PENDING_ACCEPTANCE',DATEADD(hour,2,SYSUTCDATETIME()),SYSUTCDATETIME());
                SELECT CONVERT(bigint,SCOPE_IDENTITY()) AS Value;
                """).ToArrayAsync();
            var reader=new InspectionRecipientRosterReader(db,new MissingInspectionOriginalCleanerSource());
            var roster=await reader.ReadAsync(actor,branch,root.InspectionId,other,Fe05NotificationKind.CleaningReworkRequested,true,default);
            var users=new InspectionUsersPrelock(db,reader,commitGuard);await users.RetainLockedUsersAsync(roster,lockedResults!,default);
            var revision=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.cleaning_reworks WHERE rework_id={ids.Single()}").SingleAsync();
            var when=await db.CleaningReworks.AsNoTracking().Where(w=>w.ReworkId==ids.Single()).Select(w=>w.RequestedAt).SingleAsync();
            var source=new Fe05NotificationSource(actor,branch,root.RoomId,root.InspectionId,roster.TaskId,root.VersionId,ids.Single(),
                '"'+roster.InspectionRevision+'"',roster.TaskId,"COMPLETED","FAIL","FAILED",new DateTimeOffset(DateTime.SpecifyKind(when,DateTimeKind.Utc)),[1],'"'+Convert.ToHexString(revision)+'"');
            return(users,new(db,users,TimeProvider.System),source);
        }
        try
        {
            await Check("Explicit authorized supervisor binding persists exact identities/cycle/actor and touches root audit",async()=>{
                var locks=await Lock();await Catalog();var source=await Assigned();var tag=await Bind(locks,source);expiredBinding=locks;
                var persisted=await db.Database.SqlQuery<long>($"SELECT responsible_supervisor_user_id AS Value FROM dbo.inspection_supervisor_bindings WHERE inspection_id={root.InspectionId} AND bound_by_user_id={actor}").SingleAsync();
                if(persisted!=actor||tag=='"'+Convert.ToHexString(source.RootRevision)+'"'||await db.AuditLogs.CountAsync(a=>a.EntityType=="inspection_supervisor_bindings")!=1)throw new Exception("Binding/audit/revision missing");
            });
            await Check("Explicit selected supervisor differs from actor and inspector without inferred attribution",async()=>{
                await FixtureRole(other,"HK_SUPERVISOR");
                var actual=await new UserEligibilityLock(db).LockAsync([actor,other],default);
                var locks=SupervisorBindingUsers.Retain(db,actual,actor,actor,other);await Catalog();await Assigned();
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET inspector_user_id={actor} WHERE inspection_id={root.InspectionId}");
                var source=await Service().ReadSupervisorAssignmentSourceAsync(branch,root.InspectionId,default);await Bind(locks,source,other);
                if(await db.Database.SqlQuery<long>($"SELECT responsible_supervisor_user_id AS Value FROM dbo.inspection_supervisor_bindings WHERE inspection_id={root.InspectionId}").SingleAsync()!=other)throw new Exception("Explicit choice replaced by actor/inspector");
            });
            await Check("Unauthorized actor cannot set responsibility despite locked source",async()=>{
                var actual=await new UserEligibilityLock(db).LockAsync([actor,other],default);
                var locks=SupervisorBindingUsers.Retain(db,actual,other,other,actor);
                await Catalog();var source=await Assigned();await Deny(()=>Bind(locks,source,user:other),"AUTHORIZATION");
            });
            await Check("Explicit supervisor requires existing branch qualification; inspector is not inferred",async()=>{
                var locks=await Lock(other);await Catalog();var source=await Assigned();await Deny(()=>Bind(locks,source,other),"NOT_ELIGIBLE");
            });
            await Check("Stale Inspection revision rejects setter",async()=>{
                var locks=await Lock();await Catalog();var source=await Assigned();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET summary_note=summary_note WHERE inspection_id={root.InspectionId}");await Deny(()=>Bind(locks,source),"STALE");
            });
            await Check("Stale current-cycle revision rejects setter",async()=>{
                var locks=await Lock();await Catalog();var source=await Assigned();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_cleaning_cycle_state SET cleaning_task_id=cleaning_task_id WHERE room_id={root.RoomId}");await Deny(()=>Bind(locks,source),"STALE");
            });
            await Check("Wrong branch/current task cannot resolve assignment source",async()=>{
                await Lock();await Catalog();await Assigned();await Deny(()=>Service().ReadSupervisorAssignmentSourceAsync(foreign,root.InspectionId,default),"SOURCE_REQUIRED");
            });
            await Check("Existing responsibility cannot be silently replaced or backfilled",async()=>{
                var locks=await Lock();await Catalog();var source=await Assigned();await Bind(locks,source);var fresh=await Service().ReadSupervisorAssignmentSourceAsync(branch,root.InspectionId,default);await Deny(()=>Bind(locks,fresh),"REASSIGNMENT");
            });
            await Check("Recipient outside retained Users roster cannot be introduced",async()=>{
                var locks=await Lock();await Catalog();var source=await Assigned();await Deny(()=>Bind(locks,source,long.MaxValue),"LATE_USER");
            });
            await Check("Revoked actor permission cannot borrow supervisor target eligibility",async()=>{
                var locks=await Lock();await Catalog();var source=await Assigned();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={actor}");await Deny(()=>Bind(locks,source),"AUTHORIZATION");
            });
            await Check("Revoked supervisor branch access blocks setter",async()=>{
                await FixtureRole(other,"HK_SUPERVISOR");var locks=await Lock(other);await Catalog();var source=await Assigned();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={other}");await Deny(()=>Bind(locks,source,other),"NOT_ELIGIBLE");
            });
            await Check("Supervisor exact-resource candidate matches existing grant but never authorizes production or bytes",async()=>{
                var x=await Failed();var r=await x.Policy.AssessAsync(x.Source,actor,InspectionRecipientRelationship.ResponsibleSupervisor,default);
                if(!r.ExistingGrantAndRelationshipMatch||r.CanRead||r.CanReadPrivateEvidence||r.AcceptedConcreteBinding!=null)throw new Exception("Candidate boundary violated");
            });
            await Check("Original cleaner relationship alone cannot borrow actor grant",async()=>{
                var x=await Failed();var r=await x.Policy.AssessAsync(x.Source,other,InspectionRecipientRelationship.OriginalCleaner,default);if(r.ExistingGrantAndRelationshipMatch)throw new Exception("Borrowed ambient permission");
            });
            await Check("Legacy ID-only cleaner fixture cannot satisfy R05 completion binding despite existing grant",async()=>{
                await FixtureRole(other,"HK_STAFF");var x=await Failed();var r=await x.Policy.AssessAsync(x.Source,other,InspectionRecipientRelationship.OriginalCleaner,default);
                if(r.ExistingGrantAndRelationshipMatch||r.CanRead||r.CanReadPrivateEvidence)throw new Exception("ID-only fixture became R05 authority");
            });
            await Check("Wrong recipient relationship is denied even if User is in retained roster",async()=>{
                var x=await Failed();if((await x.Policy.AssessAsync(x.Source,other,InspectionRecipientRelationship.ResponsibleSupervisor,default)).ExistingGrantAndRelationshipMatch)throw new Exception("Wrong role relationship");
            });
            await Check("Wrong source branch fails retained source checks",async()=>{
                var x=await Failed();await Deny(()=>x.Policy.AssessAsync(x.Source with{BranchId=foreign},actor,InspectionRecipientRelationship.ResponsibleSupervisor,default),"WRONG_SOURCE");
            });
            await Check("Stale event revision cannot become recipient read authority",async()=>{
                var x=await Failed();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET summary_note=summary_note WHERE inspection_id={root.InspectionId}");if((await x.Policy.AssessAsync(x.Source,actor,InspectionRecipientRelationship.ResponsibleSupervisor,default)).ExistingGrantAndRelationshipMatch)throw new Exception("Stale source allowed");
            });
            await Check("Recipient branch revocation denies source candidate at staging",async()=>{
                var x=await Failed();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor}");if((await x.Policy.AssessAsync(x.Source,actor,InspectionRecipientRelationship.ResponsibleSupervisor,default)).ExistingGrantAndRelationshipMatch)throw new Exception("Revoked branch allowed");
            });
            await Check("Recipient grant revocation denies exact-source candidate",async()=>{
                var x=await Failed();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={actor}");if((await x.Policy.AssessAsync(x.Source,actor,InspectionRecipientRelationship.ResponsibleSupervisor,default)).ExistingGrantAndRelationshipMatch)throw new Exception("Revoked grant allowed");
            });
            await Check("Responsibility change invalidates retained binding and poisons commit guard",async()=>{
                var x=await Failed();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE b SET responsible_supervisor_user_id={other},inspection_revision_at_binding=i.row_version FROM dbo.inspection_supervisor_bindings b JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id WHERE b.inspection_id={root.InspectionId}");await Deny(()=>x.Policy.AssessAsync(x.Source,actor,InspectionRecipientRelationship.ResponsibleSupervisor,default),"ROSTER_CHANGED");
                try{await db.Database.CurrentTransaction!.CommitAsync();throw new Exception("Poisoned transaction committed");}catch(InvalidOperationException e)when(e.Message.Contains("roll back")){}
            });
            await Check("Cycle change rejects retained recipient without taking late Users locks",async()=>{
                var x=await Failed();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_cleaning_cycle_state SET cleaning_task_id=cleaning_task_id WHERE room_id={root.RoomId}");await Deny(()=>x.Policy.AssessAsync(x.Source,actor,InspectionRecipientRelationship.ResponsibleSupervisor,default));
            });
            await Check("Owner source coordination remains closed even for candidate match",async()=>{
                var x=await Failed();var identity=new Identity(new(actor,logins[actor].Session.SessionId,false));
                var coordinator=new InspectionOwnerSourceCoordination(db,identity,new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System),
                    new PermissionService(identity,new EfPermissionDataSource(db),new UnresolvedPermissionResourceResolver(),TimeProvider.System),new InspectionOwnerOperation(),x.Users);
                await Deny(()=>coordinator.CanRecipientReadAsync(db,x.Source,actor,default),"READ_CONTRACT_REQUIRED");
            });
            await Check("Retained binding capability cannot cross transactions",async()=>{
                try{expiredBinding!.Require(db,actor,other,actor);throw new Exception("Cross-transaction capability accepted");}
                catch(InvalidOperationException e)when(e.Message=="FE05_EXACT_CALLER_SERIALIZABLE_TRANSACTION_REQUIRED") {}
            });
            await Check("Rework assignee candidate matches exact persisted resource and own existing CLEANING_VIEW",async()=>{
                await FixtureRole(other,"HK_STAFF");var x=await Rework();var r=await x.Policy.AssessAsync(x.Source,other,InspectionRecipientRelationship.ReworkAssignee,default);
                if(!r.ExistingGrantAndRelationshipMatch||r.CanRead||r.CanReadPrivateEvidence||r.AcceptedConcreteBinding!=null)throw new Exception("Rework candidate authority escaped");
            });
            await Check("Rework recipient without own grant cannot borrow actor permissions",async()=>{
                var x=await Rework();if((await x.Policy.AssessAsync(x.Source,other,InspectionRecipientRelationship.ReworkAssignee,default)).ExistingGrantAndRelationshipMatch)throw new Exception("Rework borrowed acting inspector grant");
            });
            await Check("Rework stale revision is rejected under retained Users",async()=>{
                await FixtureRole(other,"HK_STAFF");var x=await Rework();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_reworks SET reason=reason WHERE rework_id={x.Source.ReworkId}");
                await Deny(()=>x.Policy.AssessAsync(x.Source,other,InspectionRecipientRelationship.ReworkAssignee,default));
            });
            await Check("Rework reassignment cannot acquire late recipient or reuse old roster",async()=>{
                var x=await Rework();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_reworks SET assignee_user_id={actor} WHERE rework_id={x.Source.ReworkId}");
                await Deny(()=>x.Policy.AssessAsync(x.Source,other,InspectionRecipientRelationship.ReworkAssignee,default));
            });
            await Check("Concurrent responsibility writer waits at sorted Users admission; no partial overwrite",async()=>{
                var locks=await Lock();await Catalog();var source=await Assigned();await Bind(locks,source);
                await using var second=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()!).Options);
                await second.Database.OpenConnectionAsync();
                using var e=JsonDocument.Parse(await File.ReadAllTextAsync(environment));
                var identity=await second.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),@@SERVERNAME)+'|'+DB_NAME()+'|'+(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE05INT001.Owner') AS Value").SingleAsync();
                if(identity!=e.RootElement.GetProperty("server").GetString()+"|"+e.RootElement.GetProperty("database").GetString()+"|"+e.RootElement.GetProperty("owner").GetString())throw new Exception("Contender isolation rejected");
                await using var contender=await second.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                await second.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT 250");
                try{await new UserEligibilityLock(second).LockAsync([actor,other],default);throw new Exception("Concurrent writer bypassed retained Users");}
                catch(Microsoft.Data.SqlClient.SqlException e2)when(e2.Number==1222){}
                finally{if(contender.GetDbTransaction().Connection!=null)await contender.RollbackAsync();}
            });
            await File.WriteAllTextAsync(Path.Combine(output,"responsibility-sql.json"),JsonSerializer.Serialize(new{success=true,callerTransaction=true,realActorSessionAndPermission=true,originalCleanerTestFixtureOnly=true,
                acceptedConcreteBinding=(string?)null,publicMutation=false,allBusinessChangesRolledBack=true,cases=results},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception e){await File.WriteAllTextAsync(Path.Combine(output,"responsibility-sql.json"),JsonSerializer.Serialize(new{success=false,cases=results,error=e.GetType().Name+": "+e.Message},new JsonSerializerOptions{WriteIndented=true}));throw;}
    }
    private sealed class Identity(CurrentUserInfo user):ICurrentUserAccessor{public bool TryGetCurrentUser(out CurrentUserInfo value){value=user;return true;}}
    private sealed class CleanerFixture(long user):IInspectionOriginalCleanerSource
    {public Task<long> ReadAsync(long inspection,long task,long room,long branch,string cycle,CancellationToken ct)=>Task.FromResult(user);}
}
