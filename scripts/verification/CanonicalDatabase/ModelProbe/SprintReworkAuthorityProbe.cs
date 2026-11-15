using System.Data;
using System.Reflection;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;

internal static class SprintReworkAuthorityProbe
{
    internal static async Task VerifyAsync(HotelOpsDbContext db,long cleaner,long branch,long rework,CancellationToken ct)
    {
        var service=new RoomTypeCreationService(db,null!,null!,null!,TimeProvider.System);
        var read=typeof(RoomTypeCreationService).GetMethod("ReadTrustedReworkAuthorityAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var eligible=typeof(RoomTypeCreationService).GetMethod("EligibleReworkAssignees",BindingFlags.Instance|BindingFlags.NonPublic)!;
        async Task<bool> Eligible()=>await ((IQueryable<User>)eligible.Invoke(service,[branch,DateTime.UtcNow])!).AnyAsync(u=>u.UserId==cleaner,ct);
        // Mutations below are only to the guarded synthetic fixture and always rolled back.
        foreach(var scenario in new[]{"no-shift","inactive","wrong-branch","wrong-department","inactive-role"})
        {
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.shift_assignments WHERE user_id={cleaner}",ct);
            switch(scenario)
            {
                case "inactive": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET account_status='LOCKED' WHERE user_id={cleaner}",ct);break;
                case "wrong-branch": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={cleaner}",ct);break;
                case "wrong-department": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET department_id=NULL WHERE user_id={cleaner}",ct);break;
                case "inactive-role": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_roles SET is_active=0 WHERE user_id={cleaner}",ct);break;
            }
            if(await Eligible()!=(scenario=="no-shift"))throw new Exception("Rework eligibility mismatch: "+scenario);
            await tx.RollbackAsync(ct);
        }
        await db.Database.ExecuteSqlRawAsync("""
            CREATE USER DEV3_ReworkAuthorityReader WITHOUT LOGIN;
            ALTER ROLE hotelops_rework_writer ADD MEMBER DEV3_ReworkAuthorityReader;
            GRANT SELECT ON SCHEMA::dbo TO DEV3_ReworkAuthorityReader;
            GRANT VIEW DEFINITION TO DEV3_ReworkAuthorityReader;
            """,ct);
        try
        {
            foreach(var scenario in new[]{"committed","source-changed","issuer-disabled","unauthorized"})
            {
                await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
                if(scenario=="source-changed") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_reworks SET reason='Changed synthetic source' WHERE rework_id={rework}",ct);
                if(scenario=="issuer-disabled") await db.Database.ExecuteSqlRawAsync("UPDATE dbo.fe04_rework_authority_issuers SET is_active=0",ct);
                await db.Database.ExecuteSqlRawAsync("EXECUTE AS USER='DEV3_ReworkAuthorityReader'",ct);
                try
                {
                    if(await db.Database.SqlQueryRaw<int>("SELECT HAS_PERMS_BY_NAME('dbo.fs016_rework_source_proofs','OBJECT','INSERT')+HAS_PERMS_BY_NAME('dbo.fs016_rework_source_proofs','OBJECT','UPDATE')+HAS_PERMS_BY_NAME('dbo.fs016_rework_source_proofs','OBJECT','DELETE') AS Value").SingleAsync(ct)!=0)
                        throw new Exception("Reader may forge authority");
                    var proof=await (Task<ReworkAuthorityProof?>)read.Invoke(service,[scenario=="unauthorized" ? 0L : cleaner,branch,rework,null,ct])!;
                    if((proof!=null)!=(scenario=="committed"))throw new Exception("Committed authority mismatch: "+scenario);
                }
                finally {await db.Database.ExecuteSqlRawAsync("REVERT",ct);}
                await tx.RollbackAsync(ct);
            }
        }
        finally {await db.Database.ExecuteSqlRawAsync("DROP USER DEV3_ReworkAuthorityReader",ct);}
    }
}
