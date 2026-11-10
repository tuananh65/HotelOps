using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.HotelStructure;
internal static class InspectionAssignmentPlanChecks
{
 internal static async Task RunAsync(string output)
 {
  Directory.CreateDirectory(output);var cases=new List<object>();
  void Check(string name,bool ok){if(!ok)throw new Exception(name);cases.Add(new{name,result="PASS"});Console.WriteLine("PASS "+name);}
  const string tag="\"0000000000000001\"";
  var fixedProposal=new ReinspectionConclusionProposal(Guid.NewGuid(),1,2,3,4,5,"COMPLETED",tag,tag,tag,tag,[],DateTime.UtcNow,false,false,false);
  var passed=ReinspectionOperationalPlan.From(fixedProposal);
  Check("All Fixed maps proposed Passed/Rework Completed/task Completed",passed.ProposedInspectionState=="PASSED"&&passed.ProposedReworkState=="COMPLETED"&&passed.ProposedCleaningTaskState=="COMPLETED");
  Check("Ready/Blocked remains original FE04 projection responsibility",passed.RequiresFe04Projection&&!passed.CanApply&&passed.RequiresNotificationContractDecision);
  Check("Actor, source revisions and original/assigned versions retained",passed.ActorUserId==3&&passed.OriginalChecklistVersionId==4&&passed.AssignedChecklistVersionId==5&&passed.InspectionRevision==tag&&passed.ReworkRevision==tag&&passed.BindingRevision==tag);
  var failed=ReinspectionOperationalPlan.From(fixedProposal with{ProposedReworkState="FAILED",NotFixedDefectIds=[10,11]});
  Check("Not Fixed maps proposed Failed/Failed/task Rework Required",failed.ProposedInspectionState=="FAILED"&&failed.ProposedReworkState=="FAILED"&&failed.ProposedCleaningTaskState=="REWORK_REQUIRED"&&!failed.CanApply);
  Check("Next-round link preserves original defect identities only",failed.RemainingOriginalDefectIds.SequenceEqual(new long[]{10,11})&&failed.RequiresNotificationContractDecision);
  foreach(var p in new[]{fixedProposal with{LifecycleApplied=true},fixedProposal with{NextRoundCreationAvailable=true},fixedProposal with{NotFixedDefectIds=[10]},fixedProposal with{ProposedReworkState="FAILED"}}){bool denied=false;try{ReinspectionOperationalPlan.From(p);}catch(InvalidOperationException){denied=true;}Check("Reject inconsistent or activated plan "+cases.Count,denied);}
  foreach(var passedCase in new[]{true,false}){
   bool denied=false;
   try{HotelOps.Api.Services.Housekeeping.InspectionRoomProjectionRules.Evaluate(new(passedCase?"PASSED":"FAILED","COMPLETED",passedCase?"PASS":"FAIL",passedCase?"COMPLETED":"REWORK_REQUIRED",passedCase?"COMPLETED":"IN_PROGRESS","INSPECTION",true,true,false,false,false,true,false));}
   catch(InvalidOperationException){denied=true;}
   Check("Original FE04 adapter cannot infer criterion facts from Fixed/Not Fixed: "+passedCase,denied);
  }
  Check("Assignment notification gate stays closed",typeof(HotelOps.Api.Controllers.InspectionAssignmentCandidateController).IsDefined(typeof(Microsoft.AspNetCore.Mvc.NonControllerAttribute),false));
  await File.WriteAllTextAsync(Path.Combine(output,"plan-results.json"),JsonSerializer.Serialize(new{success=true,operationalApplied=false,cases},new JsonSerializerOptions{WriteIndented=true}));
  Console.WriteLine($"{cases.Count}/{cases.Count} PASS proposed plan only; no authoritative source/projection credit");
 }
}
