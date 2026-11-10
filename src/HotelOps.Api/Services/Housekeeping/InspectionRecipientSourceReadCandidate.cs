using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

internal enum InspectionRecipientRelationship { ResponsibleSupervisor, OriginalCleaner, ReworkAssignee }
// A proposal assessment is NEVER an authorization token, response DTO or file capability.
internal sealed record InspectionRecipientReadAssessment(bool ExistingGrantAndRelationshipMatch,string Reason)
{
    internal string? AcceptedConcreteBinding=>null;
    internal bool CanRead=>false;
    internal bool CanReadPrivateEvidence=>false;
}

internal sealed class InspectionRecipientSourceReadCandidate(HotelOpsDbContext db,InspectionUsersPrelock users,TimeProvider clock)
{
    internal async Task<InspectionRecipientReadAssessment> AssessAsync(Fe05NotificationSource source,long recipient,
        InspectionRecipientRelationship relationship,CancellationToken ct)
    {
        if(!Enum.IsDefined(relationship))return new(false,"INVALID_RELATIONSHIP");
        // Existing retained actual Users map, exact physical transaction, reread of cycle/
        // inspector/supervisor binding/R04 port; this NEVER adds a Users lock.
        await users.RequireAlreadyLockedAsync(db,source,recipient,ct);
        var roster=users.Roster;
        var expected=relationship switch {
            InspectionRecipientRelationship.ResponsibleSupervisor=>roster.SupervisorId,
            InspectionRecipientRelationship.OriginalCleaner=>roster.OriginalCleanerId,
            _=>roster.AssigneeId
        };
        var rework=relationship==InspectionRecipientRelationship.ReworkAssignee;
        if(expected!=recipient||recipient<=0||rework!=(source.ReworkId!=null)
            ||(!rework&&roster.Kind!=Fe05NotificationKind.InspectionFailed)
            ||(rework&&roster.Kind!=Fe05NotificationKind.CleaningReworkRequested))return new(false,"WRONG_PERSISTED_RELATIONSHIP");
        if(!TryRevision(source.Revision,out var revision)||source.InspectionStatus!="COMPLETED"
            ||source.InspectionResult!="FAIL"||source.WorkflowState!="FAILED")return new(false,"INVALID_FAILED_SOURCE");
        // Do not trust event/collection/client fields as persisted conclusion authority.
        if(await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.room_inspections i WITH(HOLDLOCK)
            JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=i.cleaning_task_id AND t.room_id=i.room_id
            JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=i.room_id AND r.branch_id={source.BranchId} AND r.is_active=1
            JOIN dbo.room_cleaning_cycle_state c WITH(HOLDLOCK) ON c.room_id=i.room_id AND c.cleaning_task_id=t.cleaning_task_id
            WHERE i.inspection_id={source.InspectionId} AND i.cleaning_task_id={source.CleaningTaskId}
              AND i.room_id={source.RoomId} AND i.version_id={source.ChecklistVersionId} AND i.row_version={revision}
              AND i.status='COMPLETED' AND i.result='FAIL' AND i.workflow_status='FAILED' AND i.inspected_at IS NOT NULL
              AND t.status='IN_PROGRESS' AND t.workflow_status='REWORK_REQUIRED'
              AND c.row_version={Convert.FromHexString(roster.CycleRevision)}
              AND NOT EXISTS(SELECT 1 FROM dbo.room_inspections newer WITH(HOLDLOCK)
                WHERE newer.cleaning_task_id=i.cleaning_task_id AND newer.inspection_id>i.inspection_id)
            """).SingleAsync(ct)!=1)return new(false,"STALE_OR_WRONG_PERSISTED_SOURCE");
        if(rework)await new Fe05Decision08MandatoryRecipients(users).ResolveMandatoryIdsAsync(db,source,Fe05NotificationKind.CleaningReworkRequested,ct);
        var now=clock.GetUtcNow().UtcDateTime;
        if(!await db.Users.AsNoTracking().AnyAsync(u=>u.UserId==recipient&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null,ct)
            ||!await db.Branches.AsNoTracking().AnyAsync(b=>b.BranchId==source.BranchId&&b.IsActive,ct)
            ||!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a=>a.UserId==recipient&&a.BranchId==source.BranchId&&a.RevokedAt==null&&a.GrantedAt<=now,ct))
            return new(false,"RECIPIENT_REVOKED_OR_OUTSIDE_BRANCH");
        // Candidate mapping only, pending PL/DEV3/FE04 approval. These are existing codes,
        // evaluated for the RECIPIENT (never the acting inspector's ambient permission).
        var code=relationship==InspectionRecipientRelationship.ResponsibleSupervisor?"INSPECTION_PERFORM":"CLEANING_VIEW";
        var paths=await new EfPermissionDataSource(db).ReadPathsAsync(recipient,code,now,ct);
        var match=paths.Any(g=>g.PermissionCode==code&&PermissionGrantRules.IsEligible(g,now)
            &&(PermissionGrantRules.EffectiveScope(g)=="GLOBAL"||g.BranchId==source.BranchId
                &&PermissionGrantRules.EffectiveScope(g) is "BRANCH" or "OWN"));
        // OWN is narrowed above to the exact persisted relationship. This is not an
        // accepted shared resolver, full Inspection history grant or same-tx FE04 proof.
        return new(match,match?"CANDIDATE_RELATIONSHIP_AND_EXISTING_GRANT_MATCH":"EXISTING_RECIPIENT_GRANT_REQUIRED");
    }
    private static bool TryRevision(string tag,out byte[] revision)
    {
        revision=[];
        if(tag.Length!=18||tag[0]!='"'||tag[^1]!='"')return false;
        try {revision=Convert.FromHexString(tag[1..^1]);return revision.Length==8;}catch(FormatException){return false;}
    }
}
