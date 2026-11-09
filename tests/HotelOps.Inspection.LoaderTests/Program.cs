using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

var passed = 0;
var failed = 0;
var command = new RejectInspectionLoadCommand(5, 1, new("Reject reason", true, "\"0000000000000001\""));
await Check("Valid persisted-shaped load fails closed on missing current-cycle contract", new(), command,
    RoomTypeBatchStatus.Unavailable, "BLOCKED_SPECIFIC_RULE:D3_CURRENT_CYCLE_BINDING");
await Check("Retired assigned version is retained", new(), command, RoomTypeBatchStatus.Unavailable,
    "BLOCKED_SPECIFIC_RULE:D3_CURRENT_CYCLE_BINDING");
await Check("Permission denied prevents reading source", new() { InitialGate = RoomTypeBatchStatus.Forbidden }, command, RoomTypeBatchStatus.Forbidden);
await Check("Invalid session prevents reading source", new() { InitialGate = RoomTypeBatchStatus.SessionInvalid }, command, RoomTypeBatchStatus.SessionInvalid);
await Check("Authorization unavailable fails closed", new() { InitialGate = RoomTypeBatchStatus.Unavailable }, command, RoomTypeBatchStatus.Unavailable);
await Check("Revocation on final read gate fails closed", new() { FinalGate = RoomTypeBatchStatus.Forbidden }, command, RoomTypeBatchStatus.Forbidden);
await Check("Assigned inspector required", new() { WrongInspector = true }, command, RoomTypeBatchStatus.Forbidden);
await Check("Source missing or outside branch scope", new() { Source = null }, command, RoomTypeBatchStatus.NotFound);
await Check("Invalid request identity prevents reads", new(), command with { InspectionId = 0 }, RoomTypeBatchStatus.InvalidReference);
await Check("Requested inspection differs from loaded graph", new(), command with { InspectionId = 99 }, RoomTypeBatchStatus.InvalidReference);
await Check("Stale persisted root revision rejected", new(), command with { Input = command.Input with { ExpectedETag = "\"0000000000000002\"" } }, RoomTypeBatchStatus.Stale);
var wrongRoom = FakeSession.SourceGraph(); wrongRoom.Domain.CleaningTask.RoomId = 999;
await Check("Persisted graph with wrong cycle room rejected", new() { Source = wrongRoom }, command, RoomTypeBatchStatus.InvalidReference);
var wrongVersion = FakeSession.SourceGraph(); wrongVersion.Domain.Criteria[0].VersionId = 999;
await Check("Persisted criterion version mismatch rejected", new() { Source = wrongVersion }, command, RoomTypeBatchStatus.InvalidReference);
await Check("Cross-source evidence rejected", new() { Source = FakeSession.SourceGraph() with { Evidence = [new(6, 999, "INSPECTION", null)] } }, command, RoomTypeBatchStatus.InvalidReference);
var missingTransaction = new FakeSession { TransactionPresent = false };
try { await RejectInspectionLoading.LoadAsync(missingTransaction, command, default); throw new Exception("Transaction accepted"); }
catch (InvalidOperationException) { passed++; Console.WriteLine("PASS Caller transaction required before authorization/read"); }
if (missingTransaction.Reads != 0 || missingTransaction.Gates != 0) throw new Exception("Transaction boundary bypassed");
using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
try { await RejectInspectionLoading.LoadAsync(new FakeSession(), command, cancelled.Token); throw new Exception("Cancellation ignored"); }
catch (OperationCanceledException) { passed++; Console.WriteLine("PASS Cancelled load rejected"); }
// Exercise the real adapter's transaction guard without opening the SQL connection.
await using (var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
    .UseSqlServer("Server=invalid;Database=NeverOpened;Integrated Security=true;TrustServerCertificate=true").Options))
{
    var service = new RoomTypeCreationService(db, null!, null!, null!, TimeProvider.System);
    try { await service.LoadRejectInspectionSourceAsync(null!, 1, command, default); throw new Exception("Real adapter accepted absent transaction"); }
    catch (InvalidOperationException error) when (error.Message.Contains("caller's clean SERIALIZABLE"))
    { passed++; Console.WriteLine("PASS Real SQL adapter rejects absent caller transaction before opening connection"); }
    if (db.Database.GetDbConnection().State != System.Data.ConnectionState.Closed) throw new Exception("SQL connection opened");
}
await Check("Persisted current cycle makes validated source available",new(){Source=FakeSession.SourceGraph() with{CurrentCleaningTaskId=2}},command,RoomTypeBatchStatus.Success);
await Check("Wrong current cycle rejected",new(){Source=FakeSession.SourceGraph() with{CurrentCleaningTaskId=99}},command,RoomTypeBatchStatus.Inactive,"CURRENT_CYCLE_MISMATCH");

var approval = FakeSession.SourceGraph();
approval.Domain.Results[0].Outcome = "PASS";
approval = approval with { Domain = approval.Domain with { Defects = [] }, CurrentCleaningTaskId = 2 };
void ApproveCheck(string name, RejectInspectionSource source, string? expectedError = null, string? tag = "\"0000000000000001\"")
{
    var result = ApproveInspectionValidator.Validate(source, tag);
    if (expectedError == null ? !result.IsValid : !result.Errors.Contains(expectedError))
        throw new Exception(name + ": " + string.Join(",", result.Errors));
    passed++; Console.WriteLine("PASS " + name);
}
ApproveCheck("UC044 mandatory PASS with no defects", approval.Domain);
ApproveCheck("UC044 mandatory FAIL rejected", FakeSession.SourceGraph().Domain, "MANDATORY_PASS_REQUIRED");
ApproveCheck("UC044 missing mandatory result", approval.Domain with { Results = [] }, "MANDATORY_RESULT_REQUIRED");
ApproveCheck("UC044 stale snapshot", approval.Domain, "INSPECTION_REVISION_STALE", "\"0000000000000002\"");
ApproveCheck("UC044 weak tag", approval.Domain, "EXPECTED_REVISION_INVALID", "W/\"0000000000000001\"");
ApproveCheck("UC044 invalid state", approval.Domain with { WorkflowState = "PASSED" }, "INSPECTION_NOT_PENDING_CONCLUSION");
ApproveCheck("UC044 wrong criterion version", approval.Domain with { Criteria = [new() { ItemId = 10, VersionId = 999, IsActive = true, IsRequired = true }] }, "CHECKLIST_SOURCE_INVALID");
ApproveCheck("UC044 duplicate results", approval.Domain with { Results = [approval.Domain.Results[0], approval.Domain.Results[0]] }, "CRITERION_RESULTS_INVALID");
ApproveCheck("UC044 cross-source result", approval.Domain with { Results = [new() { InspectionId = 99, ChecklistItemId = 10, Outcome = "PASS" }] }, "CRITERION_RESULTS_INVALID");
var optional = approval.Domain with { Criteria = [approval.Domain.Criteria[0], new() { ItemId = 11, VersionId = 4, IsActive = true }],
    Results = [approval.Domain.Results[0], new() { InspectionId = 1, ChecklistItemId = 11, Outcome = "FAIL" }],
    Defects = [new() { DefectId = 20, InspectionId = 1, ChecklistItemId = 11, Description = "Optional defect" }] };
ApproveCheck("UC044 optional FAIL with defect follows GBR062 mandatory rule", optional);
ApproveCheck("UC044 optional FAIL still needs defect", optional with { Defects = [] }, "FAILED_CRITERION_DEFECT_REQUIRED");
ApproveCheck("UC044 foreign defect rejected", approval.Domain with { Defects = FakeSession.SourceGraph().Domain.Defects }, "DEFECT_SOURCE_INVALID");
foreach (var scenario in new[] {
    ("UC044 reader valid retired version", new FakeSession { Source = approval }, RoomTypeBatchStatus.Success),
    ("UC044 reader forbidden", new FakeSession { Source = approval, InitialGate = RoomTypeBatchStatus.Forbidden }, RoomTypeBatchStatus.Forbidden),
    ("UC044 reader final auth revoked", new FakeSession { Source = approval, FinalGate = RoomTypeBatchStatus.Forbidden }, RoomTypeBatchStatus.Forbidden),
    ("UC044 reader wrong current cycle", new FakeSession { Source = approval with { CurrentCleaningTaskId = 99 } }, RoomTypeBatchStatus.Inactive),
    ("UC044 reader evidence owner mismatch", new FakeSession { Source = approval with { Evidence = [new(6,99,"INSPECTION",null)] } }, RoomTypeBatchStatus.InvalidReference) })
{
    var result = await RejectInspectionLoading.LoadConclusionAsync(scenario.Item2, 5, 1,
        source => ApproveInspectionValidator.Validate(source, "\"0000000000000001\"").Errors, default);
    if (result.Status != scenario.Item3) throw new Exception(scenario.Item1);
    passed++; Console.WriteLine("PASS " + scenario.Item1);
}
Console.WriteLine($"{passed} PASS / {failed} FAIL — read orchestration doubles, no SQL execution");
if (failed != 0) Environment.ExitCode = 1;

async Task Check(string name, FakeSession session, RejectInspectionLoadCommand request, RoomTypeBatchStatus expected, string? blocker = null)
{
    try
    {
        var result = await RejectInspectionLoading.LoadAsync(session, request, default);
        if (result.Status != expected || result.Blocker != blocker) throw new Exception($"Unexpected {result}");
        if ((session.InitialGate != null || request.InspectionId <= 0) && session.Reads != 0) throw new Exception("Unauthorized read");
        if (session.Reads != 0 && (session.Branch != request.BranchId || session.Inspection != request.InspectionId)) throw new Exception("Request identity not forwarded");
        if (blocker != null && (session.Reads != 1 || session.Gates != 2)) throw new Exception("Missing read/recheck");
        passed++; Console.WriteLine($"PASS {name}");
    }
    catch (Exception error) { failed++; Console.Error.WriteLine($"FAIL {name}: {error.Message}"); }
}

sealed class FakeSession : IRejectInspectionReadSession
{
    public bool TransactionPresent { get; init; } = true;
    public RoomTypeBatchStatus? InitialGate { get; init; }
    public RoomTypeBatchStatus? FinalGate { get; init; }
    public bool WrongInspector { get; init; }
    public RejectInspectionLoadedSource? Source { get; init; } = SourceGraph();
    public int Reads { get; private set; }
    public int Gates { get; private set; }
    public long Branch { get; private set; }
    public long Inspection { get; private set; }
    public void RequireCallerTransaction() { if (!TransactionPresent) throw new InvalidOperationException("Caller transaction required"); }
    public Task<RoomTypeBatchStatus?> AuthorizeAsync(long branch, CancellationToken ct)
        => Task.FromResult(++Gates == 1 ? InitialGate : FinalGate);
    public Task<RejectInspectionLoadedSource?> ReadLockedAsync(long branch, long inspection, CancellationToken ct)
    {
        Reads++; Branch = branch; Inspection = inspection;
        if (WrongInspector) throw new RejectInspectionOwnershipException();
        return Task.FromResult(Source);
    }
    public static RejectInspectionLoadedSource SourceGraph() => new(new(
        new() { InspectionId = 1, CleaningTaskId = 2, RoomId = 3, VersionId = 4, Status = "IN_PROGRESS" },
        new() { CleaningTaskId = 2, RoomId = 3 }, new() { VersionId = 4, Status = "RETIRED" },
        [new() { ItemId = 10, VersionId = 4, IsActive = true, IsRequired = true }],
        [new() { InspectionId = 1, ChecklistItemId = 10, Outcome = "FAIL" }],
        [new() { DefectId = 20, InspectionId = 1, ChecklistItemId = 10, Description = "Unclean surface" }],
        "PENDING_CONCLUSION", [0, 0, 0, 0, 0, 0, 0, 1]), [new(6, 1, "INSPECTION", null)]);
}
