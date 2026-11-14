using System.Text.Json;
using System.Text.Json.Nodes;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class PreconditionTraceVerification
{
    internal static async Task<int> Run(string output)
    {
        var assertions=0;void Check(bool value,string label){if(!value)throw new InvalidOperationException(label);assertions++;}
        var b=new JsonObject{["actorUserId"]=3L};
        void Set(string key,object value)=>b[key]=JsonSerializer.SerializeToNode(value);
        Set("receiptDocumentId",220L);Set("expectedRowVersion",Convert.ToBase64String(new byte[8]));Set("receiptExpectedRowVersion",Convert.ToBase64String(new byte[8]));
        Set("lines",new[]{new{stockDocumentLineId=1L,replenishmentLineId=2L}});Set("receiptLines",new[]{new{lineId=1L,acceptedQty=3,rejectedQty=0}});
        Check(b["receiptLines"]![0]!["acceptedQty"]!.GetValue<decimal>()==3,"Original Cases SerializeToNode integer supports GetValue decimal; not evidence of historical numeric failure");
        Check(b["actorUserId"]!.GetValue<long>()==3&&b["receiptDocumentId"]!.GetValue<long>()==220,"Original scalar IDs supported");
        foreach(var value in new JsonNode[]{JsonValue.Create(3)!,JsonValue.Create(3L)!,JsonValue.Create(3m)!,JsonNode.Parse("3")!})Check(PreconditionTrace.Decimal(value)==3,"JSON number representations preserve exact decimal value");
        string? originalException=null;
        try{_ = JsonSerializer.Serialize(new{status="PASS",phase="before-negatives",documentId=220L,Status="DRAFT",DocumentType="RECEIPT",checks=new{valid=true}},new JsonSerializerOptions{WriteIndented=true});}
        catch(InvalidOperationException error){originalException=error.GetType().Name;Check(error.Message.Contains("Status")&&error.Message.Contains("status")&&error.Message.Contains("constructor"),"Exact original status/Status constructor-binding collision reproduced");}
        if(originalException is null)throw new Exception("Original failure was not reproduced");
        await File.WriteAllTextAsync(Path.Combine(output,"FIX05_ORIGINAL_SERIALIZATION_REPRO.json"),JsonSerializer.Serialize(new{operation="JsonSerializer.Serialize(old precondition receipt anonymous type)",expectedException="InvalidOperationException",actualException=originalException,conflictingMembers=new[]{"status","Status"},constructorParameter="status",jsonNumericRead="PASS",sqlConnections=0}));
        var tests=new (string name,string category,Func<object> work)[]{
            ("missing","JSON_MISSING_FIELD",()=>PreconditionTrace.Field(b,"missing")),
            ("null","JSON_NULL_FIELD",()=>PreconditionTrace.Field(new JsonObject{["x"]=null},"x")),
            ("wrong-type","JSON_WRONG_TYPE",()=>PreconditionTrace.Decimal(JsonValue.Create("3")!)),
            ("wrong-id-type","JSON_WRONG_TYPE",()=>PreconditionTrace.Long(JsonValue.Create(true)!)),
            ("array-empty","JSON_CARDINALITY",()=>PreconditionTrace.SingleElement(new JsonArray())),
            ("sql-no-row","SQL_NO_ROW",()=>PreconditionTrace.One(Array.Empty<int>(),"SQL")),
            ("sql-multiple","SQL_MULTIPLE_ROWS",()=>PreconditionTrace.One(new[]{1,2},"SQL")),
            ("navigation-multiple","NAVIGATION_MULTIPLE_ROWS",()=>PreconditionTrace.One(new[]{1,2},"NAVIGATION")),
            ("fixture-missing","FIXTURE_MISSING",()=>PreconditionTrace.Fixture(new Dictionary<int,int>(),59)),
            ("unknown-operation","HARNESS_OPERATION",()=>throw new InvalidOperationException("SECRET_MARKER_MUST_NOT_PERSIST"))
        };
        foreach(var test in tests)
        {
            var file="FIX05_TEST_"+test.name+".json";var trace=new PreconditionTrace(output,file);
            await trace.Step("previous-passed","HARNESS",()=>true);
            try{await trace.Step("exact-failing-step","HARNESS",()=>
                {var started=JsonNode.Parse(File.ReadAllText(Path.Combine(output,file)))!;Check(started["steps"]![1]!["Status"]!.GetValue<string>()=="STARTED","STARTED persisted before work");return test.work();});throw new Exception("Must fail closed");}
            catch(PreconditionStepFailure error)
            {
                Check(error.Step=="exact-failing-step"&&error.Category==test.category,"Named category retained");
                var diagnostic=FailureDiagnostics.Capture(error,"A059",null,null,null,output);Check(diagnostic.FailedOperation==error.Step&&diagnostic.ValidationCategory==test.category,"Existing failure infrastructure retains exact operation/category");
            }
            var text=await File.ReadAllTextAsync(Path.Combine(output,file));var result=JsonNode.Parse(text)!;
            Check(result["status"]!.GetValue<string>()=="FAIL"&&result["steps"]![0]!["Status"]!.GetValue<string>()=="PASS"&&result["steps"]![1]!["Status"]!.GetValue<string>()=="FAIL","Partial earlier PASS plus later FAIL persisted");
            Check(!text.Contains("SECRET_MARKER_MUST_NOT_PERSIST"),"Exception payload never persisted");
        }
        var good=new PreconditionTrace(output,"FIX05_TEST_pass.json");await good.Document(220,"DRAFT","RECEIPT");await good.Check("invariant",()=>true);await good.Complete();var goodJson=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(output,"FIX05_TEST_pass.json")))!;Check(goodJson["status"]!.GetValue<string>()=="PASS","Only explicit completion grants precondition PASS");Check(goodJson["document"]!["documentStatus"]!.GetValue<string>()=="DRAFT","Receipt outcome and documentStatus serialize without constructor collision");
        var bad=new PreconditionTrace(output,"FIX05_TEST_invariant.json");try{await bad.Check("wrong-state",()=>false);throw new Exception("Invariant must fail");}catch(PreconditionStepFailure e){Check(e.Category=="BUSINESS_INVARIANT","False business rule is not a successful check");}
        await using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer("Server=lpc:KIENPC;Database=HotelOps_DEV2_FullStack_Trial;Integrated Security=true;Encrypt=false").Options);
        var receipt=new StockDocument{DocumentId=220};var root=new MaterialReplenishment{ReplenishmentId=65};
        Check(db.StockDocuments.AsNoTracking().Include(x=>x.StockDocumentLines).Where(x=>x.DocumentId==receipt.DocumentId).ToQueryString().Contains("stock_documents"),"Receipt query translates offline");
        Check(db.MaterialReplenishments.AsNoTracking().Include(x=>x.MaterialReplenishmentLines).Where(x=>x.ReplenishmentId==root.ReplenishmentId).ToQueryString().Contains("material_replenishment"),"Replenishment query translates offline");
        await File.WriteAllTextAsync(Path.Combine(output,"FIX05_DIAGNOSTIC_VERIFICATION.json"),JsonSerializer.Serialize(new{status="PASS",assertions,originalNumericRead="PASS_NOT_CAUSE",oldReceiptSerialization="INVALID_OPERATION_STATUS_CONSTRUCTOR_COLLISION_REPRODUCED",sqlConnections=0,exactSourceFailedOperation="JsonSerializer.Serialize(new { status, ..., doc.Status, ... })",scope="Original serializer exception reproduced; corrected metadata, parser/cardinality/partial persistence/fail-closed and SQL translation tested; real SQL acceptance NOT_RUN"},new JsonSerializerOptions{WriteIndented=true}));
        return assertions;
    }
}
