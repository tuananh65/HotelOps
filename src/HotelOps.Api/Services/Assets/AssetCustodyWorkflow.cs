using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetCustodyWorkflow(HotelOpsDbContext db,AssetProfileAccess profile,InsightAccess access)
{
    public async Task<object> ReadAsync(long assetId,CancellationToken ct,int page=1)
    {
        if(assetId<=0||page is <1 or >100000)throw new InsightException(400,"INVALID_ID","Invalid asset identity or page.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId],ct);
        var asset=await db.Assets.AsNoTracking().SingleOrDefaultAsync(a=>a.AssetId==assetId,ct)
            ??throw new InsightException(404,"ASSET_UNAVAILABLE","Asset unavailable.");
        await profile.RequireAsync(asset.BranchId,ct);
        await RequireStorageAsync(ct);
        if(!await profile.Query(asset.BranchId).AnyAsync(a=>a.AssetId==assetId,ct))
            throw new InsightException(409,"ASSET_SCOPE_INCONSISTENT","Resolve inconsistent asset placement or custody.");
        var sender=asset.CurrentDepartmentId is {} department&&asset.LifecycleStatus is "IN_USE" or "UNDER_REPAIR"
            &&await AssetHandoverAuthority.AllowsAsync(db,access.UserId,asset.BranchId,department,false,ct);
        var targets=new List<object>();
        if(sender)
        {
            var departments=await db.Departments.AsNoTracking().Where(d=>d.BranchId==asset.BranchId&&d.IsActive&&d.DepartmentId!=asset.CurrentDepartmentId)
                .OrderBy(d=>d.DepartmentName).Select(d=>new{d.DepartmentId,d.DepartmentName}).ToArrayAsync(ct);
            var users=await db.UserRoles.AsNoTracking().Where(r=>r.IsActive&&r.Role.IsActive&&r.Role.RoleCode=="DEPT_MANAGER"
                &&(r.BranchId==null||r.BranchId==asset.BranchId)).Select(r=>r.UserId).Distinct().Order().ToArrayAsync(ct);
            foreach(var d in departments)
                foreach(var user in users)
                    if(await AssetHandoverAuthority.AllowsAsync(db,user,asset.BranchId,d.DepartmentId,true,ct))
                        targets.Add(new{d.DepartmentId,d.DepartmentName,UserId=user,
                            Name=await db.Users.AsNoTracking().Where(u=>u.UserId==user).Select(u=>u.FullName).SingleAsync(ct)});
        }
        var scopedVersions=db.Database.SqlQuery<long>($"""
            SELECT v.handover_id AS Value FROM dbo.asset_handover_versions v
            JOIN dbo.audit_logs a ON a.audit_id=v.submission_audit_id
            WHERE a.branch_id={asset.BranchId} AND a.entity_type='asset_handovers' AND a.action='CREATE'
              AND a.entity_id=CONVERT(varchar(30),v.handover_id) AND a.new_values=v.snapshot_json
              AND TRY_CONVERT(bigint,JSON_VALUE(v.snapshot_json,'$.from_branch_id'))={asset.BranchId}
              AND TRY_CONVERT(bigint,JSON_VALUE(v.snapshot_json,'$.to_branch_id'))={asset.BranchId}
            """);
        var anyPending=await db.AssetHandovers.AnyAsync(h=>h.AssetId==assetId&&h.HandoverStatus=="PENDING",ct);
        var pending=await db.AssetHandovers.AsNoTracking().Where(h=>h.AssetId==assetId&&h.HandoverStatus=="PENDING"&&scopedVersions.Contains(h.HandoverId))
            .Select(h=>new{h.HandoverId,h.HandoverNo,h.ToDepartmentId,h.ToUserId,h.CreatedBy,h.ConditionNote,h.CreatedAt}).ToArrayAsync(ct);
        var canDecide=pending.Length==1&&pending[0].ToUserId==access.UserId&&pending[0].ToDepartmentId is {} target
            &&await AssetHandoverAuthority.AllowsAsync(db,access.UserId,asset.BranchId,target,true,ct);
        var version=await AssetVersion.ReadAsync(db,assetId,ct);
        var photoLimits=await AssetRegistrationPhotos.LimitsAsync(db,asset.BranchId,ct);
        var size=await profile.PageSizeAsync(ct);
        var total=await db.AssetHandovers.AsNoTracking().CountAsync(h=>h.AssetId==assetId&&scopedVersions.Contains(h.HandoverId),ct);
        var historyIncomplete=await db.AssetHandovers.AsNoTracking().AnyAsync(h=>h.AssetId==assetId&&!scopedVersions.Contains(h.HandoverId),ct);
        var history=await db.Database.SqlQuery<CustodyHistoryRow>($"""
            SELECT h.handover_id AS Id,h.handover_no AS Number,h.handover_status AS State,h.created_at AS CreatedAt,
              h.condition_note AS ConditionNote,h.created_by AS SenderId,v.snapshot_json AS Snapshot,
              (SELECT TOP(1) da.new_values FROM dbo.audit_logs da WHERE da.entity_type='asset_handovers'
                AND da.entity_id=CONVERT(varchar(30),h.handover_id) AND da.action='UPDATE' AND da.branch_id={asset.BranchId}
                AND da.actor_user_id=h.to_user_id
                AND JSON_VALUE(CASE WHEN ISJSON(da.new_values)=1 THEN da.new_values ELSE '[]' END,'$.notification_kind')='ASSET_HANDOVER_DECIDED'
                ORDER BY da.audit_id DESC) AS DecisionSnapshot,
              CONVERT(bit,CASE WHEN h.handover_status='REJECTED' AND h.created_by={access.UserId}
                AND NOT EXISTS(SELECT 1 FROM dbo.asset_handover_versions next WHERE next.previous_handover_id=h.handover_id)
                THEN 1 ELSE 0 END) AS CanResubmit
            FROM dbo.asset_handovers h JOIN dbo.asset_handover_versions v ON v.handover_id=h.handover_id
            JOIN dbo.audit_logs a ON a.audit_id=v.submission_audit_id
            WHERE h.asset_id={assetId} AND a.branch_id={asset.BranchId} AND a.entity_type='asset_handovers' AND a.action='CREATE'
              AND a.entity_id=CONVERT(varchar(30),v.handover_id) AND a.new_values=v.snapshot_json
              AND TRY_CONVERT(bigint,JSON_VALUE(v.snapshot_json,'$.from_branch_id'))={asset.BranchId}
              AND TRY_CONVERT(bigint,JSON_VALUE(v.snapshot_json,'$.to_branch_id'))={asset.BranchId}
            ORDER BY h.created_at DESC,h.handover_id DESC
            OFFSET {(page-1)*size} ROWS FETCH NEXT {size} ROWS ONLY
            """).ToArrayAsync(ct);
        await profile.RequireAsync(asset.BranchId,ct);
        if(sender&& !await AssetHandoverAuthority.AllowsAsync(db,access.UserId,asset.BranchId,asset.CurrentDepartmentId!.Value,false,ct))
            throw new InsightException(403,"ACCESS_DENIED","Custody authority changed.");
        await tx.CommitAsync(ct);
        return new{asset.AssetId,asset.AssetCode,asset.AssetName,asset.BranchId,asset.CurrentDepartmentId,asset.CustodianUserId,
            asset.ConditionStatus,asset.LifecycleStatus,Version=version,CanSubmit=sender&&!anyPending,CanDecide=canDecide,Targets=targets,Pending=pending,
            History=history,HistoryIncomplete=historyIncomplete,Page=page,PageSize=size,Total=total,PhotoLimits=photoLimits};
    }
    private sealed class CustodyHistoryRow
    {
        public long Id{get;set;} public string Number{get;set;}=""; public string State{get;set;}="";
        public DateTime CreatedAt{get;set;} public string? ConditionNote{get;set;} public long SenderId{get;set;}
        public string? Snapshot{get;set;} public string? DecisionSnapshot{get;set;} public bool CanResubmit{get;set;}
    }
}
