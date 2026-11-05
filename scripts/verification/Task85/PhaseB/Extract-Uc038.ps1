$ErrorActionPreference='Stop'
$snapshot='cf8fb020362f8f64192e7f2f14a10547cebe5b91'
function Historical([string]$path) { $value=git show "${snapshot}:$path"; if($LASTEXITCODE){throw "Missing $path"}; return ($value -join "`n")+"`n" }
function NewSource([string]$path,[string]$value) {if(Test-Path -LiteralPath $path){throw "Existing source $path"}; [IO.File]::WriteAllText((Join-Path (Get-Location) $path),$value,[Text.UTF8Encoding]::new($false))}
$s=Historical 'src/HotelOps.Api/Services/HotelStructure/OperationalHistoryService.cs'
$begin=$s.IndexOf('    public Task<RoomTypeBatchResult> InspectionHistoryAsync(')
$end=$s.IndexOf('    public Task<RoomTypeBatchResult> TurnoverHistoryAsync(')
if($begin -lt 0 -or $end -le $begin){throw 'Unexpected historical methods'}
$s=$s.Remove($begin,$end-$begin).Replace('public sealed partial class RoomTypeCreationService','public sealed partial class TurnoverHistoryService')
$s=$s.Replace('RoomTypeBatchResult','TurnoverHistoryResult').Replace('RoomTypeBatchStatus','TurnoverHistoryStatus').Replace('BatchAsync(','ReadAsync(')
$s=$s.Replace('RoomTypeSchema.RoomsAsync','TurnoverHistorySchema.RoomsAsync').Replace('ChecklistSchema.StateAsync','TurnoverHistorySchema.ChecklistStateAsync').Replace('HousekeepingSchema.InstalledAsync','TurnoverHistorySchema.HousekeepingInstalledAsync')
$s=$s.Replace('}, ct, allowScopedList: true, requireRoomTypeSchema: false, permissionCode: "CLEANING_VIEW");','}, ct);')
$s=$s.Replace('db.ChecklistCriteria.AsNoTracking()','db.Database.SqlQueryRaw<CriterionGroupRow>("SELECT item_id AS ItemId,criterion_group AS [Group] FROM dbo.inspection_checklist_items")')
# Return only the restored turnover attachment route; UC048 is not being restored.
$s=$s.Replace('var inspections = await HistoryInspectionDetails(inspectionRows, token);','var inspections = (await HistoryInspectionDetails(inspectionRows, token)).Select(i => i with { Evidence = i.Evidence.Select(e => e with { DownloadPath = $"/api/rooms/{roomId}/turnover-history/{cycleId}/evidence/{e.Id}?branchId={room.BranchId}" }).ToArray() }).ToArray();')
$s=$s.Replace('    public sealed class DefectLocationRow', '    private sealed class CriterionGroupRow { public long ItemId { get; set; } public string? Group { get; set; } }'+"`n"+'    public sealed class DefectLocationRow')
NewSource 'src/HotelOps.Api/Services/HotelStructure/TurnoverHistoryService.cs' $s
$files=Historical 'src/HotelOps.Api/Services/HotelStructure/OperationalHistoryFiles.cs'
NewSource 'src/HotelOps.Api/Services/HotelStructure/TurnoverHistoryFiles.cs' ($files.Replace('RoomTypeCreationService','TurnoverHistoryService'))
$directory=[IO.File]::ReadAllText((Join-Path (Get-Location) 'src/HotelOps.Api/Services/HotelStructure/RoomTypeDirectoryService.cs'))
$begin=$directory.IndexOf('    private async Task<RoomTypeDirectoryResult> ReadAsync(')
$end=$directory.IndexOf('    private IQueryable<RoomTypeDirectoryRow> Rows()')
$boundary=$directory.Substring($begin,$end-$begin).Replace('RoomTypeDirectory','TurnoverHistory')
$boundary=$boundary.Replace('            if (await TurnoverHistorySchema.ReadAsync(db, ct) != 1) return new(TurnoverHistoryStatus.SchemaRequired);','')
$header=@'
using System.ComponentModel.DataAnnotations;
using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public enum TurnoverHistoryStatus { Success, SessionInvalid, Forbidden, NotFound, Inactive, SchemaRequired, Unavailable }
public sealed record TurnoverHistoryResult(TurnoverHistoryStatus Status, object? Value = null);

/// <summary>UC038 read-only extraction. Owns only its read transaction; no business writers.</summary>
public sealed partial class TurnoverHistoryService(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, ISqlSessionService sessions)
{
    private const string Permission = "CLEANING_VIEW";
'@
$footer=@'
    private static int? SqlNumber(Exception error)
    { for (Exception? e = error; e != null; e = e.InnerException) if (e is SqlException sql) return sql.Number; return null; }
}
'@
NewSource 'src/HotelOps.Api/Services/HotelStructure/TurnoverHistoryBoundary.cs' ($header+"`n"+$boundary+$footer+"`n")
$schema=@'
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

// Read-only historical compatibility probes; no DDL or shared EF model changes.
internal static class TurnoverHistorySchema
{
    internal static async Task<IQueryable<Room>?> RoomsAsync(HotelOpsDbContext db, CancellationToken ct)
        => await RoomTypeDirectorySchema.ReadAsync(db, ct) switch
        {
            0 => db.Rooms,
            1 => db.Rooms.FromSqlRaw("SELECT r.* FROM dbo.rooms r INNER JOIN dbo.room_types t ON t.room_type_id=r.room_type_id WHERE t.branch_id IS NULL OR t.branch_id=r.branch_id"),
            _ => null
        };
'@
$check=Historical 'src/HotelOps.Api/Data/ChecklistSchema.cs'
$body=$check.Substring($check.IndexOf('{')+1); $body=$body.Substring(0,$body.LastIndexOf('}')).Replace('StateAsync','ChecklistStateAsync')
$hk=Historical 'src/HotelOps.Api/Data/HousekeepingSchema.cs'
$hbody=$hk.Substring($hk.IndexOf('{')+1); $hbody=$hbody.Substring(0,$hbody.LastIndexOf('}')).Replace('InstalledAsync','HousekeepingInstalledAsync').Replace('ChecklistSchema.StateAsync','ChecklistStateAsync')
NewSource 'src/HotelOps.Api/Services/HotelStructure/TurnoverHistorySchema.cs' ($schema+"`n"+$body+"`n"+$hbody+"`n}`n")
$controller=Historical 'src/HotelOps.Api/Controllers/OperationalHistoryController.cs'
$begin=$controller.IndexOf('    [HttpGet("api/inspection-history")]')
$end=$controller.IndexOf('    [HttpGet("api/rooms/')
$controller=$controller.Remove($begin,$end-$begin).Replace('OperationalHistoryController(RoomTypeCreationService','TurnoverHistoryController(TurnoverHistoryService').Replace('RoomTypeBatchResult','TurnoverHistoryResult').Replace('RoomTypeBatchStatus','TurnoverHistoryStatus')
$proposal=Join-Path $PSScriptRoot 'TurnoverHistoryController.cs.proposal'
if(Test-Path $proposal){throw 'Existing controller proposal'}
[IO.File]::WriteAllText($proposal,$controller,[Text.UTF8Encoding]::new($false))
