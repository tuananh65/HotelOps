using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;
internal static class IncidentRoomBlockSource
{
    internal const string Kind="INCIDENT_ROOM_BLOCK_CHANGED",SourceType="INCIDENT_ROOM_BLOCK";
    internal sealed record Source(long AuditId,long RoomId,long IncidentId,long BranchId,long ActorId,DateTime At,long BlockId,string Action,string Readiness);
    internal static Source? Parse(AuditLog a)
    {
        if(a.EntityType!="rooms" || a.Action!="UPDATE" || a.AuditId<=0 || a.ActorUserId is not >0 || a.BranchId is not >0 || !long.TryParse(a.EntityId,out var room) || room<=0) return null;
        try
        {
            using var json=JsonDocument.Parse(a.NewValues ?? "null");var r=json.RootElement;
            var incident=r.GetProperty("incident_id").GetInt64();var block=r.GetProperty("block_id").GetInt64();
            var action=r.GetProperty("action").GetString();var readiness=r.GetProperty("effective_readiness").GetString();
            if(r.GetProperty("notification_kind").GetString()!=Kind || r.GetProperty("notification_version").GetInt32()!=1
                || incident<=0 || block<=0 || action is not ("BLOCK" or "REMOVE") || string.IsNullOrEmpty(readiness)) return null;
            return new(a.AuditId,room,incident,a.BranchId.Value,a.ActorUserId.Value,a.OccurredAt,block,action,readiness);
        }
        catch(Exception e) when(e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException){return null;}
    }
    internal static async Task<Source?> ReadAsync(HotelOpsDbContext db,long id,CancellationToken ct)
    {
        var audit=await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a=>a.AuditId==id,ct);var source=audit==null?null:Parse(audit);
        if(source==null || db.Database.CurrentTransaction==null) return null;
        return await db.RoomBlocks.AsNoTracking().AnyAsync(b=>b.BlockId==source.BlockId && b.RoomId==source.RoomId && b.IncidentId==source.IncidentId
            && b.Room.BranchId==source.BranchId && b.Incident!.BranchId==source.BranchId
            && (source.Action=="BLOCK" ? b.BlockedAt==source.At && b.BlockedBy==source.ActorId : b.ReleasedAt==source.At && b.ReleasedBy==source.ActorId),ct) ? source : null;
    }
    internal static Task<bool> CanReadAsync(HotelOpsDbContext db,long user,Source r,CancellationToken ct)
        => IncidentRoomReleaseSource.CanReadAsync(db,user,new(r.AuditId,r.RoomId,0,r.IncidentId,r.BranchId,r.ActorId,r.At,r.BlockId.ToString(CultureInfo.InvariantCulture),r.Readiness),ct);
}
