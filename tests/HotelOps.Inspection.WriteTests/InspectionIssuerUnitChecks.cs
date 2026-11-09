using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
internal static class InspectionIssuerUnitChecks
{
    internal static void Run()
    {
        var count=0;var id=Guid.NewGuid();const string tag="\"0000000000000001\"";
        var input=new RejectInspectionInput("Reason",true,tag);
        var reject=InspectionIssuerOperation.Reject(id,1,2,3,tag,input);
        void Check(string title,bool condition){if(!condition)throw new Exception(title);count++;Console.WriteLine("PASS "+title);}
        Check("Canonical identity deterministic",reject.Fingerprint.SequenceEqual(InspectionIssuerOperation.Reject(id,1,2,3,tag,input).Fingerprint));
        Check("Canonical reason normalized",reject.Fingerprint.SequenceEqual(InspectionIssuerOperation.Reject(id,1,2,3,tag,input with{Reason=" Reason "}).Fingerprint));
        Check("Revision bound",!reject.Fingerprint.SequenceEqual(InspectionIssuerOperation.Reject(id,1,2,3,"\"0000000000000002\"",input).Fingerprint));
        Check("Branch bound",!reject.Fingerprint.SequenceEqual(InspectionIssuerOperation.Reject(id,1,20,3,tag,input).Fingerprint));
        Check("Actor bound",!reject.Fingerprint.SequenceEqual(InspectionIssuerOperation.Reject(id,10,2,3,tag,input).Fingerprint));
        Check("Confirmation bound",!reject.Fingerprint.SequenceEqual(InspectionIssuerOperation.Reject(id,1,2,3,tag,input with{Confirmed=false}).Fingerprint));
        var request=new RequestReworkRequest(4,DateTime.UtcNow.AddHours(2),[8,9],"Reason");
        var rework=InspectionIssuerOperation.Rework(id,1,2,3,tag,request);
        Check("UC045 and UC046 operation identities separated",!reject.Fingerprint.SequenceEqual(rework.Fingerprint));
        Check("Selected defect set canonical",rework.Fingerprint.SequenceEqual(InspectionIssuerOperation.Rework(id,1,2,3,tag,request with{DefectIds=[9,8]}).Fingerprint));
        Check("Assignee bound",!rework.Fingerprint.SequenceEqual(InspectionIssuerOperation.Rework(id,1,2,3,tag,request with{AssigneeId=40}).Fingerprint));
        Check("Due bound",!rework.Fingerprint.SequenceEqual(InspectionIssuerOperation.Rework(id,1,2,3,tag,request with{DueAt=request.DueAt.AddMinutes(1)}).Fingerprint));
        try{InspectionIssuerOperation.Reject(Guid.Empty,1,2,3,tag,input);throw new Exception("Empty operation accepted");}catch(ArgumentException){count++;Console.WriteLine("PASS Empty operation refused");}
        Console.WriteLine($"{count}/{count} PASS — candidate canonical request unit checks, not replay acceptance");
    }
}
