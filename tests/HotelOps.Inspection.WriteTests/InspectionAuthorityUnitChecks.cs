using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.EntityFrameworkCore;

internal static class InspectionAuthorityUnitChecks
{
    internal static async Task RunAsync()
    {
        var count=0;
        var binding=new ReworkAuthorityBinding(1,2,3,4,5,6,[0,0,0,0,0,0,0,1]);
        var snapshot=new ReworkAuthoritySnapshot(binding,3,true,new byte[32],new byte[32]);
        var operation=Guid.NewGuid();
        void Check(string name,Action run){run();count++;Console.WriteLine("PASS "+name);}
        void Reject(string name,ReworkAuthoritySnapshot value,long actor=7,Guid? op=null,long transaction=42)
            =>Check(name,()=>{try{RoomTypeCreationService.PrepareAuthorityPayload(value,actor,op??operation,transaction);}catch(InvalidOperationException){return;}throw new Exception("Accepted invalid payload");});
        var prepared=RoomTypeCreationService.PrepareAuthorityPayload(snapshot,7,operation,42);
        Check("R01 issuer/origin/version exact",()=>{
            var v=prepared.Receipt;
            if(v.Issuer!="FE05.UC045.UC046"||v.Origin!="FE05_COMMITTED_V1"||v.Version!=1||v.IssuerVersion!=1||v.Contract!="FE05_REWORK_AUTHORITY_V1")throw new Exception("Contract mismatch");
        });
        Check("R01 receipt JSON uses consumer field names and formats",()=>{
            using var json=JsonDocument.Parse(JsonSerializer.Serialize(prepared.Receipt,JsonSerializerOptions.Web));var v=json.RootElement;
            foreach(var field in new[]{"contract","origin","version","issuer","issuerVersion","actorUserId","operationId","writerTransactionId","reworkId","inspectionId","cleaningTaskId","roomId","branchId","versionId","inspectionETag","sourceDigest","evidenceDigest","conclusion"})if(!v.TryGetProperty(field,out _))throw new Exception(field);
            if(v.GetProperty("inspectionETag").GetString()!="\"0000000000000001\""||v.GetProperty("sourceDigest").GetString()!.Length!=64)throw new Exception("Encoding mismatch");
        });
        Check("Audit fields match consumer receipt",()=>{if(prepared.Audit.OperationId!=operation||prepared.Audit.AuthorityContract!=prepared.Receipt.Contract||prepared.Audit.SourceDigest!=prepared.Receipt.SourceDigest)throw new Exception("Audit mismatch");});
        Reject("Reject invalid actor",snapshot,actor:0);
        Reject("Reject empty operation",snapshot,op:Guid.Empty);
        Reject("Reject invalid writer transaction",snapshot,transaction:0);
        Reject("Reject wrong cycle",snapshot with{CurrentCycleId=99});
        Reject("Reject nonfailed source",snapshot with{CurrentFailedSource=false});
        Reject("Reject invalid revision",snapshot with{Binding=binding with{InspectionRevision=new byte[7]}});
        Reject("Reject incomplete source digest",snapshot with{SourceDigest=new byte[31]});
        Reject("Reject incomplete evidence digest",snapshot with{EvidenceDigest=new byte[31]});
        Reject("Reject invalid persisted identity",snapshot with{Binding=binding with{BranchId=0}});
        Check("Prepared receipt never proves commit in its writer transaction",()=>{
            var proof=new ReworkAuthorityProof(binding,ReworkAuthorityContract.Origin,1,ReworkAuthorityContract.Issuer,1,7,operation,42,snapshot.SourceDigest,snapshot.EvidenceDigest);
            var observed=new ReworkAuthorityObservation(binding,3,true,true,true,true,true,42,snapshot.SourceDigest,snapshot.EvidenceDigest);
            if(ReworkAuthorityContract.Validate(proof,observed)!="REWORK_AUTHORITY_NOT_COMMITTED")throw new Exception("Uncommitted receipt accepted");
        });
        await using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer("Server=invalid;Database=NeverOpened;Integrated Security=true;TrustServerCertificate=true").Options);
        var service=new RoomTypeCreationService(db,null!,null!,null!,TimeProvider.System);
        try{await service.PrepareReworkAuthorityInCallerTransactionAsync(null!,1,db.Database.GetDbConnection(),null!,5,2,1,"\"0000000000000001\"",operation,[1],null,default);throw new Exception("Missing transaction accepted");}
        catch(InvalidOperationException e)when(e.Message=="INSPECTION_CALLER_SERIALIZABLE_TRANSACTION_REQUIRED"){count++;Console.WriteLine("PASS Real caller adapter rejects absent transaction before SQL");}
        if(db.Database.GetDbConnection().State!=System.Data.ConnectionState.Closed)throw new Exception("Unexpected SQL open");
        Console.WriteLine($"{count}/{count} PASS — payload/guard unit checks, not SQL authority verification");
    }
}
