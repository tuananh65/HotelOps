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
        foreach(var scenario in new[]{"no-shift","inactive","branch-revoked","wrong-branch","wrong-department","inactive-role"})
        {
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.shift_assignments WHERE user_id={cleaner}",ct);
            switch(scenario)
            {
                case "inactive": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.users SET account_status='LOCKED' WHERE user_id={cleaner}",ct);break;
                case "branch-revoked": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={cleaner}",ct);break;
                case "wrong-branch": await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT dbo.branches(branch_code,branch_name) VALUES('D3-REWORK-OTHER','Synthetic other branch');
                    DECLARE @other bigint=SCOPE_IDENTITY();
                    UPDATE dbo.user_roles SET branch_id=@other WHERE user_id={cleaner};
                    """,ct);break;
                case "wrong-department": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.departments SET department_code='D3-OTHER' WHERE department_id=(SELECT department_id FROM dbo.users WHERE user_id={cleaner})",ct);break;
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
                    var proof=await (Task<ReworkAuthorityProof?>)read.Invoke(service,[scenario=="unauthorized" ? 0L : cleaner,branch,rework,SprintReworkFixture.EvidenceRoot,ct])!;
                    if((proof!=null)!=(scenario=="committed"))throw new Exception("Committed authority mismatch: "+scenario);
                    if(proof!=null)
                    {
                        await db.Database.ExecuteSqlInterpolatedAsync($"""
                            EXEC dbo.fe05_issue_rework_authority @actor={proof.ActorUserId},@branch={branch},@rework={rework},
                              @operation={proof.OperationId},@source_digest={proof.SourceDigest},@evidence_digest={proof.EvidenceDigest};
                            """,ct);
                        if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.audit_logs WHERE entity_type='cleaning_reworks' AND entity_id={rework.ToString()}").SingleAsync(ct)!=1
                            || await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations WHERE actor_user_id={proof.ActorUserId} AND operation_key={proof.OperationId}").SingleAsync(ct)!=1)
                            throw new Exception("Authority retry duplicated occurrence");
                    }
                }
                finally {await db.Database.ExecuteSqlRawAsync("REVERT",ct);}
                await tx.RollbackAsync(ct);
            }
        }
        finally {await db.Database.ExecuteSqlRawAsync("DROP USER DEV3_ReworkAuthorityReader",ct);}
    }
}
