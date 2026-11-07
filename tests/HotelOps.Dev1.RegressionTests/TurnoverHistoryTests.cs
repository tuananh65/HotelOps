using System.ComponentModel.DataAnnotations;
using System.Reflection;
using HotelOps.Api.Controllers;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class TurnoverHistoryTests
{
    [Theory]
    [InlineData(0,1,20,1,50)] [InlineData(1,0,20,1,50)] [InlineData(1,1,101,1,50)]
    [InlineData(1,int.MaxValue,100,1,50)] [InlineData(1,1,20,0,50)] [InlineData(1,1,20,1,101)]
    public void History_rejects_unsafe_identifiers_and_pagination(long room,int page,int size,int timeline,int timelineSize)
        => Assert.Throws<ValidationException>(()=>TurnoverReadRules.Validate(room,null,new(Page:page,PageSize:size,TimelinePage:timeline,TimelinePageSize:timelineSize),null));
    [Fact]
    public void History_requires_UTC_ordered_dates_valid_result_and_cycle_for_evidence()
    {
        Assert.Throws<ValidationException>(()=>TurnoverReadRules.Validate(1,null,new(From:DateTime.SpecifyKind(DateTime.Now,DateTimeKind.Unspecified)),null));
        Assert.Throws<ValidationException>(()=>TurnoverReadRules.Validate(1,null,new(From:DateTime.UtcNow,To:DateTime.UtcNow.AddDays(-1)),null));
        Assert.Throws<ValidationException>(()=>TurnoverReadRules.Validate(1,null,new(Result:"READY"),null));
        Assert.Throws<ValidationException>(()=>TurnoverReadRules.Validate(1,null,new(),1));
        Assert.Equal("PASS",TurnoverReadRules.Validate(1,null,new(Result:" pass "),null).Result);
    }
    [Fact]
    public void Timeline_orders_real_timestamps_with_stable_source_identity_ties()
    {
        var at=DateTime.UtcNow;TurnoverEvent[] rows=[new("PROGRESS",2,"LOGGED",at,1,null,null),new("ASSIGNMENT",3,"ENDED",at,1,null,null),new("TASK",1,"CREATED",at.AddMinutes(-1),1,null,null),new("PROGRESS",1,"LOGGED",at,1,null,null)];
        var sorted=TurnoverReadRules.Order(rows);Assert.Equal(new[]{"TASK","ASSIGNMENT","PROGRESS","PROGRESS"},sorted.Select(x=>x.SourceType));Assert.Equal(1,sorted[2].SourceId);
        Assert.Empty(TurnoverReadRules.Order([]));
    }
    [Theory]
    [InlineData(null,null)][InlineData("not-json",null)][InlineData("{}",null)][InlineData("{\"reason\":123}",null)][InlineData("{\"reason\":\"Guest stayed\"}","Guest stayed")]
    public void Cancellation_reason_only_comes_from_valid_persisted_audit(string? json,string? expected)
        => Assert.Equal(expected,TurnoverReadRules.CancellationReason(json));
    [Fact]
    public void UC038_has_exactly_three_authenticated_GET_routes_and_no_collision_or_mutation()
    {
        var services=new ServiceCollection();services.AddLogging();services.AddControllers().AddApplicationPart(typeof(OperationalHistoryController).Assembly);
        using var provider=services.BuildServiceProvider();var actions=provider.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>().ToArray();
        var selected=actions.Where(x=>x.AttributeRouteInfo?.Template?.Contains("turnover-history")==true).ToArray();Assert.Equal(3,selected.Length);
        Assert.All(selected,a=>{Assert.Equal(typeof(OperationalHistoryController),a.ControllerTypeInfo.AsType());Assert.NotNull(a.MethodInfo.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpGetAttribute>());Assert.NotNull(a.ControllerTypeInfo.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());Assert.Single(actions,x=>x.AttributeRouteInfo?.Template==a.AttributeRouteInfo!.Template);});
    }
}
