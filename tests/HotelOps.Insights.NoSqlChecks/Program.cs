using System.Text.Json;
using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(1));
await NoSqlChecks.Run(budget.Token);
var projectionChecks=0;
void ProjectCheck(HotelOps.Api.Entities.MaintenanceWork work,long? branch)
{
 var actual=HotelOps.Api.Security.Authorization.MaintenanceWorkSources.Project(new[]{work}.AsQueryable()).Single().BranchId;
 if(actual!=branch)throw new InvalidOperationException("Authoritative Maintenance source projection mismatch");
 projectionChecks++;
}
ProjectCheck(new(){MaintenanceWorkId=1,IncidentId=2,Incident=new(){IncidentId=2,BranchId=3}},3);
ProjectCheck(new(){MaintenanceWorkId=1},null);
ProjectCheck(new(){MaintenanceWorkId=1,IncidentId=2,Incident=new(){IncidentId=2,BranchId=3},PreventiveMaintenanceRuns={new(){RunId=1}}},null);
ProjectCheck(new(){MaintenanceWorkId=1,PreventiveMaintenanceRuns={new(){RunId=1},new(){RunId=2}}},null);
Console.WriteLine($"PASS authoritative Maintenance projection checks={projectionChecks}; no SQL connections.");
internal static class Evidence
{
 internal static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
 internal static Task Save(string name,object value,CancellationToken ct) { ct.ThrowIfCancellationRequested(); Console.WriteLine(JsonSerializer.Serialize(new { name, result=value })); return Task.CompletedTask; }
}
