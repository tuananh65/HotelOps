using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

if(args.Length!=1||File.Exists(args[0]))throw new ArgumentException("Unused receipt required");
const string trial="HotelOps_Canonical_20261104_063549_81d4bc64_Fresh";
var source=Environment.GetEnvironmentVariable("TASK81_TRIAL_SQL_SOURCE")??throw new Exception("Explicit trial source required");
var cs=new SqlConnectionStringBuilder{DataSource=source,InitialCatalog=trial,IntegratedSecurity=true,TrustServerCertificate=true,ConnectTimeout=10,ApplicationName="HotelOps Task81 readiness"}.ConnectionString;
HotelOpsDbContext Open()=>new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(cs).Options);
using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=timeout.Token;
await using var db=Open();var cases=new List<string>();
void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);cases.Add(label);Console.WriteLine("PASS "+label);}
var actual=await db.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),DB_NAME()) AS [Value]").SingleAsync(ct);
var marker=await db.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),value) AS [Value] FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.WorkTime.Contract'").SingleAsync(ct);
if(actual!=trial||marker!="Task77-v1")throw new Exception("Approved disposable DB rejected");
Check(true,"Explicit approved trial name and A1 marker verified before fixture work");
async Task<JsonElement> JsonSql(string sql)
{
    await db.Database.OpenConnectionAsync(ct);await using var command=db.Database.GetDbConnection().CreateCommand();command.CommandText=sql;
    await using var r=await command.ExecuteReaderAsync(ct);var text="";while(await r.ReadAsync(ct))text+=r.GetString(0);
    using var doc=JsonDocument.Parse(text);return doc.RootElement.Clone();
}
var columns=await JsonSql("SELECT t.name AS table_name,c.name AS column_name,ty.name AS sql_type,c.is_nullable FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id WHERE t.name IN ('shift_templates','work_shifts','shift_assignments','workdays','work_intervals') ORDER BY t.name,c.column_id FOR JSON PATH");
var indexes=await JsonSql("SELECT t.name AS table_name,i.name AS index_name,i.is_unique,c.name AS column_name,ic.key_ordinal FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE t.name IN ('shift_templates','work_shifts','shift_assignments','workdays') ORDER BY t.name,i.name,ic.key_ordinal FOR JSON PATH");
var foreignKeys=await JsonSql("SELECT fk.name,OBJECT_NAME(fk.parent_object_id) AS parent_table,OBJECT_NAME(fk.referenced_object_id) AS referenced_table,fk.is_disabled,fk.is_not_trusted FROM sys.foreign_keys fk WHERE OBJECT_NAME(fk.parent_object_id) IN ('shift_templates','work_shifts','shift_assignments','workdays') FOR JSON PATH");
var grants=await JsonSql("SELECT r.role_code,p.permission_code,rp.data_scope FROM dbo.role_permissions rp JOIN dbo.roles r ON r.role_id=rp.role_id JOIN dbo.permissions p ON p.permission_id=rp.permission_id WHERE p.permission_code IN ('SHIFT_VIEW','SHIFT_MANAGE') ORDER BY r.role_code,p.permission_code FOR JSON PATH");
foreach(var type in new[]{typeof(ShiftTemplate),typeof(WorkShift),typeof(ShiftAssignment)})
{
    var entity=db.Model.FindEntityType(type)!;var table=entity.GetTableName();
    Check(!columns.EnumerateArray().Any(c=>c.GetProperty("table_name").GetString()==table&&c.GetProperty("column_name").GetString()=="row_version"),"Actual SQL lacks rowversion: "+table);
    Check(!entity.GetProperties().Any(p=>p.IsConcurrencyToken),"EF has no concurrency token: "+table);
}
Check(db.Model.FindEntityType(typeof(Workday))!.FindProperty("RowVersion")!.IsConcurrencyToken,"A1 Workday concurrency token remains mapped");
Check(grants.EnumerateArray().Any(g=>g.GetProperty("role_code").GetString()=="DEPT_MANAGER"&&g.GetProperty("permission_code").GetString()=="SHIFT_MANAGE"&&g.GetProperty("data_scope").GetString()=="BRANCH"),"Actual trial catalog has SHIFT_MANAGE/BRANCH for DEPT_MANAGER (not a user/session authorization test)");
await using(var first=Open())await using(var second=Open())
{
    var user=await db.Users.AsNoTracking().Where(u=>u.DeletedAt==null).Select(u=>u.UserId).FirstAsync(ct);
    await first.Database.OpenConnectionAsync(ct);await second.Database.OpenConnectionAsync(ct);
    var blocker=await first.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
    var waiter=await second.Database.SqlQuery<int>($"SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
    await using var tx1=await first.Database.BeginTransactionAsync(ct);await using var tx2=await second.Database.BeginTransactionAsync(ct);
    await new UserEligibilityLock(first).LockAsync([user],ct);
    var pending=new UserEligibilityLock(second).LockAsync([user],ct);var observed=false;
    for(var n=0;n<120;n++){if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.dm_exec_requests WHERE session_id={waiter} AND blocking_session_id={blocker}").SingleAsync(ct)>0){observed=true;break;}await Task.Delay(25,ct);}
    Check(observed,"Two real SQL sessions serialize on existing retained User lock (DMV proof)");
    await tx1.RollbackAsync(ct);await pending;Check(true,"Waiting User lock acquires after owner rollback");await tx2.RollbackAsync(ct);
}
var prefix="T81_"+Guid.NewGuid().ToString("N")[..8];long id;
await using(var tx=await db.Database.BeginTransactionAsync(ct))
{
    var branch=await db.Branches.FirstAsync(b=>b.IsActive,ct);var department=await db.Departments.FirstAsync(d=>d.BranchId==branch.BranchId&&d.IsActive,ct);
    var u=new User{Username=prefix,EmployeeCode=prefix,Email=prefix+"@example.invalid",FullName="Task81 fixture",AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=DateTime.UtcNow,PasswordHash="Unusable test-only value"};db.Add(u);await db.SaveChangesAsync(ct);id=u.UserId;
    var template=new ShiftTemplate{BranchId=branch.BranchId,ShiftCode=prefix,ShiftName=prefix,StartTime=new(8,0),EndTime=new(12,0),IsActive=true};db.Add(template);await db.SaveChangesAsync(ct);
    var shift=new WorkShift{BranchId=branch.BranchId,DepartmentId=department.DepartmentId,ShiftTemplateId=template.ShiftTemplateId,ShiftDate=new(2030,1,1),ScheduledStartAt=new(2030,1,1,8,0,0,DateTimeKind.Utc),ScheduledEndAt=new(2030,1,1,12,0,0,DateTimeKind.Utc),Status="PLANNED",CreatedBy=id,CreatedAt=DateTime.UtcNow,RequiredStaffCount=1};db.Add(shift);await db.SaveChangesAsync(ct);
    db.Add(new ShiftAssignment{ShiftId=shift.ShiftId,UserId=id,AssignmentStatus="ASSIGNED",AssignedBy=id,AssignedAt=DateTime.UtcNow});await db.SaveChangesAsync(ct);
    try{await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.shift_assignments(shift_id,user_id,assignment_status,assigned_by,assigned_at) VALUES({shift.ShiftId},{id},'ASSIGNED',{id},SYSUTCDATETIME())",ct);throw new Exception("Duplicate not rejected");}
    catch(SqlException e)when(e.Number is 2601 or 2627){Check(true,"Existing SQL unique index rejects duplicate (shift,user); this is not cross-shift overlap prevention");}
    await tx.RollbackAsync(ct);db.ChangeTracker.Clear();
}
Check(!await db.Users.AnyAsync(u=>u.UserId==id,ct),"All duplicate-test fixture rows rolled back");
await File.WriteAllTextAsync(args[0],JsonSerializer.Serialize(new{success=true,checks=cases.Count,cases,trial,columns,indexes,foreignKeys,grants,
    scope="CURRENT_METADATA_DUPLICATE_INDEX_AND_USER_LOCK_FOUNDATION_NOT_UC138_WRITER_ACCEPTANCE",newDdl=false,fixtureRollback=true,finishedUtc=DateTimeOffset.UtcNow},new JsonSerializerOptions{WriteIndented=true}),ct);
