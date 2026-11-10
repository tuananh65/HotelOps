using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    // Producer-side integrity preparation only: returns no bytes, URL, endpoint or recipient
    // read capability. Key/digest/version is persisted in existing attachments.storage_key,
    // already included in FE04's original EvidenceDigest; no second authority hash.
    internal async Task<InspectionEvidenceReadLease> PinVersionedInspectionEvidenceInCallerTransactionAsync(SessionReference ticket,long sid,
        long branch,long inspection,long attachment,string expectedInspectionETag,string root,NotificationCommitGuard guard,CancellationToken ct)
    {
        var tx=Fe05CallerTransaction.Require(db);guard.RequireAttached(db,tx);
        InspectionEvidenceReadLease? lease=null;
        try
        {
            if(inspectionUsers==null||!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted||ticket.IsRestricted)
                throw new Fe05NotificationUnavailableException("FE05_EVIDENCE_OWNER_REQUIRED");
            inspectionUsers.RequireMember(actor.UserId);var roster=inspectionUsers.Roster;
            if(roster.InspectionId!=inspection||roster.BranchId!=branch||!WorkflowStrongTag(expectedInspectionETag))
                throw new Fe05NotificationUnavailableException("FE05_EVIDENCE_SOURCE_MISMATCH");
            var session=await sessions.ValidateSessionAsync(ticket,ct);
            if(!session.IsValid||session.Session?.SessionId!=sid||session.Session.IsRestricted||!(await permissions.EvaluateBranchAsync("INSPECTION_PERFORM",branch,ct)).IsGranted)
                throw new Fe05NotificationUnavailableException("FE05_EVIDENCE_OWNER_AUTHORIZATION_REQUIRED");
            async Task<VersionedEvidenceRow> Read(CancellationToken token)
                =>await db.Database.SqlQuery<VersionedEvidenceRow>($"""
                    SELECT a.storage_key AS StorageKey,a.file_size_bytes AS Size,i.row_version AS InspectionRevision
                    FROM dbo.attachments a WITH(HOLDLOCK)
                    JOIN dbo.room_inspections i WITH(HOLDLOCK) ON a.entity_type='INSPECTION' AND a.entity_id=i.inspection_id
                    JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=i.room_id AND r.branch_id={branch} AND r.is_active=1
                    JOIN dbo.room_cleaning_cycle_state c WITH(HOLDLOCK) ON c.room_id=i.room_id AND c.cleaning_task_id=i.cleaning_task_id
                    WHERE a.attachment_id={attachment} AND a.deleted_at IS NULL AND i.inspection_id={inspection}
                      AND i.inspector_user_id={actor.UserId} AND i.cleaning_task_id={roster.TaskId}
                      AND c.row_version={Convert.FromHexString(roster.CycleRevision)}
                    """).SingleOrDefaultAsync(token)??throw new Fe05NotificationUnavailableException("FE05_DIRECT_VERSIONED_EVIDENCE_REQUIRED");
            var row=await Read(ct);
            if(row.Size==null||WorkflowTag(row.InspectionRevision)!=expectedInspectionETag)throw new Fe05NotificationUnavailableException("FE05_EVIDENCE_SOURCE_STALE");
            var version=InspectionEvidenceVersion.Parse(row.StorageKey,row.Size.Value);
            lease=await InspectionEvidenceReadLease.OpenAsync(root,version,ct);
            var physical=db.Database.CurrentTransaction!.GetDbTransaction();var pinned=lease;
            guard.BindGraph(db,tx,"FE05:Evidence:"+attachment+":"+Guid.NewGuid(),async token=>
            {
                try
                {
                    Fe05CallerTransaction.Require(db,db.Database.GetDbConnection(),physical);
                    var observed=await Read(token);
                    // The conclusion itself may legitimately advance the Inspection revision;
                    // original source revision is checked above, storage key/length stay fixed; FE04 still owns full proof metadata.
                    if(observed.Size!=row.Size||observed.StorageKey!=row.StorageKey)throw new Fe05NotificationUnavailableException("FE05_EVIDENCE_BINDING_CHANGED");
                    await pinned.VerifyAsync(token);
                }
                catch{guard.Fail(db,tx);throw;}
            });
            return pinned;
        }
        catch{if(lease!=null)await lease.DisposeAsync();guard.Fail(db,tx);throw;}
    }
    private sealed class VersionedEvidenceRow
    {public string StorageKey{get;set;}="";public long? Size{get;set;}public byte[] InspectionRevision{get;set;}=[];}
}
