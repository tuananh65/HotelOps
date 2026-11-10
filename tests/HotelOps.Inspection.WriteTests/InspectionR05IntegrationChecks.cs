using System.Data;
using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

internal static class InspectionR05IntegrationChecks
{
    internal static void Unit()
    {
        var count=0;void Check(string n,bool v){if(!v)throw new Exception(n);Console.WriteLine("PASS "+n);count++;}
        var revision=new byte[]{0,0,0,0,0,0,0,1};var time=DateTime.SpecifyKind(new DateTime(2026,11,10),DateTimeKind.Utc);
        var binding=InspectionCleanerBinding.From(new(4,5,2,11,9,12,time,revision,20));revision[7]=2;
        Check("Completion revision copied immutably from FE04 bytes",binding.CompletionRevision=="0000000000000001");
        var r=new InspectionRecipientRoster(1,2,3,4,5,6,null,8,9,"0000000000000030","0000000000000040","0000000000000050",Fe05NotificationKind.InspectionFailed,binding);
        foreach(var b in new[]{binding with{AssignmentId=21},binding with{CleanerUserId=21},binding with{CompletionProgressId=21},binding with{CompletedAtUtc=time.AddSeconds(1)},
            binding with{CompletionRevision="0000000000000020"},binding with{WriterTransactionId=21},binding with{TaskId=21},binding with{RoomId=21},binding with{BranchId=21}})
            Check("Retained attribution invalidates changed "+b,!r.SameParticipants(r with{CleanerBinding=b}));
        Check("Own Inspection revision can change without conflating completion token",r.SameParticipants(r with{InspectionRevision="0000000000000090"}));
        Check("Completion revision is distinct from current cycle revision",binding.CompletionRevision!=r.CycleRevision);
        Check("Missing typed source is unequal to a retained R05 source",!r.SameParticipants(r with{CleanerBinding=null}));
        Check("Public Reject and Rework gates remain closed",RoomTypeCreationService.RejectWriteDependencies.Length>0&&RoomTypeCreationService.ReworkWriteDependencies.Length>0);
        Console.WriteLine(count+"/"+count+" PASS");
    }
    internal static async Task SqlAsync(HotelOpsDbContext db,string environment,string output,Func<Task> guard,NotificationCommitGuard commitGuard)
    {
        var results=new List<object>();await guard();
        var root=await db.RoomInspections.AsNoTracking().OrderBy(i=>i.InspectionId).FirstAsync();
        var second=await db.RoomInspections.AsNoTracking().OrderBy(i=>i.InspectionId).Skip(1).FirstAsync();
        var actor=root.InspectorUserId;var other=await db.Users.Where(u=>u.Username=="int.denied").Select(u=>u.UserId).SingleAsync();
        var branch=await db.Rooms.Where(r=>r.RoomId==root.RoomId).Select(r=>r.BranchId).SingleAsync();
        var foreign=await db.Branches.Where(b=>b.BranchCode=="BR002").Select(b=>b.BranchId).SingleAsync();
        using var identityReceipt=JsonDocument.Parse(await File.ReadAllTextAsync(environment));
        var name=identityReceipt.RootElement.GetProperty("database").GetString()!;
        // New FAST010 owned database only. Do not repair/retarget FE04 or earlier FE05 trials.
        if(!name.StartsWith("HotelOps_Canonical_20261110_"))throw new Exception("Fresh FAST010 target required");
        await guard();
        await db.Database.ExecuteSqlRawAsync("DECLARE @sql nvarchar(max)=N'ALTER AUTHORIZATION ON DATABASE::'+QUOTENAME(DB_NAME())+N' TO [sa]'; EXEC(@sql);");
        await guard();
        await db.Database.ExecuteSqlRawAsync("IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE04.UnblockR01.Owner') EXEC sys.sp_addextendedproperty @name=N'HotelOps.FE04.UnblockR01.Owner',@value=N'DEV1_DISPOSABLE';");
        var scriptRoot=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
        var install=await File.ReadAllTextAsync(Path.Combine(scriptRoot,"scripts/database/release/fe04-cleaner-r04/01_CleanerAttributionCandidate.sql"));
        await guard();if(await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.fe04_cleaner_attributions','U') IS NULL THEN 0 ELSE 1 END AS Value").SingleAsync()==0)
            await db.Database.ExecuteSqlRawAsync(install);
        var definition=await db.Database.SqlQuery<string>($"SELECT definition AS Value FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.fe04_capture_cleaner_attribution')").SingleAsync();
        var expected=System.Text.RegularExpressions.Regex.Match(install,@"EXEC\(N'(CREATE PROCEDURE[\s\S]*?)'\);").Groups[1].Value.Replace("''","'");
        if(definition.Replace("\r\n","\n")!=expected.Replace("\r\n","\n"))throw new Exception("Installed R05 procedure mismatch");
        await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            if(await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.inspection_supervisor_bindings','U') IS NULL THEN 0 ELSE 1 END AS Value").SingleAsync()==0)
            {
                var supervisor=await File.ReadAllTextAsync(Path.Combine(scriptRoot,"scripts/database/fe05-fast008/01_SupervisorBindingCandidate.sql"));
                foreach(var batch in System.Text.RegularExpressions.Regex.Split(supervisor,@"(?im)^\s*GO\s*\r?$"))if(!string.IsNullOrWhiteSpace(batch)){await guard();await db.Database.ExecuteSqlRawAsync(batch);}
            }
            await tx.CommitAsync();
        }
        async Task Exec(FormattableString sql){await guard();await db.Database.ExecuteSqlInterpolatedAsync(sql);}
        using var secret=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(environment))!,"runtime.private.json")));
        var passwords=new PasswordCredentialService(Options.Create(new PasswordHasherOptions{CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount}));
        var logins=new Dictionary<long,CreatedSqlSession>();
        await using(var setup=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            await guard();foreach(var user in new[]{actor,other})
            {
                var verified=await new CredentialLockoutService(db,passwords,TimeProvider.System).VerifyAsync(user,secret.RootElement.GetProperty("password").GetString()!,
                    (u,_)=>Task.FromResult(u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"));
                logins[user]=await new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System).CreateSessionAsync(verified);
            }
            // Synthetic eligibility data only: existing role/catalog, no new production permission.
            if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.shift_templates WHERE shift_code='FAST010'").SingleAsync()==0)
            await Exec($"""
                UPDATE dbo.users SET department_id=(SELECT department_id FROM dbo.departments WHERE branch_id={branch} AND department_code='HK') WHERE user_id={other};
                INSERT dbo.user_branch_access(user_id,branch_id,access_level,granted_at) VALUES({other},{branch},'STANDARD',DATEADD(hour,-1,SYSUTCDATETIME()));
                INSERT dbo.user_roles(user_id,role_id,branch_id,assigned_at,is_active)
                SELECT {other},role_id,{branch},DATEADD(hour,-1,SYSUTCDATETIME()),1 FROM dbo.roles WHERE role_code='HK_STAFF';
                DECLARE @now datetime2(7)=SYSUTCDATETIME(),@template bigint,@shift bigint;
                INSERT dbo.shift_templates(branch_id,shift_code,shift_name,start_time,end_time,break_minutes,crosses_midnight,is_active)
                VALUES({branch},'FAST010','Synthetic FAST010','00:00','23:59',0,0,1); SET @template=SCOPE_IDENTITY();
                INSERT dbo.work_shifts(branch_id,department_id,shift_template_id,shift_date,scheduled_start_at,scheduled_end_at,required_staff_count,status,created_by,created_at)
                SELECT {branch},department_id,@template,CONVERT(date,@now),DATEADD(hour,-1,@now),DATEADD(hour,8,@now),1,'PLANNED',{actor},@now
                FROM dbo.departments WHERE branch_id={branch} AND department_code='HK'; SET @shift=SCOPE_IDENTITY();
                INSERT dbo.shift_assignments(shift_id,user_id,assignment_status,assigned_by,assigned_at) VALUES(@shift,{other},'ASSIGNED',{actor},DATEADD(hour,-1,@now));
                """);
            await setup.CommitAsync();db.ChangeTracker.Clear();
        }
        Ctx Context(long who=0)
        {
            if(who==0)who=actor;var current=new Actor(new(who,logins[who].Session.SessionId,false));
            var permissions=new PermissionService(current,new EfPermissionDataSource(db),new UnresolvedPermissionResourceResolver(),TimeProvider.System);
            RoomTypeCreationService? service=null;
            var source=new InspectionR05CleanerSource(db,current,permissions,()=>service!);if(db.Database.CurrentTransaction==null)source.BindCaller(logins[who].Ticket,logins[who].Session.SessionId);
            var reader=new InspectionRecipientRosterReader(db,source);var users=new InspectionUsersPrelock(db,reader,commitGuard);
            service=new(db,current,permissions,new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System),TimeProvider.System,inspectionCommitGuard:commitGuard,inspectionUsers:users,inspectionRoster:reader);
            return new(service,source,reader,users,current,permissions);
        }
        async Task Catalog()=>_ = await db.Database.SqlQuery<int>($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction'; SELECT @r AS Value").ToArrayAsync();
        async Task<long> Capture(long task,long cleaner,bool ambiguous=false)
        {
            await Exec($"""
                UPDATE dbo.cleaning_tasks SET workflow_status='PENDING_INSPECTION',actual_start_at=DATEADD(minute,-20,(SELECT created_at FROM dbo.room_inspections WHERE cleaning_task_id={task})),actual_end_at=DATEADD(second,-1,(SELECT created_at FROM dbo.room_inspections WHERE cleaning_task_id={task})) WHERE cleaning_task_id={task};
                INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status)
                SELECT {task},{cleaner},{actor},DATEADD(minute,-30,actual_end_at),'ACTIVE' FROM dbo.cleaning_tasks WHERE cleaning_task_id={task};
                INSERT dbo.cleaning_progress_logs(cleaning_task_id,progress_percent,status,logged_by,logged_at)
                SELECT {task},100,'COMPLETED',{cleaner},actual_end_at FROM dbo.cleaning_tasks WHERE cleaning_task_id={task};
                """);
            var assignment=await db.CleaningAssignments.Where(a=>a.CleaningTaskId==task&&a.AssignmentStatus=="ACTIVE").Select(a=>a.AssignmentId).SingleAsync();
            var progress=await db.CleaningProgressLogs.Where(p=>p.CleaningTaskId==task).Select(p=>p.ProgressId).SingleAsync();
            if(ambiguous)await Exec($"INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,ended_at,assignment_status) VALUES({task},{actor},{actor},DATEADD(hour,-2,SYSUTCDATETIME()),DATEADD(hour,-1,SYSUTCDATETIME()),'REASSIGNED')");
            var method=typeof(RoomTypeCreationService).GetMethod("CaptureCleanerAttribution",BindingFlags.NonPublic|BindingFlags.Instance)!;
            try{await (Task)method.Invoke(Context().Service,[task,assignment,cleaner,progress,CancellationToken.None])!;}
            catch(TargetInvocationException e)when(e.InnerException!=null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();}
            return progress;
        }
        // Actual R05 capture participant, synthetic fixture; NOT full UC034 service command.
        await using(var completion=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            await new UserEligibilityLock(db).LockAsync([actor,other],default);await Catalog();
            if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.fe04_cleaner_attributions WHERE cleaning_task_id={root.CleaningTaskId}").SingleAsync()==0)
                await Capture(root.CleaningTaskId!.Value,other);
            await completion.CommitAsync();db.ChangeTracker.Clear();
        }
        // Exercise FAST009 trusted setter with real session/permission on explicit synthetic assignment.
        await using(var binding=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var locked=await new UserEligibilityLock(db).LockAsync([actor],default);await Catalog();
            foreach(var inspection in new[]{root,second})
            {
                if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.inspection_supervisor_bindings WHERE inspection_id={inspection.InspectionId}").SingleAsync()!=0)continue;
                await Exec($"UPDATE dbo.room_inspections SET status='PENDING',workflow_status='ASSIGNED' WHERE inspection_id={inspection.InspectionId}");
                var scope=SupervisorBindingUsers.Retain(db,locked,actor,actor,actor);var service=Context().Service;
                var source=await service.ReadSupervisorAssignmentSourceAsync(branch,inspection.InspectionId,default);
                await service.BindSupervisorInAssignmentAsync(logins[actor].Ticket,logins[actor].Session.SessionId,branch,inspection.InspectionId,actor,source.RootRevision,source.CycleRevision,"Explicit FAST010 synthetic supervisor",scope,default);
                await Exec($"UPDATE dbo.room_inspections SET status='IN_PROGRESS',workflow_status='PENDING_CONCLUSION' WHERE inspection_id={inspection.InspectionId}");
            }
            await binding.CommitAsync();db.ChangeTracker.Clear();
        }
        var completionRevision=await db.Database.SqlQuery<byte[]>($"SELECT completion_revision AS Value FROM dbo.fe04_cleaner_attributions WHERE cleaning_task_id={root.CleaningTaskId}").SingleAsync();
        var writer=await db.Database.SqlQuery<long>($"SELECT writer_transaction_id AS Value FROM dbo.fe04_cleaner_attributions WHERE cleaning_task_id={root.CleaningTaskId}").SingleAsync();
        async Task Deny(Func<Task> run)
        {try{await run();}catch(Fe05NotificationUnavailableException){return;}throw new Exception("Expected fail-closed rejection");}
        async Task StateUnchanged(string before,long audits)
        {if(await State()!=before||await db.AuditLogs.LongCountAsync()!=audits)throw new Exception("Rollback state escaped");}
        async Task<string> State()=>await db.Database.SqlQuery<string>($"SELECT (SELECT (SELECT inspection_id,row_version FROM dbo.room_inspections ORDER BY inspection_id FOR JSON PATH) AS roots,(SELECT * FROM dbo.fe04_cleaner_attributions ORDER BY cleaning_task_id FOR JSON PATH) AS attribution,(SELECT * FROM dbo.inspection_supervisor_bindings ORDER BY inspection_id FOR JSON PATH) AS supervisors,(SELECT cleaning_task_id,actual_end_at,row_version FROM dbo.cleaning_tasks ORDER BY cleaning_task_id FOR JSON PATH) AS tasks,(SELECT room_id,cleaning_task_id,row_version FROM dbo.room_cleaning_cycle_state ORDER BY room_id FOR JSON PATH) AS cycles,(SELECT assignment_id,cleaning_task_id,assignee_user_id,assignment_status,ended_at FROM dbo.cleaning_assignments ORDER BY assignment_id FOR JSON PATH) AS assignments,(SELECT progress_id,cleaning_task_id,logged_by,logged_at FROM dbo.cleaning_progress_logs ORDER BY progress_id FOR JSON PATH) AS progress,(SELECT user_id,account_status,employment_status FROM dbo.users ORDER BY user_id FOR JSON PATH) AS users,(SELECT user_role_id,user_id,role_id,is_active FROM dbo.user_roles ORDER BY user_role_id FOR JSON PATH) AS roles,(SELECT access_id,user_id,revoked_at FROM dbo.user_branch_access ORDER BY access_id FOR JSON PATH) AS access,(SELECT role_id,permission_id,data_scope FROM dbo.role_permissions ORDER BY role_id,permission_id FOR JSON PATH) AS grants FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS Value").SingleAsync();
        async Task Check(string title,Func<Ctx,InspectionRecipientRoster,Task> action)
        {
            await guard();var context=Context();var hint=await context.Reader.DiscoverAsync(actor,branch,root.InspectionId,null,Fe05NotificationKind.InspectionFailed,default);
            var before=await State();var audits=await db.AuditLogs.LongCountAsync();
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try{
                var actual=await new UserEligibilityLock(db).LockAsync(hint.Users,default);
                await context.Users.RetainLockedUsersAsync(hint,actual,default);await Catalog();
                await action(context,hint);
                if(!ReferenceEquals(tx.GetDbTransaction(),db.Database.CurrentTransaction!.GetDbTransaction()))throw new Exception("Caller transaction changed");
            }
            finally{if(tx.GetDbTransaction().Connection!=null)await tx.RollbackAsync();db.ChangeTracker.Clear();}
            await StateUnchanged(before,audits);results.Add(new{title,result="PASS",rollback=true});Console.WriteLine("PASS "+title);
        }
        async Task<Fe05NotificationSource> Failed(Ctx c)
        {
            await Exec($"UPDATE dbo.room_inspections SET status='COMPLETED',result='FAIL',workflow_status='FAILED',inspected_at=SYSUTCDATETIME() WHERE inspection_id={root.InspectionId}; UPDATE dbo.cleaning_tasks SET status='IN_PROGRESS',workflow_status='REWORK_REQUIRED' WHERE cleaning_task_id={root.CleaningTaskId}");
            var r=await c.Reader.ReadAsync(actor,branch,root.InspectionId,null,Fe05NotificationKind.InspectionFailed,true,default);
            return new(actor,branch,root.RoomId,root.InspectionId,r.TaskId,root.VersionId,null,'"'+r.InspectionRevision+'"',r.TaskId,"COMPLETED","FAIL","FAILED",DateTimeOffset.UtcNow,[1]);
        }
        try
        {
            await Check("R05 attribution read authorized by FE05 in exact caller connection/Serializable transaction",async(c,h)=>{
                await c.Users.ConfirmAfterSourcesAsync(false,default);var a=await c.Service.ReadAuthorizedInspectionCleanerAsync(logins[actor].Ticket,logins[actor].Session.SessionId,branch,root.InspectionId,default);
                if(a==null||a.CleanerUserId!=other||a.AssignmentId!=h.CleanerBinding!.AssignmentId||a.CompletionProgressId!=h.CleanerBinding.CompletionProgressId||!a.CompletionRevision.SequenceEqual(completionRevision)||a.WriterTransactionId!=writer
                    ||await db.Database.SqlQuery<long>($"SELECT CURRENT_TRANSACTION_ID() AS Value").SingleAsync()==writer)throw new Exception("Exact prior-transaction completion identity missing");
            });
            await Check("CompletionRevision survives later current task rowversion changes",async(c,h)=>{
                await Exec($"UPDATE dbo.cleaning_tasks SET completion_note='rowversion advanced' WHERE cleaning_task_id={root.CleaningTaskId}");
                await c.Users.ConfirmAfterSourcesAsync(false,default);var a=await c.Service.ReadAuthorizedInspectionCleanerAsync(logins[actor].Ticket,logins[actor].Session.SessionId,branch,root.InspectionId,default);
                var live=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.cleaning_tasks WHERE cleaning_task_id={root.CleaningTaskId}").SingleAsync();
                if(a==null||!a.CompletionRevision.SequenceEqual(completionRevision)||live.SequenceEqual(a.CompletionRevision))throw new Exception("Completion revision confused with live revision");
            });
            await Check("Changed completion source fails R05 read before domain mutation",async(c,h)=>{
                await Exec($"UPDATE dbo.cleaning_tasks SET actual_end_at=DATEADD(second,1,actual_end_at) WHERE cleaning_task_id={root.CleaningTaskId}");await Deny(()=>c.Users.ConfirmAfterSourcesAsync(false,default));
            });
            await Check("Wrong cycle fails retained source without late Users locks",async(c,h)=>{
                await Exec($"DELETE dbo.room_cleaning_cycle_state WHERE room_id={root.RoomId}");await Deny(()=>c.Users.ConfirmAfterSourcesAsync(false,default));
            });
            await Check("Wrong branch fails exact retained read before FE04 reader",async(c,h)=>{
                await Deny(()=>c.Service.ReadAuthorizedInspectionCleanerAsync(logins[actor].Ticket,logins[actor].Session.SessionId,foreign,root.InspectionId,default));
            });
            await Check("Wrong Inspection cannot reuse retained actor membership",async(c,h)=>{
                await Deny(()=>c.Service.ReadAuthorizedInspectionCleanerAsync(logins[actor].Ticket,logins[actor].Session.SessionId,branch,second.InspectionId,default));
            });
            await Check("Revoked actor SQL session is rejected before attribution read",async(c,h)=>{
                await new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System).RevokeSessionAsync(logins[actor].Ticket);
                await Deny(()=>c.Users.ConfirmAfterSourcesAsync(false,default));
            });
            await Check("Revoked actor grant is rejected before FE04 reader",async(c,h)=>{
                await Exec($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={actor}");await Deny(()=>c.Users.ConfirmAfterSourcesAsync(false,default));
            });
            await Check("Revoked cleaner account makes persisted attribution ineligible",async(c,h)=>{
                await Exec($"UPDATE dbo.users SET account_status='INACTIVE' WHERE user_id={other}");await Deny(()=>c.Users.ConfirmAfterSourcesAsync(false,default));
            });
            await Check("Revoked cleaner branch access fails closed",async(c,h)=>{
                await Exec($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={other}");await Deny(()=>c.Users.ConfirmAfterSourcesAsync(false,default));
            });
            await Check("Exact supervisor source candidate matches existing grant without granting read/evidence",async(c,h)=>{
                var source=await Failed(c);var r=await new InspectionRecipientSourceReadCandidate(db,c.Users,TimeProvider.System).AssessAsync(source,actor,InspectionRecipientRelationship.ResponsibleSupervisor,default);
                if(!r.ExistingGrantAndRelationshipMatch||r.CanRead||r.CanReadPrivateEvidence)throw new Exception("Supervisor candidate boundary");
            });
            await Check("Original cleaner candidate matches R05 identity and its own CLEANING_VIEW only",async(c,h)=>{
                var source=await Failed(c);var r=await new InspectionRecipientSourceReadCandidate(db,c.Users,TimeProvider.System).AssessAsync(source,other,InspectionRecipientRelationship.OriginalCleaner,default);
                if(!r.ExistingGrantAndRelationshipMatch||r.AcceptedConcreteBinding!=null||r.CanRead||r.CanReadPrivateEvidence)throw new Exception("Cleaner candidate boundary");
            });
            await Check("Cleaner source relation cannot substitute for revoked CLEANING_VIEW grant",async(c,h)=>{
                var source=await Failed(c);await Exec($"DELETE rp FROM dbo.role_permissions rp JOIN dbo.permissions p ON p.permission_id=rp.permission_id JOIN dbo.roles role ON role.role_id=rp.role_id WHERE role.role_code='HK_STAFF' AND p.permission_code='CLEANING_VIEW'");
                if((await new InspectionRecipientSourceReadCandidate(db,c.Users,TimeProvider.System).AssessAsync(source,other,InspectionRecipientRelationship.OriginalCleaner,default)).ExistingGrantAndRelationshipMatch)throw new Exception("Missing grant allowed");
            });
            await Check("Responsible supervisor binding revision change invalidates entire retained roster",async(c,h)=>{
                await Exec($"UPDATE b SET reason='changed',inspection_revision_at_binding=i.row_version FROM dbo.inspection_supervisor_bindings b JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id WHERE b.inspection_id={root.InspectionId}");await Deny(()=>c.Users.ConfirmAfterSourcesAsync(false,default));
            });
            await Check("Post-completion reassignment preserves original assignment identity",async(c,h)=>{
                await Exec($"UPDATE dbo.cleaning_assignments SET ended_at=SYSUTCDATETIME(),assignment_status='REASSIGNED' WHERE assignment_id={h.CleanerBinding!.AssignmentId}; INSERT dbo.cleaning_assignments(cleaning_task_id,assignee_user_id,assigned_by,assigned_at,assignment_status) VALUES({root.CleaningTaskId},{actor},{actor},SYSUTCDATETIME(),'ACTIVE')");
                await c.Users.ConfirmAfterSourcesAsync(false,default);var r=await c.Reader.ReadAsync(actor,branch,root.InspectionId,null,Fe05NotificationKind.InspectionFailed,true,default);
                if(r.CleanerBinding!=h.CleanerBinding||r.OriginalCleanerId!=other)throw new Exception("Latest-assignment fallback used");
            });
            await Check("Mandatory recipients use BOTH supervisor and typed original-cleaner binding",async(c,h)=>{
                var source=await Failed(c);var resolver=new InspectionMandatoryRecipients(c.Users,new Fe05Decision08MandatoryRecipients(c.Users));
                if(!(await resolver.ResolveMandatoryIdsAsync(db,source,Fe05NotificationKind.InspectionFailed,default)).SequenceEqual(new[]{actor,other}.Order()))throw new Exception("Mandatory role missing");
            });
            await Check("Concrete owner coordination still rejects unapproved source-read binding",async(c,h)=>{
                var source=await Failed(c);var coordination=new InspectionOwnerSourceCoordination(db,c.Current,new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System),c.Permissions,new InspectionOwnerOperation(),c.Users);
                await Deny(()=>coordination.CanRecipientReadAsync(db,source,other,default));
            });
            await Check("Failed mandatory read poisons guarded commit; caller rollback restores state",async(c,h)=>{
                await Exec($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={other}");await Deny(()=>c.Users.ConfirmAfterSourcesAsync(false,default));
                try{await db.Database.CurrentTransaction!.CommitAsync();throw new Exception("Poisoned read committed");}catch(InvalidOperationException e)when(e.Message.Contains("roll back")){}
            });
            await Check("FE05 owner reservation preconditions validate real R05 binding before domain mutation",async(c,h)=>{
                var tag='"'+h.InspectionRevision+'"';var identity=InspectionIssuerOperation.Reject(Guid.NewGuid(),actor,branch,root.InspectionId,tag,new("Synthetic reservation preconditions",true,tag));
                var request=new Fe05OperationRequest(identity.Id,branch,root.InspectionId,Convert.ToHexString(identity.Fingerprint),[Fe05NotificationKind.InspectionFailed]);
                var owner=new InspectionOwnerOperation();owner.Bind(db,logins[actor].Ticket,logins[actor].Session.SessionId,request);
                var coordinator=new InspectionOwnerSourceCoordination(db,c.Current,new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System),c.Permissions,owner,c.Users);
                await coordinator.RequireReservationAsync(db,request,new(actor,logins[actor].Session.SessionId,false),default);
            });
            await Check("R05 reader refuses a different physical connection",async(c,h)=>{
                using var otherConnection=new Microsoft.Data.SqlClient.SqlConnection();
                try{await c.Service.ReadInspectionCleanerAttributionAsync(otherConnection,db.Database.CurrentTransaction!.GetDbTransaction(),root.InspectionId,branch,default);throw new Exception("Wrong connection accepted");}
                catch(InvalidOperationException e)when(e.Message=="REWORK_AUTHORITY_CALLER_SERIALIZABLE_TRANSACTION_REQUIRED"){}
            });
            await Check("Concurrent cleaner revocation waits at retained Users prelock and rolls back",async(c,h)=>{
                await c.Users.ConfirmAfterSourcesAsync(false,default);
                await using var contender=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(db.Database.GetConnectionString()!).Options);
                await contender.Database.OpenConnectionAsync();
                var identity=await contender.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),@@SERVERNAME)+'|'+DB_NAME()+'|'+(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE05INT001.Owner') AS Value").SingleAsync();
                if(identity!=identityReceipt.RootElement.GetProperty("server").GetString()+"|"+name+"|"+identityReceipt.RootElement.GetProperty("owner").GetString())throw new Exception("Contender isolation rejected");
                await using var tx=await contender.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                try{await contender.Database.ExecuteSqlInterpolatedAsync($"SET LOCK_TIMEOUT 250; UPDATE dbo.users SET account_status='INACTIVE' WHERE user_id={other}");throw new Exception("Concurrent revocation bypassed Users prelock");}
                catch(Microsoft.Data.SqlClient.SqlException e)when(e.Number==1222){}
                finally{if(tx.GetDbTransaction().Connection!=null)await tx.RollbackAsync();}
            });
            // Advisory token tampering/staleness: identities come from SQL but are not authority until reread.
            foreach(var change in new[]{"CompletionRevision","AssignmentId","CompletionProgressId","WriterTransactionId"})
            {
                var c=Context();var hint=await c.Reader.DiscoverAsync(actor,branch,root.InspectionId,null,Fe05NotificationKind.InspectionFailed,default);var b=hint.CleanerBinding!;
                var stale=b with{CompletionRevision=change=="CompletionRevision"?"0000000000000000":b.CompletionRevision,AssignmentId=change=="AssignmentId"?long.MaxValue:b.AssignmentId,
                    CompletionProgressId=change=="CompletionProgressId"?long.MaxValue:b.CompletionProgressId,WriterTransactionId=change=="WriterTransactionId"?long.MaxValue:b.WriterTransactionId};
                await guard();await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                try{var users=await new UserEligibilityLock(db).LockAsync(hint.Users,default);await c.Users.RetainLockedUsersAsync(hint with{CleanerBinding=stale},users,default);await Catalog();await Deny(()=>c.Users.ConfirmAfterSourcesAsync(false,default));}
                finally{if(tx.GetDbTransaction().Connection!=null)await tx.RollbackAsync();}
                results.Add(new{title="Stale advisory "+change+" rejected against actual R05 reader",result="PASS",rollback=true});
            }
            var missing=Context();await Deny(()=>missing.Reader.DiscoverAsync(actor,branch,second.InspectionId,null,Fe05NotificationKind.InspectionFailed,default));
            results.Add(new{title="Missing attribution fails closed; no legacy/assignment fallback",result="PASS"});
            await guard();await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                await new UserEligibilityLock(db).LockAsync([actor,other],default);await Catalog();await Capture(second.CleaningTaskId!.Value,other,true);
                if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.fe04_cleaner_attributions WHERE cleaning_task_id={second.CleaningTaskId}").SingleAsync()!=0)throw new Exception("Ambiguous capture elected a cleaner");
                if(await Context().Service.ReadInspectionCleanerAttributionAsync(db.Database.GetDbConnection(),tx.GetDbTransaction(),second.InspectionId,branch,default)!=null)throw new Exception("Ambiguous source read succeeded");
                await tx.RollbackAsync();
            }
            results.Add(new{title="Multi-cleaner capture/read remains null; no primary cleaner inferred",result="PASS",rollback=true});
            var denied=Context(other);await Deny(()=>denied.Reader.DiscoverAsync(other,branch,root.InspectionId,null,Fe05NotificationKind.InspectionFailed,default));
            results.Add(new{title="Unauthorized cleaner cannot discover whole Inspection as acting reader",result="PASS"});
            await File.WriteAllTextAsync(Path.Combine(output,"r05-fe05-sql.json"),JsonSerializer.Serialize(new{success=true,r05Pin="95f7422e284bc81bcfcf9f460dace52022de5cc7",realSqlSession=true,
                componentFixtureOnly=true,fullUc034Command=false,publicAuthoritativeMutation=false,acceptedConcreteBinding=(string?)null,cases=results},new JsonSerializerOptions{WriteIndented=true}));
            foreach(var entry in results.Skip(21))Console.WriteLine("PASS "+JsonSerializer.Serialize(entry));
        }
        catch(Exception e){await File.WriteAllTextAsync(Path.Combine(output,"r05-fe05-sql.json"),JsonSerializer.Serialize(new{success=false,cases=results,error=e.GetType().Name+": "+e.Message},new JsonSerializerOptions{WriteIndented=true}));throw;}
    }
    private sealed record Ctx(RoomTypeCreationService Service,InspectionR05CleanerSource Source,InspectionRecipientRosterReader Reader,InspectionUsersPrelock Users,ICurrentUserAccessor Current,IPermissionService Permissions);
    private sealed class Actor(CurrentUserInfo value):ICurrentUserAccessor{public bool TryGetCurrentUser(out CurrentUserInfo user){user=value;return true;}}
}
