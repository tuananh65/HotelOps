using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Dependencies;
using HotelOps.Api.Services.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Executes the actual reader, PermissionService and CanManage adapter with scripted
// relational rows. No real SQL connection, grant changes or deactivation writes.
internal static class Uc006InactiveBranchChecks
{
    internal static async Task<IReadOnlyList<string>> RunAsync()
    {
        var checks=new List<string>();
        void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
        async Task Deny<T>(Func<Task> run,string name) where T:Exception
        {try{await run();}catch(T){checks.Add(name);return;}throw new Exception("Expected "+typeof(T).Name+": "+name);}
        await using(var f=new Fixture())
        {
            var snapshot=await f.Reader.ReadUserGlobalAsync(7,default);
            Check(snapshot.Branches.Select(s=>s.BranchId).SequenceEqual(new long[]{1,2})&&snapshot.Branches[1].Facts.Any(x=>x.Category=="TARGET_BRANCH"&&x.State=="INACTIVE")&&!snapshot.CanDeactivate,
                "UC006-01 global authority covers active and inactive branches with no responsibility");
            Check(f.Grants.BranchEvaluations==0,"UC006 global scan never invokes inactive branch authorization");
        }
        await using(var f=new Fixture{HasAsset=true})
        {
            var snapshot=await f.Reader.ReadUserGlobalAsync(7,default);
            Check(snapshot.Branches.Single(s=>s.BranchId==2).Facts.Any(x=>x.Category=="ASSET"&&x.RecordId==41&&x.Classification==DependencyClassification.BLOCKING),
                "UC006-02 inactive branch target custody remains visible and blocking");
        }
        await using(var f=new Fixture())
        {
            f.Identity.Value=null;
            await Deny<DependencyScopeException>(()=>f.Reader.ReadUserGlobalAsync(7,default),"UC006-03 unauthenticated global review denied before queries");
            Check(f.Connection.Commands.Count==0,"UC006 denied identity opens no provider commands");
        }
        await using(var f=new Fixture())
        {
            f.Grants.GlobalMaster=false;
            Check((await f.Permissions.EvaluateBranchAsync("MASTER_DATA_MANAGE",1,default)).IsGranted,"UC006 test actor has real branch-scoped permission");
            await Deny<DependencyScopeException>(()=>f.Reader.ReadUserGlobalAsync(7,default),"UC006-04 branch-only permission cannot enter global review");
        }
        await using(var f=new Fixture())
        {
            _=await f.Reader.ReadUserAsync(7,1,default);
            await Deny<DependencyScopeException>(()=>f.Reader.ReadUserAsync(7,2,default),"UC006-05 ordinary inactive-branch read still denied by existing PermissionService");
            Check(f.Grants.BranchEvaluations>=3,"UC006 ordinary active branch authorization runs initially and finally");
        }
        await using(var f=new Fixture{HasAsset=true})
        {
            var before=await f.Reader.ReadUserGlobalAsync(7,default);f.AssetStamp=2;
            var after=await f.Reader.ReadUserGlobalAsync(7,default);
            Check(before.Fingerprint!=after.Fingerprint,"UC006-06 inactive-branch same-ID responsibility change changes global fingerprint");
        }
        await using(var f=new Fixture{HasAsset=true})
        {
            await using var tx=await f.Db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var expected=await f.Reader.ReadUserGlobalInCallerTransactionAsync(7,default);f.AssetStamp=2;
            await Deny<DependencySnapshotConflictException>(()=>f.Reader.RevalidateUserGlobalInCallerTransactionAsync(7,expected,default),"UC006-07a caller-transaction reread rejects stale inactive-branch facts");
            f.AssetStamp=1;
            Check((await f.Reader.RevalidateUserGlobalInCallerTransactionAsync(7,expected,default)).Fingerprint==expected.Fingerprint,"UC006 unchanged caller-transaction reread preserves fingerprint");
            f.IncludeThirdBranch=true;
            await Deny<DependencySnapshotConflictException>(()=>f.Reader.RevalidateUserGlobalInCallerTransactionAsync(7,expected,default),"UC006-07b caller-transaction reread rejects changed branch coverage");
            Check(f.Connection.Commands.Take(2).All(s=>s.Contains("UPDLOCK")&&s.Contains("dbo.users"))&&f.Connection.Commits==0,"UC006 retained User-first entry and zero independent commits");
        }
        await using(var f=new Fixture{HasAsset=true,UnknownAsset=true})
        {
            var snapshot=await f.Reader.ReadUserGlobalAsync(7,default);var branch=snapshot.Branches.Single(s=>s.BranchId==2);
            Check(branch.Facts.Any(x=>x.Category=="ASSET"&&x.State=="ALIEN"&&x.Classification==DependencyClassification.UNKNOWN_POLICY)&&branch.Unresolved.Contains("DEV2_ASSET_LIFECYCLE")&&!snapshot.CanDeactivate,
                "UC006-08 unknown responsibility in inactive branch remains unresolved and cannot clear deactivation");
        }
        await using(var f=new Fixture())
        {
            f.Grants.GlobalUserManage=false;
            await Deny<DependencyScopeException>(()=>f.Reader.ReadUserGlobalAsync(7,default),"UC006 target CanManage denial remains mandatory");
            f.Grants.GlobalUserManage=true;f.Identity.Value=new(9,1,true);
            await Deny<DependencyScopeException>(()=>f.Reader.ReadUserGlobalAsync(7,default),"UC006 restricted actor denied");
            f.Identity.Value=new(9,0,false);
            await Deny<DependencyScopeException>(()=>f.Reader.ReadUserGlobalAsync(7,default),"UC006 invalid session denied");
        }
        await using(var f=new Fixture())
        {
            f.Grants.RevokeMasterAfter=2;
            await Deny<DependencyScopeException>(()=>f.Reader.ReadUserGlobalAsync(7,default),"UC006 global authority revoked during read fails whole review");
        }
        await using(var f=new Fixture())
        {
            f.ChangeCoverageWithinRead=true;
            await Deny<DependencySnapshotConflictException>(()=>f.Reader.ReadUserGlobalAsync(7,default),"UC006 coverage changed within a review fails instead of returning incomplete success");
        }
        await using(var f=new Fixture{HasUnscopedWork=true})
        {
            var snapshot=await f.Reader.ReadUserGlobalAsync(7,default);
            Check(snapshot.Unscoped.Any(w=>w.Category=="MAINTENANCE_UNRESOLVED_SCOPE"&&w.RecordId==71)&&!snapshot.CanDeactivate,"UC006 unscoped target Maintenance remains explicitly unresolved");
        }
        return checks;
    }

    private sealed class Identity:ICurrentUserAccessor
    {public CurrentUserInfo? Value=new(9,1,false);public bool TryGetCurrentUser(out CurrentUserInfo user){user=Value.GetValueOrDefault();return Value.HasValue;}}
    private sealed class Grants:IPermissionDataSource
    {
        public bool GlobalMaster=true,GlobalUserManage=true;public int BranchEvaluations,MasterReads,RevokeMasterAfter=int.MaxValue;
        public Task<bool> IsActiveUserAsync(long id,CancellationToken ct)=>Task.FromResult(id==9);
        public Task<bool> IsActiveBranchAsync(long id,CancellationToken ct){BranchEvaluations++;return Task.FromResult(id==1);}
        public Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long id,string code,DateTime now,CancellationToken ct)
        {
            if(code=="MASTER_DATA_MANAGE")MasterReads++;
            var global=code=="MASTER_DATA_MANAGE"?GlobalMaster&&MasterReads<=RevokeMasterAfter:GlobalUserManage;
            IReadOnlyList<PermissionGrantPath> rows=global?[new(code,"ALL",null,true,true,now.AddDays(-1),null,false,false)]
                :code=="MASTER_DATA_MANAGE"?[new(code,"BRANCH",1,true,true,now.AddDays(-1),null,true,true)]:[];
            return Task.FromResult(rows);
        }
    }
    private sealed class Fixture:IAsyncDisposable
    {
        public bool HasAsset,UnknownAsset,IncludeThirdBranch,ChangeCoverageWithinRead,HasUnscopedWork;public int AssetStamp=1,BranchLists;
        public Identity Identity {get;}=new();public Grants Grants {get;}=new();public Connection Connection {get;}=new();
        public HotelOpsDbContext Db {get;}public PermissionService Permissions {get;}public Dev2DependencyReader Reader {get;}
        public Fixture()
        {
            Db=new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(Connection).Options);
            Permissions=new(Identity,Grants,new UnresolvedPermissionResourceResolver(),TimeProvider.System);
            var access=new UserAccountStatusAccess(new UserCredentialResetAccess(Db,Identity,Permissions,Grants,TimeProvider.System));
            Reader=new(Db,Identity,Permissions,access);Connection.Reader=Rows;
        }
        private DataTable Rows(Command command)
        {
            var sql=command.CommandText;
            long branch=Convert.ToInt64(command.Parameters.Cast<DbParameter>().FirstOrDefault(p=>p.ParameterName.Contains("branch",StringComparison.OrdinalIgnoreCase))?.Value??1L);
            if(sql.Contains("FROM [branches]"))
            {
                if(sql.Contains("EXISTS"))return Table([( "Value",typeof(bool))],new object?[]{true});
                if(sql.Contains("[is_active]"))return Table([("branch_id",typeof(long)),("is_active",typeof(bool))],new object?[]{branch,branch!=2});
                BranchLists++;var ids=new List<object?[]>{new object?[]{1L},new object?[]{2L}};
                if(IncludeThirdBranch||ChangeCoverageWithinRead&&BranchLists>1)ids.Add(new object?[]{3L});
                return Table([("branch_id",typeof(long))],ids.ToArray());
            }
            if(sql.Contains("FROM [users]")||sql.Contains("FROM dbo.users"))
            {
                if(sql.Contains("UPDLOCK"))return Table([("user_id",typeof(long)),("deleted_at",typeof(DateTime)),("account_status",typeof(string)),("employment_status",typeof(string))],new object?[]{7L,null,"ACTIVE","ACTIVE"});
                return Table([("user_id",typeof(long)),("row_version",typeof(byte[])),("deleted_at",typeof(DateTime)),("account_status",typeof(string)),("employment_status",typeof(string))],new object?[]{7L,new byte[]{1},null,"ACTIVE","ACTIVE"});
            }
            if(sql.Contains("FROM [assets]"))
            {
                if(!HasAsset)return new DataTable();var state=UnknownAsset?"ALIEN":"IN_USE";
                if(!sql.Contains("[asset_id]"))return Table([("lifecycle_status",typeof(string))],new object?[]{state});
                if(branch!=2)return new DataTable();
                return Table([("Id",typeof(long)),("State",typeof(string)),("custodian_user_id",typeof(long)),("current_department_id",typeof(long)),("current_room_id",typeof(long)),("current_area_id",typeof(long)),("updated_at",typeof(DateTime))],new object?[]{41L,state,7L,null,null,null,new DateTime(2026,11,9,0,0,AssetStamp,DateTimeKind.Utc)});
            }
            if(HasUnscopedWork&&sql.Contains("FROM [maintenance_works]")&&sql.Contains("[maintenance_work_id]")&&!sql.Contains("[actual_start_at]"))
                return Table([("maintenance_work_id",typeof(long)),("status",typeof(string)),("incident_id",typeof(long)),("department_id",typeof(long)),("assignee_id",typeof(long)),("resolved_at",typeof(DateTime)),("reviewed_at",typeof(DateTime))],new object?[]{71L,"IN_PROGRESS",null,null,7L,null,null});
            // Other authoritative source queries return genuinely empty fixture sets;
            // translation and collection still run through the real EF SQL Server provider.
            return new DataTable();
        }
        public async ValueTask DisposeAsync()=>await Db.DisposeAsync();
    }
    private static DataTable Table((string Name,Type Type)[] columns,params object?[][] rows)
    {var table=new DataTable();foreach(var c in columns)table.Columns.Add(c.Name,c.Type);foreach(var row in rows)table.Rows.Add(row.Select(v=>v??DBNull.Value).ToArray());return table;}
    private sealed class Connection:DbConnection
    {
        private ConnectionState state=ConnectionState.Closed;public Func<Command,DataTable> Reader=null!;public List<string> Commands=[];public int Commits;
        [AllowNull]public override string ConnectionString{get;set;}="";public override string Database=>"uc006-scripted";public override string DataSource=>"uc006-scripted";public override string ServerVersion=>"16.0";public override ConnectionState State=>state;
        public override void Open()=>state=ConnectionState.Open;public override void Close()=>state=ConnectionState.Closed;public override void ChangeDatabase(string name)=>throw new NotSupportedException();
        protected override DbCommand CreateDbCommand()=>new Command(this);protected override DbTransaction BeginDbTransaction(IsolationLevel level)=>new Transaction(this,level);
    }
    private sealed class Transaction(Connection connection,IsolationLevel level):DbTransaction
    {public override IsolationLevel IsolationLevel=>level;protected override DbConnection DbConnection=>connection;public override void Commit(){connection.Commits++;throw new Exception("Independent commit forbidden in reader tests");}public override void Rollback(){}}
    private sealed class Command(Connection connection):DbCommand
    {
        private readonly SqlCommand parameters=new();[AllowNull]public override string CommandText{get;set;}="";public override int CommandTimeout{get;set;}public override CommandType CommandType{get;set;}public override bool DesignTimeVisible{get;set;}public override UpdateRowSource UpdatedRowSource{get;set;}
        protected override DbConnection? DbConnection{get;set;}=connection;protected override DbTransaction? DbTransaction{get;set;}protected override DbParameterCollection DbParameterCollection=>parameters.Parameters;
        protected override DbParameter CreateDbParameter()=>new SqlParameter();public override void Cancel(){}public override void Prepare(){}public override int ExecuteNonQuery()=>throw new Exception("Writes forbidden");public override object ExecuteScalar()=>throw new NotSupportedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior){connection.Commands.Add(CommandText);return connection.Reader(this).CreateDataReader();}
    }
}
