using System.Text.Json;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Housekeeping;

var passed = 0;
var failed = 0;
var input = new RejectInspectionInput(" Inspection failed ", true, "\"0000000000000001\"");

Run("Complete failed inspection is eligible", () => Valid(Source(), input));
Run("Optional unanswered criterion is allowed", () => Valid(Source(), input));
Run("Mandatory PASS with optional FAIL may be rejected", () =>
{
    var source = Source();
    source.Defects[0].ChecklistItemId = 11;
    Valid(source with { Results = [
        new() { InspectionId = 1, ChecklistItemId = 10, Outcome = "PASS" },
        new() { InspectionId = 1, ChecklistItemId = 11, Outcome = "FAIL" }
    ] }, input);
});
foreach (var state in new string?[] { null, "ASSIGNED", "INSPECTING", "PASSED", "FAILED", "CANCELLED" })
    Run($"Reject invalid state {state ?? "NULL"}", () => Invalid(Source() with { WorkflowState = state }, input, "INSPECTION_NOT_PENDING_CONCLUSION"));
foreach (var reason in new string?[] { null, "", "  ", "bad\nreason", new('x', 501) })
    Run($"Reject invalid reason {reason?.Length}", () => Invalid(Source(), input with { Reason = reason }, "REJECT_REASON_INVALID"));
Run("Existing 500 character reason boundary", () => Valid(Source(), input with { Reason = new('x', 500) }));
foreach (var confirm in new bool?[] { null, false })
    Run($"Reject confirmation {confirm}", () => Invalid(Source(), input with { Confirmed = confirm }, "REJECT_CONFIRMATION_REQUIRED"));
Run("Missing mandatory result", () => Invalid(Source() with { Results = [] }, input, "MANDATORY_RESULT_REQUIRED"));
Run("Fail without defect", () => Invalid(Source() with { Defects = [] }, input, "FAILED_CRITERION_DEFECT_REQUIRED"));
Run("All PASS cannot justify Reject", () =>
{
    var source = Source() with { Defects = [] };
    source.Results[0].Outcome = "PASS";
    Invalid(source, input, "REJECT_DEFECT_REQUIRED");
});
Case("Wrong cleaning cycle", s => s.Inspection.CleaningTaskId = 999, "INSPECTION_SOURCE_MISMATCH");
Case("Coarse completed status cannot be pending conclusion", s => s.Inspection.Status = "COMPLETED", "INSPECTION_CONCLUSION_STATE_INCONSISTENT");
Case("Preexisting result cannot be pending conclusion", s => s.Inspection.Result = "FAIL", "INSPECTION_CONCLUSION_STATE_INCONSISTENT");
Case("Wrong room", s => s.CleaningTask.RoomId = 999, "INSPECTION_SOURCE_MISMATCH");
Case("Wrong version", s => s.Version.VersionId = 999, "INSPECTION_SOURCE_MISMATCH");
Case("Cross-version criterion", s => s.Criteria[0].VersionId = 999, "CHECKLIST_SOURCE_INVALID");
Case("Foreign inspection result", s => s.Results[0].InspectionId = 999, "CRITERION_RESULTS_INVALID");
Case("Foreign criterion result", s => s.Results[0].ChecklistItemId = 999, "CRITERION_RESULTS_INVALID");
Case("Invalid result outcome", s => s.Results[0].Outcome = "Fixed", "CRITERION_RESULTS_INVALID");
Case("Foreign inspection defect", s => s.Defects[0].InspectionId = 999, "DEFECT_SOURCE_INVALID");
Case("Defect on another criterion", s => s.Defects[0].ChecklistItemId = 11, "DEFECT_SOURCE_INVALID");
Case("Blank defect description", s => s.Defects[0].Description = " ", "DEFECT_SOURCE_INVALID");
Case("Inactive mandatory criterion cannot supply a result", s => s.Criteria[0].IsActive = false, "CRITERION_RESULTS_INVALID");
Run("Duplicate criterion results", () =>
{
    var source = Source();
    Invalid(source with { Results = [source.Results[0], source.Results[0]] }, input, "CRITERION_RESULTS_INVALID");
});
Run("Every failed criterion needs its own valid association", () =>
{
    var source = Source();
    Invalid(source with { Results = [source.Results[0], new() { InspectionId = 1, ChecklistItemId = 11, Outcome = "FAIL" }] },
        input, "FAILED_CRITERION_DEFECT_REQUIRED");
});
Run("Missing checklist", () => Invalid(Source() with { Criteria = [] }, input, "CHECKLIST_SOURCE_INVALID"));
Run("Stale rowversion", () => Invalid(Source(), input with { ExpectedETag = "\"0000000000000002\"" }, "INSPECTION_REVISION_STALE"));
foreach (var tag in new string?[] { null, "", "W/\"0000000000000001\"", "*", "\"000000000000000Z\"", "\"0000000000000001\",\"0000000000000002\"" })
    Run($"Invalid ETag {tag}", () => Invalid(Source(), input with { ExpectedETag = tag }, "EXPECTED_REVISION_INVALID"));
Run("Missing source revision", () => Invalid(Source() with { Revision = [] }, input, "INSPECTION_REVISION_INVALID"));
Run("Validation leaves entities and request unchanged", () =>
{
    var source = Source();
    var before = JsonSerializer.Serialize(new { source, input });
    Valid(source, input);
    if (before != JsonSerializer.Serialize(new { source, input })) throw new Exception("Input mutated");
});
Console.WriteLine($"{passed} PASS / {failed} FAIL — in-memory domain checks only");
if (failed != 0) Environment.ExitCode = 1;

void Case(string name, Action<RejectInspectionSource> change, string error) => Run(name, () =>
{
    var source = Source();
    change(source);
    Invalid(source, input, error);
});
void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception error) { failed++; Console.Error.WriteLine($"FAIL {name}: {error.Message}"); }
}
static void Valid(RejectInspectionSource source, RejectInspectionInput input)
{
    var result = RejectInspectionValidator.Validate(source, input);
    if (!result.IsValid) throw new Exception(string.Join(", ", result.Errors));
}
static void Invalid(RejectInspectionSource source, RejectInspectionInput input, string expected)
{
    var result = RejectInspectionValidator.Validate(source, input);
    if (result.IsValid || !result.Errors.Contains(expected)) throw new Exception($"Expected {expected}; actual {string.Join(", ", result.Errors)}");
}
static RejectInspectionSource Source() => new(
    new() { InspectionId = 1, CleaningTaskId = 2, RoomId = 3, VersionId = 4, Status = "IN_PROGRESS" },
    new() { CleaningTaskId = 2, RoomId = 3 },
    new() { VersionId = 4 },
    [new() { ItemId = 10, VersionId = 4, IsActive = true, IsRequired = true },
     new() { ItemId = 11, VersionId = 4, IsActive = true }],
    [new() { InspectionId = 1, ChecklistItemId = 10, Outcome = "FAIL" }],
    [new() { DefectId = 20, InspectionId = 1, ChecklistItemId = 10, Description = "Unclean surface" }],
    "PENDING_CONCLUSION", [0, 0, 0, 0, 0, 0, 0, 1]);
