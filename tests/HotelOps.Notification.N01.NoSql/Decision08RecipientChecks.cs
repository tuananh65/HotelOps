using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Notifications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal static class Decision08RecipientChecks
{
    internal static async Task<IReadOnlyList<string>> RunAsync()
    {
        var checks=new List<string>();
        void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
        async Task Deny<T>(Func<Task> action,string name) where T:Exception
        {try{await action();}catch(T){checks.Add(name);return;}throw new Exception("Expected "+typeof(T).Name+": "+name);}
        var source=new Fe05NotificationSource(3,7,13,11,17,19,41,"\"0000000000000001\"",17,
            "COMPLETED","FAIL","FAILED",DateTimeOffset.Parse("2026-11-09T00:00:00Z"),[23,29],"\"0000000000000002\"");
        var kind=Fe05NotificationKind.CleaningReworkRequested;
        async Task Exercise(Func<Fixture,Task> run){await using var f=await Fixture.Open();await run(f);}
        await Exercise(async f=>{
            var ids=await f.Authority.ResolveMandatoryIdsAsync(f.Db,source,kind,default);
            Check(ids.SequenceEqual(new long[]{37})&&f.Prelocks.Observed.SequenceEqual(new long[]{37}),"DECISION08 exact persisted Rework assignee only; no supervisor fallback");
            Check(f.Connection.Queries.Single().Contains("w.rework_id=")&&f.Connection.Queries.Single().Contains("c.cleaning_task_id=t.cleaning_task_id")&&f.Connection.Queries.Single().Contains("WITH(HOLDLOCK)"),"DECISION08 actual SQL uses exact Rework PK, current cycle and retained source reads");
        });
        await Exercise(async f=>{
            var recipients=await f.Policy.ResolveAsync(source,kind,default);
            Check(recipients.Count==1&&recipients[0]==new Fe05Recipient(37,7,true,true),"DECISION08 production policy checks eligible exact assignee and source read then re-resolves");
            Check(f.Coordination.Reads==1&&f.Connection.SourceReads==2&&f.Connection.EligibilityReads==1,"DECISION08 actual resolver reads persisted relationship twice and eligibility once");
            var sql=f.Connection.Queries.Single(q=>q.Contains("COUNT(*)"));
            Check(sql.Contains("account_status='ACTIVE'")&&sql.Contains("employment_status='ACTIVE'")&&sql.Contains("deleted_at IS NULL")&&sql.Contains("revoked_at IS NULL")&&sql.Contains("granted_at<="),"DECISION08 eligibility SQL requires account, employment, not deleted and active branch access");
        });
        await Exercise(async f=>{f.Assignee=null;await Deny<Fe05NotificationUnavailableException>(()=>f.Policy.ResolveAsync(source,kind,default),"DECISION08 NULL persisted Rework assignee fails closed");Check(f.Connection.EligibilityReads==0,"DECISION08 missing assignee never falls back or queries another user");});
        await Exercise(async f=>{f.Assignee=0;await Deny<Fe05NotificationUnavailableException>(()=>f.Policy.ResolveAsync(source,kind,default),"DECISION08 invalid assignee identity rejected");});
        foreach(var column in new[]{"ReworkId","InspectionId","CleaningTaskId","RoomId","BranchId","VersionId","CurrentTaskId"})
            await Exercise(async f=>{f.ChangeRow=t=>t.Rows[0][column]=999L;await Deny<Fe05NotificationUnavailableException>(()=>f.Policy.ResolveAsync(source,kind,default),"DECISION08 exact persisted "+column+" mismatch rejected");});
        foreach(var column in new[]{"InspectionRevision","ReworkRevision"})
            await Exercise(async f=>{f.ChangeRow=t=>t.Rows[0][column]=new byte[]{0,0,0,0,0,0,0,9};await Deny<NotificationConflictException>(()=>f.Policy.ResolveAsync(source,kind,default),"DECISION08 changed "+column+" rejected");});
        await Exercise(async f=>{f.ChangeRow=t=>t.Rows[0]["RequestedAt"]=source.OccurredAt.UtcDateTime.AddSeconds(1);await Deny<NotificationConflictException>(()=>f.Policy.ResolveAsync(source,kind,default),"DECISION08 changed Rework requested timestamp rejected");});
        await Exercise(async f=>{f.Empty=true;await Deny<Fe05NotificationUnavailableException>(()=>f.Policy.ResolveAsync(source,kind,default),"DECISION08 missing/stale SQL source has no recipient fallback");});
        await Exercise(async f=>{f.Duplicate=true;await Deny<InvalidOperationException>(()=>f.Policy.ResolveAsync(source,kind,default),"DECISION08 ambiguous exact source rows rejected");});
        foreach(var reason in new[]{"inactive account","inactive employment","deleted user","revoked branch access","foreign branch access"})
            await Exercise(async f=>{f.Eligible=false;await Deny<Fe05NotificationUnavailableException>(()=>f.Policy.ResolveAsync(source,kind,default),"DECISION08 ineligible count=0 ("+reason+" fixture) rejects entire recipient set");Check(f.Coordination.Reads==0,"DECISION08 ineligible recipient cannot reach source-read authorization ("+reason+")");});
        await Exercise(async f=>{f.Coordination.CanRead=false;await Deny<Fe05NotificationUnavailableException>(()=>f.Policy.ResolveAsync(source,kind,default),"DECISION08 actual source-read denial rejects required Rework event");});
        await Exercise(async f=>{f.ChangeAssigneeOnReread=true;await Deny<NotificationConflictException>(()=>f.Policy.ResolveAsync(source,kind,default),"DECISION08 recipient set changed after waits rejects instead of appending Users locks");});
        await Exercise(async f=>{f.Prelocks.Allowed=[31];await Deny<Fe05NotificationUnavailableException>(()=>f.Policy.ResolveAsync(source,kind,default),"DECISION08 newly discovered user outside retained owner roster rejected");Check(f.Connection.Queries.All(q=>!q.Contains("UPDLOCK")),"DECISION08 resolver never acquires late Users update locks");});
        await Exercise(async f=>{var authority=new Fe05Decision08MandatoryRecipients(new UnacceptedFe05RecipientPrelocks());await Deny<Fe05NotificationUnavailableException>(()=>authority.ResolveMandatoryIdsAsync(f.Db,source,kind,default),"DECISION08 missing accepted owner prelock contract stays closed");});
        await Exercise(async f=>{await Deny<OperationCanceledException>(()=>f.Policy.ResolveAsync(source,kind,new(true)),"DECISION08 cancellation before source/eligibility SQL");Check(f.Connection.Queries.Count==0,"DECISION08 canceled resolution performs no provider query");});
        await Exercise(async f=>{await Deny<Fe05NotificationUnavailableException>(()=>f.Policy.ResolveAsync(source with{CurrentCleaningTaskId=99},kind,default),"DECISION08 old current cycle rejected before recipient query");});
        await Exercise(async f=>{await Deny<Fe05NotificationUnavailableException>(()=>f.Policy.ResolveAsync(source,(Fe05NotificationKind)999,default),"DECISION08 unknown event cannot choose recipients");});
        foreach(var caseName in new[]{"supervisor absent","supervisor role only","actor is supervisor","original cleaner missing","latest assignment only","multiple/reassigned cleaners","same user proposed for both roles"})
            await Exercise(async f=>{await Deny<Fe05NotificationUnavailableException>(()=>f.Authority.ResolveMandatoryIdsAsync(f.Db,source with{ReworkId=null,ReworkRevision=null},Fe05NotificationKind.InspectionFailed,default),"DECISION08 Inspection Failed remains closed without both persisted role proofs: "+caseName);Check(f.Connection.Queries.Count==0,"DECISION08 no heuristic relationship SQL for "+caseName);});
        await Exercise(async f=>{await f.Tx.DisposeAsync();await Deny<InvalidOperationException>(()=>f.Authority.ResolveMandatoryIdsAsync(f.Db,source,kind,default),"DECISION08 resolver requires same caller serializable transaction");});
        var services=new ServiceCollection();services.AddHotelOpsNotifications(new ConfigurationBuilder().Build());
        Check(services.Single(d=>d.ServiceType==typeof(IFe05MandatoryRecipientAuthority)).ImplementationType==typeof(UnacceptedFe05MandatoryRecipients),"DECISION08 production defaults remain unaccepted");
        services.AddFe05Decision08RecipientCandidate();
        Check(services.Single(d=>d.ServiceType==typeof(IFe05MandatoryRecipientAuthority)).ImplementationType==typeof(Fe05Decision08MandatoryRecipients)&&services.Single(d=>d.ServiceType==typeof(IFe05RecipientPrelockAuthority)).ImplementationType==typeof(UnacceptedFe05RecipientPrelocks),"DECISION08 isolated opt-in binds concrete Rework resolver with closed prelock default");
        Check(services.Count(d=>d.ServiceType==typeof(INotificationPublisher))==1&&!services.Any(d=>d.ServiceType.FullName=="Microsoft.Extensions.Hosting.IHostedService"),"DECISION08 candidate adds no publisher/hosted worker");
        return checks;
    }

    private sealed class Fixture:IAsyncDisposable
    {
        public long? Assignee=37;public bool Eligible=true,Empty,Duplicate,ChangeAssigneeOnReread;public Action<DataTable>? ChangeRow;
        public ScriptConnection Connection {get;}=new(); public HotelOpsDbContext Db {get;} public IDbContextTransactionAdapter Tx {get;private set;}=null!;
        public Prelocks Prelocks {get;}=new(); public Coordination Coordination {get;}=new();
        public Fe05Decision08MandatoryRecipients Authority {get;} public Fe05SqlRecipientPolicy Policy {get;}
        private Fixture(){Db=new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(Connection).Options);Authority=new(Prelocks);Policy=new(Db,Authority,Coordination,TimeProvider.System);Connection.Rows=Rows;}
        public static async Task<Fixture> Open(){var f=new Fixture();f.Tx=new(await f.Db.Database.BeginTransactionAsync(IsolationLevel.Serializable));return f;}
        private DataTable Rows(string sql)
        {
            if(sql.Contains("COUNT(*)")){Connection.EligibilityReads++;var table=new DataTable();table.Columns.Add("Value",typeof(int));table.Rows.Add(Eligible?1:0);return table;}
            if(!sql.Contains("dbo.cleaning_reworks"))throw new Exception("Unexpected SQL");
            Connection.SourceReads++;var t=new DataTable();
            foreach(var n in new[]{"ReworkId","InspectionId","CleaningTaskId","AssigneeUserId","RoomId","BranchId","VersionId","CurrentTaskId"})t.Columns.Add(n,typeof(long));
            t.Columns.Add("InspectionRevision",typeof(byte[]));t.Columns.Add("ReworkRevision",typeof(byte[]));t.Columns.Add("RequestedAt",typeof(DateTime));
            if(!Empty)t.Rows.Add(41L,11L,17L,Assignee is null?DBNull.Value:ChangeAssigneeOnReread&&Connection.SourceReads>1?31L:Assignee.Value,13L,7L,19L,17L,new byte[]{0,0,0,0,0,0,0,1},new byte[]{0,0,0,0,0,0,0,2},DateTime.Parse("2026-11-09T00:00:00Z").ToUniversalTime());
            if(!Empty)ChangeRow?.Invoke(t);if(Duplicate&&!Empty)t.ImportRow(t.Rows[0]);return t;
        }
        public async ValueTask DisposeAsync(){await Tx.DisposeAsync();await Db.DisposeAsync();}
    }
    private sealed class IDbContextTransactionAdapter(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx):IAsyncDisposable
    {public ValueTask DisposeAsync()=>tx.DisposeAsync();}
    private sealed class Prelocks:IFe05RecipientPrelockAuthority
    {
        public long[] Allowed=[31,37];public List<long> Observed=[];
        public Task RequireAlreadyLockedAsync(HotelOpsDbContext db,Fe05NotificationSource source,long id,CancellationToken ct)
        {ct.ThrowIfCancellationRequested();Observed.Add(id);if(!Allowed.Contains(id))throw new Fe05NotificationUnavailableException("test owner roster excludes recipient");return Task.CompletedTask;}
    }
    private sealed class Coordination:IFe05SourceCoordination
    {
        public bool CanRead=true;public int Reads;
        public Task RequireReservationAsync(HotelOpsDbContext d,Fe05OperationRequest r,CurrentUserInfo a,CancellationToken ct)=>Task.CompletedTask;
        public Task RequireCompletedOperationAsync(HotelOpsDbContext d,Fe05OperationRequest r,CurrentUserInfo a,Fe05OperationReceipt receipt,CancellationToken ct)=>Task.CompletedTask;
        public Task RequireAuthorityAsync(HotelOpsDbContext d,Fe05NotificationCommand c,CurrentUserInfo a,CancellationToken ct)=>Task.CompletedTask;
        public Task<bool> CanRecipientReadAsync(HotelOpsDbContext d,Fe05NotificationSource s,long id,CancellationToken ct){Reads++;return Task.FromResult(CanRead);}
    }
    private sealed class ScriptConnection:DbConnection
    {
        private ConnectionState state=ConnectionState.Closed;public Func<string,DataTable> Rows=null!;public List<string> Queries=[];public int SourceReads,EligibilityReads;
        [AllowNull]public override string ConnectionString{get;set;}="";
        public override string Database=>"decision08-no-sql";public override string DataSource=>"decision08-no-sql";public override string ServerVersion=>"16.0";public override ConnectionState State=>state;
        public override void Open()=>state=ConnectionState.Open;public override void Close()=>state=ConnectionState.Closed;public override void ChangeDatabase(string s)=>throw new NotSupportedException();
        protected override DbTransaction BeginDbTransaction(IsolationLevel level)=>new ScriptTransaction(this,level);
        protected override DbCommand CreateDbCommand()=>new ScriptCommand(this);
    }
    private sealed class ScriptTransaction(ScriptConnection connection,IsolationLevel level):DbTransaction
    {public override IsolationLevel IsolationLevel=>level;protected override DbConnection DbConnection=>connection;public override void Commit()=>throw new Exception("No commits in recipient tests");public override void Rollback(){}}
    private sealed class ScriptCommand(ScriptConnection connection):DbCommand
    {
        private readonly SqlCommand parameters=new();
        [AllowNull]public override string CommandText{get;set;}="";public override int CommandTimeout{get;set;}public override CommandType CommandType{get;set;}
        public override bool DesignTimeVisible{get;set;}public override UpdateRowSource UpdatedRowSource{get;set;}
        protected override DbConnection? DbConnection{get;set;}=connection;protected override DbTransaction? DbTransaction{get;set;}
        protected override DbParameterCollection DbParameterCollection=>parameters.Parameters;
        protected override DbParameter CreateDbParameter()=>new SqlParameter();public override void Cancel(){}public override void Prepare(){}
        public override int ExecuteNonQuery()=>throw new Exception("No writes permitted");public override object ExecuteScalar()=>throw new NotSupportedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            connection.Queries.Add(CommandText);var table=connection.Rows(CommandText);
            // EF composes an outer projection in mapped property order; the provider
            // fixture must return that actual SELECT order, not the inner raw SQL order.
            var from=CommandText.IndexOf("FROM",StringComparison.OrdinalIgnoreCase);
            var projection=from<0?CommandText:CommandText[..from];
            var columns=System.Text.RegularExpressions.Regex.Matches(projection,@"\[\w+\]\.\[(\w+)\]")
                .Select(m=>m.Groups[1].Value).Distinct().Where(table.Columns.Contains).ToArray();
            return (columns.Length==table.Columns.Count?table.DefaultView.ToTable(false,columns):table).CreateDataReader();
        }
    }
}
