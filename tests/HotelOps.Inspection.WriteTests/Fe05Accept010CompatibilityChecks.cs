#if ACCEPT010_REFERENCE
using System.Text.Json;
using HotelOps.Accept010;
using HotelOps.Sql09;

internal static class Fe05Accept010CompatibilityChecks
{
    internal static async Task RunAsync(string package,string output)
    {
        Directory.CreateDirectory(output);int count=0;
        void Check(string title,bool condition){if(!condition)throw new Exception(title);count++;Console.WriteLine("PASS "+title);}
        var plan=AcceptancePlans.L2(Path.Combine(package,"scripts/verification/Accept010/L2Plan.json"));
        Check("Original ACCEPT010 14 IDs preserved without Oxx translation",plan.Select(p=>p.Id).SequenceEqual(Enumerable.Range(1,14).Select(i=>i.ToString("D2"))));
        foreach(var id in new[]{"07","08","10"})Check("Original both concurrency orders retained for "+id,plan.Single(p=>p.Id==id).Orderings.SequenceEqual(new[]{"A holds then B contends","B holds then A contends"}));
        Check("FE05 owner implements original exact interface",typeof(IAcceptedFe05OwnerCaseDriver).IsAssignableFrom(typeof(Fe05Accept010OwnerDriver)));
        Check("FE05 observer implements original exact interface",typeof(IAcceptedFe05CaseObserver).IsAssignableFrom(typeof(Fe05Accept010CaseObserver)));
        var request=new HotelOps.Api.Services.Notifications.Fe05OperationRequest(Guid.NewGuid(),1,2,new string('A',64),[HotelOps.Api.Services.Notifications.Fe05NotificationKind.InspectionFailed]);
        try{Fe05ActualCaseSqlObservation.RequireCommittedSource(new("FAILED","REWORK_REQUIRED",null,"\"0000000000000001\"",1,1,1,0,JsonSerializer.Serialize(new{contract="SYNTHETIC"}),true,false),request,3);throw new Exception("Synthetic receipt accepted");}
        catch(InvalidOperationException e)when(e.Message=="FE05_OBSERVER_ACTUAL_RECEIPT_AUTHORITY_MISMATCH"){Check("Observer candidate rejects synthetic contract despite self-declared graph flags",true);}
        var gate=AcceptanceGate.Closed();var signed=new SignedGrant("unaccepted",[],[]);var invoked=false;
        IAcceptedFe05OwnerCaseDriver owner=new Fe05Accept010OwnerDriver(gate,signed,(_,_,_)=>{invoked=true;throw new Exception("Unexpected factory");});
        var observer=new Fe05Accept010CaseObserver(gate,signed,(_,_,_)=>{invoked=true;throw new Exception("Unexpected observer factory");});
        var receipts=new List<object>();
        foreach(var p in plan){
            try{await owner.ExecuteAsync(p.Id,null!,default);throw new Exception("Hold bypassed");}
            catch(InvalidOperationException e)when(e.Message=="ACCEPT010_SQL_EXECUTION_HOLD_BEFORE_CONNECTION"){receipts.Add(new{caseId=p.Id,result="NOT_RUN",blocker=e.Message,orderings=p.Orderings});}
        }
        Check("14 original cases refuse before fixture/context factory",!invoked&&receipts.Count==14);
        try{await observer.RequireBindingsBeforeAsync(plan[0],null!,default);throw new Exception("Observer hold bypassed");}
        catch(InvalidOperationException e)when(e.Message=="ACCEPT010_SQL_EXECUTION_HOLD_BEFORE_CONNECTION"){Check("Independent observer also holds before source/connection factory",!invoked);}
        try{await new L2Fe05Driver(gate,owner,observer).ExecuteFutureAsync(AcceptanceGate.Checkpoint,signed,plan[0],default);throw new Exception("Original L2 driver hold bypassed");}
        catch(InvalidOperationException e)when(e.Message=="ACCEPT010_SQL_EXECUTION_HOLD_BEFORE_CONNECTION"){Check("Original L2 runtime gate remains unchanged",!invoked);}
        await File.WriteAllTextAsync(Path.Combine(output,"accept010-l2-results.json"),JsonSerializer.Serialize(new{sourceIdentity="9ca9572711d6cc34c532e9530edd4c602bae615215e3a94a3d12eaf80b91fc1e",sourceStatus="LOCAL_ONLY_UNCOMMITTED",compatibilityChecksPassed=count,sqlPass=0,sqlNotRun=14,l1SqlNotRun=24,fixtureFactoryInvoked=invoked,actualCaseBindingsComplete=false,results=receipts},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"{count}/{count} PASS contract/hold checks; L1 24 NOT_RUN, L2 14 NOT_RUN (refusals are not SQL PASS)");
    }
}
#endif
