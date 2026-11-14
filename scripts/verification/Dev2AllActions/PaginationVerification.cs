using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using HotelOps.Api.Controllers;
using HotelOps.Api.Controllers.Filters;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Dev2Authorization;
using HotelOps.Api.Services.Maintenance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal sealed partial class Fixtures
{
    internal async Task VerifyMaterialAvailability(JsonNode body,int page,int pageSize)
    {
        await using var db=open();
        var source=await db.MaterialFulfillmentLines.AsNoTracking()
            .Where(x=>x.FulfillmentId==FulfillmentId&&x.StockDocumentLineId!=null)
            .Select(x=>new{x.FulfillmentLineId,x.StockDocumentLineId,x.RequestLineId,x.Fulfillment.RequestId,x.Fulfillment.WarehouseId,x.StockDocumentLine!.DocumentId,x.StockDocumentLine.ItemId,x.StockDocumentLine.BaseQty}).SingleAsync();
        var items=body["items"]!.AsArray();
        if(body["page"]!.GetValue<int>()!=page||body["pageSize"]!.GetValue<int>()!=pageSize||body["total"]!.GetValue<int>()!=1||items.Count!=(page==1?1:0))
            throw new InvalidOperationException("A034 exact pagination DTO mismatch");
        if(page!=1)return;
        var line=items.Single()!;
        if(line["stockDocumentLineId"]!.GetValue<long>()!=source.StockDocumentLineId||line["documentId"]!.GetValue<long>()!=source.DocumentId
            ||line["fulfillmentId"]!.GetValue<long>()!=FulfillmentId||line["fulfillmentLineId"]!.GetValue<long>()!=source.FulfillmentLineId
            ||line["requestId"]!.GetValue<long>()!=Requests[51].RequestId||line["requestLineId"]!.GetValue<long>()!=source.RequestLineId
            ||line["incidentId"]!.GetValue<long>()!=Incidents[34].IncidentId||line["warehouseId"]!.GetValue<long>()!=Warehouse.WarehouseId
            ||source.WarehouseId!=Warehouse.WarehouseId||source.RequestId!=Requests[51].RequestId
            ||line["itemId"]!.GetValue<long>()!=source.ItemId||line["issuedBaseQty"]!.GetValue<decimal>()!=source.BaseQty||source.BaseQty!=2
            ||line["consumedBaseQty"]!.GetValue<decimal>()!=0||line["returnedBaseQty"]!.GetValue<decimal>()!=0||line["remainingBaseQty"]!.GetValue<decimal>()!=2)
            throw new InvalidOperationException("A034 availability does not match persisted A051 issue");
    }
}

internal static class PaginationVerification
{
    internal static async Task<int> Run(string output)
    {
        var count=0;void Check(bool value,string name){if(!value)throw new InvalidOperationException(name);count++;}
        var method=typeof(MaintenanceWorksController).GetMethod("MaterialAvailability")!;
        Check((int)method.GetParameters().Single(x=>x.Name=="page").DefaultValue! ==1,"Controller page contract");
        Check((int)method.GetParameters().Single(x=>x.Name=="pageSize").DefaultValue! ==20,"Controller pageSize contract");
        var service=DispatchProxy.Create<IMaintenanceWorkService,PaginationServiceProbe>();var probe=(PaginationServiceProbe)(object)service;
        using var provider=new ServiceCollection().AddSingleton(service).BuildServiceProvider();
        var http=new DefaultHttpContext{RequestServices=provider};
        var known=Dev2ActionAdmissionRegistry.KnownActions.Single(x=>x.DecisionId=="A004A-034");
        var boundType=typeof(Dev2DemoBindings).Assembly.GetType("HotelOps.Api.Services.Dev2Authorization.Dev2BoundAction")!;
        var read=typeof(Dev2DemoBindings).GetMethod("ReadExpandedAsync",BindingFlags.Static|BindingFlags.NonPublic)!;
        var cases=new List<object>();
        foreach(var input in new[]{new Dictionary<string,object?>(),new(){{"page",3},{"pageSize",7}},new(){{"page",2}},new(){{"pageSize",5}},new(){{"page",0}},new(){{"pageSize",101}},new(){{"pageSize",0}},new(){{"page",int.MaxValue},{"pageSize",100}}})
        {
            input["id"]=34L;
            var page=input.TryGetValue("page",out var p)?(int)p!:1;var size=input.TryGetValue("pageSize",out var s)?(int)s!:20;
            var bound=Activator.CreateInstance(boundType,[known,http,new CurrentUserInfo(1,1,false),input])!;
            var result=await (Task<IActionResult>)read.Invoke(null,[bound,CancellationToken.None])!;
            Check(probe.Last==(34L,page,size),"Actual A034 handler forwards root and pagination");
            var valid=page>=1&&size is >=1 and <=100&&(long)(page-1)*size<=int.MaxValue;
            Check(valid?result is OkObjectResult:result is BadRequestObjectResult,"Actual service paging validation preserved");
            if(valid)Check(((MaintenanceMaterialAvailability)((OkObjectResult)result).Value!).PageSize==size,"Handler DTO preserved");
            cases.Add(new{arguments=input,expectedStatus=valid?200:400,actualStatus=valid?((OkObjectResult)result).StatusCode:((BadRequestObjectResult)result).StatusCode});
        }
        Check(HostAcceptance.AllowedFocus("A004A-034")&&!HostAcceptance.AllowedFocus("A004A-035"),"Exact A034 focus allowlist");
        await File.WriteAllTextAsync(Path.Combine(output,"FIX07_PAGINATION_OFFLINE.json"),JsonSerializer.Serialize(new{status="PASS",assertions=count,cases,sqlConnections=0,sqlAcceptance="NOT_RUN",note="Actual binding with service probe; invalid inputs execute real service validation before SQL. Auth negatives remain in host acceptance and existing offline security suites."},new JsonSerializerOptions{WriteIndented=true}));
        return count;
    }
}

public class PaginationServiceProbe:DispatchProxy
{
    internal (long,int,int) Last;
    protected override object? Invoke(MethodInfo? targetMethod,object?[]? args)
    {
        if(targetMethod?.Name!="MaterialAvailabilityAsync")throw new InvalidOperationException("Unexpected service call");
        var id=(long)args![0]!;var page=(int)args[1]!;var size=(int)args[2]!;Last=(id,page,size);
        if(page<1||size is <1 or >100||(long)(page-1)*size>int.MaxValue)
        {
            // Invalid paging is checked before any constructor dependency is read; no SQL is opened.
            var actual=(MaintenanceWorkService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MaintenanceWorkService));
            return actual.MaterialAvailabilityAsync(id,page,size,(CancellationToken)args[3]!);
        }
        return Task.FromResult<MaintenanceMaterialAvailability?>(new(page,size,0,Array.Empty<MaintenanceMaterialAvailabilityLine>()));
    }
}
