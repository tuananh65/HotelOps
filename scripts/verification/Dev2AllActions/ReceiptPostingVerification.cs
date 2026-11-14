using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal sealed partial class Fixtures
{
    internal async Task VerifyReceiptPostingEffect(TrialCase c,CancellationToken ct=default)
    {
        await using var db=open();var doc=await db.StockDocuments.AsNoTracking().SingleAsync(x=>x.DocumentId==Receipts[59].DocumentId,ct);
        var root=await db.MaterialReplenishments.AsNoTracking().Include(x=>x.MaterialReplenishmentLines).SingleAsync(x=>x.ReplenishmentId==Replenishments[59].ReplenishmentId,ct);
        var line=await db.StockDocumentLines.AsNoTracking().SingleAsync(x=>x.DocumentId==doc.DocumentId,ct);
        var movement=await db.StockMovements.AsNoTracking().Where(x=>x.DocumentLine.DocumentId==doc.DocumentId).ToListAsync(ct);
        var links=await db.MaterialReplenishmentReceiptLines.AsNoTracking().Where(x=>x.ReplenishmentLine.ReplenishmentId==root.ReplenishmentId).ToListAsync(ct);
        var checks=new Dictionary<string,bool>{
            ["posted-with-approval-sod"]=doc.Status=="POSTED"&&doc.ApprovedBy==Users[1].UserId&&doc.PostedBy==Users[2].UserId&&doc.PostedAt is not null,
            ["rowversions-changed"]=Convert.ToBase64String(doc.RowVersion)!=c.Body!["receiptExpectedRowVersion"]!.GetValue<string>()&&Convert.ToBase64String(root.RowVersion)!=c.Body!["expectedRowVersion"]!.GetValue<string>(),
            ["stock"]=movement.Count==1&&movement[0].SignedBaseQty==3&&movement[0].CreatedBy==Users[2].UserId&&movement[0].WarehouseId==Warehouse.WarehouseId&&line.ActualQty==3&&line.BaseQty==3,
            ["credit"]=links.Count==1&&links[0].StockDocumentLineId==line.LineId&&links[0].CreditedBaseQty==3&&links[0].LinkedBy==Users[2].UserId,
            ["workflow"]=root.Status=="PARTIALLY_RECEIVED"&&root.MaterialReplenishmentLines.Single().ReceivedBaseQty==3&&root.MaterialReplenishmentLines.Single().IsOpenCoverage,
            ["inventory-audit"]=await db.AuditLogs.AnyAsync(x=>x.EntityType=="stock_documents"&&x.EntityId==doc.DocumentId.ToString()&&x.ActorUserId==Users[2].UserId,ct),
            ["replenishment-audit"]=await db.AuditLogs.AnyAsync(x=>x.EntityType=="material_replenishments"&&x.EntityId==root.ReplenishmentId.ToString()&&x.ActorUserId==Users[2].UserId,ct)
        };
        await File.WriteAllTextAsync(Path.Combine(output,"FIX04_A059_POSITIVE_SQL.json"),JsonSerializer.Serialize(new{status=checks.Values.All(x=>x)?"PASS":"FAIL",checks},new JsonSerializerOptions{WriteIndented=true}),ct);
        if(checks.Values.Any(x=>!x))throw new InvalidOperationException("A059 SQL posting effects failed");
    }

}

internal static class ReceiptPostingVerification
{
    internal static int Run()
    {
        var assertions=0;void Check(bool value,string name){if(!value)throw new InvalidOperationException(name);assertions++;}
        var fixtures=new Fixtures(()=>throw new InvalidOperationException("Offline must not open SQL"),"")
        {Warehouse=new(){WarehouseId=10},Uom=new(){UomId=20},Users=[new(){UserId=1},new(){UserId=2},new(){UserId=3}]};
        fixtures.Items[59]=new(){ItemId=59};fixtures.Items[58]=new(){ItemId=58};
        var factory=typeof(Fixtures).GetMethod("Receipt",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var draft=(StockDocument)factory.Invoke(fixtures,[59,"DRAFT",3m])!;var posted=(StockDocument)factory.Invoke(fixtures,[58,"POSTED",3m])!;
        Check(draft.Status=="DRAFT"&&draft.DocumentType=="RECEIPT","Actual receipt factory supports Inventory DRAFT input");
        Check(draft.ApprovedBy==2&&draft.ApprovedAt is not null,"Approval identity retained independently of document status");
        Check(draft.ApprovedBy!=fixtures.Users[2].UserId,"Approver cannot be poster");
        Check(draft.PostedBy is null&&draft.PostedAt is null,"Not posted before fault/positive test");
        var line=draft.StockDocumentLines.Single();Check(line.ItemId==59&&line.UomId==20&&draft.DestinationWarehouseId==10,"Exact item/UOM/warehouse");
        Check(line.RequestedQty==3&&line.ActualQty==0&&line.BaseQty==0&&line.ConversionFactor==1,"DRAFT quantities");
        Check(posted.Status=="POSTED"&&posted.PostedBy==3&&posted.StockDocumentLines.Single().ActualQty==3,"A058 POSTED fixture retained");
        Check(HostAcceptance.AllowedFocus("A004A-059")&&HostAcceptance.AllowedFocus("A004A-001"),"Exact allowlisted focuses");
        Check(!HostAcceptance.AllowedFocus("A004A-058")&&!HostAcceptance.AllowedFocus("ALL")&&!HostAcceptance.AllowedFocus("A004A-059/../"),"Unknown focus cannot start SQL");
        return assertions;
    }
}
