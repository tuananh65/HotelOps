using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;
public sealed partial class RoomTypeCreationService
{
    public static IReadOnlyList<string> RoomLifecycleReleaseGates {get;}=Array.AsReadOnly(new[]{"ROOM_LIFECYCLE_V1_WRITER_GUARDS"});
    private async Task<bool> RoomLifecycleInstalled(CancellationToken ct)=>await HousekeepingSchema.InstalledAsync(db,ct)
        && await LocationGuardSchema.ReadyAsync(db,ct) && await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.RoomLifecycle.V1' AND CONVERT(nvarchar(10),value)='1')
              AND COL_LENGTH('dbo.room_status_history','effective_from')=40 AND COL_LENGTH('dbo.room_status_history','effective_to')=40
              AND (SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=0 AND name IN('TR_room_lifecycle_cleaning_v1',
                'TR_room_lifecycle_reworks_v1','TR_room_lifecycle_inspections_v1','TR_room_lifecycle_blocks_v1','TR_room_lifecycle_assets_v1','TR_room_lifecycle_intervals_v1'))=6
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)==1;
    private static CreatedRoomDto LifecycleRoom(Room r)=>new(r.RoomId,r.BranchId,r.FloorId,r.AreaId,r.RoomTypeId,r.RoomNumber,r.Description,r.OperationalStatus,r.IsActive);
    public Task<RoomTypeBatchResult> ReadRoomLifecycleAsync(SessionReference ticket,long sessionId,long branchId,long roomId,CancellationToken ct)
        =>BatchAsync(ticket,sessionId,branchId,async(_,token)=>{
            if(!await RoomLifecycleInstalled(token))return new(RoomTypeBatchStatus.SchemaRequired);
            var room=await db.Rooms.AsNoTracking().SingleOrDefaultAsync(r=>r.RoomId==roomId&&r.BranchId==branchId,token);
            if(room==null)return new(RoomTypeBatchStatus.NotFound);
            var value=LifecycleRoom(room);
            var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token);
            var readiness=await new EffectiveRoomReadiness(db).ReadAsync([roomId],now,token);
            var dependencies=room.IsActive?await RoomLifecycleDependencies(branchId,roomId,token):[];
            return new(RoomTypeBatchStatus.Success,new{room=value,effectiveReadiness=readiness[roomId].Readiness,dependencies},await LifecycleTag(value,room.UpdatedAt,token));
        },ct,featurePermission:token=>RoomTypeAdministrationAsync(branchId,token));
    private async Task<string> LifecycleTag(CreatedRoomDto value,DateTime? updated,CancellationToken ct)
    {
        var key=value.Id.ToString(CultureInfo.InvariantCulture);
        var revision=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="rooms"&&a.EntityId==key).Select(a=>(long?)a.AuditId).MaxAsync(ct)??0;
        return '"'+Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{value,updated,revision},Json)))+'"';
    }
    public Task<RoomTypeBatchResult> ChangeRoomLifecycleCandidateAsync(SessionReference ticket,long sessionId,long branchId,long roomId,
        bool reactivate,string expected,RoomLifecycleRequest request,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ticket);ArgumentNullException.ThrowIfNull(request);
        var reason=request.ValidatedReason();Batch2Tag(expected);
        if(branchId<=0||roomId<=0)throw new ValidationException("Positive scoped room identity required.");
        return BatchAsync(ticket,sessionId,branchId,async(actor,token)=>{
            if(!await RoomLifecycleInstalled(token))return new(RoomTypeBatchStatus.SchemaRequired);
            var room=await db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.rooms WITH(UPDLOCK,HOLDLOCK) WHERE room_id={roomId} AND branch_id={branchId}")
                .AsNoTracking().SingleOrDefaultAsync(token);
            if(room==null)return new(RoomTypeBatchStatus.NotFound);
            var before=LifecycleRoom(room);
            if(await LifecycleTag(before,room.UpdatedAt,token)!=expected)return new(RoomTypeBatchStatus.Stale);
            if(room.IsActive==reactivate)return new(RoomTypeBatchStatus.Inactive);
            if(reactivate && (!await db.Floors.AsNoTracking().AnyAsync(f=>f.FloorId==room.FloorId&&f.BranchId==branchId&&f.IsActive,token)
                ||!await db.BranchRoomTypes.AnyAsync(t=>t.RoomTypeId==room.RoomTypeId&&(t.BranchId==null||t.BranchId==branchId)&&t.IsActive,token)
                ||room.AreaId is {} area&&!await db.Areas.AnyAsync(a=>a.AreaId==area&&a.BranchId==branchId&&a.IsActive&&(a.FloorId==null||a.FloorId==room.FloorId),token)))
                return new(RoomTypeBatchStatus.InvalidReference);
            if(!reactivate){var dependencies=await RoomLifecycleDependencies(branchId,roomId,token);
                if(dependencies.Length>0)return new(RoomTypeBatchStatus.DependenciesExist,dependencies);}
            var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token),DateTimeKind.Utc);
            var state=await RoomReadinessHistory.CaptureAsync(db,roomId,token,now);
            var physical=reactivate?"DIRTY":room.OperationalStatus;
            if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.rooms SET is_active={reactivate},operational_status={physical},updated_at={now} WHERE room_id={roomId} AND branch_id={branchId} AND is_active={room.IsActive}",token)!=1)
                return new(RoomTypeBatchStatus.Stale);
            var after=before with{IsActive=reactivate,OperationalStatus=physical};
            db.AuditLogs.Add(new AuditLog{ActorUserId=actor,BranchId=branchId,EntityType="rooms",EntityId=roomId.ToString(CultureInfo.InvariantCulture),
                Action="UPDATE",OccurredAt=now,OldValues=JsonSerializer.Serialize(before,Json),
                NewValues=JsonSerializer.Serialize(new{operation=reactivate?"REACTIVATE_ROOM":"DEACTIVATE_ROOM",record=after,reason},Json),
                ChangedFields="[\"isActive\",\"operationalStatus\"]"});
            await db.SaveChangesAsync(token);db.ChangeTracker.Clear();
            // Readiness consumes the committed-in-this-transaction activation audit epoch.
            await RoomReadinessHistory.FinishAsync(db,state,"ROOM_LIFECYCLE",roomId,actor,now,reason,token);
            var persistedUpdate=await db.Rooms.AsNoTracking().Where(r=>r.RoomId==roomId).Select(r=>r.UpdatedAt).SingleAsync(token);
            return new(RoomTypeBatchStatus.Success,after,await LifecycleTag(after,persistedUpdate,token));
        },ct,featurePermission:token=>RoomTypeAdministrationAsync(branchId,token));
    }
}
