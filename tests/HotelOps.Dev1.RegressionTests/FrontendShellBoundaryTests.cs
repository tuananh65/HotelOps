using System.Security.Claims;
using HotelOps.Api.Hosting;
using HotelOps.Api.Middleware;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
public sealed class FrontendShellBoundaryTests
{
 [Theory]
 [InlineData("/notifications","GET",true,true)]
 [InlineData("/change-password","GET",true,true)]
 [InlineData("/notifications","HEAD",true,true)]
 [InlineData("/api/notifications","GET",true,false)]
 [InlineData("/assets/missing.js","GET",true,false)]
 [InlineData("/private.json","GET",true,false)]
 [InlineData("/notifications","POST",true,false)]
 [InlineData("/notifications","GET",false,false)]
 public async Task OnlyMarkedSafeShellBypassesCookieAndRestriction(string path,string method,bool marked,bool allowed)
 {
  var context=new DefaultHttpContext();context.Request.Path=path;context.Request.Method=method;
  context.Request.Headers.Cookie="__Host-HotelOps.Auth=invalid-synthetic";
  using var services=new ServiceCollection().AddLogging().BuildServiceProvider();context.RequestServices=services;context.Response.Body=new MemoryStream();
  if(marked)context.SetEndpoint(new Endpoint(_=>Task.CompletedTask,new EndpointMetadataCollection(new CanonicalFrontendShell()),"shell"));
  var reached=false;await new InvalidCookieMiddleware(_=>{reached=true;return Task.CompletedTask;}).InvokeAsync(context);
  Assert.Equal(allowed,reached);if(!allowed)Assert.Equal(401,context.Response.StatusCode);
  context.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub","7")],"synthetic"));
  var now=DateTime.UtcNow;context.Items[typeof(SessionSnapshot)]=new SessionSnapshot(1,7,now,now.AddMinutes(5),now.AddMinutes(15),true);
  reached=false;await new RestrictedSessionMiddleware(_=>{reached=true;return Task.CompletedTask;}).InvokeAsync(context);
  Assert.Equal(allowed,reached);if(!allowed)Assert.Equal(403,context.Response.StatusCode);
 }
}
