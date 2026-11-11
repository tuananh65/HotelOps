using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Rbac;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.Rbac;
public static class RoleCatalogQueries
{
 // Distinct active employed accounts with temporally active assignment, not effective access.
 public static Expression<Func<Role,RoleCatalogDto>> Projection(DateTime now)=>r=>new(r.RoleId,r.RoleCode,r.RoleName,r.Description,r.IsSystemRole,r.IsActive,
 r.UserRoles.Where(a=>a.IsActive&&a.AssignedAt<=now&&(a.ExpiresAt==null||a.ExpiresAt>now)&&a.User.DeletedAt==null&&a.User.AccountStatus=="ACTIVE"&&a.User.EmploymentStatus=="ACTIVE").Select(a=>a.UserId).Distinct().Count(),r.RolePermissions.Count);
 public static void Validate(int page,int pageSize,string? search){if(page<1||page>1000000||pageSize<1||pageSize>100||search?.Length>100||search?.Any(char.IsControl)==true)throw new ValidationException("Invalid catalog query.");}
}
/** Called only inside RoleCatalogReadFilter SQL read boundary. */
public sealed class RoleCatalogService(HotelOpsDbContext db,TimeProvider clock)
{
 public async Task<CatalogPage<RoleCatalogDto>> RolesAsync(int page,int pageSize,string? search,bool? active,CancellationToken ct){RoleCatalogQueries.Validate(page,pageSize,search);var q=db.Roles.AsNoTracking();if(active is {} a)q=q.Where(r=>r.IsActive==a);if(!string.IsNullOrWhiteSpace(search)){var term=search.Trim();q=q.Where(r=>r.RoleCode.Contains(term)||r.RoleName.Contains(term));}var total=await q.CountAsync(ct);var rows=await q.OrderBy(r=>r.RoleId).Skip((page-1)*pageSize).Take(pageSize).Select(RoleCatalogQueries.Projection(clock.GetUtcNow().UtcDateTime)).ToArrayAsync(ct);return new(rows,page,pageSize,total);}
 public async Task<RoleDetailDto?> DetailAsync(long id,CancellationToken ct){if(id<=0)throw new ValidationException("Positive role ID required.");var role=await db.Roles.AsNoTracking().Where(r=>r.RoleId==id).Select(RoleCatalogQueries.Projection(clock.GetUtcNow().UtcDateTime)).SingleOrDefaultAsync(ct);if(role==null)return null;var grants=await db.RolePermissions.AsNoTracking().Where(g=>g.RoleId==id).OrderBy(g=>g.RolePermissionId).Select(g=>new RoleGrantDto(g.RolePermissionId,new(g.Permission.PermissionId,g.Permission.PermissionCode,g.Permission.PermissionName,g.Permission.ModuleCode,g.Permission.ResourceCode,g.Permission.ActionCode,g.Permission.Description),g.DataScope)).Take(10001).ToArrayAsync(ct);if(grants.Length>10000)throw new CatalogLimitException();return new(role,grants);}
 public async Task<CatalogPage<PermissionCatalogDto>> PermissionsAsync(int page,int pageSize,CancellationToken ct){RoleCatalogQueries.Validate(page,pageSize,null);var q=db.Permissions.AsNoTracking();var total=await q.CountAsync(ct);var rows=await q.OrderBy(p=>p.PermissionId).Skip((page-1)*pageSize).Take(pageSize).Select(p=>new PermissionCatalogDto(p.PermissionId,p.PermissionCode,p.PermissionName,p.ModuleCode,p.ResourceCode,p.ActionCode,p.Description)).ToArrayAsync(ct);return new(rows,page,pageSize,total);}
}
public sealed class CatalogLimitException:Exception;
