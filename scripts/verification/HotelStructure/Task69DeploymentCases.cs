using System.Diagnostics;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

internal static class Task69DeploymentCases
{
    internal static readonly List<string> Receipts=[];
    internal static async Task<bool> DeployAsync(HotelOpsDbContext db,string stage,bool expected,Action<bool,string> check,CancellationToken ct)
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../../"));
        var receipt=Path.Combine(root,"obj","task69-deploy-"+stage+"-"+Guid.NewGuid().ToString("N")+".local.json");
        var start=new ProcessStartInfo("powershell"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=root};
        foreach(var arg in new[]{"-NoProfile","-File",Path.Combine(root,"scripts/database/release/uc017/Run-Uc017.ps1"),
            "-Stage",stage,"-Server",db.Database.GetDbConnection().DataSource,"-Database",db.Database.GetDbConnection().Database,"-Receipt",receipt})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;
        try
        {
            var stdout=process.StandardOutput.ReadToEndAsync(ct);var stderr=process.StandardError.ReadToEndAsync(ct);
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(timeout.Token);
            await File.WriteAllTextAsync(receipt+".log",await stdout+await stderr,ct);
            using var json=JsonDocument.Parse(await File.ReadAllTextAsync(receipt,ct));
            var success=process.ExitCode==0&&json.RootElement.GetProperty("success").GetBoolean();
            Receipts.Add(Path.GetRelativePath(root,receipt).Replace('\\','/'));
            check(success==expected,"UC017 actual deployment entry point "+stage+" expected success="+expected+" (receipt "+Path.GetFileName(receipt)+")");
            return expected?success:!success;
        }
        finally{if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync(CancellationToken.None);}}
    }

    internal static async Task FailureRecoveryAsync(HotelOpsDbContext db,Action<bool,string> check,CancellationToken ct)
    {
        var before=await db.RoomTypes.AsNoTracking().OrderBy(x=>x.RoomTypeId).Select(x=>new{x.RoomTypeId,x.TypeCode,x.TypeName,x.StandardCapacity,x.IsActive}).ToArrayAsync(ct);
        // Genuine DDL failure after columns are added, not an application-only rejection.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER T69_DeploymentFault ON DATABASE FOR CREATE_INDEX AS
            BEGIN
             IF COL_LENGTH(N'dbo.room_types',N'branch_id') IS NOT NULL
              THROW 51070,'Task69 disposable post-column deployment fault',1;
             THROW 51071,'Task69 fault ran before columns existed',1;
            END
            """,ct);
        try
        {
            await DeployAsync(db,"Apply",false,check,ct);
            var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../../"));
            check((await File.ReadAllTextAsync(Path.Combine(root,Receipts[^1])+".log",ct)).Contains("post-column deployment fault"),"UC017 failure is the intended real DDL fault after columns exist");
            check(await RoomTypeSchema.ReadAsync(db,ct)==RoomTypeContract.Legacy,"UC017 mid-DDL failure rolls back columns, FK, CHECK, indexes and contract marker");
            check((await db.RoomTypes.AsNoTracking().OrderBy(x=>x.RoomTypeId).Select(x=>new{x.RoomTypeId,x.TypeCode,x.TypeName,x.StandardCapacity,x.IsActive}).ToArrayAsync(ct)).SequenceEqual(before),"UC017 failed deployment preserves all legacy rows");
            check(await db.Database.SqlQueryRaw<int>("SELECT CONVERT(int,COUNT(*)) AS [Value] FROM sys.extended_properties WHERE major_id=OBJECT_ID(N'dbo.room_types') AND name LIKE N'HotelOps.UC017.%'").SingleAsync(ct)==0,"UC017 failed deployment leaves no version/original-index marker");
        }
        finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER T69_DeploymentFault ON DATABASE;",ct);}
        // The caller retries the same actual package after removing only this owned fault.
    }

    internal static async Task MappingAsync(HotelOpsDbContext db,long id,long branch,long checklist,Action<bool,string> check,CancellationToken ct)
    {
        var actual=await db.BranchRoomTypes.AsNoTracking().Include(x=>x.Branch).Include(x=>x.DefaultChecklist).SingleAsync(x=>x.RoomTypeId==id,ct);
        check(actual.BranchId==branch&&actual.Branch?.BranchId==branch&&actual.DefaultChecklistId==checklist&&actual.DefaultChecklist?.ChecklistId==checklist
            &&actual.StandardCleaningMinutes==35&&actual.StandardCapacity==3&&actual.TypeCode=="T68_NEW","UC017 Database First V1 projection materializes actual scalar/FK/navigation data");
        var metadata=db.Model.FindEntityType(typeof(RoomTypeBranchV1))!;
        check(metadata.GetViewName()=="room_types"&&metadata.GetTableName()==null&&metadata.GetProperties().Count()==9
            &&metadata.GetProperties().Count(x=>x.IsNullable&&x.Name is "BranchId" or "StandardCleaningMinutes" or "DefaultChecklistId")==3,"UC017 V1 model is a nullable legacy-preserving read-only SQL projection");
        check(metadata.GetIndexes().Count()==3&&metadata.GetIndexes().Count(x=>x.IsUnique&&x.GetFilter()!=null)==2
            &&metadata.GetIndexes().Any(x=>!x.IsUnique&&x.Name=="IX_room_types_default_checklist_v1")
            &&metadata.GetForeignKeys().Select(x=>x.FindAnnotation("Relational:Name")?.Value as string).Order()
                .SequenceEqual(new[]{"FK_room_types_branch_v1","FK_room_types_default_checklist_v1"}.Order()),"UC017 V1 read model has actual filtered/supporting indexes and FK annotations");
        check(!db.Model.FindEntityType(typeof(RoomType))!.GetIndexes().Any(),"UC017 legacy projection no longer advertises obsolete global uniqueness");
        db.Add(new RoomTypeBranchV1{TypeCode="T69_NO_VIEW_WRITE",TypeName="Read-only"});
        var refused=false;
        try{await db.SaveChangesAsync(ct);}catch(InvalidOperationException){refused=true;}
        finally{db.ChangeTracker.Clear();}
        check(refused&&!await db.RoomTypes.AnyAsync(x=>x.TypeCode=="T69_NO_VIEW_WRITE",ct),"UC017 read-only V1 projection cannot silently INSERT an unvalidated row");
    }

    internal static async Task<long> RoomGuardsAsync(HotelOpsDbContext db,Room valid,Room foreign,long legacy,long checklist,Action<bool,string> check,CancellationToken ct)
    {
        db.Add(foreign);var refused=false;
        try{await db.SaveChangesAsync(ct);}
        catch(DbUpdateException error)when(error.InnerException is SqlException sql&&sql.Errors.Cast<SqlError>().Any(x=>x.Number==51069)){refused=true;}
        finally{db.ChangeTracker.Clear();}
        check(refused&&!await db.Rooms.AnyAsync(x=>x.RoomNumber==foreign.RoomNumber,ct),"UC017 actual EF Room INSERT rejects a foreign-branch type with SQL51069 and no row");
        async Task Reject(FormattableString sql,string label)
        {
            var blocked=false;try{await db.Database.ExecuteSqlInterpolatedAsync(sql,ct);}
            catch(SqlException error)when(error.Errors.Cast<SqlError>().Any(x=>x.Number==51069)){blocked=true;}
            check(blocked,label);
        }
        await Reject($"UPDATE dbo.rooms SET room_type_id={foreign.RoomTypeId} WHERE room_id={valid.RoomId}","UC017 Room UPDATE cannot restore foreign-branch responsibility");
        check(await db.Rooms.Where(x=>x.RoomId==valid.RoomId).Select(x=>x.RoomTypeId).SingleAsync(ct)==valid.RoomTypeId,"UC017 rejected Room update preserves original FK");
        await Reject($"INSERT dbo.rooms(branch_id,floor_id,room_type_id,room_number,operational_status,is_active) VALUES({valid.BranchId},{valid.FloorId},{valid.RoomTypeId},{"T69_MULTI_OK"},{"AVAILABLE"},1),({valid.BranchId},{valid.FloorId},{foreign.RoomTypeId},{"T69_MULTI_BAD"},{"AVAILABLE"},1)",
            "UC017 one invalid row rejects the complete multirow assignment statement");
        check(!await db.Rooms.AnyAsync(x=>x.RoomNumber.StartsWith("T69_MULTI_"),ct),"UC017 multirow trigger rejection leaves no partial Room insert");
        var otherBranch=await db.BranchRoomTypes.Where(x=>x.RoomTypeId==foreign.RoomTypeId).Select(x=>x.BranchId).SingleAsync(ct);
        await Reject($"UPDATE dbo.room_types SET branch_id={otherBranch},type_code={"T69_ILLEGAL_MOVE"} WHERE room_type_id={valid.RoomTypeId}","UC017 scoped RoomType branch cannot be changed behind existing Rooms even with a free code");
        await Reject($"UPDATE dbo.room_types SET branch_id={otherBranch},standard_cleaning_minutes=35,default_checklist_id={checklist} WHERE room_type_id={legacy}",
            "UC017 legacy global RoomType cannot be silently assigned a branch");
        check(await db.BranchRoomTypes.Where(x=>x.RoomTypeId==legacy).Select(x=>x.BranchId).SingleAsync(ct)==null,"UC017 legacy RoomType remains global after rejected conversion");
        var otherFloor=new Floor{BranchId=otherBranch!.Value,FloorCode="T69_LEGACY_F",FloorName="Legacy compatibility",IsActive=true};
        db.Add(otherFloor);await db.SaveChangesAsync(ct);
        var legacyRoom=new Room{BranchId=otherBranch.Value,FloorId=otherFloor.FloorId,RoomTypeId=legacy,RoomNumber="T69_LEGACY_SHARED",OperationalStatus="AVAILABLE",IsActive=true};
        db.Add(legacyRoom);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        check(await db.Rooms.AnyAsync(x=>x.RoomId==legacyRoom.RoomId,ct),"UC017 preserves allowed global legacy RoomType use across branches");
        return legacyRoom.RoomId;
    }
}
