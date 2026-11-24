using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace HotelOps.Compact.Tests;
public sealed class CompactV2Tests
{
    [Fact]public void ModelMatchesP0TablesAndSixViews()
    {
        using var db=new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer("Server=localhost;Database=ModelOnly;Integrated Security=true").Options);
        var entities=db.Model.GetEntityTypes().ToArray();
        Assert.Equal(49,entities.Count(e=>e.GetTableName()!=null));Assert.Equal(6,entities.Count(e=>e.GetViewName()!=null));
        foreach(var table in new[]{"supply_configurations","staff_supply_allocations","staff_supply_balances","supply_reservations","supply_stock_documents","staff_supply_ledger","request_workflows","request_workflow_lines","request_workflow_events"})
            Assert.Contains(entities,e=>e.GetTableName()==table);
        Assert.DoesNotContain(entities,e=>e.GetTableName() is "workdays" or "shifts" or "time_corrections");
        var work=db.Model.FindEntityType(typeof(WorkItem))!;
        Assert.True(work.FindProperty(nameof(WorkItem.RoomId))!.IsNullable);
        Assert.True(work.FindProperty(nameof(WorkItem.TurnoverNo))!.IsNullable);
        Assert.True(work.FindProperty(nameof(WorkItem.ChecklistTemplateId))!.IsNullable);
        Assert.NotNull(work.FindProperty(nameof(WorkItem.VerifiedBy)));
        Assert.NotNull(db.Model.FindEntityType(typeof(WorkInterval))!.FindProperty(nameof(WorkInterval.AssignmentId)));
    }
    [Theory]
    [InlineData("CLEANING","IN_PROGRESS","complete","PENDING_INSPECTION")]
    [InlineData("REWORK","IN_PROGRESS","complete","PENDING_REINSPECTION")]
    [InlineData("INSPECTION","PENDING_CONCLUSION","pass","PASSED")]
    [InlineData("INSPECTION","PENDING_CONCLUSION","fail","FAILED")]
    [InlineData("OPERATIONAL","IN_PROGRESS","pause","PAUSED")]
    [InlineData("OPERATIONAL","PAUSED","resume","IN_PROGRESS")]
    [InlineData("OPERATIONAL","IN_PROGRESS","complete","PENDING_VERIFICATION")]
    [InlineData("OPERATIONAL","PENDING_VERIFICATION","verify","CLOSED")]
    [InlineData("MAINTENANCE","IN_PROGRESS","complete","COMPLETED")]
    [InlineData("MAINTENANCE","COMPLETED","verify","CLOSED")]
    [InlineData("INCIDENT","RESOLVED","verify","CLOSED")]
    [InlineData("INTERNAL_REQUEST","COMPLETED","verify","CLOSED")]
    public void WorkflowPreservesReviewStages(string kind,string status,string action,string expected)=>Assert.Equal(expected,WorkRules.Transition(kind,status,action));
    [Theory]
    [InlineData("CLEANING","ASSIGNED","complete")]
    [InlineData("CLEANING","PENDING_INSPECTION","verify")]
    [InlineData("INSPECTION","ASSIGNED","pass")]
    [InlineData("OPERATIONAL","CLOSED","complete")]
    [InlineData("OPERATIONAL","IN_PROGRESS","verify")]
    [InlineData("INTERNAL_REQUEST","SUBMITTED","complete")]
    [InlineData("MAINTENANCE","PENDING","verify")]
    public void SkippedStagesAreRejected(string kind,string status,string action)=>Assert.Throws<ApiException>(()=>WorkRules.Transition(kind,status,action));
    [Fact]public void VersionAndOperationKeysAreRequired()
    {
        Assert.Throws<ApiException>(()=>Output.Version(new byte[8],"garbage"));
        Assert.Throws<ApiException>(()=>Output.Version(new byte[8],Convert.ToBase64String([1,0,0,0,0,0,0,0])));
        Assert.Throws<ApiException>(()=>Output.Key(Guid.Empty));
    }
    [Fact]public void CredentialsNeverAppearInResourceDto()
    {
        var result=Output.Record(new User{PasswordHash="secret",SecurityStamp=Guid.NewGuid()});
        Assert.DoesNotContain("passwordHash",result.Fields.Keys);Assert.DoesNotContain("securityStamp",result.Fields.Keys);
        Assert.DoesNotContain("tokenHash",Output.Record(new UserSession{TokenHash=[1,2,3]}).Fields.Keys);
    }
    [Fact]public void RecordValidationTargetsConstructorParameters()
    {
        var assembly=typeof(HotelOps.Api.Compact.Contracts.LoginRequest).Assembly;
        foreach(var type in assembly.GetTypes().Where(t=>t.Namespace=="HotelOps.Api.Compact.Contracts"))
            foreach(var property in type.GetProperties())
                Assert.Empty(property.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.ValidationAttribute),true));
        Assert.NotEmpty(typeof(HotelOps.Api.Compact.Contracts.LoginRequest).GetConstructors().Single(c=>c.IsPublic).GetParameters()[0].GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute),true));
    }
}
