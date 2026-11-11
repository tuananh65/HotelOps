using System.Data.Common;
using System.Runtime.ExceptionServices;
using HotelOps.Api.Data;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using HotelOps.Api.Services.Rbac;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace HotelOps.Api.Controllers.Filters;
// Admission before automatic model-state rejection/lookup. Same request-scoped SQL context;
// reuse existing retained Users universe/session/grant read boundary, no new lock protocol.
public sealed class RoleCatalogReadFilter(HotelOpsDbContext db,ICurrentUserAccessor current,IPermissionService permissions,TimeProvider clock):IAsyncActionFilter,IOrderedFilter
{
 public int Order=>-3000;
 public async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next){
 context.HttpContext.Response.Headers.CacheControl="no-store";
 if(!context.HttpContext.Request.IsHttps){context.Result=Error(400,"HTTPS_REQUIRED");return;}
 if(!HotelOpsCookieTicket.TryRead(context.HttpContext.User,out var ticket,out var sessionId)){context.Result=Error(401,"SESSION_INVALID");return;}
 if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket!.UserId||actor.SessionId!=sessionId){context.Result=Error(401,"SESSION_INVALID");return;}
 if(actor.IsRestricted||ticket.IsRestricted){context.Result=Error(403,"PASSWORD_CHANGE_REQUIRED");return;}
 ActionExecutedContext? executed=null;
 void Set(IActionResult result){if(executed==null)context.Result=result;else executed.Result=result;}
 Task<PermissionEvaluation> Authorize(CancellationToken ct)=>RoleCatalogAuthority.EvaluateAsync(permissions,ct);
 try{var guarded=await new UserDirectoryReadGuard(db,current,permissions,clock).RunAuthorizedAsync(ticket!,Authorize,async()=>{executed=await next();if(executed.Exception is {} e&&!executed.ExceptionHandled)ExceptionDispatchInfo.Capture(e).Throw();return executed;},context.HttpContext.RequestAborted);
 Set(guarded.Status switch {UserDirectoryReadStatus.Success=>guarded.Value!.Result!,UserDirectoryReadStatus.SessionInvalid=>Error(401,"SESSION_INVALID"),UserDirectoryReadStatus.Forbidden=>Error(403,"PERMISSION_DENIED"),_=>Error(503,"CATALOG_UNAVAILABLE")});
 }catch(Exception e)when(!context.HttpContext.RequestAborted.IsCancellationRequested&&e is DbException or SessionStoreUnavailableException or CatalogLimitException){if(executed!=null)executed.ExceptionHandled=true;Set(Error(503,"CATALOG_UNAVAILABLE"));}
 }
 public static ObjectResult Error(int status,string code)=>new(new ProblemDetails{Status=status,Title=code,Type="about:blank",Extensions={["code"]=code}}){StatusCode=status,ContentTypes={"application/problem+json"}};
}
