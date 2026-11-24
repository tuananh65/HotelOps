using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace HotelOps.Api.Compact.Infrastructure;

public static class ProxyConfiguration
{
    public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        // Never trust arbitrary clients or every Docker/private network.
        foreach (var address in configuration.GetSection("Compact:KnownProxies").Get<string[]>() ?? [])
        {
            if (!IPAddress.TryParse(address, out var proxy))
                throw new InvalidOperationException("Compact:KnownProxies must contain literal IP addresses.");
            options.KnownProxies.Add(proxy);
        }
    }
}
