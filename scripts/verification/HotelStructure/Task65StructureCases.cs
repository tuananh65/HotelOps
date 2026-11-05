using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class Task65StructureCases
{
    internal static async Task RunAsync(HotelOpsDbContext db, Func<DbCommandInterceptor?,HotelOpsDbContext> open, HttpClient http,
        Func<string,Task<CookieJar>> login, IReadOnlyDictionary<string,User> identities, long branch,long otherBranch,Action<bool,string> check,CancellationToken ct)
    {
        var f = new Floor { BranchId=branch,FloorCode="T65_F",FloorName="Original floor",FloorNumber=-2,IsActive=true };
        var f2 = new Floor { BranchId=branch,FloorCode="T65_PARENT",FloorName="Second floor",IsActive=true };
        var foreign = new Floor { BranchId=otherBranch,FloorCode="T65_OTHER",FloorName="Foreign marker",IsActive=true };
        var inactive = new Floor { BranchId=branch,FloorCode="T65_INACTIVE",FloorName="Archived",IsActive=false };
        db.AddRange(f,f2,foreign,inactive); await db.SaveChangesAsync(ct);
        var a = new Area { BranchId=branch,FloorId=f.FloorId,AreaCode="T65_A",AreaName="Original area",IsActive=true };
        var free = new Area { BranchId=branch,AreaCode="T65_FREE",AreaName="Empty area",IsActive=true };
        db.AddRange(a,free); await db.SaveChangesAsync(ct);
        var rt=await db.RoomTypes.Select(x=>x.RoomTypeId).FirstAsync(ct);
        db.Add(new Room { BranchId=branch,FloorId=f.FloorId,AreaId=a.AreaId,RoomTypeId=rt,RoomNumber="T65_ROOM",OperationalStatus="AVAILABLE",IsActive=true });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        var jars=new Dictionary<string,CookieJar>(); foreach(var key in new[]{"admin","other","branch","viewer","denied","owner","restricted"}) jars[key]=await login(key);
        async Task<HttpResponseMessage> Send(HttpMethod method,string path,CookieJar jar,object? body,string? tag,CancellationToken token)
        {
            using var req=new HttpRequestMessage(method,path);
            if(jar.Cookies.Count>0) req.Headers.Add("Cookie",string.Join("; ",jar.Cookies.Select(x=>x.Key+"="+x.Value)));
            if(jar.Token!=null)req.Headers.Add(HotelOps.Api.Security.HotelOpsAntiforgery.HeaderName,jar.Token);
            if(tag!=null)req.Headers.TryAddWithoutValidation("If-Match",tag);
            if(body!=null)req.Content=JsonContent.Create(body);
            var response=await http.SendAsync(req,token); jar.Read(response); return response;
        }
        string Path(bool area,long id,long? b=null)=>$"/api/branches/{b??branch}/{(area?"areas":"floors")}/{id}";
        async Task<(JsonElement Value,string Tag)> Get(bool area,long id,CookieJar? jar=null)
        {
            using var r=await Send(HttpMethod.Get,Path(area,id),jar??jars["admin"],null,null,ct);
            check(r.StatusCode==HttpStatusCode.OK && r.Headers.CacheControl?.NoStore==true && HotelStructureUpdateService.IsStrongTag(r.Headers.ETag?.Tag),"UC015 GET edit details with strong ETag/no-store");
            using var json=JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));
            check(json.RootElement.EnumerateObject().All(p=>new[]{"kind","id","branchId","code","name","description","floorId","floorNumber","isActive"}.Contains(p.Name)),"UC015 edit DTO allowlist");
            return(json.RootElement.Clone(),r.Headers.ETag!.Tag);
        }
        object Body(string code,string name="Updated name",long? floor=null,bool area=false)=>area
            ? new {code,name,reason="Verified structural correction",description=" Updated description ",floorId=floor}
            : (object)new{code,name,reason="Verified structural correction",description=" Updated description "};
        async Task<string?> Put(string path,CookieJar jar,object body,string? tag,int status)
        {
            using var r=await Send(HttpMethod.Put,path,jar,body,tag,ct);var text=await r.Content.ReadAsStringAsync(ct);
            check((int)r.StatusCode==status,"UC015 HTTPS PUT expected "+status+", actual "+(int)r.StatusCode);
            check(!text.Contains("SqlException")&&!text.Contains("passwordHash")&&!text.Contains("sessionKey")&&!text.Contains("Foreign marker"),"UC015 PUT no sensitive/provider/foreign disclosure");
            if(status==200)check(r.Headers.CacheControl?.NoStore==true&&HotelStructureUpdateService.IsStrongTag(r.Headers.ETag?.Tag),"UC015 update returns new strong ETag/no-store");
            return r.Headers.ETag?.Tag;
        }
        var admin=jars["admin"];var original=await Get(false,f.FloorId);var ar=await Get(true,a.AreaId);
        var beforeUsers=await Fingerprint("users","user_roles","user_branch_access","roles","permissions","role_permissions","rooms","assets");
        var oldCount=await db.AuditLogs.CountAsync(x=>x.EntityType=="floors"&&x.EntityId==f.FloorId.ToString()&&x.Action=="UPDATE",ct);
        var next=await Put(Path(false,f.FloorId),admin,Body(" t65_f "," New floor "),original.Tag,200);
        check(next!=original.Tag && (await db.Floors.AsNoTracking().SingleAsync(x=>x.FloorId==f.FloorId,ct)).FloorName=="New floor","UC015 Floor persisted normalized values and changed revision");
        var saved=await db.AuditLogs.AsNoTracking().Where(x=>x.EntityType=="floors"&&x.EntityId==f.FloorId.ToString()&&x.Action=="UPDATE").OrderByDescending(x=>x.AuditId).FirstAsync(ct);
        using(var old=JsonDocument.Parse(saved.OldValues!))using(var after=JsonDocument.Parse(saved.NewValues!))
            check(saved.ActorUserId==identities["admin"].UserId&&saved.BranchId==branch&&old.RootElement.GetProperty("name").GetString()=="Original floor"
                && after.RootElement.GetProperty("reason").GetString()=="Verified structural correction"&&after.RootElement.GetProperty("value").GetProperty("name").GetString()=="New floor","UC015 atomic audit binds SQL actor/reason/before/after");
        using(var fields=JsonDocument.Parse(saved.ChangedFields!))check(fields.RootElement.EnumerateArray().Select(x=>x.GetString()).SequenceEqual(new[]{"name","description"}),"UC015 audit records exact changed field names");
        check(await db.AuditLogs.CountAsync(x=>x.EntityType=="floors"&&x.EntityId==f.FloorId.ToString()&&x.Action=="UPDATE",ct)==oldCount+1,"UC015 one update audit");
        await Put(Path(false,f.FloorId),admin,Body("T65_F"),original.Tag,412);
        var restored=await Put(Path(false,f.FloorId),admin,new{code="T65_F",name="Original floor",reason="Restore original representation",description=(string?)null},next,200);
        check(restored!=original.Tag,"UC015 ABA returns a new tag despite identical representation");
        await Put(Path(false,f.FloorId),admin,Body("T65_F"),original.Tag,412);
        var noop=await Put(Path(false,f.FloorId),admin,new{code="T65_F",name="Original floor",reason="Confirm same representation",description=(string?)null},restored,200);
        check(noop!=restored,"UC015 no-op save consumes its tag with an audit revision");
        await Put(Path(true,a.AreaId),admin,Body("T65_A","Move area",f2.FloorId,true),ar.Tag,409);
        var root=await Put(Path(true,a.AreaId),admin,Body("T65_A","Branch area",null,true),ar.Tag,200);
        await Put(Path(true,a.AreaId),admin,Body("T65_A","Floor area",f.FloorId,true),root,200);
        var freeTag=(await Get(true,free.AreaId)).Tag;
        freeTag=(await Put(Path(true,free.AreaId),admin,Body("T65_FREE","Reparent empty area",f2.FloorId,true),freeTag,200))!;
        await Put(Path(true,free.AreaId),admin,Body("T65_FREE","Invalid parent",foreign.FloorId,true),freeTag,400);
        await Put(Path(true,free.AreaId),admin,Body("T65_FREE","Inactive parent",inactive.FloorId,true),freeTag,400);
        await Put(Path(false,inactive.FloorId),admin,Body("T65_INACTIVE"),(await Get(false,inactive.FloorId)).Tag,409);
        await Put(Path(false,f.FloorId),admin,Body("T65_PARENT"),noop,409);
        await Put(Path(false,f.FloorId),admin,Body("T65_A"),noop,409);
        await Put(Path(false,f.FloorId),admin,Body("T65_F"),null,428);
        foreach(var tag in new[]{"*","W/"+noop,"\"bad\"",noop+", "+noop})await Put(Path(false,f.FloorId),admin,Body("T65_F"),tag,400);
        foreach(var body in new object[]{new{code="",name="x",reason="why"},new{code="T65_F",name="",reason="why"},new{code="T65_F",name="x",reason=" "},new{code="T65_F",name=new string('x',101),reason="why"},new{code="T65_F",name="x",reason="why",branchId=otherBranch},new{code="T65_F",name="x",reason="why",floorNumber=2},new{code="T65_F",name="x",reason="why",isActive=false}})
            await Put(Path(false,f.FloorId),admin,body,noop,400);
        await Put(Path(false,0),admin,Body("T65_F"),noop,400);
        await Put(Path(false,long.MaxValue),admin,Body("T65_F"),noop,404);
        await Put(Path(false,foreign.FloorId),admin,Body("T65_F"),noop,404);
        await Put(Path(false,f.FloorId),new(),Body("T65_F"),noop,400); // unsafe anonymous request fails existing CSRF before cookie auth
        using(var anonymous=await Send(HttpMethod.Get,Path(false,f.FloorId),new(),null,null,ct))check(anonymous.StatusCode==HttpStatusCode.Unauthorized,"UC015 anonymous GET401");
        var anon=new CookieJar();using(var bootstrap=await Send(HttpMethod.Get,"/api/auth/csrf",anon,null,null,ct))
        {using var json=JsonDocument.Parse(await bootstrap.Content.ReadAsStringAsync(ct));anon.Token=json.RootElement.GetProperty("csrfToken").GetString();}
        await Put(Path(false,f.FloorId),anon,Body("T65_F"),noop,401);
        foreach(var who in new[]{"viewer","denied","owner","restricted"})await Put(Path(false,f.FloorId),jars[who],Body("T65_F"),noop,403);
        await Put(Path(false,foreign.FloorId,otherBranch),jars["branch"],Body("T65_F"),noop,403);
        var corrupt=await db.Areas.Where(x=>x.AreaCode=="T64_CORRUPT").Select(x=>x.AreaId).SingleAsync(ct);
        using(var rejected=await Send(HttpMethod.Get,Path(true,corrupt),jars["branch"],null,null,ct))
            check(rejected.StatusCode==HttpStatusCode.NotFound,"UC015 legacy Area linked to foreign Floor is not disclosed even to scoped manager");
        var scoped=(await Get(false,f.FloorId,jars["branch"])).Tag;
        await Put(Path(false,f.FloorId),jars["branch"],Body("T65_F","Scoped update"),scoped,200);
        check(await Fingerprint("users","user_roles","user_branch_access","roles","permissions","role_permissions","rooms","assets")==beforeUsers,"UC015 functional PUTs preserve full Users/catalog/assignments/Room/Asset references");
        await Task65StructureConcurrency.RunAsync(db,open,login,identities,branch,f.FloorId,free.AreaId,Send,check,ct);

        async Task<string> Fingerprint(params string[] tables)
        {
            var parts=new List<string>();foreach(var table in tables){await using var c=db.Database.GetDbConnection().CreateCommand();c.CommandText=$"SELECT * FROM dbo.[{table}] ORDER BY 1 FOR JSON PATH,INCLUDE_NULL_VALUES";
                await using var r=await c.ExecuteReaderAsync(ct);var text=new System.Text.StringBuilder();while(await r.ReadAsync(ct))text.Append(r.GetString(0));parts.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.ToString()))));}return string.Join(':',parts);
        }
    }
}
