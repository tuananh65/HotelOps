using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
internal static class SprintNotificationScenario
{
 internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long actor,string csrf,List<object> checks,CancellationToken ct,string? browserUsername=null,string? browserPassword=null)
 {
  if(await db.Notifications.AnyAsync(ct)||await db.NotificationRecipients.AnyAsync(ct))throw new Exception("Empty notification fixture required");
  var prefix="D3NOTE"+Guid.NewGuid().ToString("N")[..10];
  var other=new User{Username=prefix,EmployeeCode=prefix,Email=prefix+"@example.invalid",PasswordHash=await db.Users.Where(u=>u.UserId==actor).Select(u=>u.PasswordHash).SingleAsync(ct),FullName="Synthetic other",AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=DateTime.UtcNow};
  long[] ids=[];
  async Task<JsonElement> Call(string path,int expected,bool post=false,bool token=true){using var request=new HttpRequestMessage(post?HttpMethod.Post:HttpMethod.Get,path);if(post&&token)request.Headers.Add("X-CSRF-TOKEN",csrf);using var response=await http.SendAsync(request,ct);checks.Add(new{path,post,expected,actual=(int)response.StatusCode});if((int)response.StatusCode!=expected)throw new Exception("Notification HTTP mismatch "+path);if(response.StatusCode==System.Net.HttpStatusCode.NoContent)return default;using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));return json.RootElement.Clone();}
  try
  {
   db.Users.Add(other);await db.SaveChangesAsync(ct);var now=DateTime.UtcNow;
   Notification Note(string suffix,DateTime date,DateTime? expires=null)=>new(){NotificationType="CLEANING_TASK_ASSIGNED",Title=prefix+suffix,Message="Synthetic notification",Priority="NORMAL",CreatedAt=date,ExpiresAt=expires,ActionUrl="https://untrusted.invalid/PRIVATE",CreatedBy=actor};
   var own=Note("OWN",now.AddMinutes(-1));var second=Note("SECOND",now.AddMinutes(-2));var foreign=Note("FOREIGN",now);var expired=Note("EXPIRED",now.AddMinutes(-2),now.AddMinutes(-1));var old=Note("OLD",now.AddDays(-366));var pending=Note("PENDING",now);
   db.Notifications.AddRange(own,second,foreign,expired,old,pending);await db.SaveChangesAsync(ct);ids=[own.NotificationId,second.NotificationId,foreign.NotificationId,expired.NotificationId,old.NotificationId,pending.NotificationId];
   foreach(var n in new[]{own,second,foreign,expired,old,pending})db.NotificationRecipients.Add(new(){NotificationId=n.NotificationId,UserId=n==foreign?other.UserId:actor,DeliveryChannel="IN_APP",DeliveryStatus=n==pending?"PENDING":"DELIVERED",DeliveredAt=n==pending?null:now});
   await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
   var page=await Call("/api/notifications",200);if(page.GetProperty("totalCount").GetInt32()!=2||page.GetProperty("unreadCount").GetInt32()!=2||page.ToString().Contains("PRIVATE"))throw new Exception("Inbox recipient/retention/delivery/link isolation");
   await Call($"/api/notifications/{own.NotificationId}/read",400,true,false);
   await Call($"/api/notifications/{foreign.NotificationId}/read",404,true);
   await Call("/api/notifications/9223372036854775807/read",404,true);
   await Call($"/api/notifications/{own.NotificationId}/read",204,true);
   var read=await db.NotificationRecipients.AsNoTracking().Where(r=>r.NotificationId==own.NotificationId&&r.UserId==actor).Select(r=>r.ReadAt).SingleAsync(ct);
   await Call($"/api/notifications/{own.NotificationId}/read",204,true);
   if(read==null||await db.NotificationRecipients.AsNoTracking().Where(r=>r.NotificationId==own.NotificationId&&r.UserId==actor).Select(r=>r.ReadAt).SingleAsync(ct)!=read)throw new Exception("Read timestamp not idempotent");
   page=await Call("/api/notifications?unread=true",200);if(page.GetProperty("totalCount").GetInt32()!=1||page.GetProperty("unreadCount").GetInt32()!=1)throw new Exception("Unread count mismatch");
   page=await Call("/api/notifications?search=SECOND&pageSize=1",200);if(page.GetProperty("totalCount").GetInt32()!=1)throw new Exception("Search mismatch");
   await Call("/api/notifications?from=2026-11-15&to=2026-11-14",400);await Call("/api/notifications?pageSize=101",400);
   await Call("/api/notifications/read-all",204,true);
   page=await Call("/api/notifications",200);if(page.GetProperty("unreadCount").GetInt32()!=0)throw new Exception("Read all mismatch");
   if(await db.NotificationRecipients.AnyAsync(r=>r.NotificationId!=own.NotificationId&&r.NotificationId!=second.NotificationId&&r.ReadAt!=null,ct))throw new Exception("Read all affected invisible recipients");
   if(await db.Notifications.CountAsync(n=>ids.Contains(n.NotificationId)&&n.ActionUrl=="https://untrusted.invalid/PRIVATE",ct)!=6)throw new Exception("Read mutated source content");
   if(browserUsername!=null&&browserPassword!=null)
   {
    await db.NotificationRecipients.Where(r=>r.UserId==actor&&(r.NotificationId==own.NotificationId||r.NotificationId==second.NotificationId)).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ReadAt,(DateTime?)null),ct);
    var browser=await SprintNotificationBrowser.RunAsync(browserUsername,browserPassword,own.NotificationId,own.Title,foreign.Title,ct);checks.Add(new{browser});
    if(!browser.GetProperty("success").GetBoolean())throw new Exception("Browser acceptance failed at "+browser.GetProperty("failureStage").GetString());
    if(await db.NotificationRecipients.CountAsync(r=>r.UserId==actor&&r.ReadAt!=null,ct)!=2)throw new Exception("Browser read markers absent in SQL");
    if(await db.UserSessions.CountAsync(s=>s.UserId==actor&&s.RevokedAt==null,ct)!=1)throw new Exception("Browser logout did not revoke its SQL session");
    checks.Add(new{browserReadMarkersPersistedInSql=true,browserSessionRevoked=true});
   }
   checks.Add(new{recipientIsolation=true,retention365=true,expiredAndPendingHidden=true,readTimestampOnce=true,readAllOwnOnly=true,sourceLinksWithheld=true});
  }
  finally
  {
   db.ChangeTracker.Clear();await using var cleanup=await db.Database.BeginTransactionAsync(CancellationToken.None);
   var owned=db.Notifications.Where(n=>n.CreatedBy==actor&&n.Title.StartsWith(prefix)).Select(n=>n.NotificationId);
   await db.NotificationRecipients.Where(r=>owned.Contains(r.NotificationId)).ExecuteDeleteAsync(CancellationToken.None);
   await db.Notifications.Where(n=>owned.Contains(n.NotificationId)).ExecuteDeleteAsync(CancellationToken.None);
   await db.Users.Where(u=>u.UserId==other.UserId&&u.Username==prefix).ExecuteDeleteAsync(CancellationToken.None);
   await cleanup.CommitAsync(CancellationToken.None);
   if(await db.Notifications.AnyAsync()||await db.NotificationRecipients.AnyAsync())throw new Exception("Notification cleanup residue");checks.Add(new{notificationCleanup=true});
  }
 }
}
