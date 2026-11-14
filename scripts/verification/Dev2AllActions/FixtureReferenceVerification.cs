using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class FixtureReferenceVerification
{
    internal static Task<int> Run()
    {
        var checks=0;void Check(bool ok){if(!ok)throw new InvalidOperationException("FIX02 reference ownership regression");checks++;}
        void Denied(Action operation,string category)
        {try{operation();throw new InvalidOperationException("Unexpected canonical selection success");}catch(AcceptanceFailure error){Check(error.Category==category);}}
        var canonical=new ReferenceValue{ReferenceId=71,GroupCode="PRIORITY",ValueCode="NORMAL",DisplayName="Normal",IsActive=true,Description="Existing catalog",SortOrder=3};
        var ledger=new FixtureReferences();var selected=ledger.Borrow(FixtureReferences.SelectPriority(new[]{canonical}));
        Check(ReferenceEquals(canonical,selected));Check(selected.ReferenceId==71);Check(ledger.CreatedIds.Length==0);
        var snapshot=FixtureReferences.ReferenceState.From(canonical);
        Denied(()=>FixtureReferences.SelectPriority(Array.Empty<ReferenceValue>()),"CANONICAL_PRIORITY_MISSING_NO_SEED_CONTRACT");
        Denied(()=>FixtureReferences.SelectPriority(new[]{canonical},"D2A_RANDOM"),"INVALID_CANONICAL_PRIORITY_CODE");
        Denied(()=>FixtureReferences.SelectPriority(new[]{canonical,canonical}),"CANONICAL_PRIORITY_CONFLICT");
        Denied(()=>FixtureReferences.SelectPriority(new[]{new ReferenceValue{ReferenceId=72,GroupCode="PRIORITY",ValueCode="NORMAL",IsActive=false}}),"CANONICAL_PRIORITY_INACTIVE");
        Denied(()=>ledger.OwnNewSeverity(canonical,"fixture"),"INVALID_FIXTURE_REFERENCE_OWNERSHIP");
        var severity=new ReferenceValue{GroupCode="SEVERITY",ValueCode="fixture",DisplayName="fixture",IsActive=true};ledger.OwnNewSeverity(severity,"fixture");
        Check(ledger.CreatedIds.Length==0);severity.ReferenceId=99;Check(ledger.CreatedIds.SequenceEqual(new[]{99L}));
        var catalog=new List<ReferenceValue>{canonical,severity};catalog.RemoveAll(x=>ledger.CreatedIds.Contains(x.ReferenceId));
        Check(catalog.Count==1&&ReferenceEquals(catalog[0],canonical));Check(ledger.BorrowedUnchanged(canonical));Check(snapshot==FixtureReferences.ReferenceState.From(canonical));
        var repeated=new FixtureReferences();Check(ReferenceEquals(repeated.Borrow(FixtureReferences.SelectPriority(catalog)),canonical));Check(repeated.CreatedIds.Length==0);
        Check(!ledger.BorrowedUnchanged(null));canonical.IsActive=false;Check(!ledger.BorrowedUnchanged(canonical));canonical.IsActive=true;
        using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer("Server=lpc:KIENPC;Database=HotelOps_DEV2_FullStack_Trial;Integrated Security=true;Encrypt=false").Options);
        var expression=ledger.OwnedQuery(db).ToQueryString();Check(expression.Contains("99")&&!expression.Contains("71"));
        var tracked=db.Entry(canonical);Check(tracked.State==EntityState.Detached);Check(snapshot==FixtureReferences.ReferenceState.From(canonical));
        return Task.FromResult(checks);
    }
}
