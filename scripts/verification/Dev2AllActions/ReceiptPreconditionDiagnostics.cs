using System.Text.Json;
using System.Text.Json.Nodes;
using HotelOps.Api.Controllers.Filters;
using Microsoft.EntityFrameworkCore;
using T=PreconditionTrace;

internal sealed partial class Fixtures
{
    private async Task<TrialCase> Case059(Dev2KnownAction known,CancellationToken ct=default)
    {
        var t=new T(output,"FIX05_A059_CASE_STEPS.json");
        var root=await t.Step("case.fixture.replenishment59","FIXTURE",()=>T.Fixture(Replenishments,59));
        var doc=await t.Step("case.fixture.receipt59","FIXTURE",()=>T.Fixture(Receipts,59));
        var actor=await t.Step("case.fixture.actor2","FIXTURE",()=>Users[2].UserId);
        var receiptLine=await t.Step("case.cardinality.receipt-lines","CARDINALITY",()=>T.One(doc.StockDocumentLines.ToArray(),"NAVIGATION"));
        var target=await t.Step("case.cardinality.replenishment-lines","CARDINALITY",()=>T.One(root.MaterialReplenishmentLines.ToArray(),"NAVIGATION"));
        await using var db=await t.Step("case.context.open","HARNESS",open);
        var rootVersion=await t.Step("case.sql.replenishment-version","SQL",async()=>T.One(await db.MaterialReplenishments.AsNoTracking().Where(x=>x.ReplenishmentId==root.ReplenishmentId).Select(x=>x.RowVersion).Take(2).ToListAsync(ct),"SQL"));
        var receiptVersion=await t.Step("case.sql.receipt-version","SQL",async()=>T.One(await db.StockDocuments.AsNoTracking().Where(x=>x.DocumentId==doc.DocumentId).Select(x=>x.RowVersion).Take(2).ToListAsync(ct),"SQL"));
        var rootText=await t.Step("case.version.replenishment-base64","SERIALIZATION",()=>Convert.ToBase64String(rootVersion));
        var receiptText=await t.Step("case.version.receipt-base64","SERIALIZATION",()=>Convert.ToBase64String(receiptVersion));
        var body=await t.Step("case.json.build","JSON",()=>
        {
            // Same JSON contract as the original A059 case; normalize through the actual wire form.
            var value=new JsonObject{["actorUserId"]=actor};
            value["expectedRowVersion"]=rootText;value["receiptDocumentId"]=JsonSerializer.SerializeToNode(doc.DocumentId);
            value["receiptExpectedRowVersion"]=receiptText;
            value["lines"]=JsonSerializer.SerializeToNode(new[]{new{stockDocumentLineId=receiptLine.LineId,replenishmentLineId=target.ReplenishmentLineId}});
            value["receiptLines"]=JsonSerializer.SerializeToNode(new[]{new{lineId=receiptLine.LineId,acceptedQty=3,rejectedQty=0}});
            return value;
        });
        await t.Complete();return new(known,"/"+known.RouteTemplate.Replace("{id:long}",root.ReplenishmentId.ToString()),body,2);
    }

    internal async Task VerifyReceiptPostingPreconditions(TrialCase c,string phase,CancellationToken ct=default)
    {
        var t=new T(output,"FIX04_A059_PRECONDITIONS_"+phase+".json");
        var receipt=await t.Step("pre.fixture.receipt59","FIXTURE",()=>T.Fixture(Receipts,59));
        var replenishment=await t.Step("pre.fixture.replenishment59","FIXTURE",()=>T.Fixture(Replenishments,59));
        await using var db=await t.Step("pre.context.open","HARNESS",open);
        var doc=await t.Step("pre.sql.receipt","SQL",async()=>T.One(await db.StockDocuments.AsNoTracking().Include(x=>x.StockDocumentLines).Where(x=>x.DocumentId==receipt.DocumentId).Take(2).ToListAsync(ct),"SQL"));
        await t.Document(doc.DocumentId,doc.Status,doc.DocumentType);
        var root=await t.Step("pre.sql.replenishment","SQL",async()=>T.One(await db.MaterialReplenishments.AsNoTracking().Include(x=>x.MaterialReplenishmentLines).Where(x=>x.ReplenishmentId==replenishment.ReplenishmentId).Take(2).ToListAsync(ct),"SQL"));
        var line=await t.Step("pre.cardinality.receipt-lines","CARDINALITY",()=>T.One(doc.StockDocumentLines.ToArray(),"NAVIGATION"));
        var target=await t.Step("pre.cardinality.replenishment-lines","CARDINALITY",()=>T.One(root.MaterialReplenishmentLines.ToArray(),"NAVIGATION"));
        var b=await t.Step("pre.json.body","JSON",()=>c.Body??throw new PreconditionFault("JSON_NULL_BODY"));
        async Task<long> Long(string name)=>await t.Step("pre.json."+name,"JSON",()=>T.Long(T.Field(b,name)));
        async Task<string> Text(string name)=>await t.Step("pre.json."+name,"JSON",()=>T.Text(T.Field(b,name)));
        var documentId=await Long("receiptDocumentId");var actorId=await Long("actorUserId");
        var version=await Text("expectedRowVersion");var receiptVersion=await Text("receiptExpectedRowVersion");
        var mapping=await t.Step("pre.json.lines.single","JSON",()=>T.SingleElement(T.Field(b,"lines")));
        var stockLineId=await t.Step("pre.json.lines.stockDocumentLineId","JSON",()=>T.Long(T.Field(mapping,"stockDocumentLineId")));
        var replenishmentLineId=await t.Step("pre.json.lines.replenishmentLineId","JSON",()=>T.Long(T.Field(mapping,"replenishmentLineId")));
        var receiptInput=await t.Step("pre.json.receiptLines.single","JSON",()=>T.SingleElement(T.Field(b,"receiptLines")));
        var inputLineId=await t.Step("pre.json.receiptLines.lineId","JSON",()=>T.Long(T.Field(receiptInput,"lineId")));
        var accepted=await t.Step("pre.json.receiptLines.acceptedQty","JSON",()=>T.Decimal(T.Field(receiptInput,"acceptedQty")));
        var rejected=await t.Step("pre.json.receiptLines.rejectedQty","JSON",()=>T.Decimal(T.Field(receiptInput,"rejectedQty")));
        await t.Check("pre.invariant.receipt-draft",()=>doc.DocumentType=="RECEIPT"&&doc.Status=="DRAFT");
        await t.Check("pre.invariant.identity-and-warehouse",()=>documentId==doc.DocumentId&&doc.DestinationWarehouseId==root.WarehouseId&&root.WarehouseId==Warehouse.WarehouseId);
        await t.Check("pre.invariant.approval-sod",()=>doc.ApprovedBy==Users[1].UserId&&doc.ApprovedAt is not null&&doc.ApprovedBy!=Users[c.Actor].UserId&&doc.PostedBy is null&&doc.PostedAt is null);
        await t.Check("pre.invariant.requester-poster-separate",()=>root.RequestedBy==Users[0].UserId&&c.Actor==2&&actorId==Users[2].UserId);
        await t.Check("pre.invariant.mapping",()=>stockLineId==line.LineId&&replenishmentLineId==target.ReplenishmentLineId&&inputLineId==line.LineId&&line.ItemId==target.ItemId&&target.ItemId==Items[59].ItemId);
        await t.Check("pre.invariant.versions",()=>doc.RowVersion is {Length:8}&&root.RowVersion is {Length:8}&&receiptVersion==Convert.ToBase64String(doc.RowVersion)&&version==Convert.ToBase64String(root.RowVersion));
        await t.Check("pre.invariant.quantities",()=>line.RequestedQty==3&&line.ActualQty==0&&line.BaseQty==0&&line.ConversionFactor==1&&line.UomId==Uom.UomId&&target.BaseUomId==Uom.UomId&&target.RequestedBaseQty==10&&target.ReceivedBaseQty==0&&accepted==3&&rejected==0);
        await t.Check("pre.invariant.workflow",()=>root.Status=="REQUESTED"&&target.IsOpenCoverage);
        var moved=await t.Step("pre.sql.stock-movement-exists","SQL",()=>db.StockMovements.AnyAsync(x=>x.DocumentLine.DocumentId==doc.DocumentId,ct));
        await t.Check("pre.invariant.not-posted",()=>!moved);
        var linked=await t.Step("pre.sql.receipt-credit-exists","SQL",()=>db.MaterialReplenishmentReceiptLines.AnyAsync(x=>x.StockDocumentLineId==line.LineId,ct));
        await t.Check("pre.invariant.not-linked",()=>!linked);
        var active=await t.Step("pre.sql.active-branch-warehouse","SQL",()=>db.Warehouses.AnyAsync(x=>x.WarehouseId==root.WarehouseId&&x.IsActive&&x.BranchId==Branch.BranchId&&x.Branch.IsActive,ct));
        await t.Check("pre.invariant.active-branch-warehouse",()=>active);
        await t.Complete();
    }
}
