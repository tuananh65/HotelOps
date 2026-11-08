using HotelOps.Api.Services.TrialPreparation;

var checks = new List<string>();
void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
void Denies(Action action, string name) { try { action(); } catch (ArgumentException) { checks.Add(name); return; } throw new InvalidOperationException(name); }
var actor = new ScopedActor(7, 2, true, true, true, true);
Check(ScopedReadPreconditions.CanConsiderHandover(actor, 2, 7), "Explicit current designated receiver passes candidate preconditions only");
foreach (var denied in new[] { actor with { Active = false }, actor with { SessionValid = false }, actor with { SourceReadGranted = false }, actor with { ActionGranted = false }, actor with { BranchId = 3 }, actor with { UserId = 8 } })
    Check(!ScopedReadPreconditions.CanConsiderHandover(denied, 2, 7), "Inactive/revoked/foreign/unauthorized receiver denied");
Check(!ScopedReadPreconditions.CanConsiderHandover(actor, 2, null), "Proposal creator is not an implicit receiver");
Check(ScopedReadPreconditions.CanRead(actor, 2) && !ScopedReadPreconditions.CanRead(actor with { SourceReadGranted = false }, 2), "Evidence permission revocation evaluated on each read");
Check(!ScopedReadPreconditions.CanConsiderResponsibility(actor, 2, true, false, true), "Revoked department eligibility denied");
Check(!ScopedReadPreconditions.CanConsiderResponsibility(actor, 2, true, true, false), "Changed location eligibility denied");
var policy = new EvidenceContentPolicy(10, new HashSet<string>(StringComparer.Ordinal) { "image/png" });
Check(EvidenceContentValidation.Accepts(10, "image/png", policy), "Explicit size boundary accepted");
foreach (var input in new[] { (11L, "image/png"), (0L, "image/png"), (-1L, "image/png"), (1L, "text/html"), (1L, "image/png;script") })
    Check(!EvidenceContentValidation.Accepts(input.Item1, input.Item2, policy), "Unsafe media or size denied");
Check(!EvidenceContentValidation.Accepts(1, "image/png", null), "No approved content policy means unavailable");
byte[] version = [1, 2, 3, 4, 5, 6, 7, 8];
var tag = "\"" + Convert.ToBase64String(version) + "\"";
Check(StrongVersionPreconditions.Evaluate(tag, version) == VersionPrecondition.ReadyForAuthorizedWriter, "Exact durable version match");
Check(StrongVersionPreconditions.Evaluate(tag, []) == VersionPrecondition.Unavailable, "Absent durable mapping never fabricates Asset version");
Check(StrongVersionPreconditions.Evaluate(tag, [2, 2, 3, 4, 5, 6, 7, 8]) == VersionPrecondition.Stale, "Old version denied even when business content returns to same value (ABA)");
Check(StrongVersionPreconditions.Evaluate(null, version) == VersionPrecondition.Missing, "Missing If-Match denied");
foreach (var invalid in new[] { "*", "W/" + tag, tag + "," + tag, "\"%%%\"", "\"\"", "\"AQI= \"" })
    Check(StrongVersionPreconditions.Evaluate(invalid, version) == VersionPrecondition.Malformed, "Weak/wildcard/list/noncanonical version denied");
var steps = new List<ChecklistStepDraft> { new("s1", "Inspect supplied instruction") };
var planned = new DateTimeOffset(2026, 11, 8, 8, 0, 0, TimeSpan.FromHours(7));
var occurrence = new PreventiveOccurrenceDraft(1, "reviewed-version", planned, steps);
steps[0] = new("s1", "Changed after generation"); steps.Add(new("s2", "Later step"));
Check(occurrence.Checklist.Count == 1 && occurrence.Checklist[0].Instruction == "Inspect supplied instruction", "Template edits cannot change occurrence snapshot");
Check(occurrence.ProposedIdentity == new PreventiveOccurrenceDraft(1, "reviewed-version", planned.ToUniversalTime(), occurrence.Checklist).ProposedIdentity, "Retry and timezone representations produce identical proposed identity");
Check(occurrence.ProposedIdentity != new PreventiveOccurrenceDraft(1, "changed-version", planned, occurrence.Checklist).ProposedIdentity, "Schedule version change is explicit");
Denies(() => new PreventiveOccurrenceDraft(1, "v", planned, [new("same", "a"), new("same", "b")]), "Duplicate checklist reference denied");
var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
Denies(() => PreventiveClockDraft.ResolveLocal(new(2026, 3, 8, 2, 30, 0), zone), "DST gap never silently shifted");
Denies(() => PreventiveClockDraft.ResolveLocal(new(2026, 11, 1, 1, 30, 0), zone), "DST overlap requires an explicit policy");
Check(PreventiveClockDraft.ResolveLocal(new(2026, 11, 8, 8, 0, 0), zone).Offset == TimeSpan.Zero, "Resolved occurrence normalized UTC");
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { pass = true, tests = checks.Count, checks, sqlConnections = 0, productionActivation = false, durableGeneration = false, qualification = "Pure trial prototypes; no real authority, storage, DDL, route, transaction or durable duplicate-race acceptance." }));
