using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetCustodyWorkflow
{
    public async Task<object> DecideAsync(long assetId,long handoverId,string? expected,bool accept,string? reason,CancellationToken ct)
    {
        reason=reason?.Trim();
        if(assetId<=0||handoverId<=0||!accept&&(string.IsNullOrEmpty(reason)||reason.Length<10)
            ||reason?.Length>4000||reason?.Any(char.IsControl)==true)
            throw new InsightException(400,"MSG03","A rejection reason of 10-4000 characters is required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId],ct);
        await RequireStorageAsync(ct);
        var asset=await db.Assets.FromSqlInterpolated($"SELECT * FROM dbo.assets WITH(UPDLOCK,HOLDLOCK) WHERE asset_id={assetId}")
            .AsNoTracking().SingleOrDefaultAsync(ct)??throw new InsightException(404,"ASSET_UNAVAILABLE","Asset unavailable.");
        await profile.RequireAsync(asset.BranchId,ct);
        if(!await profile.Query(asset.BranchId).AnyAsync(a=>a.AssetId==assetId,ct)||asset.LifecycleStatus is not("IN_USE" or "UNDER_REPAIR"))
            throw new InsightException(409,"ASSET_CUSTODY_INELIGIBLE","Operational asset with consistent placement/custody required.");
        var token=await AssetVersion.RequireAsync(db,assetId,expected,ct);
        var handover=await db.AssetHandovers.FromSqlInterpolated($"SELECT * FROM dbo.asset_handovers WITH(UPDLOCK,HOLDLOCK) WHERE handover_id={handoverId} AND asset_id={assetId}")
            .SingleOrDefaultAsync(ct)??throw new InsightException(404,"HANDOVER_UNAVAILABLE","Handover unavailable.");
        if(handover.HandoverStatus!="PENDING")throw new InsightException(409,"HANDOVER_FINALIZED","Only a pending handover can be decided.");
        if(handover.ToUserId!=access.UserId||handover.ToDepartmentId is not {} target
            ||!await AssetHandoverAuthority.AllowsAsync(db,access.UserId,asset.BranchId,target,true,ct))
            throw new InsightException(403,"ACCESS_DENIED","Only the currently authorized designated receiving Head may decide.");
        if(asset.CurrentDepartmentId!=handover.FromDepartmentId||asset.CustodianUserId!=handover.FromUserId)
            throw new InsightException(409,"CUSTODY_CHANGED","Current custody differs from the submission snapshot.");
        var proof=await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.asset_handover_versions v WITH(HOLDLOCK)
            JOIN dbo.audit_logs a WITH(HOLDLOCK) ON a.audit_id=v.submission_audit_id
            WHERE v.handover_id={handoverId} AND a.entity_type='asset_handovers' AND a.entity_id=CONVERT(varchar(30),{handoverId})
              AND a.branch_id={asset.BranchId} AND a.actor_user_id={handover.CreatedBy} AND a.action='CREATE'
              AND a.occurred_at={handover.CreatedAt} AND a.old_values IS NULL AND a.new_values=v.snapshot_json
              AND TRY_CONVERT(bigint,JSON_VALUE(v.snapshot_json,'$.asset_id'))={assetId}
              AND TRY_CONVERT(bigint,JSON_VALUE(v.snapshot_json,'$.to_department_id'))={target}
              AND TRY_CONVERT(bigint,JSON_VALUE(v.snapshot_json,'$.to_user_id'))={access.UserId}
              AND TRY_CONVERT(bigint,JSON_VALUE(v.snapshot_json,'$.from_department_id'))={handover.FromDepartmentId}
              AND (TRY_CONVERT(bigint,JSON_VALUE(v.snapshot_json,'$.from_user_id'))={handover.FromUserId}
                OR JSON_VALUE(v.snapshot_json,'$.from_user_id') IS NULL AND {handover.FromUserId} IS NULL)
              AND JSON_VALUE(v.snapshot_json,'$.handover_status')='PENDING'
            """).SingleAsync(ct);
        if(proof!=1)throw new InsightException(409,"HANDOVER_PROVENANCE_UNAVAILABLE","Submission evidence is incomplete; no custody change is permitted.");
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        handover.HandoverStatus=accept?"ACCEPTED":"REJECTED";handover.AcceptedAt=accept?now:null;
        await db.SaveChangesAsync(ct);
        var department=accept?target:asset.CurrentDepartmentId;
        var custodian=accept?access.UserId:asset.CustodianUserId;
        if(await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.assets SET current_department_id={department},custodian_user_id={custodian},updated_at={now}
            WHERE asset_id={assetId} AND row_version={token}
            """,ct)!=1)throw new InsightException(412,"MSG04","Asset changed. Reload before deciding.");
        var resultSnapshot=new{handover_id=handoverId,asset_id=assetId,branch_id=asset.BranchId,
            from_branch_id=asset.BranchId,to_branch_id=asset.BranchId,from_department_id=handover.FromDepartmentId,
            to_department_id=target,from_user_id=handover.FromUserId,to_user_id=access.UserId,
            handover_status=handover.HandoverStatus,accepted_at=handover.AcceptedAt,decided_at=now,reason,
            sender_id=handover.CreatedBy,notification_kind="ASSET_HANDOVER_DECIDED",notification_version=1};
        db.AuditLogs.Add(new AuditLog{EntityType="asset_handovers",EntityId=handoverId.ToString(CultureInfo.InvariantCulture),
            BranchId=asset.BranchId,ActorUserId=access.UserId,Action="UPDATE",OccurredAt=now,
            OldValues=JsonSerializer.Serialize(new{handover_status="PENDING"}),NewValues=JsonSerializer.Serialize(resultSnapshot)});
        if(accept)db.AuditLogs.Add(new AuditLog{EntityType="assets",EntityId=assetId.ToString(CultureInfo.InvariantCulture),
            BranchId=asset.BranchId,ActorUserId=access.UserId,Action="UPDATE",OccurredAt=now,
            OldValues=JsonSerializer.Serialize(new{branch_id=asset.BranchId,current_department_id=asset.CurrentDepartmentId,custodian_user_id=asset.CustodianUserId}),
            NewValues=JsonSerializer.Serialize(new{branch_id=asset.BranchId,current_department_id=target,custodian_user_id=access.UserId,handover_id=handoverId}),
            ChangedFields="[\"current_department_id\",\"custodian_user_id\"]"});
        await db.SaveChangesAsync(ct);await profile.RequireAsync(asset.BranchId,ct);
        if(!await AssetHandoverAuthority.AllowsAsync(db,access.UserId,asset.BranchId,target,true,ct))
            throw new InsightException(403,"ACCESS_DENIED","Receiving authority changed.");
        var result=new{HandoverId=handoverId,State=accept?"ACKNOWLEDGED":"REJECTED",Message=accept?"MSG08":"MSG19",Version=await AssetVersion.ReadAsync(db,assetId,ct)};
        await tx.CommitAsync(ct);return result;
    }
}
