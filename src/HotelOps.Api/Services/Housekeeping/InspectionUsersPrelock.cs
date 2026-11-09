using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace HotelOps.Api.Services.HotelStructure;

// DECISION08 owner assertion. Only the reviewed BatchAsync callback supplies the real
// UserEligibilityLock results. No DMV, inferred roster, lock extension or second transaction.
public sealed class InspectionUsersPrelock(HotelOpsDbContext db,InspectionRecipientRosterReader reader,NotificationCommitGuard? guard = null)
    : IFe05RecipientPrelockAuthority
{
    private Guid? transactionId;
    private System.Data.Common.DbTransaction? physical;
    private InspectionRecipientRoster? retained;
    private bool poisoned;
    internal InspectionRecipientRoster Roster=>retained??throw new Fe05NotificationUnavailableException("FE05_COMPLETE_USERS_ROSTER_REQUIRED");
    internal Task RetainLockedUsersAsync(InspectionRecipientRoster discovered,
        IReadOnlyDictionary<long,UserEligibilityLock.LockedUser?> locked,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();Fe05CallerTransaction.Require(db);
        if(poisoned||transactionId!=null)throw Fail("FE05_ROSTER_CANNOT_BE_EXTENDED");
        try
        {
            var ids=discovered.Users;
            if(ids.Any(id=>id<=0)||!ids.SequenceEqual(locked.Keys.Order()))throw Fail("FE05_COMPLETE_USERS_RESULTS_MISMATCH");
            foreach(var id in ids)
            {
                UserEligibilityLock.RequireEligible(id,locked);
                if(locked[id]!.UserId!=id)throw Fail("FE05_LOCKED_USER_IDENTITY_MISMATCH");
            }
            // Capture immutable identity facts, never retain a mutable caller dictionary.
            retained=discovered;transactionId=db.Database.CurrentTransaction!.TransactionId;
            physical=db.Database.CurrentTransaction.GetDbTransaction();
            return Task.CompletedTask;
        }
        catch { Poison();throw; }
    }
    internal async Task ConfirmAfterSourcesAsync(bool allowOwnInspectionRevisionChange,CancellationToken ct)
    {
        RequireTransaction();var before=Roster;
        InspectionRecipientRoster now;
        try { now=await reader.ReadAsync(before.ActorId,before.BranchId,before.InspectionId,before.AssigneeId,before.Kind,true,ct); }
        catch { Poison();throw; }
        if(!before.SameParticipants(now)||(!allowOwnInspectionRevisionChange&&before.InspectionRevision!=now.InspectionRevision))
            throw Fail("FE05_ROSTER_CHANGED_ROLLBACK_REQUIRED");
    }
    internal void RequireMember(long id)
    {
        RequireTransaction();if(!Roster.Users.Contains(id))throw Fail("FE05_LATE_USER_ROLLBACK_REQUIRED");
    }
    public async Task RequireAlreadyLockedAsync(HotelOpsDbContext context,Fe05NotificationSource source,long recipientId,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(!ReferenceEquals(context,db))throw Fail("FE05_ROSTER_WRONG_CONTEXT");RequireMember(recipientId);var roster=Roster;
        if(source.ActorId!=roster.ActorId||source.BranchId!=roster.BranchId||source.InspectionId!=roster.InspectionId
            ||source.CleaningTaskId!=roster.TaskId||source.RoomId!=roster.RoomId||source.CurrentCleaningTaskId!=roster.TaskId)
            throw Fail("FE05_ROSTER_WRONG_SOURCE");
        if(source.ReworkId is {} rework)
        {
            var revision=Convert.FromHexString((source.ReworkRevision??throw Fail("FE05_REWORK_REVISION_REQUIRED")).Trim('"'));
            var assignee=await db.Database.SqlQuery<long>($"SELECT assignee_user_id AS Value FROM dbo.cleaning_reworks WITH(HOLDLOCK) WHERE rework_id={rework} AND inspection_id={source.InspectionId} AND cleaning_task_id={source.CleaningTaskId} AND row_version={revision}").SingleOrDefaultAsync(ct);
            if(assignee!=roster.AssigneeId||assignee!=recipientId)throw Fail("FE05_REWORK_RECIPIENT_CHANGED_ROLLBACK_REQUIRED");
        }
        await ConfirmAfterSourcesAsync(true,ct);
    }
    private void RequireTransaction()
    {
        if(poisoned||transactionId==null||db.Database.CurrentTransaction?.TransactionId!=transactionId
            ||!ReferenceEquals(db.Database.CurrentTransaction.GetDbTransaction(),physical))throw Fail("FE05_ROSTER_TRANSACTION_INVALID");
        Fe05CallerTransaction.Require(db,db.Database.GetDbConnection(),physical);
    }
    private void Poison(){poisoned=true;if(db.Database.CurrentTransaction is {} tx)guard?.Fail(db,tx.TransactionId);}
    private Fe05NotificationUnavailableException Fail(string reason){Poison();return new(reason);}
}
