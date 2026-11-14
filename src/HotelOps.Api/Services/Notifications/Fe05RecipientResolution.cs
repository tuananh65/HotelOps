using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

// Required IDs must be resolved by an accepted policy against retained authoritative assignments.
// No inferred supervisor, department broadcast or permissive default is supplied here.
public interface IFe05MandatoryRecipientAuthority
{
    Task<IReadOnlyList<long>> ResolveMandatoryIdsAsync(HotelOpsDbContext db,
        Fe05NotificationSource source, Fe05NotificationKind kind, CancellationToken ct);
}
public sealed class UnacceptedFe05MandatoryRecipients : IFe05MandatoryRecipientAuthority
{
    public Task<IReadOnlyList<long>> ResolveMandatoryIdsAsync(HotelOpsDbContext db,
        Fe05NotificationSource source, Fe05NotificationKind kind, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); throw new Fe05NotificationUnavailableException("FE05_MANDATORY_RECIPIENT_POLICY_NOT_ACCEPTED"); }
}

public sealed class Fe05SqlRecipientPolicy(HotelOpsDbContext db, IFe05MandatoryRecipientAuthority authority,
    IFe05SourceCoordination coordination, TimeProvider clock) : IFe05NotificationRecipientPolicy
{
    public async Task<IReadOnlyList<Fe05Recipient>> ResolveAsync(Fe05NotificationSource source,
        Fe05NotificationKind kind, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Fe05CallerTransaction.Require(db);
        var ids = (await authority.ResolveMandatoryIdsAsync(db,source,kind,ct)).Distinct().Order().ToArray();
        if (ids.Length is <1 or >200 || ids.Any(x=>x<=0))
            throw new Fe05NotificationUnavailableException("FE05_RECIPIENTS_UNAVAILABLE");
        var recipients = new List<Fe05Recipient>();
        foreach (var id in ids)
        {
            // HOLDLOCK reads protect the already prelocked eligibility set; no late Users U-lock
            // after source locks. Owner protocol must acquire Users ascending before domain work.
            var eligible = await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM dbo.users u WITH(HOLDLOCK)
                WHERE u.user_id={id} AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE' AND u.deleted_at IS NULL
                  AND EXISTS(SELECT 1 FROM dbo.user_branch_access a WITH(HOLDLOCK)
                    WHERE a.user_id=u.user_id AND a.branch_id={source.BranchId}
                      AND a.revoked_at IS NULL AND a.granted_at<={clock.GetUtcNow().UtcDateTime})
                """).SingleAsync(ct)==1;
            var canRead = eligible && await coordination.CanRecipientReadAsync(db,source,id,ct);
            if (!canRead) throw new Fe05NotificationUnavailableException("FE05_RECIPIENTS_UNAVAILABLE");
            recipients.Add(new(id,source.BranchId,true,true));
        }
        // Re-resolve under the retained owner locks; a changed mandatory set is a conflict.
        if (!ids.SequenceEqual((await authority.ResolveMandatoryIdsAsync(db,source,kind,ct)).Distinct().Order()))
            throw new NotificationConflictException();
        return recipients.AsReadOnly();
    }
}
