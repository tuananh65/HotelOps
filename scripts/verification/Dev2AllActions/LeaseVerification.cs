using System.Reflection;
using HotelOps.Api.Services.Dev2Authorization;
using Microsoft.EntityFrameworkCore.Storage;

internal static class LeaseVerification
{
    internal static async Task<int> Run()
    {
        var assertions=0;void Check(bool ok){if(!ok)throw new InvalidOperationException("Action-owned transaction boundary regression");assertions++;}
        var type=typeof(Dev2ServiceAuthority).Assembly.GetType("HotelOps.Api.Services.Dev2Authorization.ActionTransactionLease")!;
        var actual=new Transaction();var lease=(IDbContextTransaction)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[actual],null)!;
        var commit=typeof(Dev2ServiceAuthority).GetMethod("CommitVerifiedAsync",BindingFlags.Static|BindingFlags.NonPublic)!;
        var checks=0;
        await (Task)commit.Invoke(null,[lease,new Func<CancellationToken,Task>(_=>{checks++;return Task.CompletedTask;}),CancellationToken.None])!;
        Check(checks==1&&actual.Commits==0); // Service revalidates; MVC still owns final commit.
        await lease.DisposeAsync();Check(actual.Disposals==0);
        await lease.RollbackAsync();Check(actual.Rollbacks==1);
        try{await lease.CommitAsync();throw new Exception("Service committed MVC transaction");}catch(InvalidOperationException){Check(actual.Commits==0);}
        await (Task)commit.Invoke(null,[actual,new Func<CancellationToken,Task>(_=>{Check(actual.Commits==0);return Task.CompletedTask;}),CancellationToken.None])!;Check(actual.Commits==1);
        try{await (Task)commit.Invoke(null,[actual,new Func<CancellationToken,Task>(_=>throw new Dev2AccessDenied(403)),CancellationToken.None])!;throw new Exception("Denied callback committed");}catch(Dev2AccessDenied){Check(actual.Commits==1);}
        return assertions;
    }
    private sealed class Transaction:IDbContextTransaction
    {
        internal int Commits,Rollbacks,Disposals;public Guid TransactionId{get;}=Guid.NewGuid();public bool SupportsSavepoints=>false;
        public void Commit()=>Commits++;public Task CommitAsync(CancellationToken ct=default){Commit();return Task.CompletedTask;}
        public void Rollback()=>Rollbacks++;public Task RollbackAsync(CancellationToken ct=default){Rollback();return Task.CompletedTask;}
        public void Dispose()=>Disposals++;public ValueTask DisposeAsync(){Dispose();return ValueTask.CompletedTask;}
    }
}
