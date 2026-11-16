using System.Collections.ObjectModel;
using HotelOps.Api.Data;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Dev2Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HotelOps.Api.Controllers.Filters;

public static class Dev2ActionAdmissionGuard
{
    // Synchronous preflight only for existing004/004B direct-filter callers; never waits on async work.
    public static void Authorize(AuthorizationFilterContext context)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        try
        {
            var accessor = context.HttpContext.RequestServices.GetService<ICurrentUserAccessor>();
            if (accessor is null) { context.Result = new StatusCodeResult(503); return; }
            if (!accessor.TryGetCurrentUser(out var actor) || actor.UserId <= 0 || actor.SessionId <= 0)
            { context.Result = new UnauthorizedResult(); return; }
            if (actor.IsRestricted) { context.Result = new ForbidResult(HotelOpsCookieTicket.Scheme); return; }
            var known = context.ActionDescriptor is ControllerActionDescriptor descriptor
                ? Dev2ActionAdmissionRegistry.Resolve(descriptor, context.HttpContext.Request.Method) : null;
            if (known is null || Dev2ActionContracts.Find(known, context.HttpContext) is null)
                context.Result = new ForbidResult(HotelOpsCookieTicket.Scheme);
        }
        catch (Exception error) when (Unavailable(error)) { context.Result = new StatusCodeResult(503); }
    }

    public static async Task AuthorizeAsync(AuthorizationFilterContext context)
    {
        Authorize(context);
        if (context.Result is not null) return;
        try
        {
            if (context.ActionDescriptor is ControllerActionDescriptor descriptor)
            {
                var known = Dev2ActionAdmissionRegistry.Resolve(descriptor, context.HttpContext.Request.Method);
                if (known is not null && Dev2DemoBindings.Eligible(context.HttpContext))
                    await Dev2DemoBindings.PreBindAsync(known, context.HttpContext, context.HttpContext.RequestAborted);
            }
        }
        catch (Dev2AccessDenied denied) { context.Result = PublicResult(denied.Status); }
        catch (Exception error) when (Unavailable(error)) { context.Result = new StatusCodeResult(503); }
    }

    public static async Task ExecuteAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        try
        {
            if (context.ActionDescriptor is not ControllerActionDescriptor descriptor) throw new Dev2AccessDenied(403);
            var known = Dev2ActionAdmissionRegistry.Resolve(descriptor, context.HttpContext.Request.Method)
                ?? throw new Dev2AccessDenied(403);
            var contract = Dev2ActionContracts.Find(known, context.HttpContext) ?? throw new Dev2AccessDenied(403);
            var httpAccessor = new HttpContextAccessor { HttpContext = context.HttpContext };
            var actor = Dev2ServiceAuthority.CurrentIdentity(httpAccessor);
            if (!context.ModelState.IsValid) { context.Result = new BadRequestResult(); return; }
            var bound = new Dev2BoundAction(known, context.HttpContext, actor,
                new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(context.ActionArguments)));
            async Task Revalidate(Dev2AuthorizationPhase phase, CancellationToken ct)
            {
                if (Dev2ServiceAuthority.CurrentIdentity(httpAccessor) != actor) throw new Dev2AccessDenied(403);
                if (!await contract.ValidateFactsAndWorkflow(bound, phase, ct)) throw new Dev2AccessDenied(403);
                var requirements = await contract.Requirements(bound, ct);
                var services = context.HttpContext.RequestServices;
                IPermissionService permissions;
                if (requirements.Any(x => x.Target.Kind == Dev2PermissionTargetKind.Resource))
                {
                    var reader = new Dev2ResourceFactReader(services.GetRequiredService<HotelOpsDbContext>());
                    var resolver = contract.ResourceResolver?.Invoke(bound, reader) ?? throw new Dev2AccessDenied(403);
                    permissions = new PermissionService(services.GetRequiredService<ICurrentUserAccessor>(),
                        services.GetRequiredService<IPermissionDataSource>(), resolver,
                        services.GetService<TimeProvider>() ?? TimeProvider.System);
                }
                else permissions = services.GetRequiredService<IPermissionService>();
                var evaluated = await new Dev2PermissionGate(permissions).EvaluateAsync(requirements, contract.Combination, ct);
                if (!evaluated.IsGranted) throw new Dev2AccessDenied(evaluated.Decision switch
                { PermissionDecision.Unauthenticated => 401, PermissionDecision.Unavailable => 503, _ => 403 });
                if (known.HttpMethod != "GET" && phase is Dev2AuthorizationPhase.BeforePersistence or Dev2AuthorizationPhase.BeforeStockPosting
                    && !await contract.TransactionAuthorityBoundary(bound, phase, ct)) throw new Dev2AccessDenied(403);
            }
            var grant = new Dev2ActionGrant(bound, contract, Revalidate);
            await grant.RevalidateAsync(Dev2AuthorizationPhase.Admission, context.HttpContext.RequestAborted);
            Dev2ServiceAuthority.Install(context.HttpContext, grant);
            if (known.HttpMethod == "GET")
            {
                await using var readTransaction = Dev2DemoBindings.ExpandedEnabled || contract.RequiresReadTransaction
                    ? await Dev2ServiceAuthority.BeginTransactionAsync(servicesDb(), httpAccessor, context.HttpContext.RequestAborted) : null;
                if (readTransaction is not null) await grant.RevalidateAsync(Dev2AuthorizationPhase.Admission, context.HttpContext.RequestAborted);
                // A scoped read implementation is mandatory; never fall through to an old unscoped query.
                var result = await contract.ScopedReadHandler!(bound, context.HttpContext.RequestAborted);
                await grant.RevalidateAsync(Dev2AuthorizationPhase.BeforeDisclosure, context.HttpContext.RequestAborted);
                if (readTransaction is not null) await readTransaction.CommitAsync(context.HttpContext.RequestAborted);
                context.Result = result;
                HotelOpsDbContext servicesDb() => context.HttpContext.RequestServices.GetRequiredService<HotelOpsDbContext>();
            }
            else
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<HotelOpsDbContext>();
                await using var actionTransaction = Dev2DemoBindings.ExpandedEnabled || contract.RequiresActionTransaction
                    ? await Dev2ServiceAuthority.BeginActionAsync(db, httpAccessor, context.HttpContext.RequestAborted) : null;
                if (actionTransaction is not null) await grant.RevalidateAsync(Dev2AuthorizationPhase.Admission, context.HttpContext.RequestAborted);
                var executed = await next();
                if (executed.Exception is Dev2AccessDenied denied)
                { executed.ExceptionHandled = true; executed.Result = PublicResult(denied.Status); }
                else if (executed.Exception is null && (executed.Result as Microsoft.AspNetCore.Mvc.Infrastructure.IStatusCodeActionResult)?.StatusCode is >= 200 and < 300)
                {
                    // Includes no-op mutations: publishing their returned detail still requires current authority.
                    await grant.RevalidateAsync(Dev2AuthorizationPhase.BeforeDisclosure, context.HttpContext.RequestAborted);
                    if (actionTransaction is not null)
                        await Dev2ServiceAuthority.CommitAsync(db, httpAccessor, actionTransaction, context.HttpContext.RequestAborted);
                }
            }
        }
        catch (Dev2AccessDenied denied) { context.Result = PublicResult(denied.Status); }
        catch (Exception error) when (Unavailable(error))
        {
            context.HttpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger(typeof(Dev2ActionAdmissionGuard).FullName!)
                .LogWarning("Action authority unavailable: {ErrorType}; {Stack}", error.GetType().Name, error.StackTrace);
            context.Result = new StatusCodeResult(503);
        }
        finally { Dev2ServiceAuthority.Remove(context.HttpContext); }
    }

    private static IActionResult PublicResult(int status) => status == 401 ? new UnauthorizedResult()
        : status == 403 ? new ForbidResult(HotelOpsCookieTicket.Scheme) : new StatusCodeResult(status);
    private static bool Unavailable(Exception error) => error is InvalidOperationException or TimeoutException
        or System.Data.Common.DbException or Microsoft.EntityFrameworkCore.DbUpdateException;
}
