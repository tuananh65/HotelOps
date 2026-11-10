using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
internal static class InspectionDecision08SqlChecks
{
    internal static async Task RunAsync(HotelOpsDbContext db,string output,Func<Task> guard,NotificationCommitGuard commitGuard)
    {
        var results=new List<object>();
        InspectionUsersPrelock? expired=null;
        InspectionRecipientRoster? advisory=null;
        var roots=await db.RoomInspections.AsNoTracking().OrderBy(x=>x.InspectionId).ToArrayAsync();
        var root=roots.First();var second=roots.Skip(1).First();
        var actor=root.InspectorUserId;var other=await db.Users.Where(x=>x.Username=="int.denied").Select(x=>x.UserId).SingleAsync();
        var branch=await db.Rooms.Where(x=>x.RoomId==root.RoomId).Select(x=>x.BranchId).SingleAsync();
        var foreign=await db.Branches.Where(x=>x.BranchCode=="BR002").Select(x=>x.BranchId).SingleAsync();
        var reader=new InspectionRecipientRosterReader(db,new OriginalCleanerFixture(other));
        async Task<string> State()=>await db.Database.SqlQuery<string>($"SELECT (SELECT (SELECT i.inspection_id,i.row_version,c.cleaning_task_id,c.row_version AS cycle_revision FROM dbo.room_inspections i JOIN dbo.room_cleaning_cycle_state c ON c.room_id=i.room_id ORDER BY i.inspection_id FOR JSON PATH) AS roots,(SELECT user_id,account_status,employment_status FROM dbo.users ORDER BY user_id FOR JSON PATH) AS users,(SELECT attachment_id,original_file_name FROM dbo.attachments ORDER BY attachment_id FOR JSON PATH) AS attachments FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS Value").SingleAsync();
        await guard();
        await using(var install=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            await guard();
            var script=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"../../../../../scripts/database/fe05-fast008/01_SupervisorBindingCandidate.sql"));
            var batches=System.Text.RegularExpressions.Regex.Split(script,@"(?im)^\s*GO\s*\r?$");
            var existing=await db.Database.SqlQuery<string>($"SELECT definition AS Value FROM sys.sql_modules WHERE object_id=OBJECT_ID('dbo.TR_fe05_supervisor_binding_candidate')").SingleOrDefaultAsync();
            if(existing!=null){if(existing.Replace("\r\n","\n").Trim()!=batches[1].Replace("\r\n","\n").Trim())throw new Exception("Refusing changed candidate schema/trigger definition");}
            else foreach(var batch in batches)if(!string.IsNullOrWhiteSpace(batch)){await guard();await db.Database.ExecuteSqlRawAsync(batch);}
            if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.inspection_supervisor_bindings").SingleAsync()!=0)throw new Exception("Unexpected authority backfill");
            await install.CommitAsync();
        }
        async Task Lock(InspectionUsersPrelock users,InspectionRecipientRoster roster)
        {
            var locked=await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync(roster.Users,default);
            await users.RetainLockedUsersAsync(roster,locked,default);
        }
        async Task Bind(long inspection,long supervisor)
        {
            await guard();await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.inspection_supervisor_bindings(inspection_id,cleaning_task_id,room_id,branch_id,
                    responsible_supervisor_user_id,cycle_revision,inspection_revision_at_binding,bound_by_user_id,reason)
                SELECT i.inspection_id,i.cleaning_task_id,i.room_id,r.branch_id,{supervisor},c.row_version,i.row_version,{actor},'Explicit isolated candidate fixture'
                FROM dbo.room_inspections i JOIN dbo.rooms r ON r.room_id=i.room_id JOIN dbo.room_cleaning_cycle_state c ON c.room_id=i.room_id
                WHERE i.inspection_id={inspection}
                """);
        }
        async Task Check(string title,Func<Task> action)
        {
            await guard();advisory=await reader.DiscoverAsync(actor,branch,root.InspectionId,other,Fe05NotificationKind.CleaningReworkRequested,default);var before=await State();var audits=await db.AuditLogs.CountAsync();
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try{await action();if(!ReferenceEquals(db.Database.CurrentTransaction!.GetDbTransaction(),tx.GetDbTransaction()))throw new Exception("Physical transaction replaced");}
            finally{if(tx.GetDbTransaction().Connection!=null)await tx.RollbackAsync();db.ChangeTracker.Clear();}
            if(await State()!=before||await db.AuditLogs.CountAsync()!=audits||await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.inspection_supervisor_bindings").SingleAsync()!=0)throw new Exception("Rollback invariant failed");
            results.Add(new{title,result="PASS",rollback=true});Console.WriteLine("PASS "+title);
        }
        async Task RejectSql(Func<Task> action){try{await action();}catch(Microsoft.Data.SqlClient.SqlException e)when(e.Number is 51997 or 547){return;}throw new Exception("Expected schema rejection");}
        async Task RejectGap(Func<Task> action){try{await action();}catch(Fe05NotificationUnavailableException){return;}throw new Exception("Expected fail-closed gap");}
        Task<InspectionRecipientRoster> ReworkRoster()=>Task.FromResult(advisory??throw new Exception("Missing advisory discovery"));
        try
        {
            await Check("Explicit supervisor FK binding touches root revision and audits in same transaction",async()=>{
                var tag=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={root.InspectionId}").SingleAsync();await Bind(root.InspectionId,actor);
                var now=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections WHERE inspection_id={root.InspectionId}").SingleAsync();
                if(tag.SequenceEqual(now)||await db.AuditLogs.CountAsync(x=>x.EntityType=="inspection_supervisor_bindings")!=1)throw new Exception("Missing revision/audit");
            });
            await Check("Multi-parent supervisor insert invalidates both inspection roots",async()=>{
                var versions=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections ORDER BY inspection_id").ToArrayAsync();
                await guard();await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT dbo.inspection_supervisor_bindings(inspection_id,cleaning_task_id,room_id,branch_id,responsible_supervisor_user_id,cycle_revision,inspection_revision_at_binding,bound_by_user_id,reason)
                    SELECT i.inspection_id,i.cleaning_task_id,i.room_id,r.branch_id,{actor},c.row_version,i.row_version,{actor},'Multi-row isolated fixture'
                    FROM dbo.room_inspections i JOIN dbo.rooms r ON r.room_id=i.room_id JOIN dbo.room_cleaning_cycle_state c ON c.room_id=i.room_id
                    WHERE i.inspection_id IN({root.InspectionId},{second.InspectionId})
                    """);
                var updated=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_inspections ORDER BY inspection_id").ToArrayAsync();
                if(versions.Length!=updated.Length||versions.Zip(updated).Any(x=>x.First.SequenceEqual(x.Second)))throw new Exception("Multi-parent revision invalidation incomplete");
                if(await db.AuditLogs.CountAsync(x=>x.EntityType=="inspection_supervisor_bindings")!=2)throw new Exception("Set-based audit incomplete");
            });
            await Check("Wrong branch binding is rejected atomically",()=>RejectSql(()=>db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.inspection_supervisor_bindings(inspection_id,cleaning_task_id,room_id,branch_id,responsible_supervisor_user_id,cycle_revision,inspection_revision_at_binding,bound_by_user_id,reason)
                SELECT i.inspection_id,i.cleaning_task_id,i.room_id,{foreign},{actor},c.row_version,i.row_version,{actor},'Wrong branch'
                FROM dbo.room_inspections i JOIN dbo.room_cleaning_cycle_state c ON c.room_id=i.room_id WHERE i.inspection_id={root.InspectionId}
                """)));
            await Check("Stale cycle revision binding is rejected",()=>RejectSql(()=>db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.inspection_supervisor_bindings(inspection_id,cleaning_task_id,room_id,branch_id,responsible_supervisor_user_id,cycle_revision,inspection_revision_at_binding,bound_by_user_id,reason)
                SELECT i.inspection_id,i.cleaning_task_id,i.room_id,{branch},{actor},0x0000000000000000,i.row_version,{actor},'Wrong cycle revision'
                FROM dbo.room_inspections i WHERE i.inspection_id={root.InspectionId}
                """)));
            await Check("Missing supervisor FK rejected",()=>RejectSql(()=>Bind(root.InspectionId,long.MaxValue)));
            await Check("Removing supervisor without accepted removal actor contract rejected",async()=>{await Bind(root.InspectionId,actor);await RejectSql(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.inspection_supervisor_bindings WHERE inspection_id={root.InspectionId}"));});
            await Check("Missing supervisor binding never falls back to inspector or creator",()=>RejectGap(()=>reader.ReadAsync(actor,branch,root.InspectionId,null,Fe05NotificationKind.InspectionFailed,true,default)));
            await Check("Missing R04 original cleaner never falls back to assignment history",async()=>{await Bind(root.InspectionId,actor);var missing=new InspectionRecipientRosterReader(db,new MissingInspectionOriginalCleanerSource());await RejectGap(()=>missing.ReadAsync(actor,branch,root.InspectionId,null,Fe05NotificationKind.InspectionFailed,true,default));});
            await Check("Roster discovery refuses execution after caller transaction started",async()=>{try{await reader.DiscoverAsync(actor,branch,root.InspectionId,other,Fe05NotificationKind.CleaningReworkRequested,default);throw new Exception("Discovery took downstream locks");}catch(InvalidOperationException e)when(e.Message.Contains("before the caller transaction")){} });
            await Check("Users retained ascending before catalog and exact source assertion",async()=>{
                var roster=await ReworkRoster();var users=new InspectionUsersPrelock(db,reader,commitGuard);await Lock(users,roster);
                await db.Database.SqlQuery<int>($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction'; SELECT @r AS Value").ToArrayAsync();
                await users.ConfirmAfterSourcesAsync(false,default);users.RequireMember(actor);users.RequireMember(other);
            });
            await Check("Missing actual locked-user results are rejected",async()=>{
                await RejectGap(async()=>await new InspectionUsersPrelock(db,reader,commitGuard).RetainLockedUsersAsync(await ReworkRoster(),new Dictionary<long,HotelOps.Api.Services.Users.UserEligibilityLock.LockedUser?>(),default));
            });
            await Check("Already-locked hook attests existing sorted Batch-style User locks without extending them",async()=>{
                var roster=await ReworkRoster();var locked=await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync(roster.Users,default);
                var users=new InspectionUsersPrelock(db,reader,commitGuard);await users.RetainLockedUsersAsync(roster,locked,default);await users.ConfirmAfterSourcesAsync(false,default);expired=users;
            });
            await Check("Retained Users attestation cannot cross caller transaction boundaries",()=>RejectGap(()=>{expired!.RequireMember(actor);return Task.CompletedTask;}));
            await Check("Missing accepted source-read policy fails closed despite branch and complete Users locks",async()=>{
                var users=new InspectionUsersPrelock(db,reader,commitGuard);await Lock(users,await ReworkRoster());
                var current=new NoActor();var policy=new PermissionService(current,new EfPermissionDataSource(db),new UnresolvedPermissionResourceResolver(),TimeProvider.System);
                var coordinator=new InspectionOwnerSourceCoordination(db,current,new SqlSessionService(db,new SessionRevocationService(db),TimeProvider.System),policy,new InspectionOwnerOperation(),users);
                var r=users.Roster;
                var source=new Fe05NotificationSource(actor,branch,r.RoomId,r.InspectionId,r.TaskId,root.VersionId,null,'"'+r.InspectionRevision+'"',r.TaskId,"COMPLETED","FAIL","FAILED",DateTimeOffset.UtcNow,[1]);
                await RejectGap(()=>coordinator.CanRecipientReadAsync(db,source,other,default));
            });
            await Check("New user after retention poisons roster and never extends it",async()=>{
                var users=new InspectionUsersPrelock(db,reader,commitGuard);await Lock(users,await ReworkRoster());
                await RejectGap(()=>{users.RequireMember(long.MaxValue);return Task.CompletedTask;});await RejectGap(()=>{users.RequireMember(actor);return Task.CompletedTask;});
                try{await db.Database.CurrentTransaction!.CommitAsync();throw new Exception("Swallowed late-user failure committed");}catch(InvalidOperationException e)when(e.Message.Contains("roll back")){}
            });
            await Check("Account revocation invalidates eligibility before downstream locks",async()=>{
                var roster=await ReworkRoster();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET account_status='INACTIVE' WHERE user_id={other}");
                try{await Lock(new InspectionUsersPrelock(db,reader,commitGuard),roster);throw new Exception("Ineligible user locked as eligible");}catch(System.ComponentModel.DataAnnotations.ValidationException){}
            });
            await Check("Stale root evidence revision rejected after Users retention",async()=>{
                var users=new InspectionUsersPrelock(db,reader,commitGuard);await Lock(users,await ReworkRoster());
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.attachments SET original_file_name='stale-roster.pdf' WHERE entity_type='INSPECTION' AND entity_id={root.InspectionId}");
                await RejectGap(()=>users.ConfirmAfterSourcesAsync(false,default));
            });
            await Check("Changed current-cycle revision invalidates retained roster",async()=>{
                var users=new InspectionUsersPrelock(db,reader,commitGuard);await Lock(users,await ReworkRoster());
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_cleaning_cycle_state SET cleaning_task_id=cleaning_task_id WHERE room_id={root.RoomId}");
                await RejectGap(()=>users.ConfirmAfterSourcesAsync(true,default));
            });
            await Check("Wrong branch cannot discover resource roster",()=>RejectGap(()=>reader.ReadAsync(actor,foreign,root.InspectionId,other,Fe05NotificationKind.CleaningReworkRequested,true,default)));
            await Check("Supervisor reassignment invalidates roster without late Users locks",async()=>{
                var locked=await new HotelOps.Api.Services.Users.UserEligibilityLock(db).LockAsync([actor,other],default);
                await Bind(root.InspectionId,actor);
                var roster=await reader.ReadAsync(actor,branch,root.InspectionId,null,Fe05NotificationKind.InspectionFailed,true,default);
                var users=new InspectionUsersPrelock(db,reader,commitGuard);await users.RetainLockedUsersAsync(roster,locked,default);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE b SET responsible_supervisor_user_id={other},inspection_revision_at_binding=i.row_version FROM dbo.inspection_supervisor_bindings b JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id WHERE b.inspection_id={root.InspectionId}");
                await RejectGap(()=>users.ConfirmAfterSourcesAsync(true,default));
            });
            await File.WriteAllTextAsync(Path.Combine(output,"decision08-sql.json"),JsonSerializer.Serialize(new{success=true,schemaCandidateOnly=true,originalCleanerFixtureOnly=true,acceptedSourceReadBinding=(string?)null,publicMutation=false,cases=results},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception e){await File.WriteAllTextAsync(Path.Combine(output,"decision08-sql.json"),JsonSerializer.Serialize(new{success=false,cases=results,error=e.GetType().Name+": "+e.Message},new JsonSerializerOptions{WriteIndented=true}));throw;}
    }
    private sealed class NoActor:ICurrentUserAccessor{public bool TryGetCurrentUser(out CurrentUserInfo user){user=default;return false;}}
    // Explicit isolated fixture seam, not an FE04 original-cleaner authority implementation.
    private sealed class OriginalCleanerFixture(long user):IInspectionOriginalCleanerSource
    {public Task<long> ReadAsync(long inspection,long task,long room,long branch,string cycleRevision,CancellationToken ct){ct.ThrowIfCancellationRequested();return Task.FromResult(user);}}
}
