using HotelOps.Api.Security;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;

[NonController] // Production OFF until privacy/SQL/HTTPS acceptance; no shared DI registration.
[ApiController, Route("api/branches/{branchId:long}/departments/{departmentId:long}/shift-plans")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ShiftPlanPreviewController(SqlSecureShiftPreview service, ShiftPlanSaveService? save = null) : ControllerBase
{
    [HttpPost("preview"), ValidateAntiForgeryToken, RequestSizeLimit(96 * 1024)]
    public async Task<IActionResult> Preview(long branchId, long departmentId, ShiftPreviewRequest input, CancellationToken ct)
    {
        if (!Request.IsHttps) return Failure(400);
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) return Failure(401);
        try { var result = await service.PreviewAsync(ticket!, sid, branchId, departmentId, input, ct); return result.Status == 200 ? Ok(result.Value) : Failure(result.Status); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Failure(503); }
        catch (Exception e) when (!ct.IsCancellationRequested) { return Failure(e is System.Data.Common.DbException or TimeoutException or HotelOps.Api.Services.Authentication.SessionStoreUnavailableException ? 503 : 500); }
    }
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(96 * 1024)]
    public async Task<IActionResult> Save(long branchId, long departmentId, ShiftPreviewRequest input, CancellationToken ct)
    {
        if (!Request.IsHttps) return Failure(400);
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) return Failure(401);
        if (save == null) return Failure(503);
        try
        {
            var result = await save.SaveAsync(ticket!, sid, branchId, departmentId, input, ct);
            return result.Status == 201 ? StatusCode(201, result.Value) : Failure(result.Status);
        }
        catch (Exception e) when (!ct.IsCancellationRequested) { return Failure(e is System.Data.Common.DbException or Microsoft.EntityFrameworkCore.DbUpdateException or TimeoutException or HotelOps.Api.Services.Authentication.SessionStoreUnavailableException ? 503 : 500); }
    }
    private ObjectResult Failure(int status) => Problem(statusCode: status, title: status == 401 ? "SESSION_INVALID" : status == 403 ? "SHIFT_PREVIEW_DENIED" : status == 400 ? "INVALID_PREVIEW" : status == 409 ? "SHIFT_PLAN_CONFLICT" : "SHIFT_PREVIEW_UNAVAILABLE");
}