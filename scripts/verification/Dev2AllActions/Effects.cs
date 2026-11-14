using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

internal sealed partial class Fixtures
{
    internal async Task VerifyEffect(string action,JsonNode response)
    {
        var n=int.Parse(action[^3..]);await using var db=open();bool valid;
        if(n==1)valid=await db.AssetHandovers.AnyAsync(x=>x.AssetId==Assets[1].AssetId&&x.ToUserId==Users[1].UserId&&x.CreatedBy==Users[0].UserId&&x.HandoverStatus=="PENDING");
        else if(n is 4 or 5)valid=await db.AssetHandovers.AnyAsync(x=>x.HandoverId==Handovers[n].HandoverId&&x.HandoverStatus==(n==4?"ACCEPTED":"REJECTED"));
        else if(n==6)valid=await db.Assets.AnyAsync(x=>x.AssetId==Assets[6].AssetId&&x.LifecycleStatus=="RETIRED");
        else if(n==14)valid=await db.Assets.AnyAsync(x=>x.AssetCode==Marker+"NEW"&&x.BranchId==Branch.BranchId);
        else if(n==15)valid=await db.Assets.AnyAsync(x=>x.AssetId==Assets[15].AssetId&&x.AssetName==Marker+"Updated");
        else if(n is 16 or 17)valid=await db.Assets.AnyAsync(x=>x.AssetId==Assets[n].AssetId&&x.CurrentAreaId==(n==16?Area.AreaId:OtherArea.AreaId))&&await db.AssetLocationHistories.AnyAsync(x=>x.AssetId==Assets[n].AssetId&&x.MovedBy==Users[0].UserId);
        else if(n==18)valid=await db.Assets.AnyAsync(x=>x.AssetId==Assets[18].AssetId&&x.ConditionStatus=="FAIR")&&await db.AssetConditionHistories.AnyAsync(x=>x.AssetId==Assets[18].AssetId&&x.ChangedBy==Users[0].UserId&&x.NewCondition=="FAIR");
        else if(n==19)valid=await db.Incidents.AnyAsync(x=>x.IncidentId==Incidents[19].IncidentId&&x.Status=="IN_PROGRESS");
        else if(n==20)valid=await db.Incidents.AnyAsync(x=>x.IncidentId==Incidents[20].IncidentId&&x.SeverityId==Severity.ReferenceId&&x.PriorityId==Priority.ReferenceId);
        else if(n==21)valid=await db.Incidents.AnyAsync(x=>x.IncidentTitle==Marker+"New incident"&&x.ReportedBy==Users[0].UserId&&x.BranchId==Branch.BranchId);
        else if(n==24)valid=await db.MaintenanceWorks.AnyAsync(x=>x.IncidentId==Incidents[24].IncidentId&&x.Title==Marker+"New work"&&x.CreatedBy==Users[0].UserId);
        else if(n==25)valid=await db.MaintenanceWorks.AnyAsync(x=>x.MaintenanceWorkId==Works[25].MaintenanceWorkId&&x.AssigneeId==Users[0].UserId&&x.Instruction=="Isolated trial authorization acceptance");
        else if(n==29)valid=await db.MaintenanceWorkLogs.AnyAsync(x=>x.MaintenanceWorkId==Works[29].MaintenanceWorkId&&x.LoggedBy==Users[0].UserId&&x.SpentMinutes==1);
        else if(n is >=26 and <=33)
        {
            var state=n switch{26 or 28 or 32=>"IN_PROGRESS",27=>"ON_HOLD",30=>"COMPLETED",31=>"CLOSED",33=>"CANCELLED",_=>throw new InvalidOperationException()};
            valid=await db.MaintenanceWorks.AnyAsync(x=>x.MaintenanceWorkId==Works[n].MaintenanceWorkId&&x.Status==state);
        }
        else if(n==43)valid=await db.MaterialRequests.AnyAsync(x=>x.RequesterId==Users[0].UserId&&x.Purpose==Marker+"Updated purpose"&&x.BranchId==Branch.BranchId);
        else if(n==44)valid=await db.MaterialRequests.AnyAsync(x=>x.RequestId==Requests[44].RequestId&&x.Purpose==Marker+"Updated purpose");
        else if(n is >=45 and <=50)
        {
            var state=n switch{45=>"PENDING_APPROVAL",46=>"NEEDS_INFORMATION",47=>"APPROVED",48=>"REJECTED",49=>"CANCELLED",50=>"REMAINDER_CLOSED",_=>throw new InvalidOperationException()};
            valid=await db.MaterialRequests.AnyAsync(x=>x.RequestId==Requests[n].RequestId&&x.Status==state);
        }
        else if(n==51)valid=await db.MaterialFulfillments.AnyAsync(x=>x.RequestId==Requests[51].RequestId&&x.FulfilledBy==Users[2].UserId&&x.Status=="ISSUED")&&await db.MaterialRequestLines.AnyAsync(x=>x.RequestId==Requests[51].RequestId&&x.FulfilledQty==2)&&await db.StockMovements.AnyAsync(x=>x.WarehouseId==Warehouse.WarehouseId&&x.SignedBaseQty==-2&&x.CreatedBy==Users[2].UserId&&x.DocumentLine.Document.ReferenceId==response["fulfillmentId"]!.GetValue<long>());
        else if(n==56)valid=await db.MaterialReplenishments.AnyAsync(x=>x.WarehouseId==Warehouse.WarehouseId&&x.MaterialReplenishmentLines.Any(l=>l.ItemId==Items[56].ItemId)&&x.RequestedBy==Users[0].UserId);
        else if(n==57)valid=await db.MaterialReplenishments.AnyAsync(x=>x.ReplenishmentId==Replenishments[57].ReplenishmentId&&x.Status=="CLOSED");
        else if(n is 58 or 59)
        {
            valid=await db.MaterialReplenishmentReceiptLines.AnyAsync(x=>x.ReplenishmentLine.ReplenishmentId==Replenishments[n].ReplenishmentId&&x.StockDocumentLine.DocumentId==Receipts[n].DocumentId)
                &&await db.StockDocuments.AnyAsync(x=>x.DocumentId==Receipts[n].DocumentId&&x.Status=="POSTED");
            if(n==59)valid=valid&&await db.StockMovements.AnyAsync(x=>x.DocumentLine.DocumentId==Receipts[59].DocumentId&&x.SignedBaseQty==3&&x.CreatedBy==Users[2].UserId);
        }
        else throw new InvalidOperationException("Every mutation requires exact persisted effect assertion: "+action);
        if(!valid)throw new InvalidOperationException("Persisted business effect mismatch: "+action);
    }

    internal Task VerifyRead(string action,JsonNode response)
    {
        void Walk(JsonNode? node)
        {
            if(node is JsonObject obj)foreach(var field in obj){if(field.Key.Equals("branchId",StringComparison.OrdinalIgnoreCase)&&field.Value is not null&&field.Value.GetValue<long>()!=Branch.BranchId)throw new InvalidOperationException("Cross-branch DTO disclosure");Walk(field.Value);}
            else if(node is JsonArray array)foreach(var row in array)Walk(row);
        }
        Walk(response);var n=int.Parse(action[^3..]);
        if(n is 2 or 7 or 9 or 12 or 13 or 22 or 34 or 35 or 37 or 52 or 60 or 61)
        {if(response["items"] is not JsonArray {Count:>0})throw new InvalidOperationException("Positive list fixture not observed: "+action);}
        else if(n is 11 or 41 or 55)
        {if(response is not JsonArray {Count:>0})throw new InvalidOperationException("Positive collection fixture not observed: "+action);}
        else if(n==8&&response["items"] is not JsonArray {Count:>0})throw new InvalidOperationException("Positive statistics fixture not observed");
        else if(n==39&&response["audit"] is not JsonArray {Count:>0})throw new InvalidOperationException("Actual material history missing");
        else if(n==54&&response["audit"] is not JsonArray {Count:>0})throw new InvalidOperationException("Actual replenishment history missing");
        var expected=n switch{3=>("handoverId",Handovers[4].HandoverId),10=>("assetId",Assets[18].AssetId),23=>("incidentId",Incidents[19].IncidentId),36=>("maintenanceWorkId",Works[7].MaintenanceWorkId),38 or 40=>("requestId",Requests[51].RequestId),42=>("fulfillmentId",FulfillmentId),53=>("replenishmentId",Replenishments[58].ReplenishmentId),_=>("",0L)};
        if(expected.Item1!=""&&response[expected.Item1]?.GetValue<long>()!=expected.Item2)throw new InvalidOperationException("Wrong DTO root");
        return Task.CompletedTask;
    }
}
