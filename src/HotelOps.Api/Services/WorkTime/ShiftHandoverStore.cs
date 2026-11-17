using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

internal sealed class ShiftHandoverRow
{
    public long Id {get;set;} public Guid RequestKey {get;set;} public long WorkdayId {get;set;}
    public long BranchId {get;set;} public long DepartmentId {get;set;} public long SenderId {get;set;}
    public long ReceiverId {get;set;} public long ReceiverAssignmentId {get;set;} public long? PreviousId {get;set;}
    public string Snapshot {get;set;}=""; public string? GeneralNote {get;set;} public string State {get;set;}="";
    public DateTime SubmittedAt {get;set;} public long SubmissionAuditId {get;set;}
    public DateTime? DecisionAt {get;set;} public long? DecisionAuditId {get;set;} public string? Reason {get;set;}
    public byte[] Version {get;set;}=[];
}
internal static class ShiftHandoverStore
{
    internal static IQueryable<ShiftHandoverRow> Rows(HotelOpsDbContext db)=>db.Database.SqlQuery<ShiftHandoverRow>($"""
        SELECT handover_id AS Id,request_key AS RequestKey,workday_id AS WorkdayId,branch_id AS BranchId,department_id AS DepartmentId,
          sender_id AS SenderId,receiver_id AS ReceiverId,receiver_shift_assignment_id AS ReceiverAssignmentId,previous_handover_id AS PreviousId,
          snapshot_json AS Snapshot,general_note AS GeneralNote,state AS State,submitted_at AS SubmittedAt,submission_audit_id AS SubmissionAuditId,
          decision_at AS DecisionAt,decision_audit_id AS DecisionAuditId,rejection_reason AS Reason,row_version AS Version
        FROM dbo.operational_shift_handovers WITH(HOLDLOCK)
        """);
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db,CancellationToken ct)=>await db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.ShiftHandover.V1' AND CONVERT(nvarchar(20),value)='1')
          AND COL_LENGTH('dbo.operational_shift_handovers','row_version')=8
          AND (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.operational_shift_handovers')
            AND name IN('PK_operational_shift_handovers','UQ_operational_shift_handover_request') AND is_unique=1 AND is_disabled=0)=2
          AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.operational_shift_handover_outbox')
            AND name='PK_operational_shift_handover_outbox' AND is_primary_key=1 AND is_disabled=0)
          AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID('dbo.operational_shift_handovers'),OBJECT_ID('dbo.operational_shift_handover_outbox'))
            AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0 AND update_referential_action=0)=13
          AND EXISTS(SELECT 1 FROM sys.triggers WHERE parent_id=OBJECT_ID('dbo.operational_shift_handovers') AND name='TR_operational_shift_handover_immutable' AND is_disabled=0)
          AND (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id IN(OBJECT_ID('dbo.operational_shift_handovers'),OBJECT_ID('dbo.operational_shift_handover_outbox'))
            AND is_disabled=0 AND is_not_trusted=0)=4
          THEN 1 ELSE 0 END AS Value
        """).SingleAsync(ct)==1;
}
