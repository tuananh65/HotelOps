using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using HotelOps.Api.Compact.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Compact.Security;

public sealed class SessionAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,
    UrlEncoder encoder,HotelOpsCompactDbContext db,TimeProvider clock)
    :AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    public const string SchemeName="CompactSession";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header=Request.Headers.Authorization.ToString();
        if(!header.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase))return AuthenticateResult.NoResult();
        var raw=header[7..].Trim();
        if(raw.Length!=64 || !raw.All(Uri.IsHexDigit))return AuthenticateResult.Fail("Invalid session");
        var hash=SHA256.HashData(Convert.FromHexString(raw)); var now=clock.GetUtcNow().UtcDateTime;
        var session=await (from s in db.UserSessions.AsNoTracking() join u in db.Users on s.UserId equals u.UserId
            where s.TokenHash==hash && s.RevokedAt==null && s.ExpiresAt>now && s.SecurityStamp==u.SecurityStamp
                && u.AccountStatus=="ACTIVE" && u.EmploymentStatus=="ACTIVE" && u.DeletedAt==null
            select new {s.SessionId,u.UserId,u.Username,u.TemporaryPasswordExpiresAt}).SingleOrDefaultAsync(Context.RequestAborted);
        if(session==null)return AuthenticateResult.Fail("Session expired or revoked");
        var claims=new List<Claim>{new(ClaimTypes.NameIdentifier,session.UserId.ToString()),
            new(ClaimTypes.Name,session.Username),new("session_id",session.SessionId.ToString())};
        if(session.TemporaryPasswordExpiresAt!=null)claims.Add(new("restricted","true"));
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims,SchemeName)),SchemeName));
    }
}
