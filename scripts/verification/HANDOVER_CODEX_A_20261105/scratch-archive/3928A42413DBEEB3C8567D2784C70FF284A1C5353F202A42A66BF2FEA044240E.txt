using System.Text.Json;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal sealed record Fixtures(Dictionary<string,long> Users, long BranchA, long BranchB, long TaskA, long TaskB, long TaskMixed, long TaskNone)
{
    internal static async Task<Fixtures> Prepare(TestDatabase target, string path, CancellationToken ct)
    {
        await using var db = target.Open();
        if (File.Exists(path))
        {
            var existing = JsonSerializer.Deserialize<Fixtures>(await File.ReadAllTextAsync(path,ct))!;
            foreach(var pair in existing.Users)
                if(!await db.Users.AsNoTracking().AnyAsync(u=>u.UserId==pair.Value && u.Username=="it06."+pair.Key,ct)) throw new InvalidOperationException("Fixture identity changed; no reset permitted.");
            // Repair only this package's branch-B fixture department; preserve credentials, roles and history.
            var branchUser=await db.Users.SingleAsync(u=>u.UserId==existing.Users["branchb"]&&u.Username=="it06.branchb"&&u.PrimaryBranchId==existing.BranchB,ct);
            if(!await db.Departments.AnyAsync(d=>d.DepartmentId==branchUser.DepartmentId&&d.BranchId==existing.BranchB,ct))
            {
                if(!await db.Departments.AnyAsync(d=>d.DepartmentId==branchUser.DepartmentId&&d.BranchId==existing.BranchA&&d.DepartmentCode=="HK",ct))throw new InvalidOperationException("Unexpected fixture department; no overwrite.");
                var department=await db.Departments.SingleOrDefaultAsync(d=>d.BranchId==existing.BranchB&&d.DepartmentCode=="HK",ct);
                if(department is null){department=new Department{BranchId=existing.BranchB,DepartmentCode="HK",DepartmentName="Housekeeping",IsActive=true};db.Add(department);await db.SaveChangesAsync(ct);}
                branchUser.DepartmentId=department.DepartmentId;await db.SaveChangesAsync(ct);
            }
            return existing;
        }
        if(await db.Users.AnyAsync(ct) || await db.Branches.AnyAsync(ct)) throw new InvalidOperationException("Unowned fixture data; no automatic reset.");
        var secrets=TestDatabase.Configuration();
        var hasher=new PasswordCredentialService(Options.Create(new PasswordHasherOptions { CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount }));
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var now=DateTime.UtcNow.AddMinutes(-5);
        var a=new Branch {BranchCode="IT06_A",BranchName="Integration A",IsActive=true,CreatedAt=now};
        var b=new Branch {BranchCode="IT06_B",BranchName="Integration B",IsActive=true,CreatedAt=now};
        db.AddRange(a,b);await db.SaveChangesAsync(ct);
        var dep=new Department {BranchId=a.BranchId,DepartmentCode="HK",DepartmentName="Housekeeping",IsActive=true};db.Add(dep);await db.SaveChangesAsync(ct);
        var depB=new Department {BranchId=b.BranchId,DepartmentCode="HK",DepartmentName="Housekeeping",IsActive=true};db.Add(depB);await db.SaveChangesAsync(ct);
        var users=new Dictionary<string,User>();
        foreach(var name in new[]{"assigned","former","none","restricted","inactive","mixed","branchb","revoked"})
        {
            var user=new User {Username="it06."+name,EmployeeCode="IT06_"+name,Email="it06."+name+"@example.invalid",FullName="Integration "+name,PrimaryBranchId=name=="branchb"?b.BranchId:a.BranchId,DepartmentId=dep.DepartmentId,EmploymentStatus="ACTIVE",AccountStatus=name=="inactive"?"INACTIVE":"ACTIVE",CreatedAt=now,TemporaryPasswordExpiresAt=name=="restricted"?DateTime.UtcNow.AddDays(7):null};
            user.PasswordHash=hasher.HashPassword(user,secrets["Passwords:"+name]??throw new InvalidOperationException("Missing private fixture credential"));
            if(name=="branchb")user.DepartmentId=depB.DepartmentId;
            db.Add(user);users.Add(name,user);
        }
        await db.SaveChangesAsync(ct);
        var roles=await db.Roles.AsNoTracking().ToDictionaryAsync(r=>r.RoleCode,r=>r.RoleId,ct);
        foreach(var (name,user) in users)
        {
            var branch=name=="branchb"||name=="mixed"?b.BranchId:a.BranchId;
            if(name!="none") db.Add(new UserRole {UserId=user.UserId,RoleId=roles["HK_STAFF"],BranchId=branch,AssignedAt=now,IsActive=true});
            db.Add(new UserBranchAccess {UserId=user.UserId,BranchId=branch,AccessLevel="STANDARD",IsPrimary=true,GrantedAt=now});
            if(name=="mixed")
            {
                db.Add(new UserRole {UserId=user.UserId,RoleId=roles["HK_SUPERVISOR"],BranchId=a.BranchId,AssignedAt=now,IsActive=true});
                db.Add(new UserRole {UserId=user.UserId,RoleId=roles["HK_STAFF"],BranchId=a.BranchId,AssignedAt=now,IsActive=false});
                db.Add(new UserBranchAccess {UserId=user.UserId,BranchId=a.BranchId,AccessLevel="STANDARD",GrantedAt=now});
            }
        }
        var type=new RoomType {TypeCode="IT06_STANDARD",TypeName="Integration",IsActive=true};
        var reference=new ReferenceValue {GroupCode="CLEANING_TYPE",ValueCode="CHECKOUT",DisplayName="Checkout",IsActive=true};
        db.AddRange(type,reference);await db.SaveChangesAsync(ct);
        var tasks=new List<CleaningTask>();
        foreach(var (name,branch) in new[]{("assigned",a),("branchb",b),("mixed",a),("none",a)})
        {
            var floor=new Floor {BranchId=branch.BranchId,FloorCode="IT06_"+name,FloorName="Integration",IsActive=true};db.Add(floor);await db.SaveChangesAsync(ct);
            var room=new Room {BranchId=branch.BranchId,FloorId=floor.FloorId,RoomTypeId=type.RoomTypeId,RoomNumber="IT06_"+name,OperationalStatus="CLEANING",IsActive=true};db.Add(room);await db.SaveChangesAsync(ct);
            var task=new CleaningTask {TaskNo="IT06_"+name,RoomId=room.RoomId,CleaningTypeId=reference.ReferenceId,Status="IN_PROGRESS",CreatedBy=users["assigned"].UserId,CreatedAt=now,ActualStartAt=now};db.Add(task);await db.SaveChangesAsync(ct);
            db.Add(new CleaningAssignment {CleaningTaskId=task.CleaningTaskId,AssigneeUserId=users[name].UserId,AssignedBy=users["assigned"].UserId,AssignedAt=now,AssignmentStatus="ACTIVE"});tasks.Add(task);
        }
        db.Add(new CleaningAssignment {CleaningTaskId=tasks[0].CleaningTaskId,AssigneeUserId=users["former"].UserId,AssignedBy=users["assigned"].UserId,AssignedAt=now.AddHours(-1),EndedAt=now.AddMinutes(-1),AssignmentStatus="REASSIGNED"});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        var result=new Fixtures(users.ToDictionary(p=>p.Key,p=>p.Value.UserId),a.BranchId,b.BranchId,tasks[0].CleaningTaskId,tasks[1].CleaningTaskId,tasks[2].CleaningTaskId,tasks[3].CleaningTaskId);
        await File.WriteAllTextAsync(path,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}),ct);
        return result;
    }
}
