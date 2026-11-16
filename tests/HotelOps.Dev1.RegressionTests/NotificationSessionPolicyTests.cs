using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class NotificationSessionPolicyTests
{
    private static readonly DateTime Now = new(2026,11,16,10,0,0,DateTimeKind.Utc);
    private static UserSession Session() => new()
    {
        UserId=7,SessionId=9,CreatedAt=Now.AddHours(-1),ExpiresAt=Now.AddSeconds(1),
        User=new User {UserId=7,AccountStatus="ACTIVE"}
    };

    [Fact]
    public void SessionThatWasValidAtReadMustFailAtExpiryBeforeCommit()
    {
        var row=Session();
        Assert.True(NotificationSessionPolicy.Current(7,9,Now).Compile()(row));
        Assert.False(NotificationSessionPolicy.Current(7,9,Now.AddSeconds(1)).Compile()(row));
        Assert.False(NotificationSessionPolicy.Current(7,9,Now.AddSeconds(2)).Compile()(row));
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("deleted")]
    [InlineData("inactive")]
    [InlineData("temporary-password")]
    [InlineData("future-created")]
    [InlineData("excessive-lifetime")]
    [InlineData("absolute-expiry")]
    [InlineData("other-user")]
    [InlineData("other-session")]
    public void CurrentSessionConditionsCannotBeBorrowedFromAnEarlierCheck(string change)
    {
        var row=Session();
        switch(change)
        {
            case "revoked":row.RevokedAt=Now;break;
            case "deleted":row.User.DeletedAt=Now;break;
            case "inactive":row.User.AccountStatus="INACTIVE";break;
            case "temporary-password":row.User.TemporaryPasswordExpiresAt=Now.AddHours(1);break;
            case "future-created":row.CreatedAt=Now.AddSeconds(1);break;
            case "excessive-lifetime":row.ExpiresAt=row.CreatedAt.Add(SqlSessionPolicy.NormalAbsoluteLifetime).AddTicks(1);break;
            case "absolute-expiry":row.CreatedAt=Now.Subtract(SqlSessionPolicy.NormalAbsoluteLifetime);break;
            case "other-user":row.UserId=8;break;
            case "other-session":row.SessionId=10;break;
        }
        Assert.False(NotificationSessionPolicy.Current(7,9,Now).Compile()(row));
    }

    [Fact]
    public void SessionPredicateTranslatesToSqlWithCurrentIdentityAndExpiry()
    {
        using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var sql=db.UserSessions.Where(NotificationSessionPolicy.Current(7,9,Now)).ToQueryString();
        Assert.Contains("session_id",sql);Assert.Contains("user_id",sql);
        Assert.Contains("expires_at",sql);Assert.Contains("revoked_at",sql);
        Assert.Contains("DATEADD",sql);Assert.Contains("temporary_password_expires_at",sql);
    }
}
