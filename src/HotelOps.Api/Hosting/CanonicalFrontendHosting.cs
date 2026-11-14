using Microsoft.Extensions.FileProviders;
namespace HotelOps.Api.Hosting;

public sealed class CanonicalFrontendShell;
public static class CanonicalFrontendHosting
{
    public static bool IsShellRequest(HttpContext context) => context.GetEndpoint()?.Metadata.GetMetadata<CanonicalFrontendShell>() is not null
        && (HttpMethods.IsGet(context.Request.Method)||HttpMethods.IsHead(context.Request.Method))
        && !context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/assets")
        && !Path.HasExtension(context.Request.Path.Value);
    // Opt-in same-origin hosting of an existing production build. No build or deployment is triggered here.
    public static void UseCanonicalFrontend(this WebApplication app)
    {
        var root=app.Configuration["CanonicalFrontend:Root"];
        if(string.IsNullOrWhiteSpace(root))return;
        var directory=Path.GetFullPath(root,app.Environment.ContentRootPath);
        var index=Path.Combine(directory,"index.html");var assets=Path.Combine(directory,"assets");
        if(!File.Exists(index)||!Directory.Exists(assets))throw new InvalidOperationException("Canonical frontend requires a production index.html and assets directory.");
        var provider=new PhysicalFileProvider(assets);
        app.Lifetime.ApplicationStopped.Register(provider.Dispose);
        app.UseStaticFiles(new StaticFileOptions {FileProvider=provider,RequestPath="/assets",OnPrepareResponse=context=>context.Context.Response.Headers.XContentTypeOptions="nosniff"});
        app.MapFallback(async context=>
        {
            // Never turn an unknown API or missing asset into a successful HTML response.
            if(context.Request.Path.StartsWithSegments("/api")||context.Request.Path.StartsWithSegments("/assets")
                ||Path.HasExtension(context.Request.Path.Value)||(!HttpMethods.IsGet(context.Request.Method)&&!HttpMethods.IsHead(context.Request.Method)))
            {context.Response.StatusCode=StatusCodes.Status404NotFound;return;}
            context.Response.ContentType="text/html; charset=utf-8";context.Response.Headers.CacheControl="no-store";
            context.Response.Headers.XContentTypeOptions="nosniff";
            await context.Response.SendFileAsync(index,context.RequestAborted);
        }).WithMetadata(new CanonicalFrontendShell());
    }
}
