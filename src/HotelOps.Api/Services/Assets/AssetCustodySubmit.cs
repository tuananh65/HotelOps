using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed record AssetCustodySubmitInput(long DepartmentId,long ReceiverId,string ConditionNote,string? Notes,
    long? PreviousHandoverId,IReadOnlyList<EvidenceUpload>? Photos);

public sealed partial class AssetCustodyWorkflow
{
    public async Task<object> SubmitAsync(long assetId,string? expected,AssetCustodySubmitInput input,string privateRoot,CancellationToken ct)
    {
        var condition=input.ConditionNote?.Trim();var notes=input.Notes?.Trim();
        if(assetId<=0||input.DepartmentId<=0||input.ReceiverId<=0||input.PreviousHandoverId is <=0
            ||string.IsNullOrEmpty(condition)||condition.Length>4000||notes?.Length>4000
            ||condition.Any(char.IsControl)||notes?.Any(c=>char.IsControl(c)&&c is not '\r' and not '\n' and not '\t')==true)
            throw new InsightException(400,"MSG03","Receiving department/head and condition are required; notes and condition maximum 4000 characters.");
        AssetRegistrationPhotos.Validate(input.Photos);
        using var files=new EvidenceFileLifetime();
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([access.UserId,input.ReceiverId],ct);
        await RequireStorageAsync(ct);
        var asset=await db.Assets.FromSqlInterpolated($"SELECT * FROM dbo.assets WITH(UPDLOCK,HOLDLOCK) WHERE asset_id={assetId}")
            .AsNoTracking().SingleOrDefaultAsync(ct)??throw new InsightException(404,"ASSET_UNAVAILABLE","Asset unavailable.");
        await profile.RequireAsync(asset.BranchId,ct);
        if(asset.CurrentDepartmentId is not {} source||asset.LifecycleStatus is not("IN_USE" or "UNDER_REPAIR")
            ||!await profile.Query(asset.BranchId).AnyAsync(a=>a.AssetId==assetId,ct))
            throw new InsightException(409,"ASSET_CUSTODY_INELIGIBLE","Operational asset with consistent current custody required.");
        await RequireParties();
        var token=await AssetVersion.RequireAsync(db,assetId,expected,ct);
        if(source==input.DepartmentId)throw new InsightException(400,"SAME_CUSTODY","Select a different receiving department.");
        if(await db.AssetHandovers.AnyAsync(h=>h.AssetId==assetId&&h.HandoverStatus=="PENDING",ct))
            throw new InsightException(409,"HANDOVER_PENDING","Resolve the pending handover first.");
        if(input.PreviousHandoverId is {} previous)
        {
            if(!await db.AssetHandovers.AsNoTracking().AnyAsync(h=>h.HandoverId==previous&&h.AssetId==assetId
                &&h.HandoverStatus=="REJECTED"&&h.CreatedBy==access.UserId&&h.FromDepartmentId==source
                &&h.FromUserId==asset.CustodianUserId,ct))
                throw new InsightException(409,"RESUBMIT_INELIGIBLE","Only the original sender can resubmit a rejected handover with unchanged source custody.");
            if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.asset_handover_versions WITH(HOLDLOCK) WHERE previous_handover_id={previous}").SingleAsync(ct)>0)
                throw new InsightException(409,"RESUBMIT_EXISTS","This version already has a successor.");
        }
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var handover=new AssetHandover{AssetId=assetId,HandoverNo="HO-"+Guid.NewGuid().ToString("N"),FromDepartmentId=source,
            FromUserId=asset.CustodianUserId,ToDepartmentId=input.DepartmentId,ToUserId=input.ReceiverId,ConditionNote=condition,
            HandoverStatus="PENDING",HandedOverAt=now,CreatedAt=now,CreatedBy=access.UserId};
        db.AssetHandovers.Add(handover);await db.SaveChangesAsync(ct);
        await AssetRegistrationPhotos.StageAsync(db,assetId,access.UserId,now,input.Photos,privateRoot,files,ct);
        var photoIds=db.ChangeTracker.Entries<Attachment>().Select(e=>e.Entity.AttachmentId).Order().ToArray();
        var snapshot=new Dictionary<string,object?>{
            ["handover_id"]=handover.HandoverId,["handover_no"]=handover.HandoverNo,["asset_id"]=assetId,
            ["branch_id"]=asset.BranchId,["from_branch_id"]=asset.BranchId,["to_branch_id"]=asset.BranchId,
            ["from_department_id"]=source,["to_department_id"]=input.DepartmentId,["from_user_id"]=asset.CustodianUserId,["to_user_id"]=input.ReceiverId,
            ["created_by"]=access.UserId,["created_at"]=now,["handed_over_at"]=now,["accepted_at"]=null,["handover_status"]="PENDING",
            ["condition_note"]=condition,["condition_status"]=asset.ConditionStatus,["notes"]=notes,["photo_ids"]=photoIds,
            ["previous_handover_id"]=input.PreviousHandoverId,["notification_kind"]=AssetHandoverSource.Kind,["notification_version"]=1};
        var serialized=JsonSerializer.Serialize(snapshot);
        var audit=new AuditLog{EntityType="asset_handovers",EntityId=handover.HandoverId.ToString(CultureInfo.InvariantCulture),
            BranchId=asset.BranchId,ActorUserId=access.UserId,Action="CREATE",OccurredAt=now,NewValues=serialized};
        db.AuditLogs.Add(audit);await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.asset_handover_versions(handover_id,previous_handover_id,submission_audit_id,snapshot_json)
            VALUES({handover.HandoverId},{input.PreviousHandoverId},{audit.AuditId},{serialized});
            """,ct);
        // Bump the asset token without changing custody. A replay cannot create another version.
        if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.assets SET updated_at={now} WHERE asset_id={assetId} AND row_version={token}",ct)!=1)
            throw new InsightException(412,"MSG04","Asset changed. Reload before submitting.");
        await profile.RequireAsync(asset.BranchId,ct);await RequireParties();
        var result=new{handover.HandoverId,handover.HandoverNo,State="PENDING_ACKNOWLEDGMENT",Version=await AssetVersion.ReadAsync(db,assetId,ct)};
        await files.BeforeCommitAsync(ct);await tx.CommitAsync(ct);return result;

        async Task RequireParties()
        {
            if(!await AssetHandoverAuthority.AllowsAsync(db,access.UserId,asset.BranchId,source,false,ct)
                ||!await AssetHandoverAuthority.AllowsAsync(db,input.ReceiverId,asset.BranchId,input.DepartmentId,true,ct))
                throw new InsightException(403,"ACCESS_DENIED","Current custody authority and an eligible receiving Department Head are required.");
        }
    }
}
