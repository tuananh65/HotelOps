using System.Security.Claims;
using System.Text.Json;
using HotelOps.Api.Middleware;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Http;

// Uses the unchanged Auth implementation; never connects to SQL or replaces an Auth guard.
internal static class RestrictedSessionVerification
{
    internal static async Task<int> Run(string output)
    {
        var assertions=0;var rows=new List<object>();
        void Check(bool value,string name){if(!value)throw new InvalidOperationException(name);assertions++;}
        var now=DateTime.UtcNow;
        AuthenticationTicketData Ticket(DateTime expiry)
        {
            // SQL datetime2 materializes without DateTimeKind; reproduce that boundary explicitly.
            expiry=DateTime.SpecifyKind(expiry,DateTimeKind.Unspecified);
            var absolute=SqlSessionPolicy.AbsoluteDeadline(now,expiry);
            var reference=SessionReference.FromProtectedTicket(1,SessionCredential.Generate(),true,now);
            var ticket=HotelOpsCookieTicket.Create(new CreatedSqlSession(reference,new(1,1,now,absolute,absolute,true)),TimeProvider.System);
            return new(absolute,ticket.Properties.ExpiresUtc!.Value);
        }
        var old=Ticket(now.AddMinutes(14));var repaired=Ticket(now.AddDays(1));
        Check(old.Absolute.Kind==DateTimeKind.Unspecified,"Old deadline inherits unspecified SQL kind");
        Check(repaired.Absolute.Kind==DateTimeKind.Utc,"Session cap retains server UTC kind");
        Check(repaired.Expires.UtcDateTime>now,"Repaired restricted cookie is usable");
        Check(repaired.Absolute==now.Add(SqlSessionPolicy.RestrictedLifetime),"Restricted session still capped at 15 minutes");
        var offset=TimeZoneInfo.Local.GetUtcOffset(old.Absolute);
        Check(Math.Abs((old.Expires.UtcDateTime-old.Absolute).TotalSeconds+offset.TotalSeconds)<1.01,"Actual cookie properties apply Windows local offset to old fixture");
        if(offset>TimeSpan.FromMinutes(14))Check(old.Expires.UtcDateTime<now,"Old fixture produces an already-expired cookie in this Windows timezone");
        async Task Expect(bool authenticated,bool snapshot,bool restricted,bool cookie,int status,string code)
        {
            var http=new DefaultHttpContext();http.Request.Path="/api/assets/1/handovers";http.Response.Body=new MemoryStream();
            if(cookie)http.Request.Headers.Cookie=HotelOpsCookieTicket.CookieName+"=invalid-test-cookie";
            if(authenticated)http.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"1")],HotelOpsCookieTicket.Scheme));
            if(snapshot)http.Items[typeof(SessionSnapshot)]=new SessionSnapshot(1,1,now,now.AddMinutes(15),now.AddMinutes(15),restricted);
            var reached=false;
            var restrictedMiddleware=new RestrictedSessionMiddleware(ctx=>{reached=true;return AuthenticationProblem.WriteAsync(ctx,ctx.User.Identity?.IsAuthenticated==true?403:401,ctx.User.Identity?.IsAuthenticated==true?"FORBIDDEN":"AUTHENTICATION_REQUIRED");});
            await new InvalidCookieMiddleware(restrictedMiddleware.InvokeAsync).InvokeAsync(http);
            http.Response.Body.Position=0;using var json=await JsonDocument.ParseAsync(http.Response.Body);
            Check(http.Response.StatusCode==status,"Exact status "+code);Check(json.RootElement.GetProperty("code").GetString()==code,"Exact code "+code);
            if(restricted&&snapshot)Check(!reached,"Restricted middleware stops business delegate");
            rows.Add(new{authenticated,snapshot,restricted,cookie,expectedStatus=status,actualStatus=http.Response.StatusCode,code,businessDelegateReached=reached});
        }
        await Expect(false,false,false,false,401,"AUTHENTICATION_REQUIRED");
        await Expect(false,false,false,true,401,"SESSION_INVALID");
        await Expect(true,false,true,true,401,"SESSION_INVALID");
        await Expect(true,true,true,true,403,"PASSWORD_CHANGE_REQUIRED");
        await Expect(true,true,false,true,403,"FORBIDDEN");
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"FIX03_AUTH_CONTRACT_OFFLINE.json"),JsonSerializer.Serialize(new{status="PASS",assertions,timeZone=TimeZoneInfo.Local.Id,localOffsetMinutes=offset.TotalMinutes,oldCookieAlreadyExpired=old.Expires.UtcDateTime<now,repairedCookieFuture=repaired.Expires.UtcDateTime>now,rows,sqlConnections=0,scope="Actual cookie ticket, deadline policy and invalid/restricted middleware; terminal permission denial is a test delegate, SQL business acceptance NOT_RUN"},new JsonSerializerOptions{WriteIndented=true}));
        return assertions;
    }
    private sealed record AuthenticationTicketData(DateTime Absolute,DateTimeOffset Expires);
}
