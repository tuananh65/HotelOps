using System.Reflection;
using HotelOps.Api.Compact.Controllers;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class MaintenanceWorkflowPolicyTests
{
    private static readonly MethodInfo BeforeAcceptance=typeof(WorkService).GetMethod("MaintenanceBeforeAcceptance",BindingFlags.Static|BindingFlags.NonPublic)!;
    private static readonly MethodInfo Photos=typeof(WorkService).GetMethod("ValidateMaintenancePhotoIds",BindingFlags.Static|BindingFlags.NonPublic)!;
    private static readonly MethodInfo Matches=typeof(WorkService).GetMethod("CleaningReportMatches",BindingFlags.Static|BindingFlags.NonPublic)!;

    [Theory]
    [InlineData("PENDING",false,false,false,"ACTIVE",true)]
    [InlineData("PENDING",true,false,false,"ACTIVE",false)]
    [InlineData("PENDING",false,true,false,"ACTIVE",false)]
    [InlineData("PENDING",false,false,true,"ACTIVE",false)]
    [InlineData("PENDING",false,false,false,"REASSIGNED",false)]
    [InlineData("IN_PROGRESS",false,false,false,"ACTIVE",false)]
    [InlineData("ON_HOLD",false,false,false,"ACTIVE",false)]
    [InlineData("COMPLETED",false,false,false,"ACTIVE",false)]
    public void HandoverIsOnlyAvailableBeforeFirstAcceptance(string status,bool accepted,bool started,bool history,string assignmentStatus,bool expected)
    {
        var work=new WorkItem{Kind="MAINTENANCE",Status=status,ActualStartAt=started?DateTime.UtcNow:null};
        var assignment=new WorkAssignment{AssignmentStatus=assignmentStatus,AcceptedAt=accepted?DateTime.UtcNow:null};
        Assert.Equal(expected,(bool)BeforeAcceptance.Invoke(null,[work,assignment,history])!);
        Assert.False((bool)BeforeAcceptance.Invoke(null,[work,null,false])!);
    }

    [Fact]
    public void OtherWorkKindsCannotUseMaintenanceHandover()=>Assert.False((bool)BeforeAcceptance.Invoke(null,
        [new WorkItem{Kind="CLEANING",Status="PENDING"},new WorkAssignment{AssignmentStatus="ACTIVE"},false])!);

    [Theory]
    [InlineData(new long[0],"MAINTENANCE_PHOTO_REQUIRED")]
    [InlineData(new long[]{1,1},"INVALID_MAINTENANCE_PHOTOS")]
    [InlineData(new long[]{0},"INVALID_MAINTENANCE_PHOTOS")]
    [InlineData(new long[]{-1},"INVALID_MAINTENANCE_PHOTOS")]
    public void MissingDuplicateOrInvalidPhotoIdsAreRejected(long[] ids,string code)
    {
        var exception=Assert.Throws<TargetInvocationException>(()=>Photos.Invoke(null,[ids]));
        Assert.Equal(code,Assert.IsType<ApiException>(exception.InnerException).Code);
    }

    [Fact]
    public void CompletionRetriesMustKeepExactPhotoSet()
    {
        const string snapshot="{\"reportType\":\"maintenance-completion\",\"attachmentIds\":[10,20]}";
        Photos.Invoke(null,[new long[]{10,20}]);
        Assert.True((bool)Matches.Invoke(null,[snapshot,new long[]{20,10}])!);
        Assert.False((bool)Matches.Invoke(null,[snapshot,new long[]{10,30}])!);
        Assert.False((bool)Matches.Invoke(null,[snapshot,null])!);
    }

    [Fact]
    public void AcceptanceStartsMaintenanceButPreservesHousekeepingTransitions()
    {
        Assert.Equal("IN_PROGRESS",WorkRules.Transition("MAINTENANCE","PENDING","accept"));
        Assert.Equal("ASSIGNED",WorkRules.Transition("CLEANING","ASSIGNED","accept"));
        Assert.Equal("ACCEPTED",WorkRules.Transition("REWORK","PENDING_ACCEPTANCE","accept"));
    }

    [Fact]
    public void IncidentInvestigationWriteEndpointIsRemoved()
    {
        Assert.DoesNotContain(typeof(WorkController).GetMethods().SelectMany(m=>m.GetCustomAttributes<HttpPostAttribute>()),
            route=>route.Template?.Contains("investigation",StringComparison.OrdinalIgnoreCase)==true);
        Assert.Null(typeof(WorkService).GetMethod("Investigate"));
    }
}
