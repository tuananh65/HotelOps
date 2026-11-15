using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintAuditScenario
{
    // Called only after SprintAuthProbe has verified exact disposable GUID/run ownership.
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient http, long actor, List<object> checks, CancellationToken ct, string username, string password)
    {
        if(await db.Branches.AnyAsync(ct)) throw new InvalidOperationException("Empty synthetic branch fixture required");
        var prefix="D3-AUDIT-"+Guid.NewGuid().ToString("N")[..8];
        var own=new Branch {BranchCode=prefix+"A",BranchName="Synthetic permitted branch",IsActive=true};
        var foreign=new Branch {BranchCode=prefix+"B",BranchName="Synthetic foreign branch",IsActive=true};
        var ids=new List<long>();
        async Task<JsonElement> Get(string path,int expected)
        {
            using var response=await http.GetAsync(path,ct);
            checks.Add(new {auditPath=path,expected,actual=(int)response.StatusCode});
            if((int)response.StatusCode!=expected) throw new Exception("Audit HTTP status mismatch: "+path);
            var body=await response.Content.ReadAsStringAsync(ct);
            if(body.Contains("SYNTHETIC_SECRET",StringComparison.Ordinal)) throw new Exception("Sensitive audit field leaked");
            using var json=JsonDocument.Parse(body);
            return json.RootElement.Clone();
        }
        try
        {
            await Get("/api/audit-logs",403);
            db.Branches.AddRange(own,foreign); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var role=await db.Roles.Where(r=>r.RoleCode=="SYS_ADMIN"&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
            db.UserRoles.Add(new UserRole {UserId=actor,RoleId=role,BranchId=own.BranchId,IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-1)});
            db.UserBranchAccesses.Add(new UserBranchAccess {UserId=actor,BranchId=own.BranchId,AccessLevel="STANDARD",GrantedAt=DateTime.UtcNow.AddMinutes(-1)});
            foreach(var branch in new long?[]{own.BranchId,foreign.BranchId,null})
            {
                var audit=new AuditLog {BranchId=branch,ActorUserId=actor,EntityType=prefix,EntityId="42",Action="UPDATE",OccurredAt=DateTime.UtcNow,
                    ChangedFields="[\"status\",\"closedAt\",\"password\",\"token\"]",
                    OldValues="{\"status\":\"PENDING\",\"closedAt\":\"2026-11-14T00:00:00Z\",\"password\":\"SYNTHETIC_SECRET\"}",
                    NewValues="{\"status\":\"COMPLETED\",\"closedAt\":null,\"token\":\"SYNTHETIC_SECRET\",\"nested\":{\"status\":\"SYNTHETIC_SECRET\"}}"};
                db.AuditLogs.Add(audit); await db.SaveChangesAsync(ct); ids.Add(audit.AuditId);
            }
            db.ChangeTracker.Clear();
            var list=await Get("/api/audit-logs?entityType="+prefix,200);
            if(list.GetProperty("totalCount").GetInt32()!=1||list.GetProperty("items")[0].GetProperty("id").GetInt64()!=ids[0])
                throw new Exception("Branch-scoped audit list leaked foreign/global rows");
            var detail=await Get("/api/audit-logs/"+ids[0],200);
            if(detail.GetProperty("before").GetProperty("status").GetString()!="PENDING"||detail.GetProperty("after").GetProperty("status").GetString()!="COMPLETED"
                ||detail.GetProperty("before").EnumerateObject().Count()!=2||detail.GetProperty("after").EnumerateObject().Count()!=2
                ||detail.GetProperty("after").GetProperty("closedAt").ValueKind!=JsonValueKind.Null
                ||detail.GetProperty("entry").GetProperty("changedFields").GetArrayLength()!=2)
                throw new Exception("Audit redaction discarded business status or exposed unknown fields");
            await Get("/api/audit-logs/"+ids[1],404);
            await Get("/api/audit-logs/"+ids[2],404);
            await Get("/api/audit-logs?branchId="+foreign.BranchId,403);
            await Get("/api/audit-logs?departmentId=1",400);
            await Get("/api/audit-logs?pageSize=101",400);
            await Get("/api/audit-logs?from=2026-11-15&to=2026-11-14",400);
            var empty=await Get("/api/audit-logs?entityType="+prefix+"&entityId=missing",200);
            if(empty.GetProperty("totalCount").GetInt32()!=0) throw new Exception("Audit exact record filter failed");
            for(var index=1;index<=25;index++)
                db.AuditLogs.Add(new(){BranchId=own.BranchId,ActorUserId=actor,EntityType=prefix,EntityId="42",Action="UPDATE",
                    OccurredAt=DateTime.UtcNow.AddMinutes(-index),OldValues="{\"progressPercent\":0}",NewValues="{\"progressPercent\":1}",ChangedFields="[\"progressPercent\"]"});
            var creation=new AuditLog{BranchId=own.BranchId,ActorUserId=actor,EntityType=prefix,EntityId="creation-only",Action="CREATE",OccurredAt=DateTime.UtcNow.AddHours(-1),NewValues="{\"status\":\"PENDING\"}",ChangedFields="[\"status\"]"};
            db.AuditLogs.Add(creation);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            async Task<string> Fingerprint()=>JsonSerializer.Serialize(await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType==prefix).OrderBy(a=>a.AuditId)
                .Select(a=>new{a.AuditId,a.BranchId,a.ActorUserId,a.EntityId,a.Action,a.OccurredAt,a.OldValues,a.NewValues,a.ChangedFields}).ToArrayAsync(ct));
            var before=await Fingerprint();
            async Task Browser(string mode)
            {
                var result=await SprintAuditBrowser.RunAsync(new{username,password,branch=own.BranchId,foreignBranch=foreign.BranchId,actor,entityType=prefix,entry=ids[0],foreignEntry=ids[1],globalEntry=ids[2],creation=creation.AuditId,mode},ct);
                checks.Add(new{auditBrowser=result});
                if(!result.GetProperty("success").GetBoolean())throw new Exception("Audit browser failed at "+result.GetProperty("stage").GetString());
                if(await db.UserSessions.CountAsync(s=>s.UserId==actor&&s.RevokedAt==null,ct)!=1)throw new Exception("Audit browser logout not persisted");
            }
            await Browser("branch-admin");
            await db.UserRoles.Where(r=>r.UserId==actor&&r.RoleId==role).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.BranchId,(long?)null),ct);
            var global=await Get("/api/audit-logs?entityType="+prefix,200);
            if(global.GetProperty("totalCount").GetInt32()!=29)throw new Exception("Global Administrator scope incomplete");
            await Get("/api/audit-logs/"+ids[2],200);
            await Browser("global-admin");
            if(before!=await Fingerprint())throw new Exception("Read-only audit workflow mutated source audit history");
            checks.Add(new{auditFingerprintUnchanged=true});
            await db.UserRoles.Where(r=>r.UserId==actor).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.IsActive,false),ct);
            await Get("/api/audit-logs/"+ids[0],403);
            await Get("/api/audit-logs/options",403);
            var ownerRole=await db.Roles.Where(r=>r.RoleCode=="OWNER"&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
            db.UserRoles.Add(new(){UserId=actor,RoleId=ownerRole,IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-1)});await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            await Get("/api/audit-logs",403);
            await Browser("denied-owner");
            checks.Add(new {auditScopeRedactionAndRevocation=true});
        }
        finally
        {
            db.ChangeTracker.Clear();
            await using var cleanup=await db.Database.BeginTransactionAsync(CancellationToken.None);
            await db.AuditLogs.Where(a=>a.EntityType==prefix&&a.ActorUserId==actor).ExecuteDeleteAsync(CancellationToken.None);
            await db.UserRoles.Where(r=>r.UserId==actor).ExecuteDeleteAsync(CancellationToken.None);
            await db.UserBranchAccesses.Where(a=>a.UserId==actor&&a.BranchId==own.BranchId).ExecuteDeleteAsync(CancellationToken.None);
            await db.Branches.Where(b=>(b.BranchId==own.BranchId||b.BranchId==foreign.BranchId)&&b.BranchCode.StartsWith(prefix)).ExecuteDeleteAsync(CancellationToken.None);
            await cleanup.CommitAsync(CancellationToken.None);
            if(await db.Branches.AnyAsync()||await db.UserRoles.AnyAsync(r=>r.UserId==actor)||await db.AuditLogs.AnyAsync(a=>a.EntityType==prefix))
                throw new Exception("Audit fixture cleanup failed");
            checks.Add(new {auditFixtureCleanup=true});
        }
    }
}
