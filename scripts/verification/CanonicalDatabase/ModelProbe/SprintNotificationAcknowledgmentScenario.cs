using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

internal static class SprintNotificationAcknowledgmentScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long own,long ordinary,long foreign,string csrf,List<object> checks,CancellationToken ct)
    {
        // Synthetic producer flag only: not proof of a real Critical incident or escalation producer.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notifications SET requires_acknowledgment=1 WHERE notification_id={own}",ct);
        var source=await db.Database.SqlQuery<string>($"SELECT (SELECT * FROM dbo.notifications ORDER BY notification_id FOR JSON PATH) AS Value").SingleAsync(ct);
        async Task Call(long[] ids,int expected,bool token=true)
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,"/api/notifications/acknowledge"){Content=JsonContent.Create(new{ids})};
            if(token)request.Headers.Add("X-CSRF-TOKEN",csrf);
            using var response=await http.SendAsync(request,ct);
            if((int)response.StatusCode!=expected)throw new Exception("Acknowledgment HTTP expected "+expected+" actual "+(int)response.StatusCode);
            lock(checks)checks.Add(new{ackStatus=expected});
        }
        async Task<string> Recipients()=>await db.Database.SqlQuery<string>($"SELECT (SELECT * FROM dbo.notification_recipients ORDER BY recipient_id FOR JSON PATH) AS Value").SingleAsync(ct);
        var before=await Recipients();
        await Call([own],400,false);await Call([],400);await Call([own,foreign],404);await Call([own,ordinary],409);
        if(await Recipients()!=before)throw new Exception("Denied acknowledgment partially changed recipients");
        try
        {
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.TR_DEV3_AckFault ON dbo.audit_logs AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE entity_type='notification_recipients') THROW 51881,'Synthetic acknowledgment failure',1; END",ct);
            await Call([own],500);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS dbo.TR_DEV3_AckFault",CancellationToken.None); }
        if(await Recipients()!=before)throw new Exception("Audit failure did not roll back acknowledgment");
        var audits=await db.AuditLogs.CountAsync(a=>a.EntityType=="notification_recipients",ct);
        await Task.WhenAll(Call([own,own],204),Call([own],204));
        var after=await Recipients();await Call([own],204);
        if(await Recipients()!=after||await db.AuditLogs.CountAsync(a=>a.EntityType=="notification_recipients",ct)!=audits+1)
            throw new Exception("Acknowledgment retry changed timestamp or duplicated audit");
        var audit=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="notification_recipients").OrderByDescending(a=>a.AuditId).FirstAsync(ct);
        using(var facts=JsonDocument.Parse(JsonSerializer.Serialize(HotelOps.Api.Services.Insights.AuditRedaction.Read(audit.NewValues))))
            if(facts.RootElement.GetProperty("notificationId").GetInt64()!=own
                ||facts.RootElement.GetProperty("readAt").GetString() is not {} readTime ||!readTime.EndsWith("+00:00")
                ||facts.RootElement.GetProperty("acknowledgedAt").GetString() is not {} ackTime ||!ackTime.EndsWith("+00:00"))
                throw new Exception("Persisted acknowledgment audit lost typed UTC timestamps in read projection");
        using var response=await http.GetAsync("/api/notifications?acknowledged=true",ct);
        var page=await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken:ct);
        if(!response.IsSuccessStatusCode||page.GetProperty("totalCount").GetInt32()!=1||page.GetProperty("items")[0].GetProperty("id").GetInt64()!=own)
            throw new Exception("Acknowledged filter or persisted timestamp failed");
        if(await db.Database.SqlQuery<string>($"SELECT (SELECT * FROM dbo.notifications ORDER BY notification_id FOR JSON PATH) AS Value").SingleAsync(ct)!=source)
            throw new Exception("Acknowledgment mutated notification source");
        checks.Add(new{ackAuditAtomic=true,ackTimestampOnce=true,ackReadDistinct=true,producerContract="SYNTHETIC_FLAG_ONLY"});
        // Reset only the owned fixture for the independent browser flow.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_recipients SET acknowledged_at=NULL WHERE notification_id={own}",ct);
    }
}
