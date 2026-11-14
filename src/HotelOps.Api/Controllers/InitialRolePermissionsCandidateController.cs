using HotelOps.Api.Entities;
using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Rbac;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Rbac;
using HotelOps.Api.Services.Users;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Controllers;

[NonController] // Initial grants V1 candidate; no runtime/DI/router cutover.
[ApiController, Route("api/roles/{roleId:long}/permissions/initial-v1")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), ValidateAntiForgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InitialRolePermissionsCandidateController(HotelOpsDbContext db, ICurrentUserAccessor current, IPermissionService permissions, TimeProvider clock, IHttpContextAccessor http) : ControllerBase
{
    [HttpPost, RequestSizeLimit(16384)]
    public async Task<IActionResult> Configure(long roleId, InitialRolePermissionsRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        if (Request.Headers.ContainsKey("If-Match")) return Problem(statusCode: 400, title: "ROLE_ETAG_UNSUPPORTED");
        try
        {
            var service = new InitialRolePermissionsService(new SqlInitialRolePermissionsTransactions(db, current, permissions, clock, http));
            var result = await service.ConfigureAsync(roleId, request, ct);
            return result.Status == 201 ? Created($"/api/roles/{roleId}/permissions", result.Value) : Problem(statusCode: result.Status, title: result.Code);
        }
        catch (InitialRoleAuthorityException e) { return Problem(statusCode: e.Status, title: "ROLE_AUTHORITY_REQUIRED"); }
        catch (GlobalAdminLockException) { return Problem(statusCode: 503, title: "ROLE_STORE_UNAVAILABLE"); }
        catch (GlobalAdministrationInvariantException) { return Problem(statusCode: 409, title: "LAST_DURABLE_ADMIN"); }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 } && e.Entries.Any(entry => entry.Entity is RolePermission)) { return Problem(statusCode: 409, title: "ROLE_CONFIGURATION_CHANGED"); }
        catch (Exception e) when (!ct.IsCancellationRequested && e is DbException or DbUpdateException or SessionStoreUnavailableException) { return Problem(statusCode: 503, title: "ROLE_STORE_UNAVAILABLE"); }
    }
}