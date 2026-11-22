using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Infrastructure;
public static class Bootstrap
{
    public static async Task Run(HotelOpsCompactDbContext db,IPasswordHasher<User> hasher,IConfiguration config,CancellationToken ct)
    {
        var username=config["Compact:Bootstrap:Username"]??throw new InvalidOperationException("Bootstrap username required.");
        var password=config["Compact:Bootstrap:Password"]??throw new InvalidOperationException("Bootstrap password required.");
        if(password.Length<12||username.Length>100||string.IsNullOrWhiteSpace(username))throw new InvalidOperationException("Invalid bootstrap credentials.");
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        if(await db.Users.AnyAsync(ct)||await db.Roles.AnyAsync(ct))throw new InvalidOperationException("Bootstrap is only permitted on an empty identity store.");
        var user=new User{EmployeeCode="BOOTSTRAP",Username=username,FullName="System Administrator",AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",SecurityStamp=Guid.NewGuid()};
        user.PasswordHash=hasher.HashPassword(user,password);db.Users.Add(user);
        var role=new Role{Code="SYS_ADMIN",Name="System Administrator",IsActive=true};db.Roles.Add(role);await db.SaveChangesAsync(ct);
        foreach(var code in Security.PermissionCatalog.Codes)
            if(!await db.Permissions.AnyAsync(p=>p.Code==code,ct))db.Permissions.Add(new Permission{Code=code,ModuleCode=code.Split('_')[0],IsActive=true});
        await db.SaveChangesAsync(ct);
        foreach(var code in new[]{"USER_VIEW","USER_MANAGE","ROLE_MANAGE","MASTER_DATA_MANAGE"})
        {
            var permission=await db.Permissions.SingleOrDefaultAsync(p=>p.Code==code,ct);
            if(permission==null){permission=new Permission{Code=code,ModuleCode="ADMIN",IsActive=true};db.Permissions.Add(permission);await db.SaveChangesAsync(ct);}
            db.RolePermissions.Add(new RolePermission{RoleId=role.RoleId,PermissionId=permission.PermissionId,DataScope="ALL"});
        }
        db.UserRoles.Add(new UserRole{RoleId=role.RoleId,UserId=user.UserId,AssignedBy=user.UserId});
        db.AuditLogs.Add(new AuditLog{ActorUserId=user.UserId,EntityType="User",EntityKey=user.UserId.ToString(),Action="BOOTSTRAP_ADMIN",OperationKey=Guid.NewGuid()});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        Console.WriteLine("Bootstrap administrator created. No operational role grants were assigned.");
    }
}
