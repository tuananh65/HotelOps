using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.WorkTime;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

if(args.Length!=1||File.Exists(args[0]))throw new ArgumentException("One unused receipt required");
const string trial="HotelOps_Canonical_20261104_063549_81d4bc64_Fresh";
var source=Environment.GetEnvironmentVariable("TASK80_TRIAL_SQL_SOURCE")??throw new Exception("Explicit source required");
var connection=new SqlConnectionStringBuilder{DataSource=source,InitialCatalog=trial,IntegratedSecurity=true,TrustServerCertificate=true,ConnectTimeout=10,ApplicationName="HotelOps Task80 component"}.ConnectionString;
using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=timeout.Token;
await using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
var actual=await db.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),DB_NAME()) AS [Value]").SingleAsync(ct);
var marker=await db.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),value) AS [Value] FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.WorkTime.Contract'").SingleAsync(ct);
if(actual!=trial||marker!="Task77-v1")throw new Exception("Approved disposable rejected");
var cases=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);cases.Add(label);Console.WriteLine("PASS "+label);}
var userId=0L;var prefix="T80_"+Guid.NewGuid().ToString("N")[..8];
var start=new DateTimeOffset(2030,1,1,0,0,0,TimeSpan.Zero);
var reader=new ShiftAssignmentConflictReader(db);
async Task<bool> Conflict(double from,double to,long? exclude=null)=>await reader.HasOverlapAsync(userId,start.AddMinutes(from),start.AddMinutes(to),exclude,ct);
await using(var tx=await db.Database.BeginTransactionAsync(ct))
{
    var a=await db.Branches.SingleAsync(b=>b.BranchCode=="BR001",ct);var b=await db.Branches.SingleAsync(b=>b.BranchCode=="BR002",ct);
    var da=await db.Departments.SingleAsync(d=>d.BranchId==a.BranchId&&d.DepartmentCode=="HK",ct);
    var u=new User{Username=prefix,EmployeeCode=prefix,Email=prefix+"@example.invalid",FullName="Overlap fixture",AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",PrimaryBranchId=a.BranchId,DepartmentId=da.DepartmentId,CreatedAt=DateTime.UtcNow,PasswordHash="Unusable verification fixture"};
    db.Add(u);await db.SaveChangesAsync(ct);userId=u.UserId;
    async Task<ShiftAssignment> Add(int from,int to,long branch,string label,string status="ASSIGNED")
    {
        var department=await db.Departments.SingleAsync(d=>d.BranchId==branch&&d.DepartmentCode=="HK",ct);
        var s=start.AddMinutes(from).UtcDateTime;var e=start.AddMinutes(to).UtcDateTime;
        var template=new ShiftTemplate{BranchId=branch,ShiftCode=prefix+label,ShiftName=label,StartTime=TimeOnly.FromDateTime(s),EndTime=TimeOnly.FromDateTime(e),CrossesMidnight=s.Date!=e.Date,IsActive=true};
        db.Add(template);await db.SaveChangesAsync(ct);
        var shift=new WorkShift{BranchId=branch,DepartmentId=department.DepartmentId,ShiftTemplateId=template.ShiftTemplateId,ShiftDate=DateOnly.FromDateTime(s),ScheduledStartAt=s,ScheduledEndAt=e,Status="PLANNED",CreatedBy=userId,CreatedAt=DateTime.UtcNow,RequiredStaffCount=1};
        db.Add(shift);await db.SaveChangesAsync(ct);
        var assignment=new ShiftAssignment{ShiftId=shift.ShiftId,UserId=userId,AssignmentStatus=status,AssignedBy=userId,AssignedAt=DateTime.UtcNow};
        db.Add(assignment);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();return assignment;
    }
    var first=await Add(480,720,a.BranchId,"A");await Add(750,900,b.BranchId,"B");await Add(1380,1860,a.BranchId,"N");await Add(1200,1320,a.BranchId,"C","CANCELLED");
    Check(await Conflict(500,600),"Same-branch assignment overlap");
    Check(await Conflict(720,780),"Another branch still blocks overlapping employee time");
    Check(!await Conflict(720,750),"Adjacent ranges do not overlap either neighbor");
    Check(await Conflict(1410,1470),"Overnight concrete interval spans calendar boundary");
    Check(!await Conflict(1210,1250),"Explicit cancelled assignment ignored");
    Check(!await Conflict(500,600,first.ShiftAssignmentId),"Trusted edit can exclude its own assignment");
    Check(await reader.HasOverlapAsync(userId,start.AddMinutes(720).AddTicks(-1),start.AddMinutes(720).AddTicks(1),null,ct),"One tick overlap is not lost to minute rounding");
    Check(!await reader.HasOverlapAsync(userId,start.AddMinutes(720),start.AddMinutes(720).AddTicks(1),null,ct),"One tick after boundary is adjacent");
    var occupied=new HashSet<int>(Enumerable.Range(480,240).Concat(Enumerable.Range(750,150)).Concat(Enumerable.Range(1380,480)));
    var random=new Random(80);
    for(var n=0;n<64;n++){var left=random.Next(0,1900);var duration=random.Next(1,180);var expected=Enumerable.Range(left,duration).Any(occupied.Contains);Check(await Conflict(left,left+duration)==expected,"Independent occupied-minute oracle "+n);}
    async Task Reject(Func<Task> action,string label){try{await action();}catch(ArgumentException){Check(true,label);return;}throw new Exception("Missing invalid-input rejection "+label);}
    await Reject(()=>Conflict(720,720),"Zero duration candidate rejected");
    await Reject(()=>Conflict(720,700),"Reversed candidate rejected");
    await Reject(()=>reader.HasOverlapAsync(userId,start.ToOffset(TimeSpan.FromHours(7)),start.AddHours(1).ToOffset(TimeSpan.FromHours(7)),null,ct),"Non-UTC input rejected");
    await Reject(()=>reader.HasOverlapAsync(0,start,start.AddHours(1),null,ct),"Invalid employee rejected");
    await Reject(()=>reader.HasOverlapAsync(userId,start,start.AddHours(1),0,ct),"Invalid exclusion rejected");
    await db.ShiftAssignments.Where(x=>x.ShiftAssignmentId==first.ShiftAssignmentId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.AssignmentStatus,"LEGACY_UNKNOWN"),ct);
    Check(await Conflict(500,600),"Unknown legacy assignment state is not treated as free");
    await Add(2000,2000,a.BranchId,"BAD");
    try{await Conflict(1900,1910);throw new Exception("Stored zero shift not rejected");}catch(InvalidOperationException){Check(true,"Corrupt stored shift fails closed");}
    Check(!db.ChangeTracker.Entries().Any(),"Read helper tracks no fixture entities");
    await tx.RollbackAsync(ct);
}
Check(!await db.Users.AnyAsync(u=>u.UserId==userId,ct),"All disposable fixture rows rolled back");
await File.WriteAllTextAsync(args[0],JsonSerializer.Serialize(new{success=true,checks=cases.Count,cases,trial,
    scope="REAL_SQL_INTERNAL_READ_COMPONENT_NOT_PUBLIC_HTTPS_AUTHORIZATION_OR_WRITE_CONCURRENCY_ACCEPTANCE",schemaChanged=false,fixtureTransactionRolledBack=true,finishedUtc=DateTimeOffset.UtcNow},new JsonSerializerOptions{WriteIndented=true}),ct);
