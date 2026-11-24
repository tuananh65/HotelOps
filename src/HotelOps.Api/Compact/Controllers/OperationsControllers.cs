using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Compact.Controllers;

[ApiController,Authorize,Route("api/v2/work")]
public sealed class WorkController(WorkService service):ControllerBase
{
    [HttpGet("internal-request-types")]public Task<object> InternalRequestTypes(long branchId,CancellationToken ct)=>service.InternalRequestTypes(branchId,ct);
    [HttpPost("internal-request-types")]public Task<object> CreateInternalRequestType(long branchId,InternalRequestTypeRequest request,CancellationToken ct)=>service.SaveInternalRequestType(branchId,null,request,ct);
    [HttpPut("internal-request-types/{id:long}")]public Task<object> UpdateInternalRequestType(long branchId,long id,InternalRequestTypeRequest request,CancellationToken ct)=>service.SaveInternalRequestType(branchId,id,request,ct);
    [HttpGet("room-issue-types")]public Task<object> RoomIssueTypes(long branchId,CancellationToken ct)=>service.RoomIssueTypes(branchId,ct);
    [HttpPut("room-issue-types")]public Task<object> RoomIssueType(long branchId,RoomIssueTypeRequest request,CancellationToken ct)=>service.SaveRoomIssueType(branchId,request,ct);
    [HttpGet("room-issue-objects")]public Task<object> RoomIssueObjects(long branchId,long roomId,CancellationToken ct,long? workId=null)=>service.RoomIssueObjects(branchId,roomId,ct,workId);
    [HttpGet("room-issues")]public Task<object> RoomIssues(long branchId,CancellationToken ct)=>service.RoomIssues(branchId,ct);
    [HttpGet("maintenance-inbox")]public Task<object> MaintenanceInbox(long branchId,CancellationToken ct,string queue="all",int page=1,string? search=null)=>service.MaintenanceInbox(branchId,queue,page,search,ct);
    [HttpPost("room-issues")]public Task<object> CreateRoomIssue(RoomIssueCreateRequest request,CancellationToken ct)=>service.CreateRoomIssue(request,ct);
    [HttpGet("room-issues/{id:long}")]public Task<object> RoomIssueDetail(long id,CancellationToken ct)=>service.RoomIssueDetail(id,ct);
    [HttpPost("room-issues/{id:long}/{command}")]public Task<object> RoomIssueAction(long id,string command,RoomIssueActionRequest request,CancellationToken ct)=>service.ActRoomIssue(id,command,request,ct);
    [HttpPost("{id:long}/inspection-result")]public Task<object> InspectionResult(long id,PaperInspectionRequest request,CancellationToken ct)=>service.PaperInspection(id,request,ct);
    [HttpPost("{id:long}/confirm-room-issue")]public Task<object> ConfirmRoomIssue(long id,ActionRequest request,CancellationToken ct)=>service.ConfirmCleaningIssue(id,request,ct);
    [HttpGet("housekeeping-history")]public Task<object> HousekeepingHistory(long branchId,CancellationToken ct,DateOnly? date=null,int page=1,string? search=null,string period="day",bool mine=false,string? status=null,string sort="latest")=>service.HousekeepingHistory(branchId,date,page,search,ct,period,mine,status,sort);
    [HttpGet("cleaning-queue")]public Task<object> CleaningQueue(long branchId,CancellationToken ct,string queue="all",int page=1,string? search=null,string sort="due")=>service.CleaningQueue(branchId,queue,page,ct,search,sort);
    [HttpGet("cleaning-rooms")]public Task<object> CleaningRooms(long branchId,CancellationToken ct,int page=1,string? search=null,long? roomId=null)=>service.CleaningRooms(branchId,page,search,roomId,ct);
    [HttpGet("housekeepers")]public Task<object> Housekeepers(long branchId,CancellationToken ct,DateOnly? date=null)=>service.Housekeepers(branchId,date,ct);
    [HttpGet("inspection-queue")]public Task<object> InspectionQueue(long branchId,CancellationToken ct,int page=1,string? search=null,string sort="oldest")=>service.InspectionQueue(branchId,page,search,ct,sort);
    [HttpGet("defects")]public Task<object> Defects(long branchId,CancellationToken ct,int page=1,string? search=null,string? status=null,string sort="latest")=>service.Defects(branchId,page,search,status,ct,sort);
    [HttpGet("{id:long}/related")]public Task<object> Related(long id,CancellationToken ct)=>service.Related(id,ct);
    [HttpGet]public Task<object> List(long? branchId,string kind,string? status,CancellationToken ct,int page=1,int pageSize=50,bool mine=false,string? search=null,long? departmentId=null,bool overdue=false,bool assignedToMe=false,string sort="latest",DateTime? from=null,DateTime? to=null,long? priorityId=null,string? queue=null,bool historyMine=false,bool cleaningCurrent=false)=>service.List(branchId,kind,status,page,pageSize,ct,mine,search,departmentId,overdue,assignedToMe,sort,from,to,priorityId,queue,historyMine,cleaningCurrent);
    [HttpGet("{id:long}")]public Task<object> Detail(long id,CancellationToken ct)=>service.Detail(id,ct);
    [HttpGet("candidates")]public Task<object> Candidates(long branchId,string kind,CancellationToken ct,long? workId=null)=>service.Candidates(branchId,kind,ct,workId);
    [HttpGet("cleaning-creation-options")]public Task<object> CleaningCreationOptions(long branchId,CancellationToken ct)=>service.CleaningCreationOptions(branchId,ct);
    [HttpGet("technicians")]public Task<object> Technicians(long branchId,CancellationToken ct)=>service.Technicians(branchId,ct);
    [HttpPost("{id:long}/maintenance")]public Task<object> Maintenance(long id,MaintenanceFromIncidentRequest request,CancellationToken ct)=>service.FromIncident(id,request,ct);
    [HttpPost]public Task<object> Create(CreateWorkRequest request,CancellationToken ct)=>service.Create(request,ct);
    [HttpPost("{id:long}/actions/{command}")]public Task<object> Act(long id,string command,ActionRequest request,CancellationToken ct)=>service.Act(id,command,request,ct);
    [HttpPut("{id:long}/checklist/{itemId:long}")]public Task<object> Checklist(long id,long itemId,ChecklistResultRequest request,CancellationToken ct)=>service.Checklist(id,itemId,request,ct);
    [HttpPost("{id:long}/findings")]public Task<object> Finding(long id,FindingRequest request,CancellationToken ct)=>service.Finding(id,request,ct);
    [HttpPost("{id:long}/findings/{findingId:long}/resolve")]public Task<object> Resolve(long id,long findingId,ActionRequest request,CancellationToken ct)=>service.ResolveFinding(id,findingId,request,ct);
    [HttpPost("{id:long}/links")]public Task<object> Link(long id,LinkRequest request,CancellationToken ct)=>service.Link(id,request,ct);
}
[ApiController,Authorize,Route("api/v2/rooms")]
public sealed class RoomsController(RoomService service):ControllerBase
{
    [HttpGet("{id:long}/workflow")]public Task<object> Workflow(long id,CancellationToken ct)=>service.Workflow(id,ct);
    [HttpGet]public Task<object> List(long branchId,CancellationToken ct)=>service.List(branchId,ct);
    [HttpGet("{id:long}/history")]public Task<object> History(long id,CancellationToken ct)=>service.History(id,ct);
    [HttpPost("{id:long}/actions/{command}")]public Task<object> Act(long id,string command,RoomActionRequest request,CancellationToken ct)=>service.Act(id,command,request,ct);
}
[ApiController,Authorize,Route("api/v2/documents")]
public sealed class DocumentsController(DocumentService service,WorkService work):ControllerBase
{
    [HttpGet("transfer-catalog")]public Task<object> TransferCatalog(long branchId,CancellationToken ct)=>service.TransferCatalog(branchId,ct);
    [HttpGet]public Task<object> List(long? branchId,string kind,CancellationToken ct,int page=1,int pageSize=50,string? status=null,string? search=null,string sort="latest",DateTime? from=null,DateTime? to=null)=>service.List(branchId,kind,page,pageSize,ct,status,search,sort,from,to);
    [HttpGet("{id:long}")]public Task<object> Detail(long id,CancellationToken ct)=>service.Detail(id,ct);
    [HttpPost]public Task<object> Create(CreateDocumentRequest request,CancellationToken ct)=>service.Create(request,ct);
    [HttpPost("{id:long}/lines")]public Task<object> AddLine(long id,DocumentLineRequest request,CancellationToken ct)=>service.Line(id,null,request,ct);
    [HttpPut("{id:long}/lines/{lineId:long}")]public Task<object> Line(long id,long lineId,DocumentLineRequest request,CancellationToken ct)=>service.Line(id,lineId,request,ct);
    [HttpPost("{id:long}/actions/{command}")]public Task<object> Act(long id,string command,ActionRequest request,CancellationToken ct)=>work.ActDocument(id,command,request,ct);
    [HttpPost("{id:long}/derive")]public Task<object> Derive(long id,DeriveDocumentRequest request,CancellationToken ct)=>service.Derive(id,request,ct);
}
[ApiController,Authorize,Route("api/v2/assets")]
public sealed class AssetsController(AssetService service):ControllerBase
{
    [HttpGet]public Task<object> List(long branchId,CancellationToken ct)=>service.List(branchId,ct);
    [HttpGet("{id:long}")]public Task<object> Detail(long id,CancellationToken ct)=>service.Detail(id,ct);
    [HttpPost("{id:long}/actions/{command}")]public Task<object> Act(long id,string command,AssetActionRequest request,CancellationToken ct)=>service.Act(id,command,request,ct);
}
[ApiController,Authorize,Route("api/v2/templates")]
public sealed class TemplatesController(TemplateService service):ControllerBase
{
    [HttpGet]public Task<object> List(long branchId,CancellationToken ct)=>service.List(branchId,ct);
    [HttpPost("checklists")]public Task<object> Checklist(ChecklistCreateRequest request,CancellationToken ct)=>service.Checklist(request,ct);
    [HttpGet("checklists/{id:long}")]public Task<object> ChecklistDetail(long id,CancellationToken ct)=>service.ChecklistDetail(id,ct);
    [HttpPost("schedules")]public Task<object> Schedule(TemplateRequest request,CancellationToken ct)=>service.Schedule(request,ct);
    [HttpGet("schedules/{id:long}")]public Task<object> Detail(long id,CancellationToken ct)=>service.Detail(id,ct);
    [HttpPut("schedules/{id:long}")]public Task<object> Update(long id,UpdateTemplateRequest request,CancellationToken ct)=>service.Update(id,request,ct);
    [HttpPost("schedules/{id:long}/disable")]public Task<object> Disable(long id,VersionRequest request,CancellationToken ct)=>service.Disable(id,request,ct);
}
[ApiController,Authorize,Route("api/v2/insights")]
public sealed class InsightsController(InsightService service):ControllerBase
{
    [HttpGet("owner")]public Task<object> Owner(long? branchId,DateOnly? from,DateOnly? to,CancellationToken ct)=>service.OwnerOverview(branchId,from,to,ct);
    [HttpGet("owner/rooms")]public Task<object> OwnerRooms(long? branchId,CancellationToken ct)=>service.OwnerRooms(branchId,ct);
    [HttpGet("owner/assets")]public Task<object> OwnerAssets(long? branchId,CancellationToken ct)=>service.OwnerAssets(branchId,ct);
    [HttpGet("owner/stock")]public Task<object> OwnerStock(long? branchId,CancellationToken ct)=>service.OwnerStock(branchId,ct);
    [HttpGet("owner/room-links/{id:long}")]public Task<object> OwnerRoomLinks(long id,CancellationToken ct)=>service.OwnerRoomLinks(id,ct);
    [HttpGet("owner/preventive/{id:long}")]public Task<object> OwnerPreventive(long id,CancellationToken ct)=>service.OwnerPreventive(id,ct);
    [HttpGet("owner/reports/{report}")]public Task<object> OwnerReport(string report,long? branchId,DateOnly? from,DateOnly? to,CancellationToken ct,string? search=null,string? status=null,string sort="latest",int page=1)=>service.OwnerReport(report,branchId,from,to,search,status,sort,page,ct);
    [HttpGet("personal")]public Task<object> Personal(long branchId,CancellationToken ct,bool maintenance=false)=>service.Personal(branchId,ct,maintenance);
    [HttpGet("administration")]public Task<object> Administration(CancellationToken ct)=>service.Administration(ct);
    [HttpGet("stock")]public Task<object> Stock(long branchId,CancellationToken ct)=>service.Stock(branchId,ct);
    [HttpGet("inventory")]public Task<object> Inventory(long branchId,CancellationToken ct)=>service.Inventory(branchId,ct);
    [HttpGet("maintenance")]public Task<object> Maintenance(long branchId,CancellationToken ct)=>service.Maintenance(branchId,ct);
    [HttpGet("housekeeping")]public Task<object> Housekeeping(long branchId,CancellationToken ct)=>service.Housekeeping(branchId,ct);
    [HttpGet("ledger")]public Task<object> Ledger(long branchId,long warehouseId,long itemId,CancellationToken ct,int page=1)=>service.Ledger(branchId,warehouseId,itemId,page,ct);
    [HttpGet("dashboard")]public Task<object> Dashboard(long branchId,CancellationToken ct,string? taskPeriod=null)=>service.Dashboard(branchId,ct,taskPeriod);
    [HttpGet("reports/{report}")]public Task<object> Report(string report,long branchId,DateTime? from,DateTime? to,CancellationToken ct)=>service.Report(report,branchId,from,to,ct);
    [HttpGet("audit")]public Task<object> Audit(long branchId,CancellationToken ct,int page=1,string? search=null)=>service.Audit(branchId,page,ct,search);
    [HttpGet("time")]public Task<object> Time(long branchId,long? userId,CancellationToken ct)=>service.Time(branchId,userId,ct);
}
[ApiController,Authorize,Route("api/v2/notifications")]
public sealed class NotificationsController(NotificationService service):ControllerBase
{
    [HttpGet]public Task<object> Inbox(CancellationToken ct,int page=1,int pageSize=50,long? branchId=null)=>service.Inbox(page,pageSize,ct,branchId);
    [HttpPost("{id:long}/read")]public Task<object> Read(long id,CancellationToken ct)=>service.Read(id,false,ct);
    [HttpPost("{id:long}/acknowledge")]public Task<object> Acknowledge(long id,CancellationToken ct)=>service.Read(id,true,ct);
}
[ApiController,Authorize,Route("api/v2/evidence")]
public sealed class EvidenceController(EvidenceService service):ControllerBase
{
    [HttpPost,RequestSizeLimit(11*1024*1024)]public Task<object> Upload([FromForm]long branchId,[FromForm]long? workId,[FromForm]long? documentId,[FromForm]long? assetId,IFormFile file,CancellationToken ct)=>service.Upload(branchId,workId,documentId,assetId,file,ct);
    [HttpGet("{id:long}")]public async Task<IActionResult> Download(long id,CancellationToken ct){var file=await service.Download(id,ct);return File(file.Data,file.Mime,file.Name);}
    [HttpPost("{id:long}/invalidate")]public Task<object> Invalidate(long id,ActionRequest request,CancellationToken ct)=>service.Invalidate(id,request,ct);
}
