using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.Notifications;
internal static class ShiftCancellationSource
{
    internal const string Kind="SHIFT_CANCELLED";
    internal static async Task<ShiftCancellationReceipt?> TargetAsync(HotelOpsDbContext db,long auditId,long user,long? creator,DateTime created,CancellationToken ct)
    {
        var audit=await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a=>a.AuditId==auditId&&a.EntityType=="shift_assignments"&&a.Action=="UPDATE"&&a.ActorUserId==creator&&a.OccurredAt==created,ct);
        if(audit?.NewValues==null)return null;
        ShiftCancellationReceipt? receipt;
        try{receipt=JsonSerializer.Deserialize<ShiftCancellationReceipt>(audit.NewValues);}catch(JsonException){return null;}
        if(receipt==null||receipt.Operation!="SHIFT_CANCEL"||receipt.EmployeeId!=user||receipt.AssignmentId<=0
            ||audit.EntityId!=receipt.AssignmentId.ToString(CultureInfo.InvariantCulture)||audit.BranchId!=receipt.BranchId)return null;
        if(!await db.ShiftAssignments.AsNoTracking().AnyAsync(a=>a.ShiftAssignmentId==receipt.AssignmentId&&a.UserId==user&&a.ShiftId==receipt.ShiftId
            &&a.AssignmentStatus=="CANCELLED"&&a.CancellationReason==receipt.Reason&&a.Shift.DepartmentId==receipt.DepartmentId,ct))return null;
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        return await ShiftAssignmentNoticeAccess.BranchAsync(db,receipt.AssignmentId,user,now,ct,true)==receipt.BranchId?receipt:null;
    }
}
