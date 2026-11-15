using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

internal sealed class InternalRequestHandoverRow
{
    public long Id{get;set;} public long RequestId{get;set;} public long BranchId{get;set;} public long DepartmentId{get;set;}
    public long SenderAssignmentId{get;set;} public long ReceiverId{get;set;}
    public string Summary{get;set;}=null!; public string? Note{get;set;} public string State{get;set;}=null!;
    public DateTime CreatedAt{get;set;} public long? DecisionEventId{get;set;} public DateTime? DecisionAt{get;set;}
    public string? RejectionReason{get;set;} public long? NewAssignmentId{get;set;}
}

internal static class InternalRequestHandoverStore
{
    internal static string Required(string? value)
    {
        var text=value?.Trim();
        if(string.IsNullOrEmpty(text)||text.Length>4000||text.Any(c=>char.IsControl(c)&&c!='\n'&&c!='\r'&&c!='\t'))
            throw new ValidationException("A nonblank handover summary/reason of at most 4000 characters is required.");
        return text;
    }
    internal static string? Optional(string? value)=>string.IsNullOrWhiteSpace(value)?null:Required(value);
    internal static async Task EnsureNoPendingAsync(HotelOpsDbContext db,long request,CancellationToken ct)
    {
        var exists=await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.internal_request_handovers') IS NULL THEN 0 ELSE 1 END AS Value").SingleAsync(ct);
        if(exists==0)return;
        if(!await InstalledAsync(db,ct))throw new InternalRequestReadException(503,"IR_HANDOVER_SCHEMA_REQUIRED");
        if(await Rows(db).AnyAsync(h=>h.RequestId==request&&h.State=="PENDING_ACKNOWLEDGMENT",ct))throw new InternalRequestReadException(409,"IR_HANDOVER_PENDING");
    }
    internal static IQueryable<InternalRequestHandoverRow> Rows(HotelOpsDbContext db)=>db.Database.SqlQuery<InternalRequestHandoverRow>($"""
        SELECT handover_id AS Id,request_id AS RequestId,branch_id AS BranchId,department_id AS DepartmentId,
          sender_assignment_id AS SenderAssignmentId,receiver_id AS ReceiverId,summary AS Summary,note AS Note,state AS State,
          created_at AS CreatedAt,decision_event_id AS DecisionEventId,decision_at AS DecisionAt,rejection_reason AS RejectionReason,new_assignment_id AS NewAssignmentId
        FROM dbo.internal_request_handovers
        """);
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db,CancellationToken ct)=>
        await db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.InternalRequestHandover.V1' AND CONVERT(nvarchar(20),value)='1')
          AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID('dbo.internal_request_handovers'),OBJECT_ID('dbo.internal_request_handover_outbox')) AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=11
          AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.internal_request_handovers') AND name='UX_ir_handover_pending' AND is_unique=1 AND is_disabled=0 AND filter_definition=N'([state]=''PENDING_ACKNOWLEDGMENT'')')
          AND (SELECT COUNT(*) FROM sys.check_constraints WHERE name IN('CK_ir_handover_state','CK_ir_handover_summary','CK_ir_handover_recipient') AND is_disabled=0 AND is_not_trusted=0)=3
          THEN 1 ELSE 0 END AS Value
        """).SingleAsync(ct)==1;
}
