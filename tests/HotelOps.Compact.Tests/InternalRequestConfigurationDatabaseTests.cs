using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Compact.Tests;

[Collection("Note2511 SQL")]
public sealed class InternalRequestConfigurationDatabaseTests
{
    private sealed class Accessor : IHttpContextAccessor { public HttpContext? HttpContext { get; set; } }
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()=>new(2026,11,27,2,10,0,TimeSpan.Zero);
    }
    private static JsonElement Json(object value)=>JsonSerializer.SerializeToElement(value,new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private static async Task<WorkService> Service(HotelOpsCompactDbContext db,long userId)
    {
        var clock=new Clock();var user=await db.Users.SingleAsync(u=>u.UserId==userId);
        var session=new UserSession{UserId=userId,TokenHash=RandomNumberGenerator.GetBytes(32),SecurityStamp=user.SecurityStamp,CreatedAt=clock.GetUtcNow().UtcDateTime,ExpiresAt=clock.GetUtcNow().UtcDateTime.AddDays(1)};
        db.UserSessions.Add(session);await db.SaveChangesAsync();
        var actor=new Actor(new Accessor{HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,userId.ToString()),new Claim("session_id",session.SessionId.ToString())],"sql-fixture"))}});
        var access=new Access(db,actor,clock);var atomic=new Atomic(db);var audit=new Audit(db,actor,clock);
        var notifications=new NotificationService(db,actor,access,atomic,clock);
        var documents=new DocumentService(db,access,actor,atomic,audit,clock,notifications);
        var supplies=new StaffSupplyService(db,access,actor,atomic,audit,clock,documents);
        var requests=new RequestWorkflowService(db,access,actor,atomic,audit,clock,supplies,documents,notifications);
        return new(db,access,actor,atomic,audit,clock,notifications,documents,new(db,actor,clock),supplies,requests);
    }

    [TaskSupplyDatabaseFact]
    public async Task ConfigurationPersistsRoutingAndRejectsWrongDepartmentOrMissingDescription()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_TASK_SUPPLY_TEST")!;
        Assert.Equal("HotelOpsCompactWorkdayTestNormSql20261127",new SqlConnectionStringBuilder(connection).InitialCatalog);
        await using var db=new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
        await using var tx=await db.Database.BeginTransactionAsync();
        var ct=CancellationToken.None;var now=new Clock().GetUtcNow().UtcDateTime;
        var sender=await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.RoleId
            join m in db.Memberships on new{ur.UserId,ur.BranchId} equals new{m.UserId,BranchId=(long?)m.BranchId}
            where r.Code=="STOREKEEPER"&&ur.RevokedAt==null&&ur.AssignedAt<=now&&m.Kind=="EMPLOYMENT"&&m.DepartmentId!=null&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now)
                &&db.Users.Any(u=>u.UserId==ur.UserId&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null)
            orderby ur.UserId select new{ur.UserId,ur.RoleId,BranchId=ur.BranchId!.Value,DepartmentId=m.DepartmentId!.Value}).FirstAsync();
        var manager=await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.RoleId where r.Code=="OPERATION_MANAGER"&&ur.BranchId==sender.BranchId&&ur.RevokedAt==null&&ur.AssignedAt<=now
            &&db.Users.Any(u=>u.UserId==ur.UserId&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null) select ur.UserId).FirstAsync();
        var otherDepartment=await db.Departments.Where(d=>d.BranchId==sender.BranchId&&d.IsActive&&d.DepartmentId!=sender.DepartmentId).Select(d=>d.DepartmentId).FirstAsync();
        var om=await Service(db,manager);var staff=await Service(db,sender.UserId);
        var request=new InternalRequestTypeRequest("Rollback routing "+Guid.NewGuid().ToString("N"),"Describe the required support",null,true,
            SenderRoleIds:[sender.RoleId],SenderDepartmentIds:[sender.DepartmentId],RecipientDepartmentIds:[otherDepartment],RequireDescription:true);
        var saved=Json(await om.SaveInternalRequestType(sender.BranchId,null,request,ct));var id=saved.GetProperty("referenceId").GetInt64();
        var config=Json(await staff.InternalRequestTypes(sender.BranchId,ct));
        Assert.Contains(config.GetProperty("items").EnumerateArray(),x=>x.GetProperty("referenceId").GetInt64()==id&&x.GetProperty("requireDescription").GetBoolean());
        var create=new CreateWorkRequest(sender.BranchId,"INTERNAL_REQUEST","Rollback request"," ",otherDepartment,null,null,null,null,null,null,null,id,null,sender.DepartmentId);
        Assert.Equal("REQUEST_DESCRIPTION_REQUIRED",(await Assert.ThrowsAsync<ApiException>(()=>staff.Create(create,ct))).Code);
        Assert.Equal("REQUESTER_DEPARTMENT_MEMBERSHIP_REQUIRED",(await Assert.ThrowsAsync<ApiException>(()=>staff.Create(create with{Description="Needs support",RequesterDepartmentId=otherDepartment},ct))).Code);
        Assert.Equal("REQUEST_RECIPIENT_NOT_ALLOWED",(await Assert.ThrowsAsync<ApiException>(()=>staff.Create(create with{Description="Needs support",DepartmentId=sender.DepartmentId},ct))).Code);
        var created=Json(await staff.Create(create with{Description="Needs support"},ct));
        Assert.True(created.GetProperty("fields").GetProperty("workId").GetInt64()>0);
        await om.SaveInternalRequestType(sender.BranchId,id,request with{RowVersion=saved.GetProperty("rowVersion").GetString(),SenderDepartmentIds=[otherDepartment]},ct);
        config=Json(await staff.InternalRequestTypes(sender.BranchId,ct));
        Assert.DoesNotContain(config.GetProperty("items").EnumerateArray(),x=>x.GetProperty("referenceId").GetInt64()==id);
        Assert.Equal("REQUESTER_DEPARTMENT_MEMBERSHIP_REQUIRED",(await Assert.ThrowsAsync<ApiException>(()=>staff.Create(create with{Description="Needs support"},ct))).Code);
        await tx.RollbackAsync();db.ChangeTracker.Clear();Assert.False(await db.ReferenceValues.AnyAsync(r=>r.ReferenceId==id));
    }
}
