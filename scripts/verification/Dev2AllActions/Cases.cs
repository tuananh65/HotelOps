using System.Text.Json.Nodes;
using HotelOps.Api.Controllers.Filters;
using Microsoft.EntityFrameworkCore;

internal sealed record TrialCase(Dev2KnownAction Identity,string Url,JsonObject? Body,int Actor);
internal sealed partial class Fixtures
{
    internal async Task<TrialCase> Case(Dev2KnownAction known)
    {
        if(known.DecisionId=="A004A-059")return await Case059(known);
        var n=int.Parse(known.DecisionId[^3..]);var actor=n is 4 or 5 or 31 or 32 or 46 or 47 or 48 or 50?1:n is 51 or 59?2:0;
        var uid=Users[actor].UserId;long id=0;long asset=0,incident=0,handover=0;
        if(n is 1 or 2)asset=Assets[n==1?1:4].AssetId;
        if(n==7)asset=Assets[7].AssetId;
        if(n==6)asset=Assets[6].AssetId;
        if(n is 3 or 4 or 5)handover=Handovers[n==5?5:4].HandoverId;
        if(n is 10 or 12)id=Assets[18].AssetId;
        if(n==13)id=Assets[16].AssetId;
        if(n is >=15 and <=18)id=Assets[n].AssetId;
        if(n is 19 or 20)id=Incidents[n].IncidentId;
        if(n==23)id=Incidents[19].IncidentId;
        if(n==24)incident=Incidents[24].IncidentId;
        if(n is >=25 and <=34)id=Works[n].MaintenanceWorkId;
        if(n==36)id=Works[7].MaintenanceWorkId;
        if(n is >=38 and <=42)id=Requests[51].RequestId;
        if(n is >=44 and <=51)id=Requests[n].RequestId;
        if(n is 53 or 55)id=Replenishments[58].ReplenishmentId;
        if(n==54)id=Replenishments[57].ReplenishmentId;
        if(n is >=57 and <=59)id=Replenishments[n].ReplenishmentId;
        var url="/"+known.RouteTemplate.Replace("{assetId:long}",asset.ToString()).Replace("{handoverId:long}",handover.ToString()).Replace("{incidentId:long}",incident.ToString()).Replace("{id:long}",id.ToString()).Replace("{fulfillmentId:long}",FulfillmentId.ToString());
        if(n is 8 or 9 or 11 or 22 or 37 or 52)url+="?branchId="+Branch.BranchId;
        if(n==35)url+="?incidentId="+Incidents[7].IncidentId;
        if(n is 60 or 61)url+="?warehouseId="+Warehouse.WarehouseId;
        if(known.HttpMethod=="GET")return new(known,url,null,actor);
        var b=new JsonObject{[n==21?"reportedBy":"actorUserId"]=uid};
        void Set(string key,object? value)=>b[key]=System.Text.Json.JsonSerializer.SerializeToNode(value);
        var reason="Isolated trial authorization acceptance";
        await using var db=open();
        if(n==1){Set("toDepartmentId",OtherDepartment.DepartmentId);Set("toUserId",Users[1].UserId);}
        if(n is 5 or 6 or 27 or 32 or 33 or 46 or 48 or 49 or 50 or 57)Set("reason",reason);
        if(n is 14 or 15){Set("assetCode",n==14?Marker+"NEW":Assets[15].AssetCode);Set("assetName",Marker+"Updated");Set("assetTypeId",AssetType.AssetTypeId);}
        if(n==14){Set("branchId",Branch.BranchId);Set("currentDepartmentId",Department.DepartmentId);}
        if(n is 16 or 17){Set("areaId",n==16?Area.AreaId:OtherArea.AreaId);Set("reason",reason);}
        if(n==17){Set("expectedLocationType","AREA");Set("expectedLocationId",Area.AreaId);}
        if(n==18){Set("expectedCondition","GOOD");Set("newCondition","FAIR");Set("note",reason);}
        if(n==19){Set("status","IN_PROGRESS");Set("note",reason);}
        if(n==20){Set("severityId",Severity.ReferenceId);Set("priorityId",Priority.ReferenceId);}
        if(n==21){Set("branchId",Branch.BranchId);Set("areaId",Area.AreaId);Set("incidentTitle",Marker+"New incident");Set("description",reason);Set("discoveredAt",DateTime.UtcNow.AddMinutes(-1));}
        if(n==24){Set("title",Marker+"New work");Set("departmentId",Department.DepartmentId);}
        if(n==25){Set("assigneeId",Users[0].UserId);Set("instruction",reason);}
        if(n is 28 or 31)Set("note",reason);
        if(n==29){Set("workNote",reason);Set("spentMinutes",1);}
        if(n==30)Set("resolutionNote",reason);
        if(n is 43 or 44)
        {
            Set("branchId",Branch.BranchId);Set("departmentId",Department.DepartmentId);Set("sourceWarehouseId",Warehouse.WarehouseId);Set("purpose",Marker+"Updated purpose");Set("neededAt",DateTime.UtcNow.AddDays(1));
            Set("lines",new[]{new{requestLineId=n==44?(long?)Requests[44].MaterialRequestLines.Single().LineId:null,lineNo=1,itemId=Items[1].ItemId,uomId=Uom.UomId,requestedQty=10}});
        }
        if(n is >=44 and <=51)Set("expectedRowVersion",Convert.ToBase64String(await db.MaterialRequests.Where(x=>x.RequestId==id).Select(x=>x.RowVersion).SingleAsync()));
        if(n==45)Set("assignedApproverUserId",Users[1].UserId);
        if(n==47)Set("lines",new[]{new{requestLineId=Requests[47].MaterialRequestLines.Single().LineId,approvedQty=10}});
        if(n==51){Set("mode","Partial");Set("documentDate",DateOnly.FromDateTime(DateTime.UtcNow));Set("lines",new[]{new{requestLineId=Requests[51].MaterialRequestLines.Single().LineId,quantity=2}});}
        if(n==56){Set("warehouseId",Warehouse.WarehouseId);Set("lines",new[]{new{lineNo=1,itemId=Items[56].ItemId,sourceType="LOW_STOCK",sourceThresholdId=Thresholds[56].ThresholdId,requestedBaseQty=10}});}
        if(n is >=57 and <=59)Set("expectedRowVersion",Convert.ToBase64String(await db.MaterialReplenishments.Where(x=>x.ReplenishmentId==id).Select(x=>x.RowVersion).SingleAsync()));
        if(n is 58 or 59)Set("lines",new[]{new{stockDocumentLineId=Receipts[n].StockDocumentLines.Single().LineId,replenishmentLineId=Replenishments[n].MaterialReplenishmentLines.Single().ReplenishmentLineId}});
        if(n==59){Set("receiptDocumentId",Receipts[59].DocumentId);Set("receiptExpectedRowVersion",Convert.ToBase64String(await db.StockDocuments.Where(x=>x.DocumentId==Receipts[59].DocumentId).Select(x=>x.RowVersion).SingleAsync()));Set("receiptLines",new[]{new{lineId=Receipts[59].StockDocumentLines.Single().LineId,acceptedQty=3,rejectedQty=0}});}
        return new(known,url,b,actor);
    }
}
