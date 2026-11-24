using System.Net;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HotelOps.Compact.Tests;

public class ProxyConfigurationTests
{
    [Theory]
    [InlineData("172.30.40.2", "https", "198.51.100.15")]
    [InlineData("172.30.40.99", "http", "172.30.40.99")]
    public async Task OnlyConfiguredProxyMayForwardClientAndScheme(string peer, string scheme, string client)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Compact:KnownProxies:0"] = "172.30.40.2" }).Build();
        var options = new ForwardedHeadersOptions();
        ProxyConfiguration.Configure(options, config);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.15";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));
        await middleware.Invoke(context);
        Assert.Equal(scheme, context.Request.Scheme);
        Assert.Equal(client, context.Connection.RemoteIpAddress!.ToString());
    }

    [Fact]
    public void InvalidProxyConfigurationFailsClosed()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Compact:KnownProxies:0"] = "0.0.0.0/0" }).Build();
        Assert.Throws<InvalidOperationException>(() => ProxyConfiguration.Configure(new(), config));
    }
}
