using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Controllers.Filters;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Dev2Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class Offline
{
    internal static async Task Run(string output)
    {
        var saved=Environment.GetEnvironmentVariable("DEV2_DEMO_AUTHORITY_V1");
        var results=new List<object>();var assertions=0;
        void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);assertions++;}
        try
        {
            assertions+=await LeaseVerification.Run();
            assertions+=await DiagnosticsVerification.Run(output);
            assertions+=await FixtureReferenceVerification.Run();
            assertions+=await RestrictedSessionVerification.Run(output);
            assertions+=ReceiptPostingVerification.Run();
            assertions+=await PreconditionTraceVerification.Run(output);
            assertions+=await RecoveryVerification.Run(output);
            assertions+=await PaginationVerification.Run(output);
            assertions+=await NormalModeVerification.Run(output);
            Environment.SetEnvironmentVariable("DEV2_DEMO_AUTHORITY_V1",Dev2ExpandedPolicyIdentity.Sha256);
            var current=new Actor();var data=new Grants();var service=new PermissionService(current,data,new UnresolvedPermissionResourceResolver(),TimeProvider.System);
            using var provider=new ServiceCollection().AddSingleton<ICurrentUserAccessor>(current).AddDbContext<HotelOpsDbContext>(x=>x.UseSqlServer("Server=lpc:KIENPC;Database=HotelOps_DEV2_FullStack_Trial;Integrated Security=true;Encrypt=false")).BuildServiceProvider();
            var http=new DefaultHttpContext{RequestServices=provider};
            var factory=typeof(Dev2DemoBindings).GetMethod("FindExpanded",BindingFlags.Static|BindingFlags.NonPublic)!;
            var assembly=typeof(Dev2DemoBindings).Assembly;var boundType=assembly.GetType("HotelOps.Api.Services.Dev2Authorization.Dev2BoundAction")!;
            var invocationType=assembly.GetType("HotelOps.Api.Services.Dev2Authorization.Dev2DomainInvocation")!;
            var read=0;var mutation=0;
            foreach(var known in Dev2ActionAdmissionRegistry.KnownActions)
            {
                var codes=Dev2DemoBindings.PermissionCodes(known.DecisionId);data.Codes=codes.ToHashSet();data.Scope="BRANCH";data.Active=true;data.Unavailable=false;current.Restricted=false;
                Check((await Dev2DemoBindings.EvaluateAsync(service,known.DecisionId,1,default)).IsGranted,known.DecisionId+" actual core positive");
                foreach(var code in codes){data.Codes.Remove(code);Check(!(await Dev2DemoBindings.EvaluateAsync(service,known.DecisionId,1,default)).IsGranted,known.DecisionId+" missing operand "+code);data.Codes.Add(code);}
                Check(!(await Dev2DemoBindings.EvaluateAsync(service,known.DecisionId,2,default)).IsGranted,known.DecisionId+" wrong branch");
                data.Scope="OWN";Check(!(await Dev2DemoBindings.EvaluateAsync(service,known.DecisionId,1,default)).IsGranted,known.DecisionId+" OWN excluded");data.Scope="BRANCH";
                current.Restricted=true;Check(!(await Dev2DemoBindings.EvaluateAsync(service,known.DecisionId,1,default)).IsGranted,known.DecisionId+" restricted");current.Restricted=false;
                data.Active=false;Check(!(await Dev2DemoBindings.EvaluateAsync(service,known.DecisionId,1,default)).IsGranted,known.DecisionId+" inactive");data.Active=true;
                data.Unavailable=true;Check(!(await Dev2DemoBindings.EvaluateAsync(service,known.DecisionId,1,default)).IsGranted,known.DecisionId+" unavailable");data.Unavailable=false;
                var contract=factory.Invoke(null,[known,http]);Check(contract is not null,known.DecisionId+" real binding exists");
                Check((bool)contract!.GetType().GetMethod("IsComplete")!.Invoke(contract,[known])!,known.DecisionId+" mandatory delegates present");
                var handler=contract.GetType().GetProperty("ScopedReadHandler")!.GetValue(contract);
                if(known.HttpMethod=="GET"){Check(handler is Delegate,known.DecisionId+" scoped read handler");read++;}
                else
                {
                    mutation++;var original=new object();var args=new Dictionary<string,object?>{{"request",original},{"input",original},{"id",42L},{"assetId",42L},{"handoverId",42L},{"incidentId",42L}};
                    var bound=Activator.CreateInstance(boundType,[known,http,new CurrentUserInfo(100,200,false),args])!;
                    var matches=(Delegate)contract.GetType().GetProperty("MatchesServiceInvocation")!.GetValue(contract)!;
                    long? root=known.DecisionId is "A004A-014" or "A004A-021" or "A004A-043" or "A004A-056"?null:42L;
                    object Call(string controller,string action,long? id,object body)=>Activator.CreateInstance(invocationType,[controller,action,id,body])!;
                    var controller=known.ControllerType.Name.Replace("Controller","");
                    Check((bool)matches.DynamicInvoke(bound,Call(controller,known.ActionName,root,original))!,known.DecisionId+" exact capability match");
                    Check(!(bool)matches.DynamicInvoke(bound,Call(controller,known.ActionName,root,new object()))!,known.DecisionId+" cloned input rejected");
                    Check(!(bool)matches.DynamicInvoke(bound,Call(controller,known.ActionName,43L,original))!,known.DecisionId+" switched root rejected");
                    Check(!(bool)matches.DynamicInvoke(bound,Call("Other",known.ActionName,root,original))!,known.DecisionId+" switched controller rejected");
                    var grantType=assembly.GetType("HotelOps.Api.Services.Dev2Authorization.Dev2ActionGrant")!;
                    var install=typeof(Dev2ServiceAuthority).GetMethod("Install",BindingFlags.Static|BindingFlags.NonPublic)!;
                    var remove=typeof(Dev2ServiceAuthority).GetMethod("Remove",BindingFlags.Static|BindingFlags.NonPublic)!;
                    var grant=Activator.CreateInstance(grantType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[bound,contract,new Func<Dev2AuthorizationPhase,CancellationToken,Task>((_,_)=>Task.CompletedTask)],null)!;
                    install.Invoke(null,[http,grant]);var accessor=new HttpContextAccessor{HttpContext=http};
                    Check(Dev2ServiceAuthority.RequireEntry(accessor,controller,[known.ActionName],100,root,original)==100,known.DecisionId+" actual service capability positive");
                    try{Dev2ServiceAuthority.RequireEntry(accessor,controller,[known.ActionName],100,root,new object());throw new InvalidOperationException("clone allowed");}catch(Dev2AccessDenied){Check(true,known.DecisionId+" actual service clone denial");}
                    try{Dev2ServiceAuthority.RequireEntry(accessor,controller,[known.ActionName],101,root,original);throw new InvalidOperationException("spoof allowed");}catch(Dev2AccessDenied){Check(true,known.DecisionId+" actual service actor denial");}
                    remove.Invoke(null,[http]);
                    try{Dev2ServiceAuthority.RequireEntry(accessor,controller,[known.ActionName],100,root,original);throw new InvalidOperationException("ungranted entry allowed");}catch(Dev2AccessDenied){Check(true,known.DecisionId+" direct ungranted service entry denial");}
                }
                results.Add(new{id=known.DecisionId,permissionCorePositive=true,permissionCoreNegative=true,contractShape=true,businessAcceptance="NOT_RUN_OFFLINE",sql="NOT_RUN"});
            }
            Check(read==26&&mutation==35,"26/35 exact matrix");
            Environment.SetEnvironmentVariable("DEV2_DEMO_AUTHORITY_V1",Dev2DemoPolicyIdentity.Sha256);
            foreach(var known in Dev2ActionAdmissionRegistry.KnownActions)Check(factory.Invoke(null,[known,http]) is null,"V1 flag cannot activate expanded bindings");
            Environment.SetEnvironmentVariable("DEV2_DEMO_AUTHORITY_V1",Dev2ExpandedPolicyIdentity.Sha256);
            using var wrongProvider=new ServiceCollection().AddDbContext<HotelOpsDbContext>(x=>x.UseSqlServer("Server=lpc:KIENPC;Database=HotelOpsDB;Integrated Security=true;Encrypt=false")).BuildServiceProvider();
            foreach(var known in Dev2ActionAdmissionRegistry.KnownActions)Check(factory.Invoke(null,[known,new DefaultHttpContext{RequestServices=wrongProvider}]) is null,"Other database cannot activate expanded bindings");
            Directory.CreateDirectory(output);var report=new{passed=true,assertions,actionComponents=61,readBindings=26,mutationBindings=35,results,sqlConnections=0,apiStartups=0,fullyAcceptedBusinessActions=0};
            await File.WriteAllTextAsync(Path.Combine(output,"OFFLINE_ACTION_RESULTS.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine(JsonSerializer.Serialize(new{passed=true,assertions,actionComponents=61,readBindings=26,mutationBindings=35,sqlConnections=0,fullyAcceptedBusinessActions=0}));
        }
        finally{Environment.SetEnvironmentVariable("DEV2_DEMO_AUTHORITY_V1",saved);}
    }
    private sealed class Actor:ICurrentUserAccessor{internal bool Restricted;public bool TryGetCurrentUser(out CurrentUserInfo current){current=new(100,200,Restricted);return true;}}
    private sealed class Grants:IPermissionDataSource
    {
        internal HashSet<string> Codes=[];internal string Scope="BRANCH";internal bool Active=true,Unavailable;
        public Task<bool> IsActiveUserAsync(long id,CancellationToken ct)=>Unavailable?throw new TimeoutException():Task.FromResult(Active);
        public Task<bool> IsActiveBranchAsync(long id,CancellationToken ct)=>Task.FromResult(true);
        public Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long id,string code,DateTime now,CancellationToken ct)=>Task.FromResult<IReadOnlyList<PermissionGrantPath>>(Codes.Contains(code)?[new(code,Scope,1,true,true,now.AddDays(-1),null,true,true)]:[]);
    }
}
