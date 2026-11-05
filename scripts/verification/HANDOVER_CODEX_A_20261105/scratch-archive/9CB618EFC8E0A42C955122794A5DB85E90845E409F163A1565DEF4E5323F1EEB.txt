using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

namespace HotelOps.Api.Security;

public static class HotelOpsCookieInfrastructure
{
    public static IServiceCollection AddHotelOpsCookieInfrastructure(this IServiceCollection services, IHostEnvironment environment)
    {
        // Deployment is not defined yet: do not silently launch production with an
        // ephemeral/shared/plaintext key ring. Configure production explicitly later.
        if (!environment.IsDevelopment() || !OperatingSystem.IsWindows())
            throw new InvalidOperationException("Configure persistent, encrypted Data Protection keys for this deployment before enabling HotelOps authentication outside Windows Development.");

        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
            throw new InvalidOperationException("Development Data Protection key directory is unavailable.");
        var directory = new DirectoryInfo(Path.Combine(localData, "HotelOps", "DataProtectionKeys", "Development"));
        for (var ancestor = directory; ancestor != null; ancestor = ancestor.Parent)
        {
            if (Directory.Exists(Path.Combine(ancestor.FullName, ".git"))
                || File.Exists(Path.Combine(ancestor.FullName, ".git")))
                throw new InvalidOperationException("Data Protection keys must be outside a Git working tree.");
        }
        services.AddDataProtection().SetApplicationName("HotelOps.Authentication.Development.v1")
            .PersistKeysToFileSystem(directory).ProtectKeysWithDpapi(protectToLocalMachine: false);

        services.AddScoped<CookieSessionValidator>();
        services.AddScoped<ICookieSessionValidator>(provider => provider.GetRequiredService<CookieSessionValidator>());
        services.AddScoped<ICookieSessionToucher>(provider => provider.GetRequiredService<CookieSessionValidator>());
        services.AddScoped<HotelOpsCookieEvents>();
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = HotelOpsCookieTicket.Scheme;
            options.DefaultChallengeScheme = HotelOpsCookieTicket.Scheme;
            options.DefaultForbidScheme = HotelOpsCookieTicket.Scheme;
            options.DefaultSignInScheme = HotelOpsCookieTicket.Scheme;
            options.DefaultSignOutScheme = HotelOpsCookieTicket.Scheme;
        }).AddCookie(HotelOpsCookieTicket.Scheme, options =>
        {
            options.Cookie.Name = HotelOpsCookieTicket.CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
            options.Cookie.Domain = null;
            options.EventsType = typeof(HotelOpsCookieEvents);
            options.SlidingExpiration = false;
            options.ExpireTimeSpan = Services.Authentication.SqlSessionPolicy.NormalAbsoluteLifetime;
        });
        services.AddAuthorization(); // No role/permission/fallback policies are added.
        return services;
    }
}
