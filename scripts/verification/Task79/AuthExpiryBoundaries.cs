using System.Text.Json;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Entities;

if (args.Length != 2 || args[0] is not ("before" or "after") || File.Exists(args[1]))
    throw new ArgumentException("before/after and unused receipt required");
var expectFix=args[0]=="after";
var created=new DateTime(2026,11,4,0,0,0,DateTimeKind.Utc);
var evidence=new List<object>();var checks=0;var failures=0;
foreach(var minutes in new[]{20d,15d,10d,0.05d,-1d})
foreach(var sqlKind in new[]{DateTimeKind.Utc,DateTimeKind.Unspecified})
foreach(var offsetHours in new[]{0,7})
{
    // SQL datetime2 is UTC-by-contract but is materialized without Kind metadata.
    var sqlCreated=DateTime.SpecifyKind(created,sqlKind);
    var expiry=DateTime.SpecifyKind(created.AddMinutes(minutes),sqlKind);
    var result=SqlSessionPolicy.AbsoluteDeadline(sqlCreated,expiry);
    var expectedTicks=Math.Min(created.AddMinutes(15).Ticks,created.AddMinutes(minutes).Ticks);
    if(result.Ticks!=expectedTicks)throw new Exception("Lifetime/minimum changed");
    checks++;
    // Deterministic models of DateTimeOffset(DateTime)'s local-zone interpretation.
    // These are explicit UTC/UTC+07 timezone models, not claims of running two OS environments.
    var cookie=result.Kind==DateTimeKind.Utc?new DateTimeOffset(result):new DateTimeOffset(result,TimeSpan.FromHours(offsetHours));
    var correct=cookie.UtcTicks==expectedTicks;
    if(!correct)failures++;
    if(expectFix&&(!correct||result.Kind!=DateTimeKind.Utc))throw new Exception("UTC output contract failed");
    evidence.Add(new{minutes,sqlKind=sqlKind.ToString(),offsetHours,absoluteKind=result.Kind.ToString(),
        ticksPreserved=result.Ticks==expectedTicks,cookieSkewSeconds=(cookie.UtcTicks-expectedTicks)/(double)TimeSpan.TicksPerSecond,
        expiredAtCreated=cookie<=new DateTimeOffset(created)});
}
var normal=SqlSessionPolicy.AbsoluteDeadline(DateTime.SpecifyKind(created,DateTimeKind.Unspecified),null);
if(normal.Ticks!=created.AddHours(12).Ticks||(expectFix&&normal.Kind!=DateTimeKind.Utc))throw new Exception("Normal lifetime changed");checks++;
if(!expectFix&&failures==0)throw new Exception("Expected baseline defect was not reproduced");
if(expectFix)
{
    foreach(var minutes in new[]{20d,15d,10d,0.05d})
    foreach(var kind in new[]{DateTimeKind.Utc,DateTimeKind.Unspecified})
    {
        var cap=created.AddMinutes(Math.Min(minutes,15));
        var raw=SessionCredential.Generate();
        var user=new User{UserId=1,AccountStatus="ACTIVE",TemporaryPasswordExpiresAt=DateTime.SpecifyKind(created.AddMinutes(minutes),kind)};
        var row=new UserSession{SessionId=1,UserId=1,SessionKeyHash=SessionCredential.Hash(raw),
            CreatedAt=DateTime.SpecifyKind(created,kind),ExpiresAt=DateTime.SpecifyKind(cap,kind),LastUsedAt=DateTime.SpecifyKind(created,kind)};
        var ticket=SessionReference.FromProtectedTicket(1,raw,true,created);
        var valid=SqlSessionPolicy.Validate(user,row,ticket,cap.AddTicks(-1));
        if(valid is null)throw new Exception("Valid session rejected one tick before cap");checks++;
        if(SqlSessionPolicy.Validate(user,row,ticket,cap) is not null)throw new Exception("Session accepted at exact cap");checks++;
        if(SqlSessionPolicy.Validate(user,row,ticket,cap.AddTicks(1)) is not null)throw new Exception("Session accepted after cap");checks++;
        var touch=SqlSessionPolicy.TouchWindow(row,valid,cap.AddTicks(-1));
        if(touch.ExpiresAt!=cap||touch.ExpiresAt.Kind!=DateTimeKind.Utc)throw new Exception("Touch extended or mis-zoned cap");checks++;
    }
}
await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(new{phase=args[0],checks,timezoneModelCases=evidence.Count,
    observedCookieSkewCases=failures,liveOperatingSystemTimezone=TimeZoneInfo.Local.Id,
    utcOsEnvironmentExecuted=false,evidence},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"{args[0]}: {checks} minimum/lifetime assertions, {evidence.Count} timezone model cases, {failures} observed cookie-skew cases; OS zone {TimeZoneInfo.Local.Id}.");
