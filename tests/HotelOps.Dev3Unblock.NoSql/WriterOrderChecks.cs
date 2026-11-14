using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Assets;
using HotelOps.Api.Services.Incidents;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Executes actual writer entry paths against a command-capturing stub; never SQL.
// Stops at first authoritative User query. This proves call order, not SQL locks/races.
internal static class WriterOrderChecks
{
    internal static async Task<IReadOnlyList<string>> RunAsync()
    {
        var checks=new List<string>();
        async Task Check(string name,Func<HotelOpsDbContext,Task> call,bool discovery=false)
        {
            await using var c=new Connection(discovery);
            await using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(c).Options);
            try{await call(db);}catch(FirstUser){
                if(c.Statements.Count!=(discovery?2:1)||!c.Statements.Last().Contains("dbo.users",StringComparison.OrdinalIgnoreCase)
                    ||!c.Statements.Last().Contains("UPDLOCK",StringComparison.OrdinalIgnoreCase))throw new Exception(name+": wrong first retained lock");
                checks.Add(name);return;
            }
            throw new Exception(name+": expected to stop before first User query");
        }
        await Check("Asset condition obtains User update lock before Asset",d=>new AssetService(d).ChangeConditionAsync(1,new(){ActorUserId=9,ExpectedCondition="GOOD",NewCondition="GOOD",Note="Order test"},default));
        await Check("Asset retirement obtains User update lock before Asset",d=>new AssetService(d).RetireAssetAsync(1,new(){ActorUserId=9,Reason="Order test reason"},default));
        await Check("Asset update obtains User update lock before Asset",d=>new AssetService(d).UpdateAssetAsync(1,new(){ActorUserId=9},default));
        await Check("Handover create locks sorted actor/receiver Users before Asset",d=>new AssetService(d).CreateHandoverAsync(1,new(){ActorUserId=9,ToUserId=10},default));
        await Check("Incident status obtains User update lock before Incident",d=>new IncidentService(d).ChangeIncidentStatusAsync(1,new(){ActorUserId=9,Status="OPEN"},default));
        await Check("Incident triage obtains User update lock before Incident",d=>new IncidentService(d).TriageIncidentAsync(1,new(){ActorUserId=9,SeverityId=1,PriorityId=1},default));
        await Check("Handover acceptance discovery is followed by User before retained Asset/Handover",d=>new AssetService(d).AcceptHandoverAsync(1,new(){ActorUserId=9},default),true);
        await Check("Handover rejection discovery is followed by User before retained Asset/Handover",d=>new AssetService(d).RejectHandoverAsync(1,new(){ActorUserId=9,Reason="Order test rejection"},default),true);
        return checks;
    }
    private sealed class FirstUser:Exception;
    private sealed class Connection(bool discovery):DbConnection
    {
        public List<string> Statements{get;}=[];
        private ConnectionState state;
        [AllowNull]public override string ConnectionString{get;set;}="";
        public override string Database=>"NoSqlOrderStub";public override string DataSource=>"NoSqlOrderStub";
        public override string ServerVersion=>"16.0";public override ConnectionState State=>state;
        public override void Open()=>state=ConnectionState.Open;public override void Close()=>state=ConnectionState.Closed;
        public override void ChangeDatabase(string name)=>throw new NotSupportedException();
        protected override DbTransaction BeginDbTransaction(IsolationLevel level)=>new Transaction(this,level);
        protected override DbCommand CreateDbCommand()=>new Command(this);
        public DbDataReader Read(string sql)
        {
            Statements.Add(sql);
            if(discovery&&Statements.Count==1){
                if(!sql.Contains("asset_handovers",StringComparison.OrdinalIgnoreCase))throw new Exception("Unexpected discovery");
                var table=new DataTable();table.Columns.Add("AssetId",typeof(long));table.Columns.Add("FromUserId",typeof(long));table.Columns.Add("ToUserId",typeof(long));table.Rows.Add(1L,2L,9L);return table.CreateDataReader();
            }
            if(sql.Contains("dbo.users",StringComparison.OrdinalIgnoreCase))throw new FirstUser();
            throw new Exception("Feature command reached before User: "+sql);
        }
    }
    private sealed class Transaction(Connection c,IsolationLevel isolation):DbTransaction
    {
        public override IsolationLevel IsolationLevel=>isolation;protected override DbConnection DbConnection=>c;
        public override void Commit()=>throw new Exception("Stub cannot commit domain work");public override void Rollback(){}
    }
    private sealed class Command(Connection owner):DbCommand
    {
        private readonly SqlCommand parameters=new();
        [AllowNull]public override string CommandText{get;set;}="";
        public override int CommandTimeout{get;set;}public override CommandType CommandType{get;set;}
        public override bool DesignTimeVisible{get;set;}public override UpdateRowSource UpdatedRowSource{get;set;}
        protected override DbConnection? DbConnection{get;set;}=owner;
        protected override DbTransaction? DbTransaction{get;set;}
        protected override DbParameterCollection DbParameterCollection=>parameters.Parameters;
        protected override DbParameter CreateDbParameter()=>new SqlParameter();
        public override void Cancel(){}public override void Prepare(){}
        public override int ExecuteNonQuery()=>throw new Exception("No domain writes in order stub");
        public override object? ExecuteScalar()=>throw new Exception("Unexpected scalar");
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)=>owner.Read(CommandText);
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior,CancellationToken ct)
        {ct.ThrowIfCancellationRequested();return Task.FromResult(owner.Read(CommandText));}
    }
}
