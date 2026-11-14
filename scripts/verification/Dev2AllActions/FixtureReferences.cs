using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

// Test setup owns only fresh entity instances registered before insertion.
// Canonical reference rows are borrowed, never added/updated/deleted by fixtures.
internal sealed class FixtureReferences
{
    private readonly List<ReferenceValue> created=[];
    internal ReferenceState? BorrowedPriority {get;private set;}
    internal long[] CreatedIds=>created.Where(x=>x.ReferenceId>0).Select(x=>x.ReferenceId).Distinct().ToArray();

    internal static ReferenceValue SelectPriority(IReadOnlyList<ReferenceValue> rows,string code="NORMAL")
    {
        if(code is not("CRITICAL" or "HIGH" or "NORMAL" or "LOW"))throw new AcceptanceFailure("INVALID_CANONICAL_PRIORITY_CODE");
        var matches=rows.Where(x=>string.Equals(x.GroupCode,"PRIORITY",StringComparison.OrdinalIgnoreCase)&&string.Equals(x.ValueCode,code,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(matches.Length==0)throw new AcceptanceFailure("CANONICAL_PRIORITY_MISSING_NO_SEED_CONTRACT");
        if(matches.Length!=1)throw new AcceptanceFailure("CANONICAL_PRIORITY_CONFLICT");
        if(!matches[0].IsActive)throw new AcceptanceFailure("CANONICAL_PRIORITY_INACTIVE");
        if(matches[0].ReferenceId<=0)throw new AcceptanceFailure("CANONICAL_PRIORITY_NOT_PERSISTED");
        return matches[0];
    }
    internal async Task<ReferenceValue> ResolvePriority(HotelOpsDbContext db,CancellationToken ct=default)
    {
        if(db.Database.CurrentTransaction is null)throw new AcceptanceFailure("PRIORITY_REQUIRES_FIXTURE_TRANSACTION");
        // HOLDLOCK retains a shared/range lock through the fixture transaction; no catalog writes.
        var rows=await db.ReferenceValues.FromSqlRaw("SELECT * FROM dbo.reference_values WITH(HOLDLOCK) WHERE group_code='PRIORITY' AND value_code='NORMAL'").AsNoTracking().ToListAsync(ct);
        return Borrow(SelectPriority(rows));
    }
    internal ReferenceValue Borrow(ReferenceValue priority)
    {
        var selected=SelectPriority(new[]{priority});BorrowedPriority=ReferenceState.From(selected);return selected;
    }
    internal void OwnNewSeverity(ReferenceValue reference,string marker)
    {
        if(reference.ReferenceId!=0||reference.GroupCode!="SEVERITY"||reference.ValueCode!=marker)throw new AcceptanceFailure("INVALID_FIXTURE_REFERENCE_OWNERSHIP");
        created.Add(reference);
    }
    internal IQueryable<ReferenceValue> OwnedQuery(HotelOpsDbContext db)
    {
        var ids=CreatedIds;
        if(BorrowedPriority is not null&&ids.Contains(BorrowedPriority.ReferenceId))throw new AcceptanceFailure("BORROWED_REFERENCE_IN_DELETE_SET");
        return db.ReferenceValues.Where(x=>ids.Contains(x.ReferenceId));
    }
    internal bool BorrowedUnchanged(ReferenceValue? actual)=>BorrowedPriority is null||actual is not null&&ReferenceState.From(actual)==BorrowedPriority;
    internal sealed record ReferenceState(long ReferenceId,string GroupCode,string ValueCode,string DisplayName,string? Description,int? SortOrder,bool IsActive)
    {
        internal static ReferenceState From(ReferenceValue x)=>new(x.ReferenceId,x.GroupCode,x.ValueCode,x.DisplayName,x.Description,x.SortOrder,x.IsActive);
    }
}

internal sealed partial class Fixtures
{
    private readonly FixtureReferences references=new();
    private async Task<ReferenceValue> CanonicalPriority(HotelOpsDbContext db)
    {
        Stage="03 departments-areas-warehouse";SaveBoundary="03.canonical-priority-resolve";
        try{return await references.ResolvePriority(db);}
        catch(Exception error){Failure=FailureDiagnostics.Capture(error,"fixture-create",Stage,SaveBoundary,db,output);throw;}
    }
    private async Task VerifyBorrowedReferences(HotelOpsDbContext db)
    {
        var borrowed=references.BorrowedPriority;
        var actual=borrowed is null?null:await db.ReferenceValues.AsNoTracking().SingleOrDefaultAsync(x=>x.ReferenceId==borrowed.ReferenceId);
        if(!references.BorrowedUnchanged(actual))throw new AcceptanceFailure("BORROWED_PRIORITY_CHANGED_OR_REMOVED");
        var remainingOwned=await references.OwnedQuery(db).CountAsync();
        await File.WriteAllTextAsync(Path.Combine(output,"FIXTURE_REFERENCE_PRESERVATION.json"),JsonSerializer.Serialize(new{status=borrowed is null?"NOT_APPLICABLE_NO_BORROW":"PASS",borrowedPriorityReferenceId=borrowed?.ReferenceId,createdReferenceIds=references.CreatedIds,remainingCreatedReferences=remainingOwned,borrowedPriorityPreserved=borrowed is not null,prioritySeedPolicy="FAIL_IF_MISSING_OR_INACTIVE_OR_CONFLICTING",scope="Exact in-memory scalar snapshot comparison; catalog values are not logged"},new JsonSerializerOptions{WriteIndented=true}));
    }
}
