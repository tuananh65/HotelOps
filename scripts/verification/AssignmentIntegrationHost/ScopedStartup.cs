using System.Reflection;
using HotelOps.Api.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Data.SqlClient;
using System.Text.Json;
[assembly: HostingStartup(typeof(ScopedStartup))]
// Verification executable only: links the unchanged API Program; never loaded by production.
public sealed class ScopedStartup : IHostingStartup {
 public void Configure(IWebHostBuilder builder) => builder.ConfigureServices((ctx,services)=>{
  var receipt=Environment.GetEnvironmentVariable("FE05_OWNED_RECEIPT") ?? throw new InvalidOperationException("Owned receipt required");
  using var doc=JsonDocument.Parse(File.ReadAllText(receipt)); var e=doc.RootElement;
  var database=e.GetProperty("database").GetString()!;var owner=e.GetProperty("owner").GetString()!;
  if(!e.GetProperty("success").GetBoolean()||!e.GetProperty("createdByThisRun").GetBoolean()||!e.GetProperty("syntheticOnly").GetBoolean()||e.GetProperty("server").GetString()!=@"DESKTOP-JI8U50T\SQLEXPRESS"||!System.Text.RegularExpressions.Regex.IsMatch(database,@"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$"))throw new InvalidOperationException("Isolation rejected");
  var cs=ctx.Configuration.GetConnectionString("HotelOpsDb")!;var b=new SqlConnectionStringBuilder(cs);
  if(b.DataSource!=@"lpc:.\SQLEXPRESS"||b.InitialCatalog!=database||!b.IntegratedSecurity)throw new InvalidOperationException("Connection identity rejected");
  services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly).ConfigureApplicationPartManager(m=>m.FeatureProviders.Add(new CandidateOnly()));
  services.AddSingleton<IStartupFilter>(new Guard(cs,database,owner));
 });
 private sealed class CandidateOnly:IApplicationFeatureProvider<ControllerFeature>{public void PopulateFeature(IEnumerable<ApplicationPart> parts,ControllerFeature feature){var t=typeof(InspectionAssignmentCandidateController).GetTypeInfo();if(!feature.Controllers.Contains(t))feature.Controllers.Add(t);}}
 private sealed class Guard(string cs,string database,string owner):IStartupFilter {
  public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)=>app=>{app.Use(async(context,go)=>{
   await using var c=new SqlConnection(cs);await c.OpenAsync(context.RequestAborted);await using var q=c.CreateCommand();q.CommandText="SELECT DB_NAME(),CONVERT(nvarchar(128),@@SERVERNAME),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FE05INT001.Owner')";await using var r=await q.ExecuteReaderAsync(context.RequestAborted);if(!await r.ReadAsync(context.RequestAborted)||r.GetString(0)!=database||r.GetString(1)!=@"DESKTOP-JI8U50T\SQLEXPRESS"||r.IsDBNull(2)||r.GetString(2)!=owner)throw new InvalidOperationException("Actual isolated identity rejected");await r.DisposeAsync();await c.CloseAsync();await go();});next(app);};
 }
}
