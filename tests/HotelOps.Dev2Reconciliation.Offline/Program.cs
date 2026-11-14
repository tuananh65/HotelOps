using System.Reflection;
using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.MaterialRequests;
int checks=0;
void Check(bool result){if(!result)throw new Exception("Reconciliation regression failed");checks++;}
object? Rule(string name,params object[] args){try{return typeof(MaterialRequestService).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException!;}}
void Invalid(Action action){try{action();throw new Exception("Invalid correlation accepted");}catch(ValidationException){checks++;}}
foreach(var status in new[]{"DRAFT","NEEDS_INFORMATION","PENDING_APPROVAL","APPROVED","CANCELLED","FULLY_ISSUED","PARTIALLY_ISSUED"}){
 Check((bool)Rule("RequiresCycle",status,true)! == (status is "DRAFT" or "NEEDS_INFORMATION" or "PENDING_APPROVAL" or "APPROVED"));
 Check((bool)Rule("RequiresCycle",status,false)! == (status is "PENDING_APPROVAL" or "APPROVED"));
}
MaterialRequest Request()=>new(){RequestId=1,SourceWarehouseId=2,MaterialRequestLines=[new(){RequestId=1,LineId=3,LineNo=1,ItemId=4,UomId=5,RequestedQty=10,ApprovedQty=8,FulfilledQty=2,IsActive=true}]};
MaterialFulfillment Batch()=>new(){FulfillmentId=6,RequestId=1,WarehouseId=2,StockDocumentId=7,Status="ISSUED",MaterialFulfillmentLines=[new(){FulfillmentId=6,RequestLineId=3,ItemId=4,UomId=5,IssuedQty=2,StockDocumentLineId=8}],StockDocument=new(){DocumentId=7,DocumentType="ISSUE",Status="POSTED",ReferenceType="MATERIAL_FULFILLMENT",ReferenceId=6,SourceWarehouseId=2,StockDocumentLines=[new(){LineId=8,DocumentId=7,ItemId=4,UomId=5,ActualQty=2,ConversionFactor=1,BaseQty=2}]}};
Rule("ValidateFulfillments",Request(),new[]{Batch()});checks++;
foreach(var change in new Action<MaterialFulfillment>[] {f=>f.RequestId=9,f=>f.StockDocument!.DocumentId=9,f=>f.StockDocument!.ReferenceId=9,f=>f.MaterialFulfillmentLines.First().FulfillmentId=9,f=>f.StockDocument!.StockDocumentLines.First().DocumentId=9,f=>f.StockDocument!.StockDocumentLines.First().BaseQty=3,f=>f.StockDocument!.StockDocumentLines.First().ConversionFactor=0,f=>f.MaterialFulfillmentLines.Clear()}){var b=Batch();change(b);Invalid(()=>Rule("ValidateFulfillments",Request(),new[]{b}));}
Invalid(()=>Rule("ValidateFulfillments",Request(),new[]{Batch(),Batch()}));
var request=Request();request.MaterialRequestLines.First().FulfilledQty=3;Invalid(()=>Rule("ValidateFulfillments",request,new[]{Batch()}));
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{status="PASS_OFFLINE_ONLY",assertions=checks,sqlConnections=0,source="Integration pure invariant fragments composed with Demo business authority",businessSqlAcceptance="NOT_RUN_AUTHORIZATION_REQUIRED"}));